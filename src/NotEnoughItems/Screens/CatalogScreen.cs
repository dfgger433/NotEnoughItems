using System.Globalization;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private const string CatalogQuantityControl = "NEI.CatalogQuantity";
    private string? _catalogQuantityDraft;

    private void DrawCatalog(Rect panel, NeiLayout.CatalogPanel layout)
    {
        NeiSkin.Background(panel, "catalog");
        Rect grid = layout.Grid;
        float cell = Mathf.Min(28 * IconScale, Mathf.Min(grid.width, grid.height));
        if (cell < 12) return;
        int columns = (int)(grid.width / cell);
        int rows = (int)(grid.height / cell);
        int size = columns * rows;
        int pages = size > 0 ? Mathf.Max(1, (_filtered.Count + size - 1) / size) : 1;
        _page = Mathf.Clamp(_page, 0, pages - 1);
        var e = Event.current;
        if (pages > 1 && e.type == EventType.ScrollWheel && grid.Contains(e.mousePosition))
        {
            _page = Wrap(_page + (e.delta.y > 0 ? 1 : -1), pages);
            e.Use();
        }
        if (NeiSkin.IconButton(layout.Previous, "previous", L("Previous page", "上一页"), pages > 1))
            _page = Wrap(_page - 1, pages);
        if (NeiSkin.IconButton(layout.Next, "next", L("Next page", "下一页"), pages > 1))
            _page = Wrap(_page + 1, pages);
        NeiSkin.Label(layout.Page, $"{_page + 1}/{pages}", TextAnchor.MiddleCenter);

        int start = _page * size;
        for (int i = 0; i < size && start + i < _filtered.Count; i++)
        {
            var slot = new Rect(grid.x + i % columns * cell, grid.y + i / columns * cell, cell - 1, cell - 1);
            ItemSlot(slot, _filtered[start + i].BrowseId, catalogSlot: true);
        }
        if (_filtered.Count == 0)
            NeiSkin.Label(new Rect(grid.x, grid.y + 4, grid.width, 24), L("No matching items", "没有匹配物品"), TextAnchor.MiddleCenter, true);
    }

    private void DrawCatalogControls(Rect panel, NeiLayout.CatalogPanel layout)
    {
        NeiSkin.Background(panel, "catalog");
        Rect history = layout.History;
        if (history.height > 0)
        {
            DrawHistoryDivider(layout.HistoryDivider);
            float historyCell = Mathf.Min(history.height, 28 * IconScale);
            int capacity = (int)(history.width / historyCell), drawn = 0;
            for (int i = 0; i < _recent.Count && drawn < capacity; i++)
            {
                // History is normalized to browse IDs at lookup time. Re-resolve here
                // so stale entries from an unloaded provider cannot draw a misleading
                // first-letter placeholder over the footer; compact the remaining rows.
                if (!_catalog.TryGetItem(_recent[i], out var recent) || !recent.IsCatalogVisible) continue;
                ItemSlot(new Rect(history.x + drawn * historyCell, history.y, historyCell - 1, historyCell - 1),
                    recent.BrowseId, catalogSlot: true);
                drawn++;
            }
        }
        DrawSearch(layout.Search);
        DrawCatalogQuantity(layout.Quantity, layout.Increase, layout.Decrease);
    }

    private static void DrawHistoryDivider(Rect divider)
    {
        if (Event.current.type != EventType.Repaint || divider.width <= 0 || divider.height <= 0) return;
        Color previous = GUI.color;
        try
        {
            GUI.color = new Color32(195, 198, 216, 190);
            const float dash = 5f, gap = 4f;
            for (float x = divider.x; x < divider.xMax; x += dash + gap)
                GUI.DrawTexture(new Rect(x, divider.y, Mathf.Min(dash, divider.xMax - x), divider.height),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
        }
        finally { GUI.color = previous; }
    }

    private void DrawCatalogQuantity(Rect field, Rect increase, Rect decrease)
    {
        var e = Event.current;
        int amount = _spawnAmount.Value;
        bool focused = GUI.GetNameOfFocusedControl() == CatalogQuantityControl;
        bool selectOnFocus = !focused && e.type == EventType.MouseDown && e.button == 0 && field.Contains(e.mousePosition);
        if (focused && e.type == EventType.KeyDown)
        {
            if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                GUI.FocusControl(null);
                focused = false;
                e.Use();
            }
            else if (!e.control && !e.command && e.character >= ' ' && e.character != '\u007f' && e.character is not (>= '0' and <= '9'))
                e.Use(); // Let Unity handle numeric input, selection, editing and clipboard shortcuts.
        }

        if (!focused) _catalogQuantityDraft = null;
        string text = _catalogQuantityDraft ?? amount.ToString(CultureInfo.InvariantCulture);
        // Don't truncate a pasted number before validation (e.g. 109 must not become 10).
        string input = NeiSkin.TextField(field, text, CatalogQuantityControl, maxLength: -1);
        if (selectOnFocus && GUI.GetNameOfFocusedControl() == CatalogQuantityControl &&
            GUIUtility.QueryStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor)
            editor.SelectAll();
        if (input != text && NeiActions.TryReadGiveAmount(input, out int? parsed))
        {
            if (parsed is int next && next != amount) _spawnAmount.Value = amount = next;
            // Only blank/zero drafts need separate state so the user can keep editing them.
            _catalogQuantityDraft = parsed.HasValue ? null : input.Trim().Length == 0 ? string.Empty : "0";
        }

        int delta = 0;
        if (NeiSkin.IconButton(increase, "add", L("Increase quantity", "增加数量"), amount < NeiActions.MaxGiveAmount)) delta++;
        if (NeiSkin.IconButton(decrease, "remove", L("Decrease quantity", "减少数量"), amount > 1)) delta--;
        if (delta != 0)
        {
            _spawnAmount.Value = NeiActions.ClampGiveAmount(amount + delta);
            _catalogQuantityDraft = null;
        }
        if (field.Contains(e.mousePosition) && GUI.GetNameOfFocusedControl() != CatalogQuantityControl)
            Tip(L($"Quantity (1–{NeiActions.MaxGiveAmount})", $"数量（1–{NeiActions.MaxGiveAmount}）"), L("Cheat mode: left click gives this many; right click gives one. Also used by bookmark quantity shortcuts.",
                "作弊模式：左键刷取所选件数，右键刷取 1 件。带数量的书签快捷键也使用此值。"));
    }

    private void DrawSearch(Rect rect)
    {
        var value = DrawSearchInput(rect, _query, L("Search…", "搜索…"), CatalogSearchControl);
        if (value != _query)
        {
            _query = value;
            _page = 0;
            ApplyFilter();
        }
        if (rect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != CatalogSearchControl)
            Tip(L("Search · Ctrl+F", "搜索 · Ctrl+F"), L("@source  $tag  #description  &id  -exclude\nUse quotes for a phrase. Right click to clear.", "@来源  $标签  #描述  &ID  -排除\n短语用双引号括住。右键清空。"));
    }

    private string DrawSearchInput(Rect rect, string value, string placeholder, string controlName = SearchControl)
    {
        if (rect.width < 22f || rect.height < 15f) return value;
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
        {
            value = string.Empty;
            _focusSearch = controlName;
            e.Use();
        }
        string next = NeiSkin.SearchField(rect, value, controlName, 160);
        if (_focusSearch == controlName && e.type == EventType.Repaint)
        {
            GUI.FocusControl(controlName);
            _focusSearch = null;
        }
        if (rect.width > 40 && next.Length == 0 && GUI.GetNameOfFocusedControl() != controlName)
            NeiSkin.Label(new Rect(rect.x + 6, rect.y, rect.width - 12, rect.height), placeholder, muted: true);
        return next;
    }
}
