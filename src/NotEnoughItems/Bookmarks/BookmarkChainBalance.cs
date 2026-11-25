using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

/// <summary>A single, explicitly saved entry in a bookmark group.</summary>
internal sealed class BookmarkChainBalanceEntry
{
    /// <param name="id">Bookmarked item or selected recipe product ID; a legacy recipe key is also accepted.</param>
    /// <param name="quantity">Number of recipe batches, or units of a plain bookmarked material.</param>
    /// <param name="recipe">The explicitly bookmarked recipe, if any.</param>
    /// <param name="showRecipe">True for an unresolved recipe bookmark; presentation does not affect a resolved recipe.</param>
    /// <param name="preferredIngredients">Explicit, zero-based ingredient slot selections.</param>
    public BookmarkChainBalanceEntry(string id, int quantity = 1, Recipe? recipe = null, bool showRecipe = false,
        IReadOnlyDictionary<int, string>? preferredIngredients = null)
        : this(id, quantity, recipe, showRecipe, preferredIngredients, preferredRecipe: false)
    {
    }

    /// <summary>Creates a bookmark balance entry with optional favorite-producer preference.</summary>
    /// <param name="id">Bookmarked item or selected recipe product ID.</param>
    /// <param name="quantity">Number of recipe batches, or units of a plain bookmarked material.</param>
    /// <param name="recipe">The explicitly bookmarked recipe, if any.</param>
    /// <param name="showRecipe">True for an unresolved recipe bookmark.</param>
    /// <param name="preferredIngredients">Explicit, zero-based ingredient slot selections.</param>
    /// <param name="preferredRecipe">True when this saved recipe is the selected favorite route for its outputs.</param>
    public BookmarkChainBalanceEntry(string id, int quantity, Recipe? recipe, bool showRecipe,
        IReadOnlyDictionary<int, string>? preferredIngredients, bool preferredRecipe)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A bookmark ID is required.", nameof(id));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Id = id.Trim();
        Quantity = quantity;
        Recipe = recipe;
        ShowRecipe = showRecipe;
        PreferredIngredients = preferredIngredients;
        PreferredRecipe = preferredRecipe;
    }

    internal string Id { get; }
    internal int Quantity { get; }
    internal Recipe? Recipe { get; }
    internal bool ShowRecipe { get; }
    internal IReadOnlyDictionary<int, string>? PreferredIngredients { get; }
    /// <summary>Whether this recipe is the user's favorite producer route for its outputs.</summary>
    internal bool PreferredRecipe { get; }
}

/// <summary>A typed material amount in the summary or one recipe step.</summary>
internal sealed class BookmarkChainBalanceAmount(string itemId, double quantity, double netQuantity, double availableQuantity,
    bool liquid, bool requirement = false, RecipeIngredient? ingredient = null, int ingredientIndex = -1)
{
    /// <summary>The item, liquid, or generic requirement browse ID.</summary>
    public string ItemId { get; } = itemId;
    /// <summary>Gross amount for a step; categorized amount for a summary row.</summary>
    public double Quantity { get; } = quantity;
    /// <summary>External input or remaining output for a step; categorized amount for a summary row.</summary>
    public double NetQuantity { get; } = netQuantity;
    /// <summary>Supply from bookmarked or carried materials; step inputs also include in-chain production.</summary>
    public double AvailableQuantity { get; } = availableQuantity;
    /// <summary>Whether the amount is in liquid units.</summary>
    public bool IsLiquid { get; } = liquid;
    /// <summary>Whether a generic requirement has no concrete item selected.</summary>
    public bool IsRequirement { get; } = requirement;
    /// <summary>Whether the input is retained instead of consumed.</summary>
    public bool IsCatalyst => Ingredient?.IsCatalyst == true;
    /// <summary>Original recipe input when applicable.</summary>
    public RecipeIngredient? Ingredient { get; } = ingredient;
    /// <summary>Zero-based ingredient slot, or -1 for an output.</summary>
    public int IngredientIndex { get; } = ingredientIndex;
}

/// <summary>One explicitly bookmarked recipe and its balanced batch count.</summary>
internal sealed class BookmarkChainBalanceStep(int entryIndex, Recipe recipe, int batches,
    IReadOnlyList<BookmarkChainBalanceAmount> inputs, IReadOnlyList<BookmarkChainBalanceAmount> outputs,
    bool isResult = false)
{
    /// <summary>Position of the source bookmark in the calculation's input enumeration.</summary>
    public int EntryIndex { get; } = entryIndex;
    /// <summary>The explicitly bookmarked recipe.</summary>
    public Recipe Recipe { get; } = recipe;
    /// <summary>Number of whole batches planned for this step.</summary>
    public int Batches { get; } = batches;
    /// <summary>Gross and net amounts for every input slot.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Inputs { get; } = inputs;
    /// <summary>Gross and remaining amounts of all products, including coproducts.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Outputs { get; } = outputs;
    /// <summary>Whether this step is a final product request rather than a dependency.</summary>
    public bool IsResult { get; } = isResult;
}

/// <summary>A material allocation from a producer step or carried inventory to a consumer step.</summary>
internal sealed class BookmarkChainBalanceLink(int? producerEntryIndex, int? consumerEntryIndex,
    string itemId, double quantity, bool fromCarried)
{
    /// <summary>Bookmark index of the producing recipe, or null for inventory.</summary>
    public int? ProducerEntryIndex { get; } = producerEntryIndex;
    /// <summary>Bookmark index of the consuming recipe, or null for an unconsumed reservation.</summary>
    public int? ConsumerEntryIndex { get; } = consumerEntryIndex;
    /// <summary>Stable item or liquid browse ID transferred by this link.</summary>
    public string ItemId { get; } = itemId;
    /// <summary>Amount transferred.</summary>
    public double Quantity { get; } = quantity;
    /// <summary>True when this allocation came from the physical carried snapshot.</summary>
    public bool FromCarried { get; } = fromCarried;
}

