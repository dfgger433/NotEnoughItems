using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void DrawCatalog(Rect panel)
    {
        var layout = new NeiLayout.CatalogPanel(panel);
        NeiSkin.Background(panel, "catalog");
        DrawCatalogGroups(layout.Group, layout.Name, layout.Add, layout.Remove);
        DrawSearch(layout.Search);

        Rect grid = layout.Grid;
        bool history = _showHistory.Value && _recent.Count > 0 && grid.height > 100;
        if (history) grid.height -= 30;
        const float cell = 28;
        int columns = Mathf.Max(1, (int)(grid.width / cell));
        int rows = Mathf.Max(1, (int)(grid.height / cell));
        int size = columns * rows;
        int pages = Mathf.Max(1, (_filtered.Count + size - 1) / size);
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
        if (history)
        {
            for (int i = 0; i < Mathf.Min(_recent.Count, columns); i++)
                ItemSlot(new Rect(grid.x + i * cell, grid.yMax, cell - 1, cell - 1), _recent[i]);
        }
    }

    // Quantity stays available from the bookmark page controls while its former catalog row holds the group name.
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

    private void DrawSearch(Rect rect)
    {
        var value = DrawSearchInput(rect, _query, L("Search…", "搜索…"), CatalogSearchControl);
        if (value != _query)
        {
            _query = value;
            _page = 0;
            SaveCatalogQuery(value);
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
