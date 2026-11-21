using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private BookmarkBook.Entry? _bookmarkDrag;
    private Vector2 _bookmarkDragStart;
    private bool _reorderBookmark;
    private string? _dragGroup;
    private readonly List<(Rect Rect, string Group)> _bookmarkHeaders = new();

    private void DrawBookmarkPanel(Rect panel)
    {
        NeiSkin.Background(panel, "bookmarks");
        float x = panel.x + 3, w = panel.width - 6;
        var paging = new Rect(x, panel.y + 2, w, 23);
        float y = paging.yMax + 3;
        if (_groups)
        {
            _groupRect = new Rect(x, y, Mathf.Max(0, w - 65), 22);
            _editGroup = NeiSkin.TextField(_groupRect, _editGroup, GroupControl, 32);
            if (NeiSkin.IconButton(new Rect(x + w - 63, y, 20, 22), "add", L("Open named bookmark page", "打开命名书签页"), !string.IsNullOrWhiteSpace(_editGroup)))
            {
                SelectBookmarkGroup(_editGroup.Trim()); _editGroup = string.Empty; _groups = false; _skipFocusFrame = true;
            }
            if (NeiSkin.Button(new Rect(x + w - 41, y, 19, 22), "↑"))
            {
                GUIUtility.systemCopyBuffer = _book.Serialize(_book.Page(_group));
                Status(L("Bookmark page copied", "书签页已复制"));
            }
            if (NeiSkin.Button(new Rect(x + w - 20, y, 20, 22), "↓"))
            {
                if (_book.Load(GUIUtility.systemCopyBuffer, _group)) { SaveBookmarks(); Status(L("Bookmarks imported", "已导入书签")); }
                else Status(L("No new compatible bookmarks on the clipboard", "剪贴板中没有新的兼容书签"));
            }
            y += 26;
        }
        var footer = new Rect(x, panel.yMax - 27, w, 23);
        var tools = NeiLayout.BottomRail(_width, _height, _center, _right).Tools;
        if (footer.Overlaps(tools)) footer.y = tools.yMin - footer.height - 3;
        DrawBookmarkEntries(new Rect(x, y, w, footer.yMin - y - 3), paging);
        if (w >= 85) DrawBookmarkPages(footer);
    }

    private void DrawBookmarkEntries(Rect rect, Rect navigation, bool compact = false)
    {
        if (rect.width < 22 || rect.height < 22) return;
        var e = Event.current;
        var entries = _book.Page(_group).ToArray();
        var groups = entries.GroupBy(entry => entry.Group, StringComparer.Ordinal).ToArray();
        int cell = compact ? 26 : 29;
        int columns = Math.Max(1, (int)((rect.width - (compact ? 0 : 18)) / cell));
        int rows = Math.Max(1, (int)(rect.height / cell));
        var layout = new List<(string Group, BookmarkBook.Entry? Item, int Row, int Column)>();
        int row = 0;
        foreach (var group in groups)
        {
            int i = 0;
            foreach (var item in group)
            {
                layout.Add((group.Key, item, row + i / columns, i % columns));
                if (++i == 1 && group.Key.Length > 0 && _collapsedGroups.Contains(group.Key)) break;
            }
            row += (i + columns - 1) / columns;
        }
        int pages = Math.Max(1, (row + rows - 1) / rows);
        _bookmarkPage = Mathf.Clamp(_bookmarkPage, 0, pages - 1);
        if (e.type == EventType.MouseDown && e.button == 1 && navigation.Contains(e.mousePosition) && entries.Length > 0)
        {
            OpenChain(string.Empty, e.shift);
            e.Use();
        }
        DrawPageButtons(navigation, ref _bookmarkPage, pages);
        if (entries.Length == 0 && rect.width > 110)
            NeiSkin.Label(new Rect(rect.x + 4, rect.y, rect.width - 8, 22), L($"{_plugin.BookmarkKey} adds a bookmark", $"按 {_plugin.BookmarkKey} 添加书签"), muted: true);

        int first = _bookmarkPage * rows;
        var shown = layout.Where(item => item.Row >= first && item.Row < first + rows).ToArray();
        BookmarkBook.Entry? hovered = null;
        (Rect Rect, string Group) hitGroup = default;
        if (!compact)
            foreach (var group in shown.Where(item => item.Group.Length > 0).GroupBy(item => item.Group))
            {
                float top = rect.y + (group.Min(item => item.Row) - first) * cell;
                float height = (group.Max(item => item.Row) - group.Min(item => item.Row) + 1) * cell;
                var bracket = new Rect(rect.x, top, 16, height - 1);
                NeiSkin.Label(new Rect(bracket.x + 1, bracket.y, 13, height), _collapsedGroups.Contains(group.Key) ? "▸" : "[", TextAnchor.MiddleCenter);
                if (e.type == EventType.Repaint) _bookmarkHeaders.Add((bracket, group.Key));
                if (bracket.Contains(e.mousePosition))
                {
                    hitGroup = (bracket, group.Key);
                    Tip(L("Bookmark group", "书签组"), L("Left: fold · Right: crafting chain · Alt+left: ungroup\nShift+A: remove group", "左键折叠 · 右键合成链 · Alt＋左键取消分组\nShift＋A 删除整组"));
                    if (e.type == EventType.MouseDown && e.button == 1) { OpenChain(group.Key, e.shift); e.Use(); }
                    else if (e.type == EventType.MouseDown && e.button == 0 && _bookmarkDrag == null)
                    {
                        if (e.alt) { if (_book.Ungroup(_group, group.Key)) SaveBookmarks(); }
                        else { _dragGroup = group.Key; _bookmarkDragStart = e.mousePosition; }
                        e.Use();
                    }
                }
            }

        foreach (var (group, item, line, column) in shown)
        {
            if (item == null) continue;
            var slot = new Rect(rect.x + (compact ? 0 : 18) + column * cell, rect.y + (line - first) * cell, cell - 1, cell - 1);
            if (slot.Contains(e.mousePosition))
            {
                hovered = item;
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    _bookmarkDrag = item; _bookmarkDragStart = e.mousePosition; _reorderBookmark = e.shift; e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && e.alt && _book.Split(item)) { SaveBookmarks(); e.Use(); }
            }
            ItemSlot(slot, item.Id, amount: item.Quantity > 1 ? item.Quantity.ToString() : null, bookmarkSlot: true);
        }
        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        {
            int direction = e.delta.y < 0 ? 1 : -1;
            if (e.control && hovered != null)
            {
                if (_book.Adjust(hovered, direction * (e.alt ? GiveQuantity : 1))) SaveBookmarks();
                e.Use();
            }
            else if (e.shift) { ChangeBookmarkPage(direction); e.Use(); }
            else if (pages > 1) { _bookmarkPage = Wrap(_bookmarkPage - direction, pages); e.Use(); }
        }
        if (e.type == EventType.MouseDrag && (_bookmarkDrag != null || _dragGroup != null)) e.Use();
        if (e.type == EventType.MouseUp && e.button == 0 && _dragGroup is { } groupDrag)
        {
            _dragGroup = null;
            if (Vector2.Distance(_bookmarkDragStart, e.mousePosition) < 4f && hitGroup.Group == groupDrag)
            {
                if (!_collapsedGroups.Add(groupDrag)) _collapsedGroups.Remove(groupDrag);
            }
            else
            {
                hovered ??= _book.Page(_group).FirstOrDefault(item => hitGroup.Group != null && item.Group == hitGroup.Group);
                if (hovered != null && _book.MoveGroup(_group, groupDrag, hovered)) SaveBookmarks();
            }
            e.Use();
        }
        if (e.type == EventType.MouseUp && e.button == 0 && _bookmarkDrag != null)
        {
            var source = _bookmarkDrag;
            _bookmarkDrag = null;
            if (Vector2.Distance(_bookmarkDragStart, e.mousePosition) >= 4)
            {
                hovered ??= _book.Page(_group).FirstOrDefault(entry => hitGroup.Group != null && entry.Group == hitGroup.Group);
                if (hovered != null && _book.Move(source, hovered, _reorderBookmark)) SaveBookmarks();
            }
            else if (source == hovered)
            {
                _catalog.TryGetItem(source.Id, out var entry);
                if (CanGiveSlot(true, source.Id, entry, e.button, _reorderBookmark || e.shift, e.alt)) GiveSlot(entry!, e.button);
                else Lookup(source.Id, false);
            }
            e.Use();
        }
    }

    private void DrawBookmarkPages(Rect row)
    {
        var pages = BookmarkPages();
        int index = Array.IndexOf(pages, _group);
        const float button = 23;
        if (NeiSkin.IconButton(new Rect(row.x, row.y, button, row.height), "previous", L("Previous bookmark page", "上一张书签页"), pages.Length > 1)) ChangeBookmarkPage(-1);
        var name = _group == "0" ? L("Default", "默认页") : _group;
        if (NeiSkin.Button(new Rect(row.x + button + 3, row.y, row.width - 2 * button - 6, row.height), $"{name}  {index + 1}/{pages.Length}", selected: _groups))
        { _groups = !_groups; if (!_groups) _skipFocusFrame = true; }
        if (NeiSkin.IconButton(new Rect(row.xMax - button, row.y, button, row.height), "next", L("Next bookmark page", "下一张书签页"), pages.Length > 1)) ChangeBookmarkPage(1);
    }

    private string[] BookmarkPages() => new[] { "0" }.Concat(_book.Entries.Select(e => e.Page)).Append(_group).Distinct().ToArray();
    private void ChangeBookmarkPage(int delta)
    {
        var pages = BookmarkPages();
        SelectBookmarkGroup(pages[Wrap(Array.IndexOf(pages, _group) + delta, pages.Length)]);
    }
    private void SelectBookmarkGroup(string group) { _group = group; _bookmarkPage = 0; _bookmarkDrag = null; }

    private void DrawLeftTools(Rect rect)
    {
        _leftTools = rect;
        float side = rect.height;
        var wrench = new Rect(rect.x, rect.y, side, side);
        var e = Event.current;
        if (e.control && e.type == EventType.MouseDown && e.button == 0 && wrench.Contains(e.mousePosition))
        {
            _plugin.TryCycleMode(out string notice);
            Status(notice);
            e.Use();
        }
        string mode = NeiActions.ModeName(NeiActions.Mode);
        if (NeiSkin.IconButton(wrench, "options", L($"{mode} mode · Ctrl+click to change", $"{mode}模式 · Ctrl＋点击切换"), selected: NeiActions.Mode != NeiActions.LocalMode.Recipe))
        { _options = true; _skipFocusFrame = true; }
        bool compact = _left.width < 35 || _left.height < 100;
        if (NeiSkin.IconButton(new Rect(rect.x + side + 3, rect.y, side, side), "bookmark", L("Show or hide bookmarks", "显示或隐藏书签"), selected: compact ? _compactBookmarks : _bookmarksVisible))
        {
            if (compact) { _bookmarksVisible = true; _compactBookmarks = !_compactBookmarks; }
            else ToggleBookmarks();
        }
    }
}
