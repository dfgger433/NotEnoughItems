using System;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private string[] CurrentBookmarks() => _bookmarks.Where(entry => entry.Group == _group).Select(entry => entry.Id).ToArray();

    private void DrawBookmarkPanel(Rect panel)
    {
        NeiSkin.Background(panel, "bookmarks");
        float x = panel.x + 3, w = panel.width - 6;
        var paging = new Rect(x, panel.y + 2, w, 23);
        float y = paging.yMax + 3;
        if (_groups)
        {
            _groupRect = new Rect(x, y, Mathf.Max(0, w - 23), 22);
            _editGroup = NeiSkin.TextField(_groupRect, _editGroup, GroupControl, 32);
            if (NeiSkin.IconButton(new Rect(x + w - 21, y, 21, 22), "add", L("Use named bookmark page", "打开命名书签页"),
                !string.IsNullOrWhiteSpace(_editGroup)))
            {
                _group = _editGroup.Trim(); _editGroup = string.Empty; _groups = false; _bookmarkPage = 0;
                _skipFocusFrame = true;
            }
            y += 26;
        }
        var entries = CurrentBookmarks();
        var footer = new Rect(x, panel.yMax - 27, w, 23);
        var tools = NeiLayout.BottomRail(_width, _height, _center, _right).Tools;
        if (footer.Overlaps(tools)) footer.y = tools.yMin - footer.height - 3;
        var grid = new Rect(x, y, w, footer.yMin - y - 3);
        DrawGrid(grid, entries.Length, i => entries[i], ref _bookmarkPage, paging);
        if (entries.Length == 0 && grid.height > 40 && w > 115)
            NeiSkin.Label(new Rect(x + 4, y, w - 8, 24), L($"{_plugin.BookmarkKey} adds a bookmark", $"按 {_plugin.BookmarkKey} 添加书签"), muted: true);
        if (w >= 85) DrawBookmarkPages(footer);
    }

    private void DrawBookmarkPages(Rect row)
    {
        var groups = new[] { "0" }.Concat(_bookmarks.Select(entry => entry.Group)).Append(_group).Distinct().ToArray();
        int index = Array.IndexOf(groups, _group);
        const float button = 23;
        if (NeiSkin.IconButton(new Rect(row.x, row.y, button, row.height), "previous", L("Previous bookmark page", "上一张书签页"), groups.Length > 1))
            SelectBookmarkGroup(groups[Wrap(index - 1, groups.Length)]);
        var name = _group == "0" ? L("Default", "默认页") : _group;
        if (NeiSkin.Button(new Rect(row.x + button + 3, row.y, row.width - 2 * button - 6, row.height),
            $"{name}  {index + 1}/{groups.Length}", selected: _groups))
        {
            _groups = !_groups;
            if (!_groups) _skipFocusFrame = true;
        }
        if (NeiSkin.IconButton(new Rect(row.xMax - button, row.y, button, row.height), "next", L("Next bookmark page", "下一张书签页"), groups.Length > 1))
            SelectBookmarkGroup(groups[Wrap(index + 1, groups.Length)]);
    }

    private void SelectBookmarkGroup(string group)
    {
        _group = group;
        _bookmarkPage = 0;
    }

    private void DrawLeftTools(Rect rect)
    {
        _leftTools = rect;
        float side = rect.height;
        if (NeiSkin.IconButton(new Rect(rect.x, rect.y, side, side), "options", L("NEI options", "NEI 设置")))
        {
            _options = true;
            _skipFocusFrame = true;
        }
        bool compact = _left.width < 35 || _left.height < 100;
        bool active = compact ? _compactBookmarks : _bookmarksVisible;
        if (NeiSkin.IconButton(new Rect(rect.x + side + 3, rect.y, side, side), "bookmark", L("Show or hide bookmarks", "显示或隐藏书签"), selected: active))
        {
            if (compact) { _bookmarksVisible = true; _compactBookmarks = !_compactBookmarks; }
            else ToggleBookmarks();
        }
        if (NeiSkin.IconButton(new Rect(rect.x + 2 * (side + 3), rect.y, side, side), "machines", L("Browse recipes by machine", "按机器查配方")))
            OpenView(null, false, null);
    }
}
