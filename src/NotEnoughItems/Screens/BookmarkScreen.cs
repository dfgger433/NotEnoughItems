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
        Color? tint = null, Func<string>? details = null, BookmarkLiquidPreview? liquidPreview = null)
    {
        internal BookmarkBook.Entry Entry { get; } = entry;
        internal string Id { get; private set; } = id;
        internal int Ingredient { get; } = ingredient;
        internal double Amount { get; private set; } = amount;
        internal string? Multiplier { get; } = multiplier;
        internal Color? Tint { get; } = tint;
        internal string? Details => details?.Invoke();

        internal void UpdatePreview(int phase)
        {
            if (liquidPreview == null) return;
            (Id, Amount) = liquidPreview.At(phase);
        }
    }

    private sealed class BookmarkLiquidPreview((string Id, double Amount)[] candidates)
    {
        internal (string Id, double Amount) At(int phase) => candidates[RecipeGridLayout.CandidateIndex(phase, candidates.Length)];
    }

    private sealed class BookmarkRow(string group)
    {
        internal string Group { get; } = group;
        internal readonly List<BookmarkCell> Cells = new();
    }

    private sealed class BookmarkDrag(BookmarkGesture gesture, BookmarkCell cell, Vector2 start, bool fromStart,
        int button = 0, bool alt = false, bool control = false, bool shift = false,
        BookmarkRow? originRow = null, int startPage = 0)
    {
        internal BookmarkGesture Gesture { get; } = gesture;
        internal BookmarkCell Cell { get; } = cell;
        internal Vector2 Start { get; } = start;
        internal bool FromStart { get; } = fromStart;
        internal int Button { get; } = button;
        internal bool Alt { get; } = alt;
        internal bool Control { get; } = control;
        internal bool Shift { get; } = shift;
        internal BookmarkRow? OriginRow { get; } = originRow;
        internal int StartPage { get; } = startPage;
        internal bool Moved { get; private set; }

        internal void MarkMoved() => Moved = true;

        internal bool IsMoving(Vector2 position, bool isDrag)
        {
            bool beyondThreshold = Vector2.Distance(Start, position) >= BookmarkClicks.DragDistance;
            if (isDrag && beyondThreshold) Moved = true;
            return Moved || beyondThreshold;
        }
    }

    private BookmarkDrag? _bookmarkDrag;
    private readonly List<(Rect Rect, BookmarkCell Cell)> _bookmarkCells = new();
    private readonly List<(Rect Rect, string Group)> _bookmarkHeaders = new();
    private Rect _bookmarkNavigation;
    private int _bookmarkLayoutColumns;
    private List<BookmarkRow>? _bookmarkLayout;

    private void DrawBookmarkPanel(Rect panel)
    {
        NeiSkin.Background(panel, "bookmarks");
        float x = panel.x + 3, w = panel.width - 6;
        const float size = NeiLayout.BookmarkControlSize, gap = 2f;
        var paging = new Rect(x, panel.y + 2, w, size);
        float y = paging.yMax + 3;
        if (_groups && w >= 85f)
        {
            float editorButton = Mathf.Min(size - 4f, (w - 22f) / 3f - gap);
            float editorActions = 3f * (editorButton + gap);
            _groupRect = new Rect(x, y, w - editorActions, size);
            _editGroup = NeiSkin.TextField(_groupRect, _editGroup, GroupControl, 32);
            float first = _groupRect.xMax + gap;
            if (NeiSkin.IconButton(new Rect(first, y, editorButton, size), "add", L("Open named bookmark page", "打开命名书签页"), !string.IsNullOrWhiteSpace(_editGroup)))
            {
                string page = _editGroup.Trim();
                if (_book.EnsurePage(page)) SaveBookmarks();
                SelectBookmarkGroup(page); _editGroup = string.Empty; _groups = false; _skipFocusFrame = true;
            }
            if (NeiSkin.IconButton(new Rect(first + editorButton + gap, y, editorButton, size), "copy",
                L("Copy bookmark page", "复制书签页"), backed: true)) CopyBookmark(_book.CopyPage(_group));
            if (NeiSkin.IconButton(new Rect(x + w - editorButton, y, editorButton, size), "paste",
                L("Paste bookmarks", "粘贴书签"), backed: true)) PasteBookmarks();
            y += size + 4f;
        }
        var footer = new Rect(x, panel.yMax - size - 4f, w, size);
        bool showFooter = w >= 72f;
        float footerButton = BookmarkNavigationButtonWidth(footer);
        // Process drag destinations before the generic MouseUp in the grid. Page arrows
        // remain normal click controls when no bookmark drag is in progress.
        var ev = Event.current;
        if (showFooter && ev.type == EventType.MouseUp && ev.button == 0 && _bookmarkDrag is { Button: 0 } drag &&
            drag.Gesture is BookmarkGesture.SortSlot or BookmarkGesture.Group && footer.Contains(ev.mousePosition) &&
            drag.IsMoving(ev.mousePosition, isDrag: false))
        {
            int direction = ev.mousePosition.x < footer.x + footerButton ? -1 : ev.mousePosition.x >= footer.xMax - footerButton ? 1 : 0;
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
        if (showFooter) DrawBookmarkPages(footer, footerButton);
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
                    bool pack = BookmarkBoardPaging.CanPackBookmarkRow(compact, group);
                    if (!pack) row = null;
                    bool foldedRecipe = entry.IsRecipeCollapsed && _book.IsChainEnabled(_group, group);
                    var cells = CellsForBookmark(entry, compact || foldedRecipe);
                    if (foldedRecipe)
                        cells = cells.Select(cell => new BookmarkCell(cell.Entry, cell.Id, cell.Ingredient, cell.Amount,
                            cell.Multiplier, cell.Tint, () => cell.Details + "\n" + (group.Length == 0
                                ? L("Alt+left: expand saved recipe chain", "Alt＋左键：展开此配方链")
                                : L("Shift+Alt+left: expand saved recipe chain", "Shift＋Alt＋左键：展开此配方链"))));
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
        const float gutter = 16;
        float cell = Mathf.Min(NeiLayout.BookmarkCellSize * IconScale, rect.width - gutter, rect.height);
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
        int pageEnd = Math.Min(first + rows, layout.Count);
        BookmarkCell? hovered = null;
        BookmarkCell? rowEdge = null;
        BookmarkRow? targetRow = null;
        string? header = null;
        Rect dropRect = default;
        Rect lastSlot = default;
        bool before = false;
        bool gutterHit = false;
        bool repaint = e.type == EventType.Repaint;
        var drag = _bookmarkDrag;
        bool moving = drag?.IsMoving(e.mousePosition, e.type == EventType.MouseDrag) == true;
        int dragRow = moving && drag!.OriginRow is { } origin && drag.Gesture is BookmarkGesture.Range or BookmarkGesture.Grow or BookmarkGesture.Shrink
            ? layout.IndexOf(origin) : -1;
        int mouseRow = first + (int)Mathf.Floor((e.mousePosition.y - rect.y) / cell);
        for (int i = first; i < pageEnd; i++)
        {
            var line = layout[i];
            float y = rect.y + (i - first) * cell;
            var bracket = new Rect(rect.x, y, gutter, cell);
            bool firstRow = i == 0 || layout[i - 1].Group != line.Group;
            bool lastRow = i + 1 == layout.Count || layout[i + 1].Group != line.Group;
            bool onRow = rect.Contains(e.mousePosition) && mouseRow == i;
            bool onBracket = onRow && bracket.Contains(e.mousePosition);
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
                targetRow = line;
                rowEdge = before ? line.Cells[0] : line.Cells[^1];
                gutterHit = onBracket;
                dropRect = new Rect(rect.x + gutter, before ? y : y + cell - 2, rect.width - gutter, 2);
            }
            if (onBracket)
            {
                header = line.Group;
                if (!moving) BookmarkBracketTip(line.Group, SelectionShiftHeld, bracket);
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
                    _bookmarkDrag = new BookmarkDrag(gesture, rowEdge!, e.mousePosition, fromStart, e.button, e.alt, e.control, e.shift, line);
                    e.Use();
                }
            }
            for (int col = 0; col < line.Cells.Count; col++)
            {
                var current = line.Cells[col];
                // Keep the row/cell identity (used by click and drag), but update the
                // automatic liquid example and its volume together on repaint.
                if (repaint) current.UpdatePreview(_candidateCycleStep);
                var slot = new Rect(rect.x + gutter + col * cell, y, cell - 1, cell - 1);
                lastSlot = slot;
                if (slot.Contains(e.mousePosition))
                {
                    hovered = current;
                    before = e.mousePosition.x < slot.center.x;
                    dropRect = new Rect(before ? slot.x : slot.xMax - 2, slot.y, 2, slot.height);
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        _bookmarkDrag = new BookmarkDrag(e.shift ? BookmarkGesture.SortSlot : BookmarkGesture.Slot,
                            current, e.mousePosition, before, alt: e.alt, startPage: _bookmarkPage);
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
                var details = repaint && hovered == current ? current.Details : null;
                if (repaint && hovered == current && e.shift && current.Amount > 0)
                    details = string.Join("\n", new[] { details, BookmarkAmountBreakdown(current) }.Where(s => !string.IsNullOrEmpty(s)));
                var ingredient = current.Ingredient >= 0 && BookmarkedRecipe(current.Entry.Id) is { } bookmarkedRecipe &&
                    current.Ingredient < bookmarkedRecipe.Inputs.Count ? bookmarkedRecipe.Inputs[current.Ingredient] : null;
                bool liquidId = current.Id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase);
                bool tracked = liquidId || current.Entry.ShowQuantity || _book.IsChainEnabled(_group, current.Entry.Group);
                bool unresolvedProperty = PropertyQuery(ingredient) != null && !liquidId;
                SlotQuantity? amount = tracked && !unresolvedProperty ? new SlotQuantity(current.Amount, liquidId || ingredient?.IsLiquid == true) : null;
                var candidates = repaint && hovered == current && ingredient?.MatchesAny == true ? _catalog.GetMatchingItems(ingredient) : null;
                ItemSlot(slot, current.Id, amount: amount, details: details, interactive: false, backing: current.Tint,
                    corner: current.Multiplier, requirementIngredient: ingredient, candidates: candidates,
                    candidateRequirement: candidates != null ? RequirementTitle(ingredient!) : null,
                    selectCandidate: candidates != null ? candidate => SelectBookmarkVariant(current.Entry, current.Ingredient, candidate) : null,
                    candidateSource: CandidateSource.Bookmark);
            }
        }
        BookmarkCell? gridTail = null;
        bool beforeTail = false, tailCollapsed = false;
        if (moving && drag is { Gesture: BookmarkGesture.SortSlot } && !drag.Cell.Entry.IsRecipe &&
            rect.Contains(e.mousePosition) && pageEnd > first)
        {
            var tailRow = layout[pageEnd - 1];
            var tailCell = tailRow.Cells[^1];
            tailCollapsed = _book.IsCollapsed(_group, tailRow.Group);
            bool canPack = !tailCollapsed && BookmarkBoardPaging.CanPackBookmarkRow(_book.IsCompact(_group, tailRow.Group), tailRow.Group);
            bool continues = pageEnd > 1 && layout[pageEnd - 2].Cells[^1].Entry == tailCell.Entry;
            if (BookmarkBoardPaging.EarlierGridDropBefore(drag.StartPage, _bookmarkPage, canPack, tailRow.Cells.Count, columns, continues) is { } insertBefore)
            {
                gridTail = tailCell;
                beforeTail = insertBefore;
                dropRect = new Rect(insertBefore ? lastSlot.x : lastSlot.xMax - 2, lastSlot.y, 2, lastSlot.height);
            }
        }
        if (moving && (gridTail != null || rowEdge != null)) NeiSkin.BookmarkTint(dropRect, new Color32(218, 217, 226, 230));

        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition) && e.delta.y != 0)
        {
            int direction = e.delta.y < 0 ? 1 : -1;
            if (pages > 1 && drag is { Gesture: BookmarkGesture.SortSlot } && !drag.Cell.Entry.IsRecipe)
            {
                // Rolling back already turns this press into a reorder, even if the pointer hasn't moved.
                drag.MarkMoved();
                _bookmarkPage = Wrap(_bookmarkPage - direction, pages);
                e.Use();
            }
            else if (e.control && !moving && (hovered != null || header != null))
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
            if (gridTail is { } tail)
            {
                if (_book.Move(active.Cell.Entry, tail.Entry, reorderOnly: true, before: beforeTail, separate: tailCollapsed)) SaveBookmarks();
            }
            else if (moving && (hovered ?? rowEdge) is { } target)
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
                bool changed = BookmarkClicks.Bracket(active.Button, active.Alt, active.Control, active.Gesture == BookmarkGesture.Group) switch
                {
                    BookmarkBracketClick.Ungroup => _book.Ungroup(_group, header),
                    BookmarkBracketClick.ToggleChain => _book.ToggleChain(_group, header),
                    BookmarkBracketClick.ToggleCollapse => _book.ToggleCollapsed(_group, header),
                    BookmarkBracketClick.ToggleLayout => _book.ToggleCompact(_group, header),
                    _ => false
                };
                if (changed) SaveBookmarks();
            }
            else if (!moving && hovered == active.Cell && active.Button == 0)
            {
                var entry = active.Cell.Entry;
                bool shift = active.Gesture == BookmarkGesture.SortSlot || e.shift;
                var click = BookmarkClicks.Slot(active.Button, active.Alt, shift, entry.Group.Length > 0,
                    _book.IsCollapsed(_group, entry.Group),
                    active.Cell.Ingredient < 0 && entry.IsRecipe && _book.IsChainEnabled(_group, entry.Group));
                bool changed = click switch
                {
                    BookmarkSlotClick.Split => _book.Split(entry),
                    BookmarkSlotClick.ExpandLane => _book.ToggleCollapsed(_group, entry.Group),
                    BookmarkSlotClick.ToggleRecipeCollapse => _book.ToggleRecipeCollapsed(entry),
                    _ => false
                };
                if (changed) SaveBookmarks();
                else if (click == BookmarkSlotClick.Default) ClickBookmark(active.Cell, 0, shift, alt: false);
            }
            e.Use();
        }
    }

    private static float BookmarkNavigationButtonWidth(Rect row) => Mathf.Min(row.height, row.width / 4f);

    private void DrawBookmarkNavigation(Rect rect, int pages, bool hasEntries)
    {
        float button = BookmarkNavigationButtonWidth(rect);
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
        string title = L("Bookmark page", "当前书签页");
        bool chain = _book.IsChainEnabled(_group);
        if (ShowBookmarkHelp(title, page: true, hasEntries, chain)) return;
        if (hasEntries && e.control && PageChainEnabled()) BookmarkPageChainTip(SelectionShiftHeld, middle);
        else if (hasEntries && chain)
            BookmarkChainTip(L("Ungrouped bookmarks", "未分组书签"), new[] { "" }, SelectionShiftHeld, middle);
        else Tip(title, BookmarkHelpPrompt, boxed: true);
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

    private void DrawBookmarkPages(Rect row, float button)
    {
        var pages = BookmarkPages();
        int index = Array.IndexOf(pages, _group);
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
        if (_pinnedItems?.Origin is CandidateSource.Bookmark or CandidateSource.Chain) ClearSupportedItemsTip();
        ClearChainTip();
        ClearBookmarkHits();
    }

    private void ClearBookmarkHits()
    {
        _bookmarkCells.Clear(); _bookmarkHeaders.Clear(); _bookmarkNavigation = default;
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

    private static string BookmarkAmount(string id, double amount, bool liquid = false) => amount.ToString("0.##", CultureInfo.InvariantCulture) +
        (liquid || id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? "mL" : "");

    private string? BookmarkAmountBreakdown(BookmarkCell cell)
    {
        // Casualties items are individual instances, not Minecraft stacks. Use the NEI display
        // units (64 items / 144 mL) only as a readable decomposition of larger amounts.
        int unit = cell.Id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? 144 : 64;
        if (cell.Amount < unit) return null;
        double groups = Math.Floor(cell.Amount / unit);
        return $"{BookmarkAmount(cell.Id, cell.Amount)} = {groups:0} × {BookmarkAmount(cell.Id, unit)} + {BookmarkAmount(cell.Id, cell.Amount - groups * unit)}";
    }

    private void DrawLeftTools(NeiLayout.ToolButtons buttons)
    {
        var wrench = buttons.Settings;
        var e = Event.current;
        if (e.control && e.type == EventType.MouseDown && e.button == 0 && wrench.Contains(e.mousePosition))
        {
            _plugin.TryCycleMode(out string notice);
            Status(notice);
            e.Use();
        }
        string mode = NeiActions.ModeName(NeiActions.Mode);
        if (NeiSkin.IconButton(wrench, "options", L($"{mode} mode · Ctrl+click to change", $"{mode}模式 · Ctrl＋点击切换"),
            selected: NeiActions.Mode == NeiActions.LocalMode.Cheat, backed: true, maxIconSize: 22f, enlargeIcon: true))
        { _options = true; _skipFocusFrame = true; }
        if (!HideBookmarksForDrag && NeiSkin.IconButton(buttons.Bookmarks, "bookmark", L("Show or hide bookmarks", "显示或隐藏书签"),
            selected: _bookmarksVisible, backed: true, maxIconSize: 22f, enlargeIcon: true))
            ToggleBookmarks();
    }
}
