using System;
using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private enum OptionsTab { Display, Layout, Items, Keys, Debug }
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
        Header(p, L("options.title"), "NEI");
        float x = p.x + 12, w = p.width - 24, tabWidth = (w - 8) / 5;
        DrawTab(OptionsTab.Display, L("options.tab.display"));
        DrawTab(OptionsTab.Layout, L("options.tab.layout"));
        DrawTab(OptionsTab.Items, L("options.tab.items"));
        DrawTab(OptionsTab.Keys, L("options.tab.keys"));
        DrawTab(OptionsTab.Debug, L("options.tab.debug"));

        var row = FirstOptionsRow(p);
        switch (_optionsTab)
        {
            case OptionsTab.Display: DrawDisplayOptions(row); DrawLanguageMenu(LanguageRow(p)); break;
            case OptionsTab.Layout: DrawLayoutOptions(row); break;
            case OptionsTab.Items: DrawItemOptions(row); break;
            case OptionsTab.Keys: DrawKeyOptions(row); break;
            case OptionsTab.Debug: DrawDebugOptions(row); break;
        }
        if (NeiSkin.Button(new Rect(x, p.yMax - 35, w, 26), L("options.done"))) Back();

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

    private void DrawDisplayOptions(Rect row)
    {
        if (NeiSkin.Checkbox(row, L("options.display.inventory_catalog"), _showInventoryGrid.Value))
            _showInventoryGrid.Value = !_showInventoryGrid.Value;
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.display.terminal_catalog"), _showGrid.Value))
            _showGrid.Value = !_showGrid.Value;
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.display.bookmarks"), _bookmarksVisible)) ToggleBookmarks();
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.display.history_strip"), _showHistory.Value))
            _showHistory.Value = !_showHistory.Value;
        row.y += 30;
        float scale = _plugin.Scale;
        if (NeiSkin.IconButton(new Rect(row.x, row.y, 26, row.height), "remove", L("options.display.smaller_sidebar"), scale > .7f))
            _plugin.Scale = Mathf.Round((scale - .05f) * 100f) / 100f;
        NeiSkin.Label(new Rect(row.x + 28, row.y, row.width - 56, row.height),
            L("options.display.sidebar_scale", scale), TextAnchor.MiddleCenter);
        if (NeiSkin.IconButton(new Rect(row.xMax - 26, row.y, 26, row.height), "add", L("options.display.larger_sidebar"), scale < 1.4f))
            _plugin.Scale = Mathf.Round((scale + .05f) * 100f) / 100f;
    }

    internal void CloseLanguageMenu() => _languageMenuOpen = false;

    private static Rect FirstOptionsRow(Rect panel) => new(panel.x + 12, panel.y + 62, panel.width - 24, 26);

    private static Rect LanguageRow(Rect panel)
    {
        var row = FirstOptionsRow(panel);
        row.y += 5 * 30; // Four toggles and the scale control precede the language selector.
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
        "en" => L("options.language.english"),
        "zh-CN" => L("options.language.simplified_chinese"),
        _ => L("options.language.auto")
    };

    private void DrawLanguageMenu(Rect row)
    {
        string language = _plugin.Language;
        bool expanded = _languageMenuOpen;
        if (NeiSkin.DropDownButton(row, L("options.language.label", LanguageName(language)), expanded))
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
        if (NeiSkin.Button(row, L("options.layout.drag_panels")))
            ToggleLayoutEditing();
        row.y += 30;
        Adjust(_bookmarkWidth, "options.layout.bookmark_width", 5, 65, 120);
        Adjust(_catalogWidth, "options.layout.catalog_width", 5, 65, 120);
        Adjust(_panelY, "options.layout.vertical_offset", 1, -6, 5);
        Adjust(_iconSize, "options.layout.icon_size", 5, 70, 150);
        if (NeiSkin.Button(row, L("options.layout.reset")))
        {
            _bookmarkWidth.Value = _catalogWidth.Value = _iconSize.Value = 100;
            _panelY.Value = _bookmarkX.Value = _bookmarkY.Value = _catalogX.Value = _catalogY.Value = 0;
        }

        void Adjust(ConfigEntry<int> option, string labelKey, int step, int min, int max)
        {
            string label = L(labelKey);
            int value = option.Value;
            if (NeiSkin.IconButton(new Rect(row.x, row.y, 24f, row.height), "remove", label, value > min))
                option.Value = Math.Max(min, value - step);
            NeiSkin.Label(new Rect(row.x + 25f, row.y, row.width - 50f, row.height), L("options.layout.percentage", label, value), TextAnchor.MiddleCenter);
            if (NeiSkin.IconButton(new Rect(row.xMax - 24f, row.y, 24f, row.height), "add", label, value < max))
                option.Value = Math.Min(max, value + step);
            row.y += 30f;
        }
    }

    private void DrawItemOptions(Rect row)
    {
        bool modeEnabled = NeiActions.Mode == NeiActions.LocalMode.Cheat;
        string mode = NeiActions.ModeName(NeiActions.Mode);
        if (NeiSkin.Button(row, L("options.items.mode", mode), NeiActions.CanEnter || modeEnabled, modeEnabled))
        {
            _plugin.TryCycleMode(out var notice);
            Status(notice);
        }
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.items.single_player_cheats"), _plugin.AllowCheats))
        {
            _plugin.SetAllowCheats(!_plugin.AllowCheats);
            Status(_plugin.AllowCheats
                ? L("options.items.cheats_allowed")
                : L("options.items.cheats_disabled"));
        }
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.items.show_item_id"), _showTooltipId.Value))
            _showTooltipId.Value = !_showTooltipId.Value;
        row.y += 32;
        var heading = new Rect(row.x + 3, row.y, row.width - 6, 18);
        NeiSkin.OptionsHeading(heading, L("options.items.spawn_mode"));
        row.y = heading.yMax + 4;
        var (nei, toPlayer) = NeiLayout.SpawnModeChoices(row);
        string neiDetails = L("options.items.spawn_nei_details");
        string vanillaDetails = L("options.items.spawn_vanilla_details");
        nei.height = toPlayer.height = Math.Max(NeiSkin.SpawnModeChoiceHeight(neiDetails, nei.width),
            NeiSkin.SpawnModeChoiceHeight(vanillaDetails, toPlayer.width));
        SpawnMode current = _plugin.ItemSpawnMode;
        DrawChoice(nei, SpawnMode.Nei, L("options.items.spawn_nei"), neiDetails);
        DrawChoice(toPlayer, SpawnMode.SpawnToPlayer, L("options.items.spawn_vanilla"), vanillaDetails);

        row.x += 3;
        row.width -= 6;
        row.y = nei.yMax + 8;
        DrawNote(L("options.items.spawn_note"));
        DrawNote(L("options.items.quantity_note"));

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

    private void DrawDebugOptions(Rect row)
    {
        NeiSkin.OptionsHeading(new Rect(row.x, row.y, row.width, 20), L("options.debug.title"));
        row.y += 28;
        if (NeiSkin.Button(row, L("options.debug.retry_catalog")))
        {
            _plugin.RetryCatalogRefresh();
            ResetRecipeResults();
            Status(_lastError == null
                ? L("options.debug.refresh_success")
                : L("options.debug.refresh_failed"));
        }
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.debug.spawn_missing_materials"), _plugin.DebugSpawnMissingMaterials))
            _plugin.DebugSpawnMissingMaterials = !_plugin.DebugSpawnMissingMaterials;
        row.y += 30;
        if (NeiSkin.Checkbox(row, L("options.debug.fill_method_tabs"), _previewMethodTabs))
        {
            _previewMethodTabs = !_previewMethodTabs;
            _previewMethod = null;
            _methodTabs.Reset();
            _recipeTargets.Clear();
        }
        row.y += 30;
        string hint = L("options.debug.preview_methods_help");
        row.height = NeiSkin.OptionsNoteHeight(hint, row.width);
        NeiSkin.OptionsNote(row, hint);
        row.y = row.yMax + 12;
        string detail;
        try
        {
            detail = _lastError == null
                ? L("options.debug.catalog_summary", _catalog.Items.Count, _catalog.Recipes.Count)
                : L("options.debug.last_catalog_error", _lastError);
        }
        catch (Exception error)
        {
            _lastError = error.Message;
            detail = L("options.debug.catalog_not_ready");
        }
        row.height = NeiSkin.OptionsNoteHeight(detail, row.width);
        NeiSkin.OptionsNote(row, detail);
    }

    private void DrawKeyOptions(Rect row)
    {
        foreach (var (entry, labelKey) in _plugin.ShortcutBindings)
        {
            bool waiting = _pendingBinding == entry;
            string value = waiting ? L("options.keys.press_key") : entry.Value == KeyCode.None ? L("options.keys.unbound") : entry.Value.ToString();
            if (NeiSkin.Button(row, L("options.keys.binding_value", L(labelKey), value), selected: waiting))
            {
                GUI.FocusControl(null);
                _pendingBinding = entry;
            }
            row.y += 30;
        }
        row.height = 64;
        NeiSkin.Text(row, L("options.keys.capture_help"));
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
