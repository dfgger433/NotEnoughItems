using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void DrawCatalog(Rect panel)
    {
        NeiSkin.Background(panel, "catalog");
        float x = panel.x + 3, w = panel.width - 6;
        var paging = new Rect(x, panel.y + 2, w, 23);
        float top = paging.yMax + 3;
        float bottom = panel.yMax - 32;
        if (NeiLayout.BottomRail(_width, _height, _center, _right).Search.Overlaps(panel)) bottom -= 28;
        bool history = _showHistory.Value && _recent.Count > 0 && bottom - top > 100;
        if (history) bottom -= 30;
        DrawGrid(new Rect(x, top, w, bottom - top), _filtered.Count, i => _filtered[i].BrowseId, ref _page, paging, 28);
        if (_filtered.Count == 0 && w > 78)
            NeiSkin.Label(new Rect(x, top + 4, w, 24), L("No matching items", "没有匹配物品"), TextAnchor.MiddleCenter, true);
        if (history)
        {
            const float cell = 27;
            for (int i = 0; i < Math.Min(_recent.Count, (int)(w / cell)); i++)
                ItemSlot(new Rect(x + i * cell, bottom, cell, cell), _recent[i]);
        }
        if (w >= 81) DrawQuantity(new Rect(x, panel.yMax - 26, w, 23));
    }

    // The lower-right field is the quantity; search lives in its own lower-central control.
    private void DrawQuantity(Rect row)
    {
        float cap = Mathf.Min(23, (row.width - 28) / 3);
        int amount = GiveQuantity;
        string tip = L("Quantity for Ctrl + left click in local cheat mode", "数量 · 单机作弊模式 Ctrl＋左键给予");
        if (NeiSkin.IconButton(new Rect(row.x, row.y, cap, row.height), "remove", L("Decrease quantity", "减少数量"), amount > 1))
            _quantityText = (amount - 1).ToString(CultureInfo.InvariantCulture);
        _quantityRect = new Rect(row.x + cap + 3, row.y, row.width - 2 * cap - 6, row.height);
        string input = NeiSkin.TextField(_quantityRect, _quantityText, QuantityControl, 2);
        if (input.All(c => c >= '0' && c <= '9')) _quantityText = input;
        if (NeiSkin.IconButton(new Rect(row.xMax - cap, row.y, cap, row.height), "add", L("Increase quantity", "增加数量"), amount < 64))
            _quantityText = (amount + 1).ToString(CultureInfo.InvariantCulture);
        if (_quantityRect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != QuantityControl)
            Tip(tip, null);
        if (GUI.GetNameOfFocusedControl() != QuantityControl)
            _quantityText = GiveQuantity.ToString(CultureInfo.InvariantCulture);
    }

    private void DrawCompactBar()
    {
        var layout = NeiLayout.BottomRail(_width, _height, _center, ShowRight ? _right : default);
        _compactBar = layout.Search;
        if (!ShowRight && _showGrid.Value || _compactBookmarks && !ShowLeft)
        {
            var stripBase = NeiLayout.BottomRail(_width, _height, default, default).Search;
            var strip = _compactGrid = new Rect(stripBase.x, stripBase.y - 58, stripBase.width, 54);
            NeiSkin.Background(strip, "catalog");
            var grid = new Rect(strip.x + 2, strip.y + 26, strip.width - 4, 26);
            var nav = new Rect(strip.x + 2, strip.y, strip.width - 4, 22);
            if (_compactBookmarks && !ShowLeft)
            {
                DrawBookmarkEntries(grid, nav, compact: true);
            }
            else DrawGrid(grid, _filtered.Count, i => _filtered[i].BrowseId, ref _page, nav);
        }
        if (_showGrid.Value)
        {
            _searchRect = _compactBar;
            DrawSearch(_searchRect);
        }
        DrawLeftTools(layout.Tools);
    }

    private void DrawSearch(Rect rect)
    {
        var value = NeiSkin.TextField(rect, _query, SearchControl, 160);
        if (_focusSearch && Event.current.type == EventType.Repaint) { GUI.FocusControl(SearchControl); _focusSearch = false; }
        if (value != _query) { _query = value; _page = 0; ApplyFilter(); }
        if (rect.width > 82 && _query.Length == 0 && GUI.GetNameOfFocusedControl() != SearchControl)
            NeiSkin.Label(new Rect(rect.x + 6, rect.y, rect.width - 12, rect.height), L("Search items…", "搜索物品…"), muted: true);
        if (rect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != SearchControl)
            Tip(L("Search · Ctrl+F", "搜索 · Ctrl+F"), L("@source  $tag  #description  &id  -exclude\nUse quotes for a phrase.", "@来源  $标签  #描述  &ID  -排除\n短语用双引号括住。"));
    }

    private void DrawGrid(Rect rect, int count, Func<int, string> idAt, ref int page, Rect navigation, float cellSize = 28)
    {
        if (rect.width < 20 || rect.height < 20) return;
        float cell = Mathf.Min(cellSize, Mathf.Min(rect.height, rect.width));
        int columns = Mathf.Max(1, (int)(rect.width / cell));
        int rows = Mathf.Max(1, (int)(rect.height / cell));
        int size = columns * rows;
        int pages = Mathf.Max(1, (count + size - 1) / size);
        page = Mathf.Clamp(page, 0, pages - 1);
        var e = Event.current;
        if (pages > 1 && e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        {
            page = Wrap(page + (e.delta.y > 0 ? 1 : -1), pages);
            e.Use();
        }
        DrawPageButtons(navigation, ref page, pages);
        int start = page * size;
        for (int i = 0; i < size && start + i < count; i++)
            ItemSlot(new Rect(rect.x + i % columns * cell, rect.y + i / columns * cell, cell - 1, cell - 1), idAt(start + i), catalogSlot: true);
    }

    private void DrawPageButtons(Rect rect, ref int page, int pages)
    {
        float button = Mathf.Min(rect.height, rect.width / 3);
        if (NeiSkin.IconButton(new Rect(rect.x, rect.y, button, rect.height), "previous", L("Previous page", "上一页"), pages > 1))
            page = Wrap(page - 1, pages);
        if (NeiSkin.IconButton(new Rect(rect.xMax - button, rect.y, button, rect.height), "next", L("Next page", "下一页"), pages > 1))
            page = Wrap(page + 1, pages);
        if (rect.width > 60)
            NeiSkin.Label(new Rect(rect.x + button, rect.y, rect.width - 2 * button, rect.height), $"{page + 1}/{pages}", TextAnchor.MiddleCenter);
    }
}
