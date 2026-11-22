using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private enum BookmarkGesture { Slot, SortSlot, Range, Group, Grow, Shrink }
    private sealed class BookmarkCell(BookmarkBook.Entry entry, string id, int ingredient, double amount, string? multiplier,
        Color? tint = null, string? details = null)
    {
        internal BookmarkBook.Entry Entry { get; } = entry;
        internal string Id { get; } = id;
        internal int Ingredient { get; } = ingredient;
        internal double Amount { get; } = amount;
        internal string? Multiplier { get; } = multiplier;
        internal Color? Tint { get; } = tint;
        internal string? Details { get; } = details;
    }

    private sealed class BookmarkRow(string group)
    {
        internal string Group { get; } = group;
        internal readonly List<BookmarkCell> Cells = new();
    }

    private sealed class BookmarkDrag(BookmarkGesture gesture, BookmarkCell cell, Vector2 start, bool fromStart,
        int button = 0, bool alt = false, bool control = false)
    {
        internal BookmarkGesture Gesture { get; } = gesture;
        internal BookmarkCell Cell { get; } = cell;
        internal Vector2 Start { get; } = start;
        internal bool FromStart { get; } = fromStart;
        internal int Button { get; } = button;
        internal bool Alt { get; } = alt;
        internal bool Control { get; } = control;
    }

    private BookmarkDrag? _bookmarkDrag;
    private readonly List<(Rect Rect, BookmarkCell Cell)> _bookmarkCells = new();
    private readonly List<(Rect Rect, string Group)> _bookmarkHeaders = new();
    private Rect _bookmarkNavigation, _favoriteRect;
    private int _bookmarkLayoutColumns;
    private List<BookmarkRow>? _bookmarkLayout;

    private void DrawBookmarkPanel(Rect panel)
    {
        NeiSkin.Background(panel, "bookmarks");
        float x = panel.x + 3, w = panel.width - 6;
        var paging = new Rect(x, panel.y + 2, w, 23);
        float y = paging.yMax + 3;
        if (_groups && w >= 85)
        {
            _groupRect = new Rect(x, y, w - 65, 22);
            _editGroup = NeiSkin.TextField(_groupRect, _editGroup, GroupControl, 32);
            if (NeiSkin.IconButton(new Rect(x + w - 63, y, 20, 22), "add", L("Open named bookmark page", "打开命名书签页"), !string.IsNullOrWhiteSpace(_editGroup)))
            {
                string page = _editGroup.Trim();
                if (_book.EnsurePage(page)) SaveBookmarks();
                SelectBookmarkGroup(page); _editGroup = string.Empty; _groups = false; _skipFocusFrame = true;
            }
            if (NeiSkin.Button(new Rect(x + w - 41, y, 19, 22), "↑")) CopyBookmark(_book.CopyPage(_group));
            if (NeiSkin.Button(new Rect(x + w - 20, y, 20, 22), "↓")) PasteBookmarks();
            y += 26;
            if (!_options)
            {
                DrawQuantity(new Rect(x, y, w, 23));
                y += 26;
            }
        }
        var footer = new Rect(x, panel.yMax - 27, w, 23);
        var tools = NeiLayout.LeftTools(_height);
        if (footer.Overlaps(tools)) footer.y = tools.yMin - footer.height - 3;
        // Process drag destinations before the generic MouseUp in the grid. Page arrows
        // remain normal click controls when no bookmark drag is in progress.
        var ev = Event.current;
        if (w >= 72 && ev.type == EventType.MouseUp && _bookmarkDrag is { Button: 0 } drag &&
            drag.Gesture is BookmarkGesture.SortSlot or BookmarkGesture.Group && footer.Contains(ev.mousePosition) &&
            Vector2.Distance(drag.Start, ev.mousePosition) >= 4)
        {
            int direction = ev.mousePosition.x <= footer.x + 23 ? -1 : ev.mousePosition.x >= footer.xMax - 23 ? 1 : 0;
            var pages = BookmarkPages();
            if (direction != 0 && pages.Length > 1)
            {
                string dest = pages[Wrap(Array.IndexOf(pages, _group) + direction, pages.Length)];
                if (_book.DropAtEnd(drag.Cell.Entry, dest, drag.Gesture == BookmarkGesture.Group))
                {
                    SaveBookmarks(); SelectBookmarkGroup(dest); _bookmarkPage = int.MaxValue;
                }
                _bookmarkDrag = null; ev.Use();
            }
        }
        DrawBookmarkEntries(new Rect(x, y, w, footer.yMin - y - 3), paging);
        if (w >= 72) DrawBookmarkPages(footer);
    }

    private List<BookmarkRow> BuildBookmarkRows(int columns)
    {
        var result = new List<BookmarkRow>();
        var entries = _book.Page(_group).ToArray();
        var hidden = HiddenRecipesInFoldedChains(entries);
        for (int start = 0; start < entries.Length;)
        {
            string group = entries[start].Group;
            int end = start + 1;
            while (end < entries.Length && entries[end].Group == group) end++;

            bool compact = _book.IsCompact(_group, group);
            bool collapsed = _book.IsCollapsed(_group, group);
            if (collapsed)
            {
                var row = new BookmarkRow(group);
                var products = entries.Skip(start).Take(end - start)
                    .SelectMany(entry => CellsForBookmark(entry, compact: false))
                    .Where(cell => cell.Ingredient < 0);
                if (_book.IsChainEnabled(_group, group))
                    row.Cells.AddRange(products.Where(cell => !cell.Entry.IsRecipe || cell.Tint == BookmarkProductTint).Take(columns));
                else
                    row.Cells.AddRange(products.Take(columns));
                // Keep a handle available for a fully balanced cycle, a zero target, or a missing recipe.
                if (row.Cells.Count == 0) row.Cells.Add(products.First());
                result.Add(row);
            }
            else
            {
                BookmarkRow? row = null;
                for (int i = start; i < end; i++)
                {
                    var entry = entries[i];
                    if (hidden.Contains(entry)) continue;
                    if (!compact) row = null;
                    bool foldedRecipe = entry.IsRecipeCollapsed && _book.IsChainEnabled(_group, group);
                    var cells = CellsForBookmark(entry, compact || foldedRecipe);
                    if (foldedRecipe)
                        cells = cells.Select(cell => new BookmarkCell(cell.Entry, cell.Id, cell.Ingredient, cell.Amount,
                            cell.Multiplier, cell.Tint, cell.Details + "\n" + L("Alt+left: expand saved recipe chain", "Alt＋左键：展开此配方链")));
                    foreach (var cell in cells)
                    {
                        if (row == null || row.Cells.Count >= columns)
                        {
                            row = new BookmarkRow(group);
                            result.Add(row);
                        }
                        row.Cells.Add(cell);
                    }
                }
            }
            start = end;
        }
        return result;
    }

    private HashSet<BookmarkBook.Entry> HiddenRecipesInFoldedChains(IEnumerable<BookmarkBook.Entry> entries)
    {
        var hidden = new HashSet<BookmarkBook.Entry>();
        foreach (string group in entries.Where(entry => entry.IsRecipeCollapsed).Select(entry => entry.Group).Distinct())
        {
            if (!_book.IsChainEnabled(_group, group) || _book.IsCollapsed(_group, group)) continue;
            var members = BookmarkGroupEntries(group);
            var folded = Enumerable.Range(0, members.Length).Where(index => members[index].IsRecipeCollapsed).ToHashSet();
            var plan = BookmarkBalance(group).Plan;
            var links = plan.Links.Where(link => link.ProducerEntryIndex.HasValue && link.ConsumerEntryIndex.HasValue)
                .Select(link => (link.ProducerEntryIndex!.Value, link.ConsumerEntryIndex!.Value));
            foreach (int index in BookmarkBook.HiddenCollapsedDependencies(members.Length, folded, links, plan.ResultEntryIndices))
                hidden.Add(members[index]);
        }
        return hidden;
    }

    private void DrawBookmarkEntries(Rect rect, Rect navigation)
    {
        if (rect.width < 26 || rect.height < 23) return;
        var e = Event.current;
        const float gutter = 14;
        float cell = Mathf.Min(29, rect.width - gutter);
        int columns = Math.Max(1, (int)((rect.width - gutter) / cell));
        int rows = Math.Max(1, (int)(rect.height / cell));
        if (_bookmarkLayout == null || _bookmarkLayoutColumns != columns)
        {
            _bookmarkLayout = BuildBookmarkRows(columns);
            _bookmarkLayoutColumns = columns;
        }
        var layout = _bookmarkLayout;
        int pages = Math.Max(1, (layout.Count + rows - 1) / rows);
        _bookmarkPage = Mathf.Clamp(_bookmarkPage, 0, pages - 1);
        DrawBookmarkNavigation(navigation, pages, layout.Count != 0);

        if (layout.Count == 0 && rect.width > 110)
            NeiSkin.Label(new Rect(rect.x + 4, rect.y, rect.width - 8, 22), L($"{_plugin.BookmarkKey} adds a bookmark", $"按 {_plugin.BookmarkKey} 添加书签"), muted: true);

        int first = _bookmarkPage * rows;
        BookmarkCell? hovered = null;
        BookmarkCell? rowEdge = null;
        string? header = null;
        Rect dropRect = default;
        bool before = false;
        bool gutterHit = false;
        bool repaint = e.type == EventType.Repaint;
        bool moving = _bookmarkDrag is { } drag && Vector2.Distance(drag.Start, e.mousePosition) >= 4;
        int dragRow = moving && _bookmarkDrag!.Gesture is BookmarkGesture.Range or BookmarkGesture.Grow or BookmarkGesture.Shrink
            ? layout.FindIndex(r => r.Cells.Any(c => ReferenceEquals(c, _bookmarkDrag.Cell))) : -1;
        int mouseRow = first + (int)Mathf.Floor((e.mousePosition.y - rect.y) / cell);
        for (int i = first; i < Math.Min(first + rows, layout.Count); i++)
        {
            var line = layout[i];
            float y = rect.y + (i - first) * cell;
            var bracket = new Rect(rect.x, y, gutter, cell);
            bool firstRow = i == 0 || layout[i - 1].Group != line.Group;
            bool lastRow = i + 1 == layout.Count || layout[i + 1].Group != line.Group;
            bool onBracket = bracket.Contains(e.mousePosition);
            bool onRow = rect.Contains(e.mousePosition) && mouseRow == i;
            if (dragRow >= 0 && i >= Math.Min(dragRow, mouseRow) && i <= Math.Max(dragRow, mouseRow))
                NeiSkin.BookmarkTint(new Rect(rect.x, y, rect.width, cell), new Color32(185, 190, 218, 55));
            if (line.Group.Length > 0)
            {
                NeiSkin.BookmarkBracket(bracket, firstRow, lastRow, _book.IsChainEnabled(_group, line.Group), onBracket);
                if (repaint) _bookmarkHeaders.Add((bracket, line.Group));
            }
            if (onRow)
            {
                before = e.mousePosition.y < y + cell * .5f;
                rowEdge = before ? line.Cells[0] : line.Cells[^1];
                gutterHit = onBracket;
                dropRect = new Rect(rect.x + gutter, before ? y : y + cell - 2, rect.width - gutter, 2);
            }
            if (onBracket)
            {
                header = line.Group;
                if (!moving) BookmarkBracketTip(line.Group, e.shift);
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1 && line.Group.Length > 0))
                {
                    BookmarkGesture gesture;
                    if (line.Group.Length == 0) gesture = BookmarkGesture.Range;
                    else if (e.button == 1 || e.alt && Input.GetKey(KeyCode.RightAlt)) gesture = BookmarkGesture.Shrink;
                    else if (e.shift) gesture = BookmarkGesture.Group;
                    else gesture = BookmarkGesture.Grow;
                    bool fromStart = firstRow && (!lastRow || before);
                    if (!firstRow && !lastRow)
                    {
                        int start = i, end = i;
                        while (start > 0 && layout[start - 1].Group == line.Group) start--;
                        while (end + 1 < layout.Count && layout[end + 1].Group == line.Group) end++;
                        fromStart = i - start <= end - i;
                    }
                    _bookmarkDrag = new BookmarkDrag(gesture, rowEdge!, e.mousePosition, fromStart, e.button, e.alt, e.control);
                    e.Use();
                }
            }
            for (int col = 0; col < line.Cells.Count; col++)
            {
                var current = line.Cells[col];
                var slot = new Rect(rect.x + gutter + col * cell, y, cell - 1, cell - 1);
                if (slot.Contains(e.mousePosition))
                {
                    hovered = current;
                    before = e.mousePosition.x < slot.center.x;
                    dropRect = new Rect(before ? slot.x : slot.xMax - 2, slot.y, 2, slot.height);
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        _bookmarkDrag = new BookmarkDrag(e.shift ? BookmarkGesture.SortSlot : BookmarkGesture.Slot, current, e.mousePosition, before);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown && e.button == 1)
                    {
                        ClickBookmark(current, 1, e.shift, e.alt);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown && e.button == 2)
                    {
                        if (_book.Remove(current.Entry)) SaveBookmarks();
                        e.Use();
                    }
                }
                if (repaint) _bookmarkCells.Add((slot, current));
                var details = current.Details;
                if (hovered == current && e.shift && current.Amount > 0)
                    details = string.Join("\n", new[] { details, BookmarkAmountBreakdown(current) }.Where(s => !string.IsNullOrEmpty(s)));
                bool tracked = current.Entry.ShowQuantity || _book.IsChainEnabled(_group, current.Entry.Group);
                string? amount = tracked ? BookmarkAmount(current.Id, current.Amount) : null;
                ItemSlot(slot, current.Id, amount: amount, details: details, interactive: false, backing: current.Tint, corner: current.Multiplier);
            }
        }
        if (moving && rowEdge != null) NeiSkin.BookmarkTint(dropRect, new Color32(218, 217, 226, 230));

        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition) && e.delta.y != 0)
        {
            int direction = e.delta.y < 0 ? 1 : -1;
            if (e.control && !moving && (hovered != null || header != null))
            {
                int delta = direction * (e.alt ? BookmarkQuantity : 1);
                bool changed = hovered == null || _book.IsCollapsed(_group, hovered.Entry.Group)
                    ? AdjustBookmarkGroup(hovered?.Entry.Group ?? header!, delta)
                    : AdjustBookmark(hovered.Entry, delta);
                if (changed) SaveBookmarks();
                e.Use();
            }
            else if (e.shift && hovered != null && CycleBookmarkVariant(hovered, direction)) e.Use();
            else if (e.shift && !moving) { ChangeBookmarkPage(direction); e.Use(); }
            else if (pages > 1) { _bookmarkPage = Wrap(_bookmarkPage - direction, pages); e.Use(); }
        }
        if (e.type == EventType.MouseDrag && _bookmarkDrag != null) e.Use();
        if (e.type == EventType.MouseUp && _bookmarkDrag is { } active && e.button == active.Button)
        {
            _bookmarkDrag = null;
            if (moving && (hovered ?? rowEdge) is { } target)
            {
                var source = active.Cell.Entry;
                var targetEntry = target.Entry;
                bool changed = active.Gesture switch
                {
                    BookmarkGesture.Group => _book.MoveGroup(_group, source.Group, targetEntry, before),
                    BookmarkGesture.Range => _book.GroupRange(source, targetEntry),
                    BookmarkGesture.Grow => _book.ResizeGroup(_group, source.Group, targetEntry, active.FromStart),
                    BookmarkGesture.Shrink => _book.ResizeGroup(_group, source.Group, targetEntry, active.FromStart, remove: true),
                    BookmarkGesture.SortSlot when source == targetEntry => ReorderBookmarkIngredients(active.Cell, target, before),
                    BookmarkGesture.SortSlot => _book.Move(source, targetEntry, true, before, separate: gutterHit),
                    _ => _book.Move(source, targetEntry, false, before)
                };
                if (changed) SaveBookmarks();
            }
            else if (moving && rect.Contains(e.mousePosition) && active.Gesture is BookmarkGesture.SortSlot or BookmarkGesture.Group)
            {
                if (_book.DropAtEnd(active.Cell.Entry, _group, active.Gesture == BookmarkGesture.Group)) SaveBookmarks();
            }
            else if (!moving && header == active.Cell.Entry.Group && !string.IsNullOrEmpty(header) &&
                active.Gesture is BookmarkGesture.Group or BookmarkGesture.Grow or BookmarkGesture.Shrink)
            {
                bool changed = active.Button == 1
                    ? (active.Control ? _book.Ungroup(_group, header) : _book.ToggleChain(_group, header))
                    : !e.shift && (active.Alt ? _book.ToggleCollapsed(_group, header) : _book.ToggleCompact(_group, header));
                if (changed) SaveBookmarks();
            }
            else if (!moving && hovered == active.Cell && active.Button == 0)
            {
                if (e.alt && _book.IsCollapsed(_group, active.Cell.Entry.Group))
                {
                    if (_book.ToggleCollapsed(_group, active.Cell.Entry.Group)) SaveBookmarks();
                }
                else if (e.alt && active.Cell.Ingredient < 0 && active.Cell.Entry.IsRecipe &&
                    _book.IsChainEnabled(_group, active.Cell.Entry.Group))
                {
                    if (_book.ToggleRecipeCollapsed(active.Cell.Entry)) SaveBookmarks();
                }
                else ClickBookmark(active.Cell, 0, active.Gesture == BookmarkGesture.SortSlot || e.shift, e.alt);
            }
            e.Use();
        }
    }

    private void DrawBookmarkNavigation(Rect rect, int pages, bool hasEntries)
    {
        float button = Mathf.Min(rect.height, rect.width / 4);
        var middle = new Rect(rect.x + button + 1, rect.y, rect.width - 2 * button - 2, rect.height);
        var e = Event.current;
        if (e.type == EventType.Repaint) _bookmarkNavigation = middle;
        if (NeiSkin.IconButton(new Rect(rect.x, rect.y, button, rect.height), "previous", L("Previous grid page", "上一屏书签"), pages > 1)) _bookmarkPage = Wrap(_bookmarkPage - 1, pages);
        if (NeiSkin.IconButton(new Rect(rect.xMax - button, rect.y, button, rect.height), "next", L("Next grid page", "下一屏书签"), pages > 1)) _bookmarkPage = Wrap(_bookmarkPage + 1, pages);
        bool onPageNumber = middle.Contains(e.mousePosition);
        if (onPageNumber && hasEntries)
        {
            if (e.type == EventType.MouseDown && (e.button == 1 || e.button == 0 && e.alt))
            {
                bool changed = e.button == 0 ? _book.ToggleCollapsed(_group) :
                    e.control ? _book.ToggleAllChains(_group) : _book.ToggleChain(_group);
                if (changed) SaveBookmarks();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && e.control && e.delta.y != 0 && _bookmarkDrag == null)
            {
                int direction = e.delta.y < 0 ? 1 : -1;
                if (AdjustBookmarkGroup("", direction * (e.alt ? BookmarkQuantity : 1))) SaveBookmarks();
                e.Use();
            }
        }
        if (NeiSkin.Button(middle, $"{_bookmarkPage + 1}/{pages}", selected: !_book.IsCompact(_group)))
        {
            if (_book.ToggleCompact(_group)) SaveBookmarks();
        }
        if (_book.IsChainEnabled(_group)) NeiSkin.BookmarkTint(new Rect(middle.x + 3, middle.yMax - 3, middle.width - 6, 1.5f), new Color32(87, 175, 103, 255));
        if (!onPageNumber) return;
        if (hasEntries && e.control && PageChainEnabled()) BookmarkPageChainTip(e.shift);
        else if (hasEntries && _book.IsChainEnabled(_group))
            BookmarkChainTip(L("Ungrouped bookmarks", "未分组书签"), new[] { "" }, e.shift);
        else Tip(L("Bookmark page", "当前书签页"), L("Left: grid/queue · Alt+left: collapse · Right: ungrouped chain\nCtrl+right: toggle all chains · Ctrl+hover: page totals\nCtrl+wheel: quantity · Ctrl+C: copy page · Ctrl+V: paste", "左键：网格／队列 · Alt＋左键：折叠 · 右键：未分组合成链\nCtrl＋右键：切换整页合成链 · Ctrl＋悬停：整页汇总\nCtrl＋滚轮：数量 · Ctrl＋C：复制页 · Ctrl＋V：粘贴"));
    }

    private bool AdjustBookmarkGroup(string group, int delta)
    {
        var entries = BookmarkGroupEntries(group);
        var legacyBatches = new Dictionary<BookmarkBook.Entry, int>();
        var recipeUnits = new Dictionary<BookmarkBook.Entry, int>();
        foreach (var entry in entries)
        {
            if (!entry.IsRecipe || BookmarkedRecipe(entry.Id) is not { } recipe) continue;
            if (entry.LegacyRecipeQuantity) legacyBatches[entry] = BookmarkBatches(entry, recipe);
            recipeUnits[entry] = Math.Max(1, BookmarkProduct(entry, recipe)?.Amount ?? 1);
        }

        if (!_book.IsChainEnabled(_group, group))
            return _book.AdjustGroup(_group, group, delta, legacyBatches, recipeUnits: recipeUnits);

        var targets = BookmarkBalance(group).Plan.ResultEntryIndices.Select(index => entries[index]).ToHashSet();
        return _book.AdjustGroup(_group, group, delta, legacyBatches, targets);
    }

    private void DrawBookmarkPages(Rect row)
    {
        var pages = BookmarkPages();
        int index = Array.IndexOf(pages, _group);
        const float button = 23;
        if (NeiSkin.IconButton(new Rect(row.x, row.y, button, row.height), "previous", L("Previous bookmark page", "上一张书签页"), pages.Length > 1)) ChangeBookmarkPage(-1);
        var name = _group == "0" ? L("Default", "默认页") : _group;
        if (NeiSkin.Button(new Rect(row.x + button + 2, row.y, row.width - 2 * button - 4, row.height), $"{name}  {index + 1}/{pages.Length}", selected: _groups))
        { _groups = !_groups; if (!_groups) _skipFocusFrame = true; }
        if (NeiSkin.IconButton(new Rect(row.xMax - button, row.y, button, row.height), "next", L("Next bookmark page", "下一张书签页"), pages.Length > 1)) ChangeBookmarkPage(1);
    }

    private string[] BookmarkPages() => new[] { "0" }.Concat(_book.PageNames).Append(_group).Distinct(StringComparer.Ordinal).ToArray();
    private void ChangeBookmarkPage(int delta)
    {
        var pages = BookmarkPages();
        SelectBookmarkGroup(pages[Wrap(Array.IndexOf(pages, _group) + delta, pages.Length)]);
    }

    private void SelectBookmarkGroup(string group)
    {
        _group = group; _bookmarkPage = 0; _bookmarkDrag = null;
        InvalidateBookmarkView();
    }

    private void InvalidateBookmarkView()
    {
        _bookmarkLayout = null;
        _bookmarkBalance.Clear();
        _cachedChainTip = null;
        _chainTipOffset = _chainTipMaxOffset = 0;
        ClearBookmarkHits();
    }

    private void ClearBookmarkHits()
    {
        _bookmarkCells.Clear(); _bookmarkHeaders.Clear(); _bookmarkNavigation = _favoriteRect = default;
    }

    private void CopyBookmark(string text)
    {
        GUIUtility.systemCopyBuffer = text;
        Status(L("Copied bookmarks", "书签已复制"));
    }

    private void PasteBookmarks()
    {
        if (_book.Import(GUIUtility.systemCopyBuffer, _group)) { SaveBookmarks(); Status(L("Bookmarks imported", "已导入书签")); }
        else Status(L("No new compatible bookmarks on the clipboard", "剪贴板中没有新的兼容书签"));
    }

    private static string BookmarkAmount(string id, double amount) => amount.ToString("0.##", CultureInfo.InvariantCulture) +
        (id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? "mL" : "");

    private string? BookmarkAmountBreakdown(BookmarkCell cell)
    {
        // Casualties items are individual instances, not Minecraft stacks. Use the NEI display
        // units (64 items / 144 mL) only as a readable decomposition of larger amounts.
        int unit = cell.Id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? 144 : 64;
        if (cell.Amount < unit) return null;
        double groups = Math.Floor(cell.Amount / unit);
        return $"{BookmarkAmount(cell.Id, cell.Amount)} = {groups:0} × {BookmarkAmount(cell.Id, unit)} + {BookmarkAmount(cell.Id, cell.Amount - groups * unit)}";
    }

    private void DrawLeftTools(Rect rect)
    {
        float side = rect.height;
        bool hideBookmarks = HideBookmarksForDrag;
        _leftTools = hideBookmarks ? new Rect(rect.x, rect.y, side, side) : rect;
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
        if (!hideBookmarks && NeiSkin.IconButton(new Rect(rect.x + side + 3, rect.y, side, side), "bookmark", L("Show or hide bookmarks", "显示或隐藏书签"), selected: _bookmarksVisible))
            ToggleBookmarks();
    }
}