/// <summary>Pure net balance for a single bookmark group.</summary>
internal sealed class BookmarkChainBalanceResult(IReadOnlyList<BookmarkChainBalanceAmount> results,
    IReadOnlyList<BookmarkChainBalanceAmount> inputs, IReadOnlyList<BookmarkChainBalanceAmount> surplus,
    IReadOnlyList<BookmarkChainBalanceAmount> missing, IReadOnlyList<BookmarkChainBalanceStep> steps,
    IReadOnlyList<BookmarkChainDiagnostic> diagnostics,
    IReadOnlyList<int>? resultEntryIndices = null,
    IReadOnlyList<BookmarkChainBalanceLink>? links = null)
{
    /// <summary>Net group result quantities from output recipes.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Results { get; } = results;
    /// <summary>Net external materials and retained tools.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Inputs { get; } = inputs;
    /// <summary>Unused production from intermediate recipes.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Surplus { get; } = surplus;
    /// <summary>External materials not covered by known supply.</summary>
    public IReadOnlyList<BookmarkChainBalanceAmount> Missing { get; } = missing;
    /// <summary>Producer-first recipe steps where acyclic, with a stable fallback inside loops.</summary>
    public IReadOnlyList<BookmarkChainBalanceStep> Steps { get; } = steps;
    /// <summary>Unresolved or invalid bookmarks and bounds reached during the calculation.</summary>
    public IReadOnlyList<BookmarkChainDiagnostic> Diagnostics { get; } = diagnostics;
    /// <summary>Explicit recipe-entry indices marked as final results, including zero-quantity entries.</summary>
    public IReadOnlyList<int> ResultEntryIndices { get; } = resultEntryIndices ?? Array.Empty<int>();
    /// <summary>Actual producer-to-consumer and carried-inventory allocations.</summary>
    public IReadOnlyList<BookmarkChainBalanceLink> Links { get; } = links ?? Array.Empty<BookmarkChainBalanceLink>();
}

/// <summary>Balances explicitly bookmarked recipes without consulting the catalog for producers.</summary>
internal static class BookmarkChainBalance
{
    private const double Epsilon = 1e-7;
    private const int MaximumBatches = 1_000_000;
    private const int MaximumExpansionDepth = 128;
    private static readonly StringComparer Ids = StringComparer.OrdinalIgnoreCase;

    /// <summary>Balances a group. Carried stock can replace upstream production for preferred ingredient choices.</summary>
    /// <param name="catalog">Catalog used to normalize item IDs and resolve substitutions.</param>
    /// <param name="entries">The entries belonging to this group only, in bookmark order.</param>
    /// <param name="available">Optional snapshot of carried quantities.</param>
    /// <param name="favoriteMaterials">Optional preferred material IDs.</param>
    /// <returns>Net bill and per-entry recipe details.</returns>
    public static BookmarkChainBalanceResult Calculate(Catalog catalog, IEnumerable<BookmarkChainBalanceEntry> entries,
        IReadOnlyDictionary<string, int>? available = null, IReadOnlyCollection<string>? favoriteMaterials = null) =>
        new Calculator(catalog, entries, ToDouble(available), favoriteMaterials: favoriteMaterials).Run(null, 0);

    /// <summary>
    /// Creates a pure crafting plan from one bookmark group. Unlike <see cref="Calculate"/>,
    /// ordinary item bookmarks are never treated as physical stock. Carried and nearby stock
    /// can satisfy inputs, but only carried stock counts towards a top-up target.
    /// </summary>
    /// <param name="catalog">Catalog used to normalize item IDs and resolve substitutions.</param>
    /// <param name="entries">The actual entries in one bookmark group, including plain item bookmarks.</param>
    /// <param name="targetIndex">Null for the whole group; otherwise the zero-based recipe entry index.</param>
    /// <param name="topUp">When true, subtract carried stock of each selected primary product before rounding batches up.</param>
    /// <param name="carried">Snapshot of carried item/liquid quantities.</param>
    /// <param name="favoriteMaterials">Optional preferred material IDs.</param>
    /// <param name="nearby">Eligible nearby stock, excluding items already in the carried snapshot.</param>
    /// <returns>A producer-first plan. A cycle is diagnosed and never expanded recursively.</returns>
    public static BookmarkChainBalanceResult PlanCraft(Catalog catalog,
        IReadOnlyList<BookmarkChainBalanceEntry> entries, int? targetIndex, bool topUp,
        IReadOnlyDictionary<string, int> carried, IReadOnlyCollection<string>? favoriteMaterials = null,
        IReadOnlyDictionary<string, int>? nearby = null)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        if (carried == null) throw new ArgumentNullException(nameof(carried));

        // Start with saved demand, not the display balance's expanded batch counts. The
        // latter already include upstream work and would count that work again as a target.
        var requested = new Dictionary<int, int>();
        if (targetIndex is int selected)
        {
            if (selected >= 0 && selected < entries.Count && entries[selected] is { Recipe: not null } entry)
                requested[selected] = entry.Quantity;
        }
        else
        {
            // Classify the saved group without physical stock, as the display balance does
            // without stock. The craft calculator below allocates actual materials separately.
            new Calculator(catalog, entries, favoriteMaterials: favoriteMaterials).GetGroupRequests(requested);
        }

        var physical = new Dictionary<string, double>(Ids);
        foreach (var pair in carried)
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                Add(physical, Normalize(catalog, pair.Key), pair.Value);

