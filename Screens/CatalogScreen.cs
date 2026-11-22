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
        int amount = BookmarkQuantity;
        string tip = L("Bookmark quantity / multiplier step · Cheat gives at most 64", "书签数量／乘数步进 · 作弊给予最多 64 件");
        if (NeiSkin.IconButton(new Rect(row.x, row.y, cap, row.height), "remove", L("Decrease quantity", "减少数量"), amount > 1))
            _quantityText = (amount - 1).ToString(CultureInfo.InvariantCulture);
        _quantityRect = new Rect(row.x + cap + 3, row.y, row.width - 2 * cap - 6, row.height);
        string input = NeiSkin.TextField(_quantityRect, _quantityText, QuantityControl, 4);
        if (input.All(c => c >= '0' && c <= '9')) _quantityText = input;
        if (NeiSkin.IconButton(new Rect(row.xMax - cap, row.y, cap, row.height), "add", L("Increase quantity", "增加数量"), amount < BookmarkBook.MaximumQuantity))
            _quantityText = (amount + 1).ToString(CultureInfo.InvariantCulture);
        if (_quantityRect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != QuantityControl)
            Tip(tip, null);
        if (GUI.GetNameOfFocusedControl() != QuantityControl)
            _quantityText = BookmarkQuantity.ToString(CultureInfo.InvariantCulture);
    }

    private void DrawBottomRail(bool showSearch)
    {
        var layout = NeiLayout.BottomRail(_width, _height, _center, ShowRight ? _right : default);
        if (showSearch)
        {
            _searchRect = layout.Search;
            DrawSearch(_searchRect);
        }
        DrawLeftTools(layout.Tools);
    }

    private void DrawSearch(Rect rect)
    {
        var value = DrawSearchInput(rect, _query, L("Search items…", "搜索物品…"));
        if (value != _query) { _query = value; _page = 0; ApplyFilter(); }
        if (rect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != SearchControl)
            Tip(L("Search · Ctrl+F", "搜索 · Ctrl+F"), L("@source  $tag  #description  &id  -exclude\nUse quotes for a phrase. Right click to clear.", "@来源  $标签  #描述  &ID  -排除\n短语用双引号括住。右键清空。"));
    }

    private string DrawSearchInput(Rect rect, string value, string placeholder)
    {
        if (rect.width < 22f || rect.height < 15f) return value;
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
        {
            value = string.Empty;
            _focusSearch = true;
            e.Use();
        }
        string next = NeiSkin.TextField(rect, value, SearchControl, 160);
        if (_focusSearch && e.type == EventType.Repaint)
        {
            GUI.FocusControl(SearchControl);
            _focusSearch = false;
        }
        if (rect.width > 82 && next.Length == 0 && GUI.GetNameOfFocusedControl() != SearchControl)
            NeiSkin.Label(new Rect(rect.x + 6, rect.y, rect.width - 12, rect.height), placeholder, muted: true);
        return next;
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
