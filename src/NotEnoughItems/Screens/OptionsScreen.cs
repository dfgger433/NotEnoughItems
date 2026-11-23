using System;
using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private enum OptionsTab { Display, Layout, Items, Keys }
    private OptionsTab _optionsTab;
    private bool _languageMenuOpen;
    internal bool LanguageMenuOpen => _options && _languageMenuOpen;
    private static readonly string[] LanguageChoices = { "auto", "en", "zh-CN" };
    private ConfigEntry<KeyCode>? _pendingBinding;
    private static readonly KeyCode[] ShortcutKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    private void DrawOptions(Rect bounds)
    {
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        Matrix4x4 matrix = GUI.matrix;
        try
        {
            // Apply the local zoom before the global NEI scale, keeping the top-left pivot fixed.
            float zoom = _optionsZoom;
            Vector2 offset = bounds.position * (1f - zoom);
            GUI.matrix = matrix * Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(zoom, zoom, 1f));
            DrawOptionsContent(NeiLayout.OptionsContentBounds(bounds, zoom));
        }
        finally { GUI.matrix = matrix; }
    }

    private void DrawOptionsContent(Rect p)
    {
        NeiSkin.RecipePanel(p);
        Header(p, L("NEI options", "NEI 选项"), "NEI");
        float x = p.x + 12, w = p.width - 24, tabWidth = (w - 6) / 4;
        DrawTab(OptionsTab.Display, L("Display", "显示"));
        DrawTab(OptionsTab.Layout, L("Layout", "布局"));
        DrawTab(OptionsTab.Items, L("Items", "物品"));
        DrawTab(OptionsTab.Keys, L("Keys", "按键"));

        var row = FirstOptionsRow(p);
        switch (_optionsTab)
        {
            case OptionsTab.Display: DrawDisplayOptions(row); DrawLanguageMenu(LanguageRow(p)); break;
            case OptionsTab.Layout: DrawLayoutOptions(row); break;
            case OptionsTab.Items: DrawItemOptions(row); break;
            case OptionsTab.Keys: DrawKeyOptions(row); break;
        }
        if (NeiSkin.Button(new Rect(x, p.yMax - 35, w, 26), L("Done", "完成"))) Back();

        void DrawTab(OptionsTab tab, string title)
        {
            var rect = new Rect(x + (int)tab * (tabWidth + 2), p.y + 30, tabWidth, 24);
            if (!NeiSkin.Tab(rect, title, _optionsTab == tab) || _optionsTab == tab) return;
            _optionsTab = tab;
            CloseLanguageMenu();
            _pendingBinding = null;
            GUI.FocusControl(null);
        }
    }

    private string ToggleLabel(string en, string zh, bool on) =>
        L(en + (on ? ": On" : ": Off"), zh + (on ? "：开" : "：关"));

    private void DrawDisplayOptions(Rect row)
    {
        if (NeiSkin.Button(row, ToggleLabel("Inventory catalog", "背包右侧目录", _showInventoryGrid.Value)))
            _showInventoryGrid.Value = !_showInventoryGrid.Value;
        row.y += 30;
        if (NeiSkin.Button(row, ToggleLabel("Terminal catalog", "终端右侧目录", _showGrid.Value)))
            _showGrid.Value = !_showGrid.Value;
        row.y += 30;
        if (NeiSkin.Button(row, ToggleLabel("Bookmarks", "书签栏", _bookmarksVisible))) ToggleBookmarks();
        row.y += 30;
        if (NeiSkin.Button(row, ToggleLabel("History strip", "浏览记录", _showHistory.Value)))
            _showHistory.Value = !_showHistory.Value;
        row.y += 30;
        if (NeiSkin.Button(row, ToggleLabel("Hide legacy UEI panel", "隐藏旧版 UEI 侧栏", _plugin.ReplaceUei)))
            _plugin.ReplaceUei = !_plugin.ReplaceUei;
        row.y += 30;
        float scale = _plugin.Scale;
        if (NeiSkin.IconButton(new Rect(row.x, row.y, 26, row.height), "remove", L("Smaller sidebar", "缩小侧栏"), scale > .7f))
            _plugin.Scale = Mathf.Round((scale - .05f) * 100f) / 100f;
        NeiSkin.Label(new Rect(row.x + 28, row.y, row.width - 56, row.height),
            L($"Sidebar scale: {scale:P0}", $"侧栏缩放：{scale:P0}"), TextAnchor.MiddleCenter);
        if (NeiSkin.IconButton(new Rect(row.xMax - 26, row.y, 26, row.height), "add", L("Larger sidebar", "放大侧栏"), scale < 1.4f))
            _plugin.Scale = Mathf.Round((scale + .05f) * 100f) / 100f;
    }

    internal void CloseLanguageMenu() => _languageMenuOpen = false;

    private static Rect FirstOptionsRow(Rect panel) => new(panel.x + 12, panel.y + 62, panel.width - 24, 26);

    private static Rect LanguageRow(Rect panel)
    {
        var row = FirstOptionsRow(panel);
        row.y += 6 * 30; // Five toggles and the scale control precede the language selector.
        return row;
    }

    private void DismissLanguageMenuOnOutsideClick(Event e)
    {
        if (!LanguageMenuOpen || e.type != EventType.MouseDown) return;
        var content = NeiLayout.OptionsContentBounds(_optionsBounds, _optionsZoom);
        var bounds = LanguageRow(content);
        bounds.height *= 1 + LanguageChoices.Length;
        Vector2 pointer = content.position + (e.mousePosition - _optionsBounds.position) / _optionsZoom;
        if (!bounds.Contains(pointer)) CloseLanguageMenu();
    }

    private string LanguageName(string language) => language switch
    {
        "en" => "English",
        "zh-CN" => "简体中文",
        _ => L("Auto", "自动")
    };

    private void DrawLanguageMenu(Rect row)
    {
        string language = _plugin.Language;
        bool expanded = _languageMenuOpen;
        if (NeiSkin.DropDownButton(row, L("Language: ", "语言：") + LanguageName(language), expanded))
        {
            _languageMenuOpen = !expanded;
            GUI.FocusControl(null);
        }
        if (!expanded) return;
        foreach (string choice in LanguageChoices)
        {
            row.y += row.height;
            if (!NeiSkin.DropDownOption(row, LanguageName(choice), choice == language)) continue;
            _plugin.Language = choice;
            CloseLanguageMenu();
        }
    }

    private void DrawLayoutOptions(Rect row)
    {
        if (NeiSkin.Button(row, ToggleLabel("Drag panels to arrange", "拖动面板调整布局", _layoutEditing), selected: _layoutEditing))
        {
            ToggleLayoutEditing();
            if (_layoutEditing) _options = false;
        }
        row.y += 30;
        Adjust(_bookmarkWidth, L("Bookmark width", "书签栏宽度"), 5, 65, 120);
        Adjust(_catalogWidth, L("Catalog width", "目录栏宽度"), 5, 65, 120);
        Adjust(_panelY, L("Vertical offset", "上下偏移"), 1, -6, 5);
        Adjust(_iconSize, L("Item icon size", "物品图标大小"), 5, 70, 150);
        if (NeiSkin.Button(row, L("Reset layout", "恢复默认布局")))
        {
            _bookmarkWidth.Value = _catalogWidth.Value = _iconSize.Value = 100;
            _panelY.Value = _bookmarkX.Value = _bookmarkY.Value = _catalogX.Value = _catalogY.Value = 0;
        }

        void Adjust(ConfigEntry<int> option, string label, int step, int min, int max)
        {
            int value = option.Value;
            if (NeiSkin.IconButton(new Rect(row.x, row.y, 24f, row.height), "remove", label, value > min))
                option.Value = Math.Max(min, value - step);
            NeiSkin.Label(new Rect(row.x + 25f, row.y, row.width - 50f, row.height), $"{label}: {value}%", TextAnchor.MiddleCenter);
            if (NeiSkin.IconButton(new Rect(row.xMax - 24f, row.y, 24f, row.height), "add", label, value < max))
                option.Value = Math.Min(max, value + step);
            row.y += 30f;
        }
    }

    private void DrawItemOptions(Rect row)
    {
        bool modeEnabled = NeiActions.Mode == NeiActions.LocalMode.Cheat;
        string mode = NeiActions.ModeName(NeiActions.Mode);
        if (NeiSkin.Button(row, L($"Mode: {mode}", $"模式：{mode}"), NeiActions.CanEnter || modeEnabled, modeEnabled))
        {
            _plugin.TryCycleMode(out var notice);
            Status(notice);
        }
        row.y += 30;
        if (NeiSkin.Button(row, ToggleLabel("Single-player cheats", "单机作弊", _plugin.AllowCheats), selected: _plugin.AllowCheats))
        {
            _plugin.SetAllowCheats(!_plugin.AllowCheats);
            Status(_plugin.AllowCheats
                ? L("Single-player cheats allowed; multiplayer permissions are set by the server.", "已允许单机作弊；多人权限仍由服务器决定。")
                : L("Local cheats disabled.", "已禁用单机作弊。"));
        }
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("Show item ID", "是否显示物品 ID"), _showTooltipId.Value))
            _showTooltipId.Value = !_showTooltipId.Value;
        row.y += 32;
        var heading = new Rect(row.x + 3, row.y, row.width - 6, 18);
        NeiSkin.OptionsHeading(heading, L("Spawn mode", "刷取模式"));
        row.y = heading.yMax + 4;
        var (nei, toPlayer) = NeiLayout.SpawnModeChoices(row);
        string neiDetails = L("Uses free slots and containers. Keeps your gear.", "放入空槽或容器，不替换装备。");
        string vanillaDetails = L("Drops at your feet for vanilla pickup and equip.", "在脚下生成，按原版规则拾取或穿戴。");
        nei.height = toPlayer.height = Math.Max(NeiSkin.SpawnModeChoiceHeight(neiDetails, nei.width),
            NeiSkin.SpawnModeChoiceHeight(vanillaDetails, toPlayer.width));
        SpawnMode current = _plugin.ItemSpawnMode;
        DrawChoice(nei, SpawnMode.Nei, L("NEI (default)", "NEI（默认）"), neiDetails);
        DrawChoice(toPlayer, SpawnMode.SpawnToPlayer, L("To player", "原版刷给玩家"), vanillaDetails);

        row.x += 3;
        row.width -= 6;
        row.y = nei.yMax + 8;
        DrawNote(L("Uncollected items stay nearby. This choice affects single-player items only; liquids still come in full temporary bottles.",
            "未拾取的物品留在附近。此选项仅影响单机实物；液体仍刷取装满的临时瓶。"));
        DrawNote(L("In Cheat mode, left click gives the quantity set next to search; right click gives one.",
            "作弊模式下，左键按搜索框旁的数量刷取，右键刷取 1 件。"));

        void DrawChoice(Rect choice, SpawnMode mode, string title, string description)
        {
            if (NeiSkin.SpawnModeChoice(choice, title, description, current == mode) && current != mode)
                _plugin.ItemSpawnMode = mode;
        }

        void DrawNote(string text)
        {
            row.height = NeiSkin.OptionsNoteHeight(text, row.width);
            NeiSkin.OptionsNote(row, text);
            row.y += row.height + 6;
        }
    }

    private void DrawKeyOptions(Rect row)
    {
        foreach (var (entry, en, zh) in _plugin.ShortcutBindings)
        {
            bool waiting = _pendingBinding == entry;
            string value = waiting ? L("Press a key…", "请按键…") : entry.Value == KeyCode.None ? L("Unbound", "未绑定") : entry.Value.ToString();
            if (NeiSkin.Button(row, L(en + ": ", zh + "：") + value, selected: waiting))
            {
                GUI.FocusControl(null);
                _pendingBinding = entry;
            }
            row.y += 30;
        }
        row.height = 64;
        NeiSkin.Text(row, L("Choose a shortcut, then press its new key. Esc cancels; Backspace clears it.", "点击要更改的快捷键，再按下新按键。Esc 取消，退格键清除绑定。"));
    }

    internal bool CaptureShortcutBinding()
    {
        if (!_options || _pendingBinding == null) return false;
        // Poll the same KeyCodes as normal gameplay (including mouse and controller buttons).
        // Do not let a captured key also open a recipe, hide NEI, or click a slot underneath.
        if (!Input.anyKeyDown) return true;
        foreach (var key in ShortcutKeys)
        {
            if (key == KeyCode.None || !Input.GetKeyDown(key)) continue;
            if (key != KeyCode.Escape) _pendingBinding.Value = key == KeyCode.Backspace ? KeyCode.None : key;
            _pendingBinding = null;
            break;
        }
        return true;
    }

    private static void Header(Rect p, string heading, string mark)
    {
        if (p.width >= 108) NeiSkin.Label(new Rect(p.x + 8, p.y + 5, p.width - 61, 19), heading);
        NeiSkin.Label(new Rect(p.xMax - Mathf.Min(55, p.width - 5), p.y + 5, Mathf.Min(48, p.width - 7), 19), mark, TextAnchor.MiddleRight, true);
    }
}
