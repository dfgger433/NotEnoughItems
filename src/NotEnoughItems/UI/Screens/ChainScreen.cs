using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private static readonly Color BookmarkProductTint = new Color32(83, 145, 218, 112);
    private static readonly Color BookmarkMaterialTint = new Color32(83, 175, 112, 102);
    private readonly Dictionary<string, (BookmarkChainBalanceResult Plan, Dictionary<BookmarkBook.Entry, BookmarkChainBalanceStep> Steps)> _bookmarkBalance = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Recipe> _bookmarkRecipes = new(StringComparer.Ordinal);
    private int _bookmarkRecipeRevision = -1;
    private sealed class ChainTip(string key, bool details, string title, string hint,
        List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint, BookmarkBook.Entry? Entry)> blocks)
    {
        internal readonly string Key = key, Title = title, Hint = hint;
        internal readonly bool Details = details;
        internal readonly List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint, BookmarkBook.Entry? Entry)> Blocks = blocks;
    }

    private ChainTip? _bookmarkTip, _cachedChainTip, _pinnedChainTip;
    private static bool BookmarkHelpHeld(Event e) => e.alt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    private string BookmarkHelpPrompt => L("bookmark.help.prompt");
    private (string Title, string Rows)? _bookmarkHelp;
    private Rect _chainTipSource, _chainTipDrawnSource, _chainTipBox;
    private readonly List<(Rect Rect, string Id, BookmarkBook.Entry? Entry, int Ingredient, RecipeIngredient? Property)> _chainTipItems = new();
    private int _chainTipOffset, _chainTipMaxOffset;
    private Dictionary<string, int> _bookmarkStock = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<(string Id, float Condition)> _carriedBookmarkTools = new();

    internal void RefreshBookmarkStock()
    {
        RefreshCarriedBookmarkTools();
        if (!ShowLeft || !_book.Page(_group).Any(entry => _book.IsChainEnabled(_group, entry.Group))) return;
        var camera = PlayerCamera.main;
        var body = camera ? camera.body : null;
        var stock = NativeCraftStock.Quantities(body);
        foreach (var pair in NativeCraftStock.Quantities(body, nearby: true))
        {
            stock.TryGetValue(pair.Key, out int prior);
            stock[pair.Key] = (int)Math.Min(int.MaxValue, (long)prior + pair.Value);
        }
        if (stock.Count == _bookmarkStock.Count && stock.All(pair => _bookmarkStock.TryGetValue(pair.Key, out var count) && count == pair.Value)) return;
        _bookmarkStock = stock;
        InvalidateBookmarkView();
    }

    private void RefreshCarriedBookmarkTools()
    {
        if (!ShowLeft || !_book.Page(_group).Any(entry => entry.IsRecipe && entry.ShowRecipe &&
            !_book.IsChainEnabled(_group, entry.Group) && !_book.IsCompact(_group, entry.Group) &&
            !_book.IsCollapsed(_group, entry.Group) && BookmarkedRecipe(entry.Id)?.Inputs.Any(IsLiveBookmarkTool) == true)) return;
        var camera = PlayerCamera.main;
        UpdateCarriedBookmarkTools(NativeCraftStock.CarriedSolids(camera ? camera.body : null));
    }

    private void UpdateCarriedBookmarkTools(HashSet<(string Id, float Condition)> stock)
    {
        if (_carriedBookmarkTools.SetEquals(stock)) return;
        _carriedBookmarkTools = stock;
        // Only the ordinary/grouped rows use this snapshot; chain plans and tips have their own stock.
        _bookmarkLayout = null;
        ClearBookmarkHits();
    }

    private static bool IsLiveBookmarkTool(RecipeIngredient ingredient) =>
        ingredient.IsCatalyst && ingredient.MatchesAny && !ingredient.IsLiquid;

    private Recipe? BookmarkedRecipe(string key)
    {
        if (_bookmarkRecipeRevision != _catalog.Revision)
        {
            _bookmarkRecipeRevision = _catalog.Revision;
            _bookmarkRecipes.Clear();
            foreach (var recipe in _catalog.Recipes) _bookmarkRecipes.TryAdd(RecipeKey(recipe), recipe);
        }
        return _bookmarkRecipes.TryGetValue(key, out var found) ? found : null;
    }

    private static RecipeOutput? BookmarkProduct(BookmarkBook.Entry entry, Recipe recipe) =>
        recipe.Outputs.FirstOrDefault(output => SameId(output.BrowseId, entry.ProductId)) ?? recipe.Outputs.FirstOrDefault();

    private static int BookmarkBatches(BookmarkBook.Entry entry, Recipe recipe)
    {
        int output = Math.Max(1, BookmarkProduct(entry, recipe)?.Amount ?? 1);
        return entry.LegacyRecipeQuantity ? (entry.Quantity + output - 1) / output : entry.Quantity;
    }

    private bool AdjustBookmark(BookmarkBook.Entry entry, int delta)
    {
        if (entry.LegacyRecipeQuantity && BookmarkedRecipe(entry.Id) is { } recipe)
        {
            return _book.StepQuantity(entry, delta, BookmarkBatches(entry, recipe));
        }
        return _book.StepQuantity(entry, delta);
    }

    private BookmarkBook.Entry[] BookmarkGroupEntries(string group) => _book.Page(_group).Where(entry => entry.Group == group).ToArray();

    private BookmarkChainBalanceEntry BalanceEntry(BookmarkBook.Entry entry)
    {
        var recipe = entry.IsRecipe ? BookmarkedRecipe(entry.Id) : null;
        // Older pins may use a raw liquid ID. Resolve by the slot type without
        // rewriting the persisted bookmark or colliding with a same-ID solid.
        var variants = entry.Variants;
        if (recipe != null && variants.Any(pin => pin.Key < recipe.Inputs.Count && recipe.Inputs[pin.Key].IsLiquid &&
            !SameId(pin.Value, Catalog.ItemKey(pin.Value, liquid: true))))
            variants = variants.ToDictionary(pin => pin.Key, pin => pin.Key < recipe.Inputs.Count && recipe.Inputs[pin.Key].IsLiquid
                ? Catalog.ItemKey(pin.Value, liquid: true) : pin.Value);
        return new BookmarkChainBalanceEntry(recipe == null ? entry.Id : BookmarkProduct(entry, recipe)?.BrowseId ?? entry.Id,
            recipe == null ? entry.Quantity : BookmarkBatches(entry, recipe), recipe, entry.IsRecipe, variants,
            recipe != null && BookmarkProduct(entry, recipe) is { } product && ReferenceEquals(recipe, FavoriteForProduct(product.BrowseId)));
    }

    private (BookmarkChainBalanceResult Plan, Dictionary<BookmarkBook.Entry, BookmarkChainBalanceStep> Steps) BookmarkBalance(string group)
    {
        if (_bookmarkBalance.TryGetValue(group, out var cached)) return cached;
        var entries = BookmarkGroupEntries(group);
        var plan = BookmarkChainBalance.Calculate(_catalog, entries.Select(BalanceEntry), _bookmarkStock, PreferredMaterials);
        cached = (plan, plan.Steps.ToDictionary(step => entries[step.EntryIndex]));
        return _bookmarkBalance[group] = cached;
    }

    private IEnumerable<BookmarkCell> CellsForBookmark(BookmarkBook.Entry entry, bool compact, bool previewCarried = true)
    {
        bool chain = _book.IsChainEnabled(_group, entry.Group);
        if (!entry.IsRecipe || BookmarkedRecipe(entry.Id) is not { } recipe)
        {
            yield return new BookmarkCell(entry, entry.Id, -1, entry.Quantity, null, details: entry.IsRecipe ?
                () => L("bookmark.saved_recipe_unavailable") : null);
            yield break;
        }
        BookmarkChainBalanceStep? step = null;
        if (chain) BookmarkBalance(entry.Group).Steps.TryGetValue(entry, out step);
        int batches = step?.Batches ?? (chain ? 0 : BookmarkBatches(entry, recipe));
        var primary = BookmarkProduct(entry, recipe);
        bool expanded = !compact && entry.ShowRecipe;
        var outputs = expanded ? recipe.Outputs.OrderByDescending(output => ReferenceEquals(output, primary)) :
            (primary == null ? Enumerable.Empty<RecipeOutput>() : new[] { primary });
        if (primary == null) yield return new BookmarkCell(entry, entry.Id, -1, batches, $"×{batches}", details: () => recipe.Name);
        foreach (var output in outputs)
        {
            var planned = step?.Outputs.FirstOrDefault(amount => SameId(amount.ItemId, output.BrowseId));
            yield return new BookmarkCell(entry, output.BrowseId, -1, (double)output.Amount * batches,
                ReferenceEquals(output, primary) && (chain || entry.ShowQuantity) ? $"×{batches}" : null,
                planned?.NetQuantity > 0 ? BookmarkProductTint : null, () => L("bookmark.saved_recipe_hint", recipe.Name));
        }
        if (!expanded) yield break;
        foreach (int index in OrderedIngredients(entry, recipe))
        {
            var ingredient = recipe.Inputs[index];
            var planned = step?.Inputs.FirstOrDefault(amount => amount.IngredientIndex == index);
            string id = planned?.ItemId ?? (ingredient.MatchesAny
                ? SelectedBookmarkVariant(entry, index, ingredient, previewCarried && !chain) : ingredient.BrowseId);
            int copies = ingredient.IsCatalyst ? 1 : batches;
            double unit = ingredient.MatchesAny && !ingredient.IsLiquid ? 1 : ingredient.Amount;
            BookmarkLiquidPreview? liquidPreview = null;
            if (planned == null && ingredient is { MatchesAny: true, IsLiquid: true })
            {
                if (_catalog.TryGetItem(id, out var liquid)) unit = LiquidInputVolume(ingredient, liquid) ?? unit;
                // The cyclic example is presentation only, never a craft allocation or a saved pin.
                if (previewCarried && !chain)
                {
                    var candidates = DiagramAlternatives(recipe, index, ingredient).Eligible;
                    string? pin = BookmarkVariantId(entry, index, ingredient);
                    if (candidates.Count > 1 && !candidates.Any(candidate => SameId(candidate.BrowseId, pin)))
                        liquidPreview = new BookmarkLiquidPreview(OrderedCandidates(recipe, index, candidates)
                            .Select(candidate => (candidate.BrowseId, (LiquidInputVolume(ingredient, candidate) ?? unit) * copies)).ToArray());
                }
            }
            double amount = planned?.Quantity ?? unit * copies;
            yield return new BookmarkCell(entry, id, index, amount, null, planned?.NetQuantity > 0 ? BookmarkMaterialTint : null,
                ingredient.MatchesAny || ingredient.IsCatalyst ? () => BookmarkIngredientDetails(ingredient) : null, liquidPreview);
        }
    }

    private string BookmarkIngredientDetails(RecipeIngredient ingredient)
    {
        string details = ingredient.MatchesAny ? L("bookmark.choose_material_hint", RequirementTitle(ingredient)) : string.Empty;
        return ingredient.IsCatalyst ? (details.Length == 0 ? "" : details + "\n") + L("bookmark.retained_tool") : details;
    }

    private static IEnumerable<int> OrderedIngredients(BookmarkBook.Entry entry, Recipe recipe) =>
        entry.IngredientOrder.Where(slot => slot < recipe.Inputs.Count).Concat(Enumerable.Range(0, recipe.Inputs.Count)).Distinct();

    private string SelectedBookmarkVariant(BookmarkBook.Entry entry, int index, RecipeIngredient ingredient, bool previewCarried)
    {
        var matches = _catalog.GetMatchingItems(ingredient);
        string? pin = BookmarkVariantId(entry, index, ingredient);
        if (pin != null && matches.FirstOrDefault(item => SameId(item.BrowseId, pin)) is { } pinned) return pinned.BrowseId;
        if (previewCarried && IsLiveBookmarkTool(ingredient))
        {
            var carried = matches.FirstOrDefault(item => _carriedBookmarkTools.Any(tool => SameId(tool.Id, item.BrowseId) &&
                tool.Condition >= ingredient.MinimumCondition && (!ingredient.NoMaxCondition || tool.Condition < .991f)));
            if (carried != null) return carried.BrowseId;
        }
        return matches.FirstOrDefault()?.BrowseId ?? ingredient.BrowseId;
    }

    private static string? BookmarkVariantId(BookmarkBook.Entry entry, int index, RecipeIngredient ingredient) =>
        entry.Variant(index) is { } pin && ingredient.IsLiquid ? Catalog.ItemKey(pin, liquid: true) : entry.Variant(index);

    private bool CycleBookmarkVariant(BookmarkCell cell, int direction)
    {
        if (BookmarkedRecipe(cell.Entry.Id) is not { } recipe || cell.Ingredient < 0 || cell.Ingredient >= recipe.Inputs.Count ||
            !recipe.Inputs[cell.Ingredient].MatchesAny) return false;
        var ingredient = recipe.Inputs[cell.Ingredient];
        var matches = _catalog.GetMatchingItems(ingredient);
        if (matches.Count == 0) return true;
        SetBookmarkVariant(cell.Entry, cell.Ingredient,
            NextBookmarkVariant(matches, BookmarkVariantId(cell.Entry, cell.Ingredient, ingredient), cell.Id, direction));
        return true;
    }

    private static string? NextBookmarkVariant(IReadOnlyList<CatalogItem> matches, string? pin, string displayed, int direction)
    {
        int current = -1;
        for (int i = 0; i < matches.Count; i++)
            if (SameId(matches[i].BrowseId, pin)) { current = i; break; }
        // Keep the automatic state reachable, but on the first turn away from it
        // don't pin the same icon the player is already seeing when others exist.
        int next = Wrap(current + 1 + direction, matches.Count + 1) - 1;
        if (current < 0 && next >= 0 && matches.Count > 1 && SameId(matches[next].BrowseId, displayed))
            next += direction;
        return next < 0 ? null : matches[next].BrowseId;
    }

    private void SetBookmarkVariant(BookmarkBook.Entry entry, int ingredientIndex, string? selection)
    {
        if (_book.SetVariant(entry, ingredientIndex, selection)) SaveBookmarks();
    }

    private void SelectBookmarkVariant(BookmarkBook.Entry entry, int ingredientIndex, CatalogItem candidate)
    {
        if (BookmarkedRecipe(entry.Id) is not { } recipe || ingredientIndex < 0 || ingredientIndex >= recipe.Inputs.Count ||
            !recipe.Inputs[ingredientIndex].MatchesAny || !recipe.Inputs[ingredientIndex].Matches(candidate)) return;
        SetBookmarkVariant(entry, ingredientIndex,
            SameId(BookmarkVariantId(entry, ingredientIndex, recipe.Inputs[ingredientIndex]), candidate.BrowseId) ? null : candidate.BrowseId);
    }

    private bool OpenBookmarkRequirement(BookmarkBook.Entry entry, int ingredientIndex)
    {
        if (BookmarkedRecipe(entry.Id) is not { } recipe || ingredientIndex < 0 || ingredientIndex >= recipe.Inputs.Count ||
            !recipe.Inputs[ingredientIndex].MatchesAny) return false;
        OpenView(entry.Id, false);
        _selectedRecipeIngredient = ingredientIndex;
        return true;
    }

    private bool ReorderBookmarkIngredients(BookmarkCell source, BookmarkCell target, bool before)
    {
        if (source.Ingredient < 0 || target.Ingredient < 0 || source.Ingredient == target.Ingredient || BookmarkedRecipe(source.Entry.Id) is not { } recipe) return false;
        var slots = OrderedIngredients(source.Entry, recipe).ToList();
        slots.Remove(source.Ingredient);
        slots.Insert(slots.IndexOf(target.Ingredient) + (before ? 0 : 1), source.Ingredient);
        return _book.SetIngredientOrder(source.Entry, slots);
    }

    private void ClickBookmark(BookmarkCell cell, int button, bool shift, bool alt)
    {
        if (PlayerCamera.main && PlayerCamera.main.dragItem) return;
        var entry = cell.Entry;
        if (alt)
        {
            if (_book.Split(entry)) SaveBookmarks();
            return;
        }
        if (_plugin.IsCrafting)
        {
            if (button == 0 && cell.Ingredient < 0 && BookmarkedRecipe(entry.Id)?.NativeRecipe is { } native)
                _plugin.OpenCrafting(native);
            return;
        }
        if (entry.IsRecipe && button == 0)
        {
            if (cell.Ingredient < 0) { Lookup(entry.Id, false); return; }
            if (_book.IsChainEnabled(_group, entry.Group) && OpenBookmarkRequirement(entry, cell.Ingredient)) return;
        }
        _catalog.TryGetItem(cell.Id, out var item);
        if (CanGiveSlot(!entry.IsRecipe, cell.Id, item, button, shift, alt)) GiveSlot(item!, button);
        else Lookup(cell.Id, button == 1);
    }

    private bool PageChainEnabled() => _book.IsChainEnabled(_group) || _book.Page(_group).Any(entry => _book.IsChainEnabled(_group, entry.Group));

    private void BookmarkBracketTip(string group, bool details, Rect source)
    {
        if (group.Length == 0)
        {
            Tip(L("bookmark.group.create"), L("bookmark.group.select_range"));
            return;
        }
        bool chain = _book.IsChainEnabled(_group, group);
        string title = chain ? L("chain.group_title") : L("bookmark.group.title");
        if (ShowBookmarkHelp(title, page: false, hasEntries: true, chain)) return;
        if (chain) BookmarkChainTip(title, new[] { group }, details, source);
        else Tip(title, BookmarkHelpPrompt);
    }

    private bool ShowBookmarkHelp(string title, bool page, bool hasEntries, bool chain)
    {
        var e = Event.current;
        if (e.type != EventType.Repaint || !BookmarkHelpHeld(e)) return false;
        _bookmarkHelp = (title, BookmarkHelp.Rows(page, hasEntries, chain, _plugin.IsCrafting, _plugin.Chinese,
            _plugin.BookmarkKeyBound ? _plugin.BookmarkKey : null, _plugin.DebugSpawnMissingMaterials));
        return true;
    }

    private string WithBookmarkHelpPrompt(string hint) => hint + "\n" + BookmarkHelpPrompt;

    private void BookmarkPageChainTip(bool details, Rect source) => BookmarkChainTip(L("chain.page_title"),
        _book.Page(_group).Select(entry => entry.Group).Distinct().Where(group => _book.IsChainEnabled(_group, group)), details, source);

    private void BookmarkChainTip(string title, IEnumerable<string> groups, bool details, Rect source)
    {
        var e = Event.current;
        if (_pinnedChainTip != null || SupportedItemsHit(e.mousePosition)) return;
        if (e.type == EventType.ScrollWheel && !e.control && !e.alt && source.Equals(_chainTipDrawnSource) && _chainTipMaxOffset > 0)
        {
            ScrollChainTip(e.delta.y);
            e.Use();
            return;
        }
        if (e.type != EventType.Repaint) return;
        details |= SelectionShiftHeld;
        string[] selected = groups.ToArray();
        string key = title + "\n" + string.Join("\n", selected);
        var tip = _cachedChainTip;
        if (tip == null || tip.Key != key || tip.Details != details)
        {
            _chainTipOffset = _chainTipMaxOffset = 0;
            _cachedChainTip = tip = BuildChainTip(key, title, selected, details);
        }
        _chainTipSource = source;
        if (tip.Blocks.Count == 0)
        {
            Tip(tip.Title, WithBookmarkHelpPrompt(tip.Hint));
            return;
        }
        _bookmarkTip = tip;
        if (details && CanPinChainTip)
        {
            ClearSupportedItemsTip();
            ClearItemTooltipInput();
            _pinnedChainTip = tip;
        }
    }

    private ChainTip BuildChainTip(string key, string title, string[] selected, bool details)
    {
        var plans = selected.Select(group => BookmarkBalance(group).Plan).ToArray();
        var blocks = new List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint, BookmarkBook.Entry? Entry)>();
        void Section(string label, IEnumerable<BookmarkChainBalanceAmount> amounts, Color? tint, BookmarkBook.Entry? entry = null)
        {
            var items = (entry == null ? amounts.GroupBy(amount => (amount.ItemId, amount.IsCatalyst)).Select(row =>
            {
                var a = row.First();
                // Retained tools are reused across independent lanes; one copy covers every group.
                double quantity = a.IsCatalyst ? row.Max(m => m.Quantity) : row.Sum(m => m.Quantity);
                return new BookmarkChainBalanceAmount(a.ItemId, quantity, 0, 0, a.IsLiquid, a.IsRequirement, a.Ingredient);
            }) : amounts).ToArray();
            if (items.Length > 0) blocks.Add((label, items, tint, entry));
        }
        Section(L("chain.tip.products"), plans.SelectMany(plan => plan.Results), BookmarkProductTint);
        Section(L("chain.tip.materials"), plans.SelectMany(plan => plan.Inputs).Where(amount => !amount.IsCatalyst), BookmarkMaterialTint);
        Section(L("chain.tip.tools"), plans.SelectMany(plan => plan.Inputs).Where(amount => amount.IsCatalyst), BookmarkMaterialTint);
        Section(L("chain.tip.surplus"), plans.SelectMany(plan => plan.Surplus), null);
        // A page summary is the sum of independent lane bills. Do not sum per-lane
        // shortages: that would credit the same carried stack once per group.
        if (plans.Length == 1) Section(L("chain.tip.shortage"), plans[0].Missing, new Color32(182, 93, 87, 105));
        if (details)
            for (int i = 0; i < plans.Length; i++)
            {
                var entries = BookmarkGroupEntries(selected[i]);
                foreach (var step in plans[i].Steps)
                    Section($"×{step.Batches}  {step.Recipe.Name}", step.Outputs.Concat(step.Inputs), null, entries[step.EntryIndex]);
            }
        bool chinese = _plugin.Chinese;
        var notes = plans.SelectMany(plan => plan.Diagnostics).Where(issue => issue.Code != BookmarkChainDiagnosticCode.MissingMaterial)
            .Select(issue => issue.DisplayMessage(chinese)).Distinct().Take(3).ToArray();
        string hint = details ? L("chain.tip.steps_hint") : L("chain.tip.hold_shift_hint");
        if (blocks.Count == 0) hint = L("chain.tip.empty") + (notes.Length == 0 ? "" : "\n" + string.Join("\n", notes));
        else if (notes.Length != 0) blocks.Add((string.Join(" · ", notes), Array.Empty<BookmarkChainBalanceAmount>(), null, null));
        return new ChainTip(key, details, title, hint, blocks);
    }

    private bool CanPinChainTip => ShowLeft && !_options && !_plugin.IsCrafting && !_plugin.NativePointerActive &&
        _bookmarkDrag == null && SelectionShiftHeld;

    private void ClearChainTip()
    {
        _bookmarkTip = _pinnedChainTip = null;
        _chainTipSource = _chainTipDrawnSource = _chainTipBox = default;
        _chainTipItems.Clear();
        _chainTipOffset = _chainTipMaxOffset = 0;
    }

    private bool ChainTipHit(Vector2 pointer) => _pinnedChainTip != null && CanPinChainTip && _chainTipBox.Contains(pointer);

    private (string? Id, RecipeIngredient? Property) ChainTipItemAt(Vector2 pointer)
    {
        foreach (var item in _chainTipItems)
            if (item.Rect.Contains(pointer)) return (item.Id, item.Property);
        return default;
    }

    private void ScrollChainTip(float delta)
    {
        if (delta == 0) return;
        _chainTipOffset = Math.Clamp(_chainTipOffset + Math.Sign(delta), 0, _chainTipMaxOffset);
        // The previous page's item geometry must not be used before the next repaint.
        _chainTipItems.Clear();
    }

    private void HandleChainTipInput(Event e)
    {
        if (_pinnedChainTip == null) return;
        if (!CanPinChainTip) { ClearChainTip(); return; }
        bool inside = ChainTipHit(e.mousePosition);
        if (e.type == EventType.ScrollWheel && (inside || _chainTipMaxOffset > 0 && _chainTipSource.Contains(e.mousePosition) && !e.control && !e.alt))
        {
            ScrollChainTip(e.delta.y);
            e.Use();
            return;
        }
        if (!inside)
        {
            if (e.type == EventType.MouseDown) ClearChainTip();
            return;
        }
        var target = _chainTipItems.Find(item => item.Rect.Contains(e.mousePosition));
        if (e.type == EventType.MouseDown && e.button is 0 or 1 && target.Id != null)
        {
            ClearChainTip();
            if (e.button != 0 || target.Entry is not { } entry || !OpenBookmarkRequirement(entry, target.Ingredient))
                Lookup(target.Id, usage: e.button == 1);
            e.Use();
        }
        else if (e.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag) e.Use();
    }

    private bool DrawBookmarkChainTip(Vector2 mouse)
    {
        var tip = _pinnedChainTip ?? _bookmarkTip;
        if (tip == null || _pinnedChainTip == null && _pinnedItems != null)
        {
            _chainTipDrawnSource = default;
            _chainTipOffset = _chainTipMaxOffset = 0;
            return false;
        }
        const float heading = 19, rowsTop = 59, pad = 6;
        float slot = 31 * IconScale;
        var viewport = new Rect(pad, pad, _width - 2 * pad, _height - 2 * pad);
        if (viewport.width < slot + 2 * pad || viewport.height < rowsTop + heading + slot + pad)
        {
            ClearChainTip();
            Tip(tip.Title, WithBookmarkHelpPrompt(tip.Hint));
            return false;
        }
        int columns = Math.Min(11, (int)((viewport.width - 2 * pad) / slot));
        float width = columns * slot + 2 * pad;
        var blocks = tip.Blocks;
        var counts = blocks.Select(block => block.Items.Count).ToArray();
        int rowCount = counts.Sum(count => Math.Max(1, (count + columns - 1) / columns));
        float naturalHeight = rowsTop + pad + blocks.Count * heading + counts.Sum(count => (count + columns - 1) / columns * slot);
        float height = Math.Min(viewport.height, naturalHeight);
        _chainTipMaxOffset = naturalHeight <= height ? 0 : rowCount - 1;
        _chainTipOffset = Math.Clamp(_chainTipOffset, 0, _chainTipMaxOffset);
        bool pinned = _pinnedChainTip != null;
        var size = new Vector2(width, height);
        var box = pinned ? TooltipPlacement.AtSlot(_chainTipSource, size, viewport) : TooltipPlacement.AtPointer(mouse, size, viewport);
        _chainTipDrawnSource = _chainTipSource;
        _chainTipBox = pinned ? box : default;
        _chainTipItems.Clear();
        (Rect Rect, BookmarkChainBalanceAmount Amount, CatalogItem? Item, string Caption, BookmarkBook.Entry? Entry)? hovered = null;
        NeiSkin.TooltipBox(box);
        NeiSkin.BookmarkTint(new Rect(box.x, box.y, width, 2), new Color32(91, 164, 111, 255));
        GUI.BeginGroup(box);
        try
        {
            bool showPageMarker = _chainTipMaxOffset > 0 && width >= 148;
            NeiSkin.Label(new Rect(pad, 4, width - 2 * pad - (showPageMarker ? 81 : 0), 20), tip.Title);
            NeiSkin.Label(new Rect(pad, 22, width - 2 * pad, 18), pinned ?
                L("chain.tip.pinned_hint") : tip.Hint, muted: true);
            NeiSkin.Label(new Rect(pad, 39, width - 2 * pad, 17), BookmarkHelpPrompt, muted: true);
            float top = rowsTop;
            int previous = -1;
            foreach (var (section, start, count) in BookmarkBoardPaging.Rows(counts, columns).Skip(_chainTipOffset))
            {
                var block = blocks[section];
                float rowHeight = (section != previous ? heading : 0) + (count != 0 ? slot : 0);
                if (top + rowHeight > height - pad) break;
                if (section != previous)
                {
                    NeiSkin.Label(new Rect(pad, top, width - 2 * pad, heading), block.Title);
                    top += heading;
                    previous = section;
                }
                for (int i = 0; i < count; i++)
                {
                    var a = block.Items[start + i];
                    var r = new Rect(pad + i * slot, top, slot - 1, slot - 1);
                    _catalog.TryGetItem(a.ItemId, out var item);
                    var caption = a.IsRequirement && a.Ingredient != null ? RequirementTitle(a.Ingredient) : item?.DisplayName ?? a.ItemId;
                    if (block.Tint is { } tint) NeiSkin.BookmarkTint(r, tint);
                    DrawItemSprite(new Rect(r.x + 3, r.y + 2, r.width - 6, r.height - 6), a.ItemId, item, caption, a.Ingredient, liquid: a.IsLiquid);
                    NeiSkin.SlotAmount(r, new SlotQuantity(Math.Max(0, a.Quantity), a.IsLiquid ||
                        a.ItemId.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase)));
                    var hit = new Rect(r.position + box.position, r.size);
                    string id = item?.BrowseId ?? (a.IsRequirement ? a.Ingredient?.BrowseId ?? a.ItemId : a.ItemId);
                    // The preview is paint-only. Keep popup targets separate from underlying slots
                    // so releasing Shift immediately restores the underlying shortcut targets.
                    if (pinned) _chainTipItems.Add((hit, id, block.Entry, a.IngredientIndex, PropertyQuery(a.Ingredient)));
                    if (pinned && hit.Contains(mouse))
                    {
                        NeiSkin.Slot(r, hovered: true, selected: true);
                        hovered = (hit, a, item, caption, block.Entry);
                    }
                }
                if (count != 0) top += slot;
            }
            if (showPageMarker) NeiSkin.Label(new Rect(width - 88, 4, 81, heading), $"↕ {_chainTipOffset + 1}/{rowCount}", TextAnchor.MiddleRight);
        }
        finally { GUI.EndGroup(); }
        if (hovered is { } selection)
        {
            var ingredient = selection.Amount.Ingredient;
            var candidates = selection.Entry != null && ingredient?.MatchesAny == true ? _catalog.GetMatchingItems(ingredient) : null;
            ItemHover(selection.Rect, selection.Caption, selection.Item,
                L("chain.tip.quantity", TreeQty(selection.Amount, selection.Amount.Quantity)),
                candidates, candidates != null ? RequirementTitle(ingredient!) : null,
                origin: CandidateSource.Chain, ingredient: ingredient,
                liquidVolume: selection.Item?.IsLiquid == true ? (float)selection.Amount.Quantity : null);
            // Keep a plain preview as the fallback if the candidate list does not fit.
            if (_itemTip is { } itemTip) QueueItemTip(mouse, selection.Rect, itemTip, box);
        }
        return true;
    }

    private Recipe? FavoriteForProduct(string id)
    {
        if (_book.FavoriteRoute(id) is { } route && BookmarkedRecipe(route) is { } chosen &&
            chosen.Outputs.Any(output => SameId(output.BrowseId, id))) return chosen;
        return _book.Favorites.Select(BookmarkedRecipe).FirstOrDefault(recipe =>
            recipe?.Outputs.Any(output => SameId(output.BrowseId, id)) == true);
    }

    private HashSet<string>? _preferredMaterials;
    private HashSet<string> PreferredMaterials => _preferredMaterials ??= _book.FavoriteMaterials
        .Concat(_book.FavoriteRoutes.Select(pair => pair.Key))
        .Concat(_book.Favorites.Select(BookmarkedRecipe).OfType<Recipe>().SelectMany(recipe => recipe.Outputs.Select(output => output.BrowseId)))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void ToggleFavorite(string product, string key)
    {
        bool remove = FavoriteForProduct(product) is { } current && SameId(RecipeKey(current), key);
        // Migrate any pre-product favorites touching this output, preserving their other outputs.
        foreach (string old in _book.Favorites.Where(k => BookmarkedRecipe(k)?.Outputs.Any(output => SameId(output.BrowseId, product)) == true).ToArray())
        {
            if (BookmarkedRecipe(old) is { } recipe)
                foreach (var output in recipe.Outputs.Where(output => !SameId(output.BrowseId, product)))
                    if (_book.FavoriteRoute(output.BrowseId) == null) _book.SetFavoriteRoute(output.BrowseId, old);
            _book.ToggleFavorite(old);
        }
        _book.SetFavoriteRoute(product, remove ? null : key);
        SaveBookmarks();
    }

    internal bool OrderFavoriteAt(Vector2 pointer)
    {
        pointer /= _scale;
        if (!ShowRight || !_right.Contains(pointer) || _options) return false;
        var hit = _itemTargets.FindLast(t => t.Rect.Contains(pointer));
        if (hit.Id == null) return false;
        var recipe = FavoriteForProduct(hit.Id);
        if (recipe == null) Status(L("bookmark.order.favorite_required"));
        else BookmarkOrder(recipe, BookmarkQuantity, hit.Id);
        return true;
    }

    internal bool OrderAndInspectAt(Vector2 pointer)
    {
        if (!_open || _options) return false;
        var recipe = _recipeTargets.Find(target => target.Heart.Contains(pointer / _scale)).Recipe;
        if (recipe == null) return false;
        BookmarkOrder(recipe, 1, FavoriteProduct(recipe), inspect: true);
        return true;
    }

    private void BookmarkOrder(Recipe root, int quantity, string? productId = null, bool inspect = false)
    {
        var favorites = PreferredMaterials.OrderBy(id => id, StringComparer.Ordinal)
            .Select(product => (Product: product, Recipe: FavoriteForProduct(product))).Where(route => route.Recipe != null).ToArray();
        var picked = new List<(Recipe Recipe, string? Product)>();
        var seen = new HashSet<Recipe> { root };
        picked.Add((root, root.Outputs.FirstOrDefault(output => SameId(output.BrowseId, productId ?? _item))?.BrowseId ?? root.Outputs.FirstOrDefault()?.BrowseId));
        for (int i = 0; i < picked.Count; i++)
            foreach (var input in picked[i].Recipe.Inputs)
            {
                var producer = favorites.FirstOrDefault(candidate => candidate.Recipe != picked[i].Recipe &&
                    candidate.Recipe!.Outputs.Any(output => SameId(output.BrowseId, candidate.Product) && OutputMatches(input, output)));
                if (producer.Recipe is { } dependency && seen.Add(dependency))
                    picked.Add((dependency, producer.Product));
            }

        string group = Guid.NewGuid().ToString("N");
        foreach (var (recipe, product) in picked)
        {
            if (_book.Add(_group, RecipeKey(recipe), recipe == root ? quantity : 1, group, productId: product, showQuantity: true) != null) continue;
            // Do not silently save a truncated order when bookmarks reach their limit.
            _book.RemoveGroup(_group, group);
            Status(L("bookmark.order.could_not_fit"));
            return;
        }
        if (!_book.IsChainEnabled(_group, group)) _book.ToggleChain(_group, group);
        SaveBookmarks();
        _open = false;
        _bookmarkPage = int.MaxValue;
        _skipFocusFrame = true;
        if (inspect) OpenTreeGroup(group);
        else Status(L("bookmark.order.added"));
    }
}
