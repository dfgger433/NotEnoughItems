using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private enum TreeView { Steps, Step, Bill }

    // Entry indices identify nodes, not recipe or item IDs: different pins and co-products
    // can legitimately give the same recipe more than one role in a saved chain.
    private sealed class TreeRow(string title, string note, string? item = null, int step = -1,
        bool heading = false, string? terms = null, int depth = 0, RecipeIngredient? ingredient = null,
        BookmarkBook.Entry? entry = null, int ingredientIndex = -1)
    {
        internal readonly string Title = title, Note = note;
        internal readonly string Search = title + "\n" + note + "\n" + item + "\n" + terms;
        internal readonly string? Item = item;
        internal readonly RecipeIngredient? Ingredient = ingredient;
        internal readonly BookmarkBook.Entry? Entry = entry;
        internal readonly int IngredientIndex = ingredientIndex;
        internal readonly int Step = step, Depth = depth;
        internal readonly bool Heading = heading;
    }

    private bool _treeOpen, _treePanning, _treeRowsChinese;
    private string _treePage = "", _treeGroup = "", _treeQuery = "";
    private TreeView _treeView;
    private BookmarkChainBalanceResult? _treePlan;
    private int _treeStep = -1;
    private readonly List<int> _treeTrail = new();
    private readonly Dictionary<int, int> _treeOrdinals = new();
    private ILookup<int?, BookmarkChainBalanceLink> _treeIncoming = null!, _treeOutgoing = null!;
    private readonly List<TreeRow> _treeRows = new(), _treeFiltered = new();
    private Vector2 _treeScroll;

    internal bool OpenBookmarkTree(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (!ShowLeft || _options || _bookmarkDrag != null || !_left.Contains(pointer)) return false;
        // Upstream's page number means DEFAULT_GROUP_ID. A slot (even in a chain) is
        // deliberately not a group hit, so normal item shortcuts still work there.
        string? group = _bookmarkNavigation.Contains(pointer) ? "" : _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
        if (group == null || !_book.IsChainEnabled(_group, group) || !_book.Page(_group).Any()) return false;
        OpenTreeGroup(group);
        return true;
    }

    private void OpenTreeGroup(string group)
    {
        _treePage = _group;
        _treeGroup = group;
        _treePlan = null;
        _treeStep = -1;
        _treeTrail.Clear();
        _treeQuery = "";
        _treeScroll = Vector2.zero;
        _treeView = TreeView.Steps;
        _open = _options = _returnToTree = _searchFocused = _treePanning = false;
        _focusSearch = null;
        _back.Clear();
        _treeOpen = _skipFocusFrame = true;
    }

    private void DrawBookmarkTree(Rect panel)
    {
        if (_group != _treePage || !_book.IsChainEnabled(_group, _treeGroup)) { Back(); return; }
        var plan = BookmarkBalance(_treeGroup).Plan;
        if (!ReferenceEquals(_treePlan, plan))
        {
            _treePlan = plan;
            _treeIncoming = plan.Links.ToLookup(edge => edge.ConsumerEntryIndex);
            _treeOutgoing = plan.Links.ToLookup(edge => edge.ProducerEntryIndex);
            _treeOrdinals.Clear();
            for (int i = 0; i < plan.Steps.Count; i++) _treeOrdinals[plan.Steps[i].EntryIndex] = i;
            if (!_treeOrdinals.ContainsKey(_treeStep)) { _treeStep = -1; _treeTrail.Clear(); if (_treeView == TreeView.Step) _treeView = TreeView.Steps; }
            BuildTreeRows(plan);
        }
        else if (_treeRowsChinese != _plugin.Chinese)
            BuildTreeRows(plan);

        NeiSkin.RecipePanel(panel);
        float x = panel.x + 8, y = panel.y + 5, w = panel.width - 16;
        string scope = _treeGroup.Length == 0 ? L("chain.tree.default_group") : L("bookmark.group.title");
        NeiSkin.Label(new Rect(x, y, w - 28, 22), L("chain.tree.title", scope));
        if (NeiSkin.IconButton(new Rect(x + w - 22, y, 22, 22), "close", L("chain.tree.close"))) { Back(); return; }
        y += 27;
        float tab = (w - 6) / 3f;
        if (NeiSkin.Tab(new Rect(x, y, tab, 22), L("chain.tree.steps_tab", plan.Steps.Count), _treeView == TreeView.Steps)) SelectTreeView(TreeView.Steps);
        if (NeiSkin.Tab(new Rect(x + tab + 3, y, tab, 22), L("chain.tree.links_tab"), _treeView == TreeView.Step, _treeStep >= 0)) SelectTreeView(TreeView.Step);
        if (NeiSkin.Tab(new Rect(x + 2 * (tab + 3), y, tab, 22), L("chain.tree.balance_tab"), _treeView == TreeView.Bill)) SelectTreeView(TreeView.Bill);
        y += 26;
        _searchRect = new Rect(x, y, w, 22);
        string query = DrawSearchInput(_searchRect, _treeQuery, L("chain.tree.search_hint"));
        if (query != _treeQuery) { _treeQuery = query; _treeScroll = Vector2.zero; FilterTreeRows(); }
        y += 26;

        if (_treeView == TreeView.Step && _treeOrdinals.TryGetValue(_treeStep, out int ordinal))
        {
            var step = plan.Steps[ordinal];
            if (NeiSkin.IconButton(new Rect(x, y, 24, 22), "previous", L("chain.tree.previous_node"))) { TreePrevious(); return; }
            var label = new Rect(x + 29, y, w - 100, 22);
            NeiSkin.Label(label, $"#{ordinal + 1}  {step.Recipe.Name}");
            if (label.Contains(Event.current.mousePosition)) Tip(step.Recipe.Name, RecipeKey(step.Recipe));
            if (NeiSkin.Button(new Rect(x + w - 68, y, 68, 22), L("chain.tree.view_recipe"))) { LookupFromTree(RecipeKey(step.Recipe)); return; }
            y += 25;
        }
        else if (_treeView == TreeView.Steps && plan.Diagnostics.Count != 0)
        {
            if (NeiSkin.Button(new Rect(x, y, w, 21), L("chain.tree.plan_notes_count", plan.Diagnostics.Count))) SelectTreeView(TreeView.Bill);
            y += 24;
        }

        if (_treeView == TreeView.Bill && !_plugin.IsCrafting)
        {
            NeiSkin.Label(new Rect(x, y, w, 18), L("chain.spawn.hotkey_hint"), muted: true);
            y += 20;
        }

        var viewport = new Rect(x, y, w, panel.yMax - y - 7);
        if (viewport.height <= 0) return;
        if (viewport.height > 105)
        {
            bool expanded = Event.current.type == EventType.Repaint && BookmarkHelpHeld(Event.current);
            viewport.height -= 19;
            var help = new Rect(x, viewport.yMax + 1, w, 18);
            NeiSkin.Label(help, BookmarkHelpPrompt, muted: true);
            if (expanded && panel.Contains(Event.current.mousePosition))
                _bookmarkHelp = (L("chain.tree.help_title"), L("chain.tree.help_rows"));
        }
        if (_treeFiltered.Count == 0)
        {
            string empty = _treeQuery.Trim().Length > 0 ? L("chain.tree.no_match") :
                L("chain.tree.no_steps");
            NeiSkin.Text(new Rect(x + 3, y + 6, w - 6, 62), empty);
            return;
        }
        DrawTreeRows(viewport);
    }

    private void SelectTreeView(TreeView view)
    {
        if (_treeView == view) return;
        _treeView = view;
        _treeQuery = "";
        _treeScroll = Vector2.zero;
        BuildTreeRows(_treePlan!);
    }

    private void VisitTreeStep(int id)
    {
        if (!_treeOrdinals.ContainsKey(id)) return;
        if (_treeView == TreeView.Step && id != _treeStep)
        {
            // A graph, not a recursive expansion: following a loop collapses the breadcrumb.
            int visited = _treeTrail.IndexOf(id);
            if (visited >= 0) _treeTrail.RemoveRange(visited, _treeTrail.Count - visited);
            else { if (_treeTrail.Count == 64) _treeTrail.RemoveAt(0); _treeTrail.Add(_treeStep); }
        }
        else if (_treeView != TreeView.Step) _treeTrail.Clear();
        ShowTreeStep(id);
    }

    private void TreePrevious()
    {
        if (_treeTrail.Count == 0) { SelectTreeView(TreeView.Steps); return; }
        int last = _treeTrail.Count - 1, id = _treeTrail[last];
        _treeTrail.RemoveAt(last);
        if (_treeOrdinals.ContainsKey(id)) ShowTreeStep(id); else SelectTreeView(TreeView.Steps);
    }

    private void ShowTreeStep(int id)
    {
        _treeStep = id;
        _treeView = TreeView.Step;
        _treeScroll = Vector2.zero;
        _treeQuery = "";
        BuildTreeRows(_treePlan!);
    }

    private void BuildTreeRows(BookmarkChainBalanceResult plan)
    {
        _treeRowsChinese = _plugin.Chinese;
        _treeRows.Clear();
        if (_treeView == TreeView.Bill)
        {
            if (plan.Diagnostics.Count != 0)
            {
                TreeHeading(L("chain.tree.plan_notes"));
                foreach (var issue in plan.Diagnostics)
                    _treeRows.Add(new TreeRow(issue.DisplayMessage(_treeRowsChinese), issue.RecipeId + "  " + issue.ItemId));
            }
            AddBill(L("chain.tree.final_products"), plan.Results, false);
            AddBill(L("chain.tree.external_materials"), plan.Inputs.Where(a => !a.IsCatalyst), true);
            AddBill(L("chain.tree.reusable_tools"), plan.Inputs.Where(a => a.IsCatalyst), true);
            AddBill(L("chain.tree.missing_after_stock"), plan.Missing, false);
            AddBill(L("chain.tree.surplus_production"), plan.Surplus, false);
            // Net bills show allocated supply; this separate snapshot is physical inventory,
            // not a claim that every carried item was consumed or every bookmark was carried.
            var relevant = plan.Steps.SelectMany(s => s.Inputs.Concat(s.Outputs)).Where(a => !a.IsRequirement)
                .GroupBy(a => a.ItemId, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .Where(a => _bookmarkStock.ContainsKey(a.ItemId)).OrderBy(TreeName, StringComparer.CurrentCultureIgnoreCase);
            bool firstStock = true;
            foreach (var a in relevant)
            {
                if (firstStock) { TreeHeading(L("chain.tree.stock_before")); firstStock = false; }
                _treeRows.Add(new TreeRow(TreeQty(a, _bookmarkStock[a.ItemId]) + "  ×  " + TreeName(a), L("chain.tree.on_hand"), a.ItemId));
            }
        }
        else if (_treeView == TreeView.Steps)
        {
            // Draw a spanning forest in goal → material order. Shared branches appear once;
            // every original edge (including cycles and splits) remains accessible in Links.
            var seen = new HashSet<int>();
            var pending = new Stack<(int Entry, int Depth)>();
            foreach (var root in plan.Steps.Where(s => s.IsResult).Concat(plan.Steps))
            {
                pending.Push((root.EntryIndex, 0));
                while (pending.Count > 0)
                {
                    var (id, depth) = pending.Pop();
                    if (!seen.Add(id) || !_treeOrdinals.TryGetValue(id, out int i)) continue;
                    var step = plan.Steps[i];
                    string role = step.IsResult ? L("chain.tree.product") : L("chain.tree.intermediate");
                    string terms = RecipeKey(step.Recipe) + " " + string.Join(" ", step.Inputs.Concat(step.Outputs).Select(a => a.ItemId + " " + TreeName(a)));
                    _treeRows.Add(new TreeRow($"#{i + 1}  {step.Recipe.Name}", $"{role}  ·  {TreeBatches(step)}", step.Outputs.FirstOrDefault()?.ItemId, id, terms: terms, depth: depth));
                    foreach (var edge in _treeIncoming[id].Reverse())
                        if (edge.ProducerEntryIndex is int source && !seen.Contains(source)) pending.Push((source, depth + 1));
                }
            }
        }
        else if (_treeOrdinals.TryGetValue(_treeStep, out int ordinal))
        {
            var step = plan.Steps[ordinal];
            AddTreeLinks(plan, step);
            TreeHeading(L("chain.tree.inputs", TreeBatches(step)));
            var entries = BookmarkGroupEntries(_treeGroup);
            BookmarkBook.Entry? bookmark = step.EntryIndex >= 0 && step.EntryIndex < entries.Length ? entries[step.EntryIndex] : null;
            foreach (var input in step.Inputs)
            {
                double supplied = Math.Max(0, input.AvailableQuantity - (input.Quantity - input.NetQuantity));
                double missing = Math.Max(0, input.Quantity - input.AvailableQuantity);
                string note = L("chain.tree.input_breakdown", TreeQty(input, input.Quantity - input.NetQuantity),
                    TreeQty(input, supplied), TreeQty(input, missing));
                if (input.IsCatalyst) note = L("chain.tree.retained_input", note);
                _treeRows.Add(new TreeRow(TreeAmountTitle(input), note, input.ItemId, ingredient: input.Ingredient,
                    entry: input.Ingredient?.MatchesAny == true ? bookmark : null, ingredientIndex: input.IngredientIndex));
            }
            TreeHeading(L("chain.tree.outputs"));
            foreach (var output in step.Outputs)
                _treeRows.Add(new TreeRow(TreeAmountTitle(output), L("chain.tree.output_remaining", TreeQty(output, output.NetQuantity)), output.ItemId));
        }
        FilterTreeRows();

        void AddBill(string heading, IEnumerable<BookmarkChainBalanceAmount> amounts, bool stock)
        {
            bool first = true;
            foreach (var amount in amounts)
            {
                if (first) { TreeHeading(heading); first = false; }
                string note = stock ? L("chain.tree.stock_breakdown", TreeQty(amount, amount.AvailableQuantity),
                    TreeQty(amount, amount.Quantity - amount.AvailableQuantity)) : heading;
                _treeRows.Add(new TreeRow(TreeAmountTitle(amount), note, amount.ItemId, ingredient: amount.Ingredient));
            }
        }
    }

    private void AddTreeLinks(BookmarkChainBalanceResult plan, BookmarkChainBalanceStep step)
    {
        AddEdges(_treeIncoming[step.EntryIndex], incoming: true);
        AddEdges(_treeOutgoing[step.EntryIndex], incoming: false);

        void AddEdges(IEnumerable<BookmarkChainBalanceLink> edges, bool incoming)
        {
            bool first = true;
            foreach (var edge in edges)
            {
                if (first) { TreeHeading(incoming ? L("chain.tree.supplied_by") : L("chain.tree.supplies")); first = false; }
                int? other = incoming ? edge.ProducerEntryIndex : edge.ConsumerEntryIndex;
                int target = -1;
                string label;
                if (other is int id && _treeOrdinals.TryGetValue(id, out int i))
                {
                    if (id != step.EntryIndex) target = id;
                    label = $"#{i + 1}  {plan.Steps[i].Recipe.Name}";
                    if (id == step.EntryIndex) label = L("chain.tree.loop", label);
                }
                else label = !incoming ? L("chain.tree.final_reservation") : edge.FromCarried ? L("chain.tree.carried_stock") : L("chain.tree.bookmarked_stock");
                string amount = BookmarkAmount(edge.ItemId, edge.Quantity) + "  ×  " + Display(edge.ItemId);
                _treeRows.Add(new TreeRow(label, amount, edge.ItemId, target));
            }
        }
    }

    private string TreeBatches(BookmarkChainBalanceStep step) => L("chain.tree.batches", step.Batches);
    private string TreeName(BookmarkChainBalanceAmount a) => a.IsRequirement && a.Ingredient != null ? RequirementTitle(a.Ingredient) : Display(a.ItemId);
    private static string TreeQty(BookmarkChainBalanceAmount a, double quantity) => BookmarkAmount(a.ItemId, Math.Max(0, quantity)) +
        (a.IsLiquid && !a.ItemId.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? "mL" : "");
    private string TreeAmountTitle(BookmarkChainBalanceAmount a) => TreeQty(a, a.Quantity) + "  ×  " + TreeName(a);
    private void TreeHeading(string title) => _treeRows.Add(new TreeRow(title, "", heading: true));

    private void FilterTreeRows()
    {
        _treeFiltered.Clear();
        string term = _treeQuery.Trim();
        if (term.Length == 0) { _treeFiltered.AddRange(_treeRows); return; }
        TreeRow? section = null;
        bool matchSection = false;
        foreach (var row in _treeRows)
        {
            if (row.Heading) { section = row; matchSection = Matches(row.Search); continue; }
            if (!matchSection && !Matches(row.Search)) continue;
            if (section != null) { _treeFiltered.Add(section); section = null; }
            _treeFiltered.Add(row);
        }
        bool Matches(string value) => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrawTreeRows(Rect viewport)
    {
        float iconSize = 30 * IconScale;
        float pitch = Math.Max(37, iconSize + 7);
        var content = new Rect(0, 0, Mathf.Max(328, viewport.width - 18), _treeFiltered.Count * (float)pitch);
        var e = Event.current;
        // Do not claim Unity's global hotControl: closing the modal mid-drag must not
        // strand the next native panel with an owner that no longer receives OnGUI events.
        if (e.type == EventType.MouseDown && e.button == 2 && viewport.Contains(e.mousePosition))
        {
            _treePanning = true;
            e.Use();
        }
        if (_treePanning)
        {
            if (e.type == EventType.MouseDrag && e.button == 2) { _treeScroll -= e.delta; e.Use(); }
            else if (e.type == EventType.MouseUp && e.button == 2) { _treePanning = false; e.Use(); }
            else if (e.type == EventType.Repaint && !Input.GetMouseButton(2)) _treePanning = false;
        }
        bool wide = content.width > viewport.width - 18;
        _treeScroll.x = Mathf.Clamp(_treeScroll.x, 0, Mathf.Max(0, content.width - viewport.width + 18));
        _treeScroll.y = Mathf.Clamp(_treeScroll.y, 0, Mathf.Max(0, content.height - viewport.height + (wide ? 18 : 0)));
        _treeScroll = NeiSkin.BeginScrollView(viewport, _treeScroll, content, wide, vertical: true);
        int jump = -1;
        (string Id, bool Usage, BookmarkBook.Entry? Entry, int Ingredient)? lookup = null;
        try
        {
            var offset = viewport.position - _treeScroll;
            var visible = new Rect(viewport.x, viewport.y, viewport.width - 18, viewport.height - (wide ? 18 : 0));
            int first = Mathf.Max(0, Mathf.FloorToInt(_treeScroll.y / pitch));
            int end = Math.Min(_treeFiltered.Count, Mathf.CeilToInt((_treeScroll.y + viewport.height) / pitch));
            for (int i = first; i < end; i++)
            {
                var row = _treeFiltered[i];
                var rect = new Rect(0, i * pitch, content.width, pitch - 1);
                if (row.Heading)
                {
                    NeiSkin.BookmarkTint(new Rect(1, rect.y + 21, 12, 2), BookmarkMaterialTint);
                    NeiSkin.Label(new Rect(18, rect.y + 9, rect.width - 20, 25), row.Title);
                    continue;
                }
                var rowHit = RecipeListLayout.VisibleHit(rect, visible, offset);
                bool hover = rowHit.Contains(e.mousePosition + offset);
                if (hover) NeiSkin.Slot(rect, true);
                float indent = Math.Min(row.Depth, 6) * 7;
                if (indent > 0)
                {
                    NeiSkin.BookmarkTint(new Rect(indent - 4, rect.y - 1, 1, 19), BookmarkMaterialTint);
                    NeiSkin.BookmarkTint(new Rect(indent - 4, rect.y + 17, 7, 1), BookmarkMaterialTint);
                }
                float start = indent + 4;
                if (row.Item != null)
                {
                    _catalog.TryGetItem(row.Item, out var item);
                    var icon = new Rect(indent + 3, rect.y + (rect.height - iconSize) / 2, iconSize, iconSize);
                    string name = row.Ingredient is { MatchesAny: true } requirement && SameId(row.Item, requirement.BrowseId)
                        ? RequirementTitle(requirement) : item?.DisplayName ?? row.Item;
                    DrawItemSprite(icon, row.Item, item, name, row.Ingredient);
                    start = indent + iconSize + 8;
                    var iconHit = RecipeListLayout.VisibleHit(icon, visible, offset);
                    bool onItem = iconHit.Contains(e.mousePosition + offset);
                    if (e.type == EventType.Repaint && rowHit.width > 0 && rowHit.height > 0)
                    {
                        var property = PropertyQuery(row.Ingredient);
                        if (item != null || property != null)
                            _itemTargets.Add((row.Step < 0 ? rowHit : iconHit, item?.BrowseId, property));
                        // Dependency rows keep their navigation on text and expose the item on the icon.
                        if (hover && (row.Step < 0 || onItem))
                        {
                            var candidates = row.Ingredient?.MatchesAny == true ? _catalog.GetMatchingItems(row.Ingredient) : null;
                            ItemHover(row.Step < 0 ? rowHit : iconHit, name, item, row.Note,
                                candidates, candidates != null ? RequirementTitle(row.Ingredient!) : null,
                                row.Entry is { } entry ? candidate => SelectBookmarkVariant(entry, row.IngredientIndex, candidate) : null,
                                ingredient: row.Ingredient);
                        }
                        else if (hover) Tip(row.Title, row.Note);
                    }
                    if (e.type == EventType.MouseDown && (item != null || e.button == 0 && row.Entry != null) &&
                        (row.Step < 0 ? hover : onItem) && (e.button is 0 or 1) && !(PlayerCamera.main && PlayerCamera.main.dragItem))
                    {
                        lookup = (row.Item, e.button == 1, row.Entry, row.IngredientIndex);
                        e.Use();
                    }
                }
                else if (hover) Tip(row.Title, row.Note);
                float width = rect.width - start - (row.Step >= 0 ? 15 : 2);
                NeiSkin.Label(new Rect(start, rect.y, width, 19), row.Title);
                NeiSkin.Label(new Rect(start, rect.y + 17, width, 18), row.Note, muted: true);
                if (row.Step >= 0)
                {
                    NeiSkin.Icon(new Rect(rect.xMax - 12, rect.center.y - 4, 6, 8), "next");
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) jump = row.Step;
                }
            }
        }
        finally { GUI.EndScrollView(); }
        if (lookup is { } itemLookup)
        {
            if (itemLookup.Usage || itemLookup.Entry is not { } entry || !OpenBookmarkRequirement(entry, itemLookup.Ingredient))
                Lookup(itemLookup.Id, itemLookup.Usage);
        }
        else if (jump >= 0) VisitTreeStep(jump);
    }
}