        if (topUp)
        {
            // Allocate final-product stock in bookmark order. Taking it here makes the same
            // carried stack unavailable to the next target, including when two targets share an ID.
            foreach (int index in requested.Keys.OrderBy(index => index).ToArray())
            {
                int batches = requested[index];
                if (batches <= 0 || entries[index].Recipe is not { } recipe) continue;
                string key = BalancePrimaryId(entries, index, recipe);
                RecipeOutput? primary = recipe.Outputs.FirstOrDefault(output => Ids.Equals(output.BrowseId, key)) ??
                    recipe.Outputs.FirstOrDefault(output => output.Amount > 0);
                double yield = primary?.Amount ?? 0;
                if (yield <= Epsilon) continue;
                double desired = yield * batches;
                double carriedProduct = Take(physical, key, desired);
                requested[index] = (int)Math.Min(MaximumBatches,
                    Math.Max(0, Math.Ceiling((desired - carriedProduct - Epsilon) / yield)));
            }
        }

        if (nearby != null)
            foreach (var pair in nearby)
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    Add(physical, Normalize(catalog, pair.Key), pair.Value);

        return new Calculator(catalog, entries, physical, craftMode: true, favoriteMaterials: favoriteMaterials).RunCraft(requested, targetIndex);
    }

    /// <summary>Balances only one saved recipe and any of its in-group dependencies.</summary>
    /// <param name="catalog">Catalog used to normalize item IDs and resolve substitutions.</param>
    /// <param name="entries">The entries belonging to this group only, in bookmark order.</param>
    /// <param name="target">The saved recipe chosen for production.</param>
    /// <param name="batches">The number of target recipe batches.</param>
    /// <param name="available">Optional carried stock, which can replace crafting prerequisites.</param>
    /// <returns>Net bill and recipe steps for the requested target only.</returns>
    public static BookmarkChainBalanceResult CalculateTarget(Catalog catalog, IEnumerable<BookmarkChainBalanceEntry> entries,
        Recipe target, int batches, IReadOnlyDictionary<string, int>? available = null) =>
        CalculateTargetCore(catalog, entries, target ?? throw new ArgumentNullException(nameof(target)), null, batches, available);

    /// <summary>Balances a particular recipe entry, even if the same recipe was saved with other variants.</summary>
    /// <param name="catalog">Catalog used to normalize item IDs and resolve substitutions.</param>
    /// <param name="entries">The entries belonging to this group only, in bookmark order.</param>
    /// <param name="entryIndex">Zero-based position of the target recipe bookmark.</param>
    /// <param name="batches">The number of target recipe batches.</param>
    /// <param name="available">Optional carried stock, which can replace crafting prerequisites.</param>
    /// <returns>Net bill and recipe steps for the requested target only.</returns>
    public static BookmarkChainBalanceResult CalculateTargetEntry(Catalog catalog, IEnumerable<BookmarkChainBalanceEntry> entries,
        int entryIndex, int batches, IReadOnlyDictionary<string, int>? available = null) =>
        CalculateTargetCore(catalog, entries, null, entryIndex, batches, available);

    private static BookmarkChainBalanceResult CalculateTargetCore(Catalog catalog, IEnumerable<BookmarkChainBalanceEntry> entries,
        Recipe? target, int? entryIndex, int batches, IReadOnlyDictionary<string, int>? available)
    {
        if (batches <= 0) throw new ArgumentOutOfRangeException(nameof(batches));
        var calculator = new Calculator(catalog, entries, ToDouble(available));
        int targetIndex = entryIndex ?? calculator.FindRecipe(target!);
        return calculator.Run(targetIndex, batches);
    }

    private static Dictionary<string, double> ToDouble(IReadOnlyDictionary<string, int>? values)
    {
        var result = new Dictionary<string, double>(Ids);
        if (values != null)
            foreach (var pair in values)
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    result[pair.Key] = pair.Value;
        return result;
    }

    private static string Normalize(Catalog catalog, string id) => Catalog.ItemKey(
        catalog.TryGetItem(id.Trim(), out var item) ? item.BrowseId : id.Trim(), false);

    private static string BalancePrimaryId(IReadOnlyList<BookmarkChainBalanceEntry> entries, int index, Recipe recipe)
    {
        if (index >= 0 && index < entries.Count && entries[index] != null && !string.IsNullOrWhiteSpace(entries[index].Id))
        {
            string id = entries[index].Id.Trim();
            RecipeOutput? output = recipe.Outputs.FirstOrDefault(candidate => Ids.Equals(candidate.BrowseId, id) || Ids.Equals(candidate.ItemId, id));
            if (output != null) return output.BrowseId;
        }
        return recipe.Outputs.FirstOrDefault(output => output.Amount > 0)?.BrowseId ?? string.Empty;
    }

    private static double At(Dictionary<string, double> amounts, string key) => amounts.TryGetValue(key, out double amount) ? amount : 0;
    private static void Add(Dictionary<string, double> amounts, string key, double amount) => amounts[key] = At(amounts, key) + amount;
    private static double Take(Dictionary<string, double> amounts, string key, double requested)
    {
        if (!amounts.TryGetValue(key, out double available)) return 0;
        double taken = Math.Min(available, Math.Max(0, requested));
        amounts[key] = available - taken;
        return taken;
    }

    private sealed class Calculator
    {
        private readonly Catalog _catalog;
        private readonly List<Node> _nodes = new();
        private readonly List<BookmarkChainDiagnostic> _diagnostics = new();
        private readonly Dictionary<string, List<Output>> _producers = new(Ids);
        private readonly Dictionary<string, double> _bookmarked = new(Ids);
        private readonly Dictionary<string, double> _available = new(Ids);
        private readonly HashSet<string> _useCarriedFirst = new(Ids);
        private readonly Dictionary<string, double> _production = new(Ids);
        private readonly Dictionary<string, double> _reserved = new(Ids);
        private readonly Dictionary<string, double> _consumed = new(Ids);
        private readonly Dictionary<string, double> _retained = new(Ids);
        private readonly HashSet<Node> _active = new();
        private readonly HashSet<int> _resultEntryIndices = new();
        private readonly List<BookmarkChainBalanceLink> _links = new();
        private readonly HashSet<Node> _roots = new();
        private readonly bool _craftMode;
        private readonly HashSet<string>? _favoriteMaterials;
        private bool _targetOnly;

        internal Calculator(Catalog catalog, IEnumerable<BookmarkChainBalanceEntry> entries,
            IReadOnlyDictionary<string, double>? available = null, bool craftMode = false,
            IReadOnlyCollection<string>? favoriteMaterials = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _craftMode = craftMode;
            _favoriteMaterials = favoriteMaterials == null ? null : new HashSet<string>(favoriteMaterials, Ids);
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (available != null)
                foreach (var pair in available)
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                        Add(_available, Normalize(pair.Key), pair.Value);

            int index = -1;
            foreach (var entry in entries)
            {
                index++;
                if (entry == null) continue;
                if (entry.Recipe == null)
                {
                    if (entry.ShowRecipe || entry.Id.StartsWith("recipe/", StringComparison.OrdinalIgnoreCase))
                        _diagnostics.Add(new(BookmarkChainDiagnosticCode.MissingRecipe,
                            "chain.diag.saved_recipe_unresolved", entry.Id));
                    else if (!_craftMode)
                        Add(_bookmarked, Normalize(entry.Id), entry.Quantity);
                    continue;
                }

                var node = new Node(entry, index, entry.Recipe);
                foreach (var output in node.Recipe.Outputs)
                    if (output.Amount > 0)
                        node.Outputs.Add(new Output(node, output, output.BrowseId));
                if (node.Outputs.Count == 0)
                {
                    _diagnostics.Add(new(BookmarkChainDiagnosticCode.InvalidRecipe,
                        "chain.diag.saved_recipe_no_output", entry.Id, node.Recipe.Id));
                    continue;
                }
                node.Primary = node.Outputs.FirstOrDefault(output => Ids.Equals(output.Key, entry.Id) ||
                    Ids.Equals(output.RecipeOutput.ItemId, entry.Id)) ?? node.Outputs[0];
                _nodes.Add(node);
                foreach (var output in node.Outputs)
                {
                    if (!_producers.TryGetValue(output.Key, out var list)) _producers.Add(output.Key, list = new());
                    list.Add(output);
                }
            }
        }

        internal int FindRecipe(Recipe target) => (_nodes.FirstOrDefault(node => ReferenceEquals(node.Recipe, target)) ??
            _nodes.FirstOrDefault(node => Ids.Equals(node.Recipe.Id, target.Id) && Ids.Equals(node.Recipe.Source, target.Source)))?.Index ?? -1;

        internal BookmarkChainBalanceResult RunCraft(IReadOnlyDictionary<int, int> requested, int? targetIndex)
        {
            _targetOnly = targetIndex.HasValue;
            ResolveIngredients();
            Node? target = _targetOnly ? _nodes.FirstOrDefault(node => node.Index == targetIndex.GetValueOrDefault()) : null;
            if (_targetOnly && target == null)
            {
                _diagnostics.Add(new(BookmarkChainDiagnosticCode.MissingRecipe,
                    "chain.diag.target_not_bookmarked"));
                return Settle();
            }

            if (_targetOnly)
            {
                _resultEntryIndices.Add(target!.Index);
                int batches = requested.TryGetValue(target.Index, out int wanted) ? wanted : 0;
                if (batches > 0) Seed(target, batches, true, reservePrimary: true);
            }
            else
            {
                foreach (var pair in requested.OrderBy(pair => pair.Key))
                {
                    Node? node = _nodes.FirstOrDefault(candidate => candidate.Index == pair.Key);
                    if (node != null && pair.Value > 0) Seed(node, pair.Value, true, reservePrimary: true);
                }
            }

            // Producers are grown only from selected roots. A zero-quantity bookmarked recipe can
            // still be selected as a producer, but it is never an implicit root.
            var roots = _nodes.Where(node => node.Batches > 0).Select(node => (Node: node, Batches: node.Batches)).ToArray();
            foreach (var root in roots) PrepareInputs(root.Node, root.Batches);
            return Settle();
        }

        internal BookmarkChainBalanceResult Run(int? targetIndex, int targetBatches)
        {
            _targetOnly = targetIndex.HasValue;
            Node? target = _targetOnly ? _nodes.FirstOrDefault(node => node.Index == targetIndex) : null;
            if (_targetOnly && target == null)
            {
                _diagnostics.Add(new(BookmarkChainDiagnosticCode.MissingRecipe,
                    "chain.diag.target_not_bookmarked"));
                return Settle();
            }

            ResolveIngredients();
            if (target != null)
            {
                Seed(target, targetBatches, true, reservePrimary: true);
            }
            else
            {
                foreach (var root in GroupRoots())
                    Seed(root.Node, root.Batches, true, root.ReservePrimary);
            }

            // Install root output capacity before following any inputs, so another root can use a
            // coproduct or recycled material even if its producer is later in the bookmark order.
            var roots = _nodes.Where(node => node.Batches > 0).Select(node => (Node: node, Batches: node.Batches)).ToArray();
            foreach (var root in roots) PrepareInputs(root.Node, root.Batches);
            return Settle();
        }

        internal void GetGroupRequests(Dictionary<int, int> requested)
        {
            ResolveIngredients();
            foreach (var root in GroupRoots())
                if (root.Batches > 0) requested.Add(root.Node.Index, root.Batches);
        }

        private IEnumerable<(Node Node, int Batches, bool ReservePrimary)> GroupRoots()
        {
            var dependencies = new HashSet<Node>(_nodes.SelectMany(node => PreferredDependencies(node)));
            var reached = new HashSet<Node>();
            foreach (var node in _nodes)
            {
                // Only the portion above the default upstream multiplier is an extra request.
                bool isDependency = dependencies.Contains(node);
                int batches = isDependency ? node.Entry.Quantity - 1 : node.Entry.Quantity;
                if (isDependency && batches <= 0) continue;
                yield return (node, batches, true);
                if (batches > 0) MarkReached(node, reached);
            }

            while (reached.Count < _nodes.Count)
            {
                // A closed component has no ordinary root; keep the display's existing anchor
                // policy, preferring an exported product over reserving circulating material.
                var seed = _nodes.Where(node => !reached.Contains(node))
                    .OrderByDescending(node => node.Outputs.Any(output => !output.IsLinked)).First();
                yield return (seed, seed.Entry.Quantity, !seed.Primary.IsLinked);
                MarkReached(seed, reached);
            }
        }

        private void ResolveIngredients()
        {
            var candidates = new Dictionary<RecipeIngredient, CatalogItem[]>();
            foreach (var node in _nodes)
            {
                var remaining = new Dictionary<string, double>(_available, Ids);
                var bookmarked = new Dictionary<string, double>(_bookmarked, Ids);
                var inputs = node.Recipe.Inputs;
                string? Pin(int index) => node.Entry.PreferredIngredients?.TryGetValue(index, out var pin) == true ? pin : null;
                CatalogItem[] Matches(RecipeIngredient ingredient)
                {
                    if (!candidates.TryGetValue(ingredient, out var matches))
                        candidates.Add(ingredient, matches = _catalog.GetMatchingItems(ingredient).ToArray());
                    return matches;
                }
                // Allocate the least flexible slots first, then restore the recipe's slot
                // order. Independent alternatives must not all select the same last item.
                foreach (int index in Enumerable.Range(0, inputs.Count)
                    .OrderBy(index => !inputs[index].MatchesAny || Pin(index) != null ? 0 : Matches(inputs[index]).Length + 1))
                {
                    RecipeIngredient ingredient = node.Recipe.Inputs[index];
                    double amount = ingredient.Amount;
                    if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
                    {
                        _diagnostics.Add(new(BookmarkChainDiagnosticCode.InvalidRecipe,
                            "chain.diag.invalid_ingredient_amount", ingredient.BrowseId, node.Recipe.Id));
                        continue;
                    }
                    if (!ingredient.IsLiquid && ingredient.QualityId.Length > 0 && ingredient.MatchesAny)
                        amount = 1; // Item quality is a per-item threshold, not a count of items.
                    if (ingredient.IsCatalyst && amount <= Epsilon) amount = 1;
                    if (amount <= Epsilon) continue;
                    string itemId = ingredient.BrowseId;
                    bool requirement = ingredient.MatchesAny;
                    if (ingredient.MatchesAny)
                    {
                        CatalogItem? choice = null;
                        if (node.Entry.PreferredIngredients != null && node.Entry.PreferredIngredients.TryGetValue(index, out var selected) &&
                            !string.IsNullOrWhiteSpace(selected) && _catalog.TryGetItem(selected, out var match) && ingredient.Matches(match))
                            choice = match;
                        else
                        {
                            choice = Matches(ingredient).Select(item => (Item: item,
                                    Stock: At(bookmarked, item.BrowseId) + At(remaining, item.BrowseId),
                                    Carried: At(remaining, item.BrowseId),
                                    Yield: BestYield(item.BrowseId, node),
                                    Favorite: IsPreferredMaterial(item.BrowseId, node),
                                    Cost: UnitCost(ingredient, item, amount)))
                                .Where(candidate => candidate.Yield > 0 || candidate.Stock > 0)
                                // A favorite producer is the preferred sample for a generic input
                                // when its carried amount covers the slot. A complete carried
                                // alternative still beats a partial favorite; if every candidate is
                                // short, prefer carried material before producer yield as a fallback.
                                .OrderByDescending(candidate => candidate.Carried + Epsilon >= candidate.Cost && candidate.Favorite)
                                .ThenByDescending(candidate => candidate.Stock + Epsilon >= candidate.Cost)
                                .ThenByDescending(candidate => candidate.Carried > Epsilon && candidate.Favorite)
                                .ThenByDescending(candidate => candidate.Carried > Epsilon)
                                .ThenByDescending(candidate => candidate.Yield / candidate.Cost)
                                .ThenByDescending(candidate => candidate.Stock / candidate.Cost)
                                .ThenBy(candidate => candidate.Item.BrowseId, Ids).Select(candidate => candidate.Item).FirstOrDefault();
                        }
                        if (choice != null)
                        {
                            itemId = choice.BrowseId;
                            amount = UnitCost(ingredient, choice, amount);
                            requirement = false;
                            if (At(_available, itemId) > Epsilon && IsPreferredMaterial(itemId, node))
                                _useCarriedFirst.Add(itemId);
                        }
                    }

                    string key = requirement ? (ingredient.IsLiquid ? "requirement/liquid/" : "requirement/item/") + itemId : itemId;
                    var input = new Input(node, ingredient, index, itemId, key, amount, requirement);
                    if (_producers.TryGetValue(key, out var outputs))
                    {
                        var matches = outputs.Where(output => output.Owner != node && Allows(input, output)).ToArray();
                        input.Producers = matches.GroupBy(output => output.Owner).OrderByDescending(group => group.Sum(output => output.Yield))
                            .ThenBy(group => group.Key.Index).Select(group => group.Key).ToArray();
                        if (input.Producers.Length > 0)
                            foreach (var output in matches.Where(output => output.Owner == input.Producers[0])) output.IsLinked = true;
                    }
                    node.Inputs.Add(input);
                    if (!ingredient.IsCatalyst && !requirement)
                        Take(remaining, key, amount - Take(bookmarked, key, amount));
                }
                node.Inputs.Sort((left, right) => left.Index.CompareTo(right.Index));
            }
        }

        private double BestYield(string key, Node consumer) => _producers.TryGetValue(key, out var producers)
            ? producers.Where(output => output.Owner != consumer).Select(output => output.Yield).DefaultIfEmpty(0).Max() : 0;

        private bool IsPreferredMaterial(string key, Node consumer) => _favoriteMaterials?.Contains(key) == true ||
            (_producers.TryGetValue(key, out var producers) && producers.Any(output => output.Owner != consumer && output.Owner.Entry.PreferredRecipe));

        private static double UnitCost(RecipeIngredient ingredient, CatalogItem item, double amount)
        {
            if (!ingredient.IsLiquid || ingredient.QualityId.Length == 0) return amount;
            double strength = item.Qualities.Where(quality => Ids.Equals(quality.id, ingredient.QualityId)).Max(quality => quality.amount);
            return amount / strength;
        }

        private static IEnumerable<Node> PreferredDependencies(Node node) => node.Inputs.Where(input => input.Producers.Length > 0)
            .Select(input => input.Producers[0]).Distinct();

        private static void MarkReached(Node root, HashSet<Node> reached)
        {
            var pending = new Stack<Node>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!reached.Add(current)) continue;
                foreach (var dependency in PreferredDependencies(current)) pending.Push(dependency);
            }
        }

        private void Seed(Node node, int batches, bool isResult, bool reservePrimary)
        {
            if (isResult)
            {
                node.IsResult = true;
                _resultEntryIndices.Add(node.Index);
            }
            int added = AddBatches(node, batches);
            if (!reservePrimary) return;
            var primaryOutputs = node.Outputs.Where(output => Ids.Equals(output.Key, node.Primary.Key)).ToArray();
            double recycled = node.Inputs.Where(input => !input.Ingredient.IsCatalyst && Ids.Equals(input.Key, node.Primary.Key) &&
                primaryOutputs.Any(output => Allows(input, output))).Sum(input => input.PerBatch) * added;
            foreach (var output in primaryOutputs)
            {
                double gross = output.Yield * added;
                double recycledHere = Math.Min(gross, recycled);
                double reserved = gross - recycledHere;
                output.Reserved += reserved;
                if (reserved > Epsilon) Add(_reserved, output.Key, reserved);
                recycled -= recycledHere;
            }
        }

        private int AddBatches(Node node, double desired)
        {
            if (desired > MaximumBatches - node.Batches && !node.LimitReported)
            {
                node.LimitReported = true;
                _diagnostics.Add(new(BookmarkChainDiagnosticCode.InvalidRecipe,
                    "chain.diag.batch_count_limited", node.Primary.Key, node.Recipe.Id));
            }
            int added = (int)Math.Min(MaximumBatches - node.Batches, Math.Max(0, desired));
            node.Batches += added;
            foreach (var output in node.Outputs) Add(_production, output.Key, output.Yield * added);
            return added;
        }

        private void PrepareInputs(Node node, int batches)
        {
            if (batches <= 0) return;
            if (!_active.Add(node))
            {
                ReportCycle(node.Primary.Key, node.Recipe.Id, depthLimit: false);
                return;
            }
            if (_active.Count > MaximumExpansionDepth)
            {
                ReportCycle(node.Primary.Key, node.Recipe.Id, depthLimit: true);
                _active.Remove(node);
                return;
            }
            foreach (var input in node.Inputs.Where(input => !input.Ingredient.IsCatalyst))
            {
                Add(_consumed, input.Key, input.PerBatch * batches);
                GrowProducers(input);
            }
            foreach (var group in node.Inputs.Where(input => input.Ingredient.IsCatalyst).GroupBy(input => input.Key, Ids))
            {
                _retained[group.Key] = Math.Max(At(_retained, group.Key), group.Sum(input => input.PerBatch));
                GrowProducers(group.First());
            }
            _active.Remove(node);
        }

        private void GrowProducers(Input input)
        {
            if (_active.Count >= MaximumExpansionDepth)
            {
                ReportCycle(input.Key, input.Owner.Recipe.Id, depthLimit: true);
                return;
            }
            foreach (var producer in input.Producers)
            {
                double initial = At(_bookmarked, input.Key) +
                    ((_targetOnly || _craftMode || _useCarriedFirst.Contains(input.Key)) ? At(_available, input.Key) : 0);
                double shortage = At(_consumed, input.Key) + At(_retained, input.Key) - initial -
                    At(_production, input.Key) + At(_reserved, input.Key);
                if (_active.Contains(producer))
                {
                    // This producer is waiting for the current ingredient itself: do not use
                    // its speculative output to satisfy a shortage that no real stock can cover.
                    double speculative = producer.Outputs.Where(output => Ids.Equals(output.Key, input.Key))
                        .Sum(output => Math.Max(0, output.Yield * producer.Batches - output.Reserved));
                    if (shortage + speculative > Epsilon)
                        ReportCycle(input.Key, producer.Recipe.Id, depthLimit: false);
                    continue;
                }
                if (shortage <= Epsilon) break;
                double yield = producer.Outputs.Where(output => Ids.Equals(output.Key, input.Key)).Sum(output => output.Yield);
                if (yield <= Epsilon)
                {
                    _diagnostics.Add(new(BookmarkChainDiagnosticCode.InvalidRecipe,
                        "chain.diag.producer_no_yield", input.Key, producer.Recipe.Id));
                    continue;
                }
                int added = AddBatches(producer, Math.Ceiling((shortage - Epsilon) / yield));
                if (added > 0) PrepareInputs(producer, added);
            }
        }

        private void ReportCycle(string itemId, string recipeId, bool depthLimit)
        {
            if (_diagnostics.Any(issue => issue.Code == BookmarkChainDiagnosticCode.Cycle &&
                Ids.Equals(issue.ItemId, itemId) && Ids.Equals(issue.RecipeId, recipeId))) return;
            _diagnostics.Add(new(BookmarkChainDiagnosticCode.Cycle, depthLimit
                ? "chain.diag.expansion_depth_limit"
                : "chain.diag.recipe_cycle", itemId, recipeId));
        }

        private BookmarkChainBalanceResult Settle()
        {
            var active = _nodes.Where(node => node.Batches > 0).ToArray();
            var initial = new Dictionary<string, double>(_bookmarked, Ids);
            // Group calculations prioritize bookmark materials, then reusable production, then carried supply.
            // In a target craft plan, available stock is already accounted for during prerequisite sizing.
            if (_targetOnly && !_craftMode)
                foreach (var pair in _available) Add(initial, pair.Key, pair.Value);
            var carried = _targetOnly && !_craftMode ? new Dictionary<string, double>(Ids) :
                new Dictionary<string, double>(_available, Ids);
            var required = new Dictionary<(string Key, bool Catalyst), Total>();

            foreach (var node in active)
                foreach (var output in node.Outputs)
                    output.Remaining = output.Yield * node.Batches;

            foreach (var input in active.SelectMany(node => node.Inputs).Where(input => !input.Ingredient.IsCatalyst)
                .OrderBy(input => input.IsRequirement ? 2 : input.Ingredient.MatchesAny ? 1 : 0))
            {
                double gross = input.PerBatch * input.Owner.Batches;
                double bookmarked = Take(initial, input.Key, gross);
                double onHand = (_craftMode || _useCarriedFirst.Contains(input.Key)) ? Take(carried, input.Key, gross - bookmarked) : 0;
                double internalSupply = TakeProduction(input, gross - bookmarked - onHand, consume: true);
                double external = gross - internalSupply;
                onHand += Take(carried, input.Key, external - bookmarked - onHand);
                double available = bookmarked + onHand;
                if (bookmarked > Epsilon) _links.Add(new(null, input.Owner.Index, input.ItemId, bookmarked, false));
                if (onHand > Epsilon) _links.Add(new(null, input.Owner.Index, input.ItemId, onHand, true));
                input.Amount = input.CreateAmount(gross, external, available + internalSupply);
                Accumulate(input, external, available);
            }
            // The same tools can be reused across steps, but several slots of one recipe may need
            // distinct copies. Use the largest remaining requirement, rather than summing all batches.
            foreach (var group in active.SelectMany(node => node.Inputs).Where(input => input.Ingredient.IsCatalyst).GroupBy(input => (input.Owner, input.Key)))
            {
                double fromInitial = At(initial, group.Key.Key) + At(carried, group.Key.Key);
                double prior = 0;
                foreach (var input in group)
                {
                    double gross = input.PerBatch;
                    double stockFirst = (_craftMode || _useCarriedFirst.Contains(input.Key)) ? Math.Min(fromInitial, gross) : 0;
                    double internalSupply = TakeProduction(input, gross - stockFirst, consume: false, skip: prior);
                    prior += internalSupply;
                    double external = gross - internalSupply;
                    double available = Math.Min(fromInitial, external);
                    fromInitial -= available;
                    input.Amount = input.CreateAmount(gross, external, available + internalSupply);
                }
                var first = group.First();
                var key = (first.Key, true);
                if (!required.TryGetValue(key, out var total)) required[key] = total = new Total(first);
                double externalTools = group.Sum(input => input.Amount!.NetQuantity);
                double missingTools = group.Sum(input => input.Amount!.Quantity - input.Amount.AvailableQuantity);
                total.Quantity = Math.Max(total.Quantity, externalTools);
                total.Missing = Math.Max(total.Missing, missingTools);
            }

            var results = OutputAmounts(active.Where(node => node.IsResult));
            var surplus = OutputAmounts(active.Where(node => !node.IsResult));
            var inputs = required.Values.Where(total => total.Quantity > Epsilon).OrderBy(total => total.Input.ItemId, Ids).ToArray();
            foreach (var total in inputs.Where(total => total.Input.IsRequirement))
                _diagnostics.Add(new(BookmarkChainDiagnosticCode.RequirementUnresolved,
                    "chain.diag.no_concrete_item", total.Input.ItemId, total.Input.Owner.Recipe.Id));
            var steps = OrderSteps(active).Select(node => new BookmarkChainBalanceStep(node.Index, node.Recipe, node.Batches,
                Array.AsReadOnly(node.Inputs.Select(input => input.Amount!).ToArray()), Array.AsReadOnly(node.Outputs.Select(output =>
                    new BookmarkChainBalanceAmount(output.Key, output.Yield * node.Batches, output.Remaining, 0, output.RecipeOutput.IsLiquid)).ToArray()), node.IsResult)).ToArray();
            foreach (var node in active)
                foreach (var output in node.Outputs.Where(o => o.Reserved > Epsilon))
                    _links.Add(new(node.Index, null, output.Key, output.Reserved, false));
            return new(results,
                Array.AsReadOnly(inputs.Select(total => total.Input.CreateAmount(total.Quantity, total.Quantity, total.Quantity - total.Missing)).ToArray()), surplus,
                Array.AsReadOnly(inputs.Where(total => total.Missing > Epsilon).Select(total => total.Input.CreateAmount(total.Missing, total.Missing, 0)).ToArray()),
                Array.AsReadOnly(steps), _diagnostics.AsReadOnly(), _resultEntryIndices.OrderBy(i => i).ToArray(), _links.AsReadOnly());

            void Accumulate(Input input, double quantity, double available)
            {
                if (quantity <= Epsilon) return;
                var key = (input.Key, false);
                if (!required.TryGetValue(key, out var total)) required[key] = total = new Total(input);
                total.Quantity += quantity;
                total.Missing += quantity - available;
            }
        }

        private static bool Allows(Input input, Output output) => output.RecipeOutput.IsLiquid == input.Ingredient.IsLiquid &&
            (input.Ingredient.IsLiquid || (output.RecipeOutput.Condition + Epsilon >= input.Ingredient.MinimumCondition &&
                (!input.Ingredient.NoMaxCondition || output.RecipeOutput.Condition < .991f)));

        private double TakeProduction(Input input, double requested, bool consume, double skip = 0)
        {
            if (requested <= Epsilon || !_producers.TryGetValue(input.Key, out var outputs)) return 0;
            double remaining = requested;
            foreach (var output in outputs)
            {
                if (!Allows(input, output)) continue;
                double free = Math.Max(0, output.Remaining - output.Reserved);
                if (skip > 0)
                {
                    double skipped = Math.Min(skip, free);
                    skip -= skipped;
                    free -= skipped;
                }
                double amount = Math.Min(free, remaining);
                if (amount <= Epsilon) continue;
                if (consume) output.Remaining -= amount;
                _links.Add(new(output.Owner.Index, input.Owner.Index, input.ItemId, amount, false));
                if (output.Owner != input.Owner) input.Owner.Dependencies.Add(output.Owner);
                remaining -= amount;
                if (remaining <= Epsilon) break;
            }
            return requested - remaining;
        }

        private static IEnumerable<Node> OrderSteps(Node[] active)
        {
            var indegree = active.ToDictionary(node => node, node => node.Dependencies.Count);
            var followers = active.ToDictionary(node => node, _ => new List<Node>());
            foreach (var node in active)
                foreach (var producer in node.Dependencies) followers[producer].Add(node);
            var byIndex = new SortedSet<int>(active.Select(node => node.Index));
            var ready = new SortedSet<int>(active.Where(node => indegree[node] == 0).Select(node => node.Index));
            var nodes = active.ToDictionary(node => node.Index);
            while (byIndex.Count > 0)
            {
                int next = ready.Count > 0 ? ready.Min : byIndex.Min;
                var node = nodes[next];
                ready.Remove(next);
                byIndex.Remove(next);
                yield return node;
                foreach (var follower in followers[node])
                    if (--indegree[follower] == 0 && byIndex.Contains(follower.Index)) ready.Add(follower.Index);
            }
        }

        private static IReadOnlyList<BookmarkChainBalanceAmount> OutputAmounts(IEnumerable<Node> nodes) => Array.AsReadOnly(nodes
            .SelectMany(node => node.Outputs).Where(output => output.Remaining > Epsilon).GroupBy(output => output.Key, Ids)
            .OrderBy(group => group.Key, Ids).Select(group =>
            {
                double amount = group.Sum(output => output.Remaining);
                return new BookmarkChainBalanceAmount(group.Key, amount, amount, 0, group.First().RecipeOutput.IsLiquid);
            }).ToArray());

        private string Normalize(string id) => Catalog.ItemKey(_catalog.TryGetItem(id.Trim(), out var item) ? item.BrowseId : id.Trim(), false);
        private static double At(Dictionary<string, double> amounts, string key) => amounts.TryGetValue(key, out double amount) ? amount : 0;
        private static void Add(Dictionary<string, double> amounts, string key, double amount) => amounts[key] = At(amounts, key) + amount;
        private static double Take(Dictionary<string, double> amounts, string key, double requested)
        {
            if (!amounts.TryGetValue(key, out double available)) return 0;
            double taken = Math.Min(available, Math.Max(0, requested));
            amounts[key] = available - taken;
            return taken;
        }
    }

    private sealed class Node(BookmarkChainBalanceEntry entry, int index, Recipe recipe)
    {
        internal BookmarkChainBalanceEntry Entry { get; } = entry;
        internal int Index { get; } = index;
        internal Recipe Recipe { get; } = recipe;
        internal List<Input> Inputs { get; } = new();
        internal List<Output> Outputs { get; } = new();
        internal HashSet<Node> Dependencies { get; } = new();
        internal Output Primary { get; set; } = null!;
        internal int Batches { get; set; }
        internal bool IsResult { get; set; }
        internal bool LimitReported { get; set; }
    }

    private sealed class Output(Node owner, RecipeOutput recipeOutput, string key)
    {
        internal Node Owner { get; } = owner;
        internal RecipeOutput RecipeOutput { get; } = recipeOutput;
        internal string Key { get; } = key;
        internal double Yield => RecipeOutput.Amount;
        internal double Remaining { get; set; }
        internal double Reserved { get; set; }
        internal bool IsLinked { get; set; }
    }

    private sealed class Input(Node owner, RecipeIngredient ingredient, int index, string itemId, string key, double perBatch, bool requirement)
    {
        internal Node Owner { get; } = owner;
        internal RecipeIngredient Ingredient { get; } = ingredient;
        internal int Index { get; } = index;
        internal string ItemId { get; } = itemId;
        internal string Key { get; } = key;
        internal double PerBatch { get; } = perBatch;
        internal bool IsRequirement { get; } = requirement;
        internal Node[] Producers { get; set; } = Array.Empty<Node>();
        internal BookmarkChainBalanceAmount? Amount { get; set; }

        internal BookmarkChainBalanceAmount CreateAmount(double quantity, double net, double available) =>
            new(ItemId, quantity, Math.Max(0, net), Math.Max(0, available), Ingredient.IsLiquid, IsRequirement, Ingredient, Index);
    }

    private sealed class Total(Input input)
    {
        internal Input Input { get; } = input;
        internal double Quantity { get; set; }
        internal double Missing { get; set; }
    }
}
