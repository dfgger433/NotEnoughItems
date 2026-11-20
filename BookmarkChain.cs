using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NotEnoughItems
{
    /// <summary>One requested product in a bookmark-group planning request.</summary>
    internal sealed class BookmarkChainTarget
    {
        public BookmarkChainTarget(string itemId, int quantity = 1, Recipe? recipe = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.", nameof(itemId));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
            ItemId = itemId.Trim();
            Quantity = quantity;
            Recipe = recipe;
        }

        public string ItemId { get; }
        public int Quantity { get; }
        /// <summary>Optional recipe explicitly chosen for this target.</summary>
        public Recipe? Recipe { get; }
    }

    /// <summary>Inputs to a pure bookmark-chain planning operation.</summary>
    internal sealed class BookmarkChainRequest
    {
        public BookmarkChainRequest(
            Catalog catalog,
            IReadOnlyList<BookmarkChainTarget> targets,
            IEnumerable<Recipe>? bookmarkedRecipes = null,
            IEnumerable<string>? bookmarkedRecipeKeys = null,
            IReadOnlyDictionary<string, int>? available = null,
            bool strictGroupRecipes = true,
            IReadOnlyDictionary<string, string>? preferredIngredients = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            Targets = Snapshot(targets);
            BookmarkedRecipes = Snapshot(bookmarkedRecipes);
            BookmarkedRecipeKeys = Snapshot(bookmarkedRecipeKeys);
            Available = Snapshot(available);
            StrictGroupRecipes = strictGroupRecipes;
            PreferredIngredients = preferredIngredients == null ? new Dictionary<string, string>() :
                new Dictionary<string, string>(preferredIngredients, StringComparer.OrdinalIgnoreCase);
        }

        public Catalog Catalog { get; }
        public IReadOnlyList<BookmarkChainTarget> Targets { get; }
        public IReadOnlyList<Recipe> BookmarkedRecipes { get; }
        public IReadOnlyList<string> BookmarkedRecipeKeys { get; }
        /// <summary>Read-only item quantities. This planner never reads or mutates game inventory.</summary>
        public IReadOnlyDictionary<string, int> Available { get; }
        /// <summary>When true, only recipes in the bookmark group may be selected.</summary>
        public bool StrictGroupRecipes { get; }
        internal IReadOnlyDictionary<string, string> PreferredIngredients { get; }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values)
        {
            return new ReadOnlyCollection<T>((values ?? Array.Empty<T>()).Where(x => x != null).ToList());
        }

        private static IReadOnlyDictionary<string, int> Snapshot(IReadOnlyDictionary<string, int>? values)
        {
            var copy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (values != null)
                foreach (var pair in values)
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                        copy[pair.Key.Trim()] = pair.Value;
            return new ReadOnlyDictionary<string, int>(copy);
        }
    }

    internal enum BookmarkChainDiagnosticCode
    {
        MissingRecipe,
        MissingItem,
        MissingMaterial,
        RequirementUnresolved,
        Cycle,
        InvalidRecipe,
        InvalidTargetRecipe,
    }

    internal sealed class BookmarkChainDiagnostic
    {
        internal BookmarkChainDiagnostic(BookmarkChainDiagnosticCode code, string message, string itemId = "", string recipeId = "")
        {
            Code = code;
            Message = message;
            ItemId = itemId;
            RecipeId = recipeId;
        }

        public BookmarkChainDiagnosticCode Code { get; }
        public string Message { get; }
        public string ItemId { get; }
        public string RecipeId { get; }
    }

    /// <summary>An amount summary used for missing materials and remaining products.</summary>
    internal sealed class BookmarkChainAmount
    {
        internal BookmarkChainAmount(string itemId, float requiredQuantity, float availableQuantity, float shortage, float surplus, bool isLiquid, bool isRequirement)
        {
            ItemId = itemId;
            RequiredQuantity = requiredQuantity;
            AvailableQuantity = availableQuantity;
            Shortage = shortage;
            Surplus = surplus;
            IsLiquid = isLiquid;
            IsRequirement = isRequirement;
        }

        public string ItemId { get; }
        public float RequiredQuantity { get; }
        public float AvailableQuantity { get; }
        public float Shortage { get; }
        public float Surplus { get; }
        public bool IsLiquid { get; }
        public bool IsRequirement { get; }
    }

    internal sealed class BookmarkChainInput
    {
        internal BookmarkChainInput(RecipeIngredient ingredient, string itemId, float requiredQuantity, float availableQuantity,
            float shortage, float surplus, bool isRequirement, string requirementKey)
        {
            Ingredient = ingredient;
            ItemId = itemId;
            RequiredQuantity = requiredQuantity;
            AvailableQuantity = availableQuantity;
            Shortage = shortage;
            Surplus = surplus;
            IsCatalyst = ingredient.IsCatalyst;
            IsLiquid = ingredient.IsLiquid;
            IsRequirement = isRequirement;
            RequirementKey = requirementKey;
        }

        public RecipeIngredient Ingredient { get; }
        /// <summary>Type-qualified item ID, or the requirement key for a generic input.</summary>
        public string ItemId { get; }
        public float RequiredQuantity { get; }
        /// <summary>Amount actually satisfied by inventory or planned intermediate output.</summary>
        public float AvailableQuantity { get; }
        public float Shortage { get; }
        public float Surplus { get; }
        public bool IsCatalyst { get; }
        public bool IsLiquid { get; }
        /// <summary>True for quality/tag requirements which must be selected by the user.</summary>
        public bool IsRequirement { get; }
        public string RequirementKey { get; }
    }

    internal enum BookmarkChainNodeStatus
    {
        Planned,
        Missing,
        Cycle,
        Invalid,
    }

    internal sealed class BookmarkChainNode
    {
        internal BookmarkChainNode(Recipe recipe, RecipeOutput output, int requestedQuantity, int batches, int producedQuantity,
            IReadOnlyList<RecipeOutput> outputs, IReadOnlyList<BookmarkChainInput> inputs, BookmarkChainNodeStatus status)
        {
            Recipe = recipe;
            Output = output;
            RequestedQuantity = requestedQuantity;
            Batches = batches;
            ProducedQuantity = producedQuantity;
            Outputs = outputs;
            Inputs = inputs;
            Status = status;
        }

        public Recipe Recipe { get; }
        /// <summary>The output that caused this node to be planned.</summary>
        public RecipeOutput Output { get; }
        public int RequestedQuantity { get; }
        public int Batches { get; }
        public int ProducedQuantity { get; }
        /// <summary>All outputs produced by the planned batches, including reusable byproducts.</summary>
        public IReadOnlyList<RecipeOutput> Outputs { get; }
        public IReadOnlyList<BookmarkChainInput> Inputs { get; }
        public BookmarkChainNodeStatus Status { get; }
    }

    internal sealed class BookmarkChainPlan
    {
        internal BookmarkChainPlan(BookmarkChainRequest request, IReadOnlyList<BookmarkChainTarget> targets,
            IReadOnlyList<BookmarkChainNode> nodes, IReadOnlyList<BookmarkChainInput> inputs,
            IReadOnlyList<BookmarkChainAmount> missing, IReadOnlyList<BookmarkChainAmount> surplus,
            IReadOnlyList<BookmarkChainDiagnostic> diagnostics)
        {
            Request = request;
            Targets = targets;
            Nodes = nodes;
            Inputs = inputs;
            Missing = missing;
            Surplus = surplus;
            Diagnostics = diagnostics;
            IsComplete = missing.Count == 0 && diagnostics.All(x => x.Code != BookmarkChainDiagnosticCode.Cycle &&
                x.Code != BookmarkChainDiagnosticCode.RequirementUnresolved && x.Code != BookmarkChainDiagnosticCode.InvalidRecipe &&
                x.Code != BookmarkChainDiagnosticCode.InvalidTargetRecipe && x.Code != BookmarkChainDiagnosticCode.MissingRecipe &&
                x.Code != BookmarkChainDiagnosticCode.MissingItem);
        }

        public BookmarkChainRequest Request { get; }
        public IReadOnlyList<BookmarkChainTarget> Targets { get; }
        public IReadOnlyList<BookmarkChainNode> Nodes { get; }
        /// <summary>All planned input requirements, aggregated by stable item or requirement key.</summary>
        public IReadOnlyList<BookmarkChainInput> Inputs { get; }
        public IReadOnlyList<BookmarkChainAmount> Missing { get; }
        public IReadOnlyList<BookmarkChainAmount> Surplus { get; }
        public IReadOnlyList<BookmarkChainDiagnostic> Diagnostics { get; }
        public bool IsComplete { get; }
    }

    /// <summary>Pure deterministic planner for a group of NEI recipe bookmarks.</summary>
    internal static class BookmarkChain
    {
        private const float Epsilon = 0.0001f;

        /// <summary>Plans all targets as one shared material graph; no game state is changed.</summary>
        public static BookmarkChainPlan Plan(BookmarkChainRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var planner = new Planner(request);
            return planner.Run();
        }

        private sealed class Planner
        {
            private readonly BookmarkChainRequest _request;
            private readonly Catalog _catalog;
            private readonly Dictionary<string, float> _stock = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, float> _generated = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            private int _remainingNodes = 2048;
            private readonly Dictionary<string, bool> _liquid = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, RequirementTotals> _requirements = new Dictionary<string, RequirementTotals>(StringComparer.OrdinalIgnoreCase);
            private readonly List<BookmarkChainNode> _nodes = new List<BookmarkChainNode>();
            private readonly List<BookmarkChainDiagnostic> _diagnostics = new List<BookmarkChainDiagnostic>();
            private readonly HashSet<string> _active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _bookmarkRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _bookmarkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            internal Planner(BookmarkChainRequest request)
            {
                _request = request;
                _catalog = request.Catalog;
                for (int i = 0; i < request.BookmarkedRecipes.Count; i++)
                {
                    Recipe recipe = request.BookmarkedRecipes[i];
                    string key = RecipeKey(recipe);
                    _bookmarkKeys.Add(key);
                    if (!_bookmarkRank.ContainsKey(key)) _bookmarkRank[key] = i;
                }
                foreach (string key in request.BookmarkedRecipeKeys.Where(x => !string.IsNullOrWhiteSpace(x)))
                    _bookmarkKeys.Add(key.Trim());

                foreach (var pair in request.Available)
                {
                    string key = NormalizeItemId(pair.Key);
                    AddStock(key, pair.Value);
                }
            }

            internal BookmarkChainPlan Run()
            {
                foreach (BookmarkChainTarget target in _request.Targets)
                {
                    string key = NormalizeItemId(target.ItemId);
                    float fulfilled = EnsureProduct(key, target.ItemId, target.Quantity, target.Recipe);
                    if (fulfilled + Epsilon < target.Quantity)
                        AddMissing(key, target.Quantity - fulfilled, null, IsRequirementKey(key), false);
                }

                var missing = _requirements.Values
                    .Where(x => x.Shortage > Epsilon)
                    .Select(x => new BookmarkChainAmount(x.ItemId, x.Required, x.Available, x.Shortage, 0f, x.IsLiquid, x.IsRequirement))
                    .OrderBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase).ToList();
                var surplus = _generated.Where(x => x.Value > Epsilon)
                    .Select(x => new BookmarkChainAmount(x.Key, 0f, 0f, 0f, x.Value, _liquid.TryGetValue(x.Key, out bool liquid) && liquid, IsRequirementKey(x.Key)))
                    .OrderBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase).ToList();

                var aggregateInputs = _requirements.Values
                    .Where(x => x.Required > Epsilon)
                    .Select(x => new BookmarkChainInput(x.Ingredient ?? new RecipeIngredient(x.ItemId), x.ItemId, x.Required,
                        x.Available, x.Shortage, _stock.TryGetValue(x.ItemId, out float remaining) ? remaining : 0f,
                        x.IsRequirement, x.IsRequirement ? x.ItemId : string.Empty))
                    .OrderBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase).ToList();

                return new BookmarkChainPlan(_request, _request.Targets,
                    new ReadOnlyCollection<BookmarkChainNode>(_nodes),
                    new ReadOnlyCollection<BookmarkChainInput>(aggregateInputs),
                    new ReadOnlyCollection<BookmarkChainAmount>(missing),
                    new ReadOnlyCollection<BookmarkChainAmount>(surplus),
                    new ReadOnlyCollection<BookmarkChainDiagnostic>(_diagnostics));
            }

            private float EnsureProduct(string key, string originalId, float quantity, Recipe? forcedRecipe)
            {
                if (quantity <= Epsilon) return 0f;
                float fromStock = TakeStock(key, quantity);
                float deficit = quantity - fromStock;
                if (deficit <= Epsilon) return fromStock;
                if (_active.Contains(key) || _active.Count >= 128 || _remainingNodes-- <= 0)
                {
                    AddDiagnostic(BookmarkChainDiagnosticCode.Cycle, "Recipe cycle or planning limit: " + originalId + ".", key);
                    return fromStock;
                }

                RecipeOutput? output;
                Recipe? recipe = SelectRecipe(key, originalId, forcedRecipe, out output);
                if (recipe == null || output == null)
                {
                    if (forcedRecipe != null) AddDiagnostic(BookmarkChainDiagnosticCode.MissingRecipe, "No permitted recipe produces " + originalId + ".", key);
                    return fromStock;
                }
                if (output.Amount <= 0)
                {
                    AddDiagnostic(BookmarkChainDiagnosticCode.InvalidRecipe, "Recipe output amount is not positive.", key, recipe.Id);
                    return fromStock;
                }

                double rounded = Math.Ceiling((double)deficit / output.Amount);
                if (rounded > 1_000_000 || recipe.Outputs.Any(item => item.Amount > 0 && item.Amount * rounded > int.MaxValue))
                {
                    AddDiagnostic(BookmarkChainDiagnosticCode.InvalidRecipe, "Requested batch count is too large: " + originalId + ".", key, recipe.Id);
                    return fromStock;
                }
                int batches = (int)rounded;
                _active.Add(key);
                int diagnosticsBefore = _diagnostics.Count;
                var nodeInputs = new List<BookmarkChainInput>();
                bool validInputs = true;
                foreach (RecipeIngredient ingredient in recipe.Inputs)
                {
                    if (ingredient == null || ingredient.Amount < 0f) continue;
                    // Catalysts/tools are retained: one tool satisfies the recipe regardless of batch count.
                    float required = ingredient.IsCatalyst ? ingredient.Amount : ingredient.Amount * batches;
                    BookmarkChainInput input = PlanInput(ingredient, required, recipe);
                    nodeInputs.Add(input);
                    if (input.Shortage > Epsilon) validInputs = false;
                }
                _active.Remove(key);

                bool hasNewError = _diagnostics.Count > diagnosticsBefore;
                BookmarkChainNodeStatus status = validInputs && !hasNewError ? BookmarkChainNodeStatus.Planned :
                    (_diagnostics.Skip(diagnosticsBefore).Any(x => x.Code == BookmarkChainDiagnosticCode.Cycle) ? BookmarkChainNodeStatus.Cycle : BookmarkChainNodeStatus.Missing);
                int produced = 0;
                // This is a planning graph, not an execution simulation: expose intended outputs even
                // when a raw input is missing, while retaining Missing status and raw shortages. This lets
                // a shared intermediate/byproduct satisfy another target without double-counting it.
                foreach (RecipeOutput recipeOutput in recipe.Outputs)
                {
                    if (recipeOutput.Amount <= 0) continue;
                    int amount = checked(recipeOutput.Amount * batches);
                    string producedKey = OutputKey(recipeOutput);
                    _liquid[producedKey] = recipeOutput.IsLiquid;
                    _generated[producedKey] = (_generated.TryGetValue(producedKey, out float earlier) ? earlier : 0f) + amount;
                }
                float generated = output.Amount * batches;
                produced = checked((int)generated);
                // Consume only the deficit requested by this invocation; pre-existing inventory was
                // already consumed above, and the remainder stays visible as surplus/reusable output.
                TakeStock(key, deficit);

                _nodes.Add(new BookmarkChainNode(recipe, output, (int)Math.Ceiling(deficit), batches, produced,
                    new ReadOnlyCollection<RecipeOutput>(recipe.Outputs.ToArray()),
                    new ReadOnlyCollection<BookmarkChainInput>(nodeInputs), status));
                return fromStock + deficit;
            }

            private BookmarkChainInput PlanInput(RecipeIngredient ingredient, float required, Recipe recipe)
            {
                string key;
                string? picked = null;
                if (ingredient.MatchesAny && _request.PreferredIngredients.TryGetValue(ingredient.BrowseId, out var selection) &&
                    _catalog.TryGetItem(selection, out var choice) && ingredient.Matches(choice))
                {
                    picked = choice.BrowseId;
                    if (ingredient.IsLiquid && ingredient.QualityId.Length > 0)
                    {
                        float strength = choice.Qualities.Where(q => string.Equals(q.id, ingredient.QualityId, StringComparison.OrdinalIgnoreCase))
                            .Max(q => q.amount);
                        required /= strength;
                    }
                    else if (!ingredient.IsCatalyst && !ingredient.IsLiquid && ingredient.QualityId.Length > 0)
                        required = (float)Math.Ceiling(required / Math.Max(Epsilon, ingredient.Amount));
                }
                if (ingredient.MatchesAny && picked == null)
                {
                    key = ingredient.BrowseId;
                    _liquid[key] = ingredient.IsLiquid;
                    float available = ingredient.IsCatalyst ? Math.Min(GetStock(key), required) : TakeStock(key, required);
                    float shortage = Math.Max(0f, required - available);
                    AddRequirement(key, ingredient, required, available, shortage, true);
                    if (shortage > Epsilon)
                    {
                        AddDiagnostic(BookmarkChainDiagnosticCode.RequirementUnresolved,
                            "A quality/tag requirement needs a user-selected matching item: " + key + ".", key, recipe.Id);
                    }
                    return new BookmarkChainInput(ingredient, key, required, available, shortage,
                        GetStock(key), true, key);
                }

                key = NormalizeItemId(picked ?? ingredient.BrowseId);
                _liquid[key] = ingredient.IsLiquid;
                if (ingredient.IsCatalyst)
                {
                    float available = GetStock(key);
                    float satisfied = Math.Min(available, required);
                    float shortage = Math.Max(0f, required - satisfied);
                    AddRequirement(key, ingredient, required, satisfied, shortage, false);
                    if (shortage > Epsilon)
                        AddDiagnostic(BookmarkChainDiagnosticCode.MissingMaterial, "Missing retained tool or catalyst: " + key + ".", key, recipe.Id);
                    return new BookmarkChainInput(ingredient, key, required, satisfied, shortage, GetStock(key), false, string.Empty);
                }

                float generatedBefore = GeneratedStock(key);
                float inventoryBefore = InventoryStock(key);
                float fulfilled = EnsureProduct(key, ingredient.ItemId, required, null);
                float shortageFinal = Math.Max(0f, required - fulfilled);
                float fromInventory = Math.Min(inventoryBefore, Math.Max(0f, required - generatedBefore));
                // For net requirements only inventory and unmet external material count. In-chain
                // products belong in the step details, not in the raw material bill.
                AddRequirement(key, ingredient, fromInventory + shortageFinal, fromInventory, shortageFinal, false);
                if (shortageFinal > Epsilon)
                    AddDiagnostic(BookmarkChainDiagnosticCode.MissingMaterial, "Missing material: " + key + ".", key, recipe.Id);
                return new BookmarkChainInput(ingredient, key, required, fulfilled, shortageFinal, GetStock(key), false, string.Empty);
            }

            private Recipe? SelectRecipe(string key, string originalId, Recipe? forced, out RecipeOutput? output)
            {
                output = null;
                if (forced != null)
                {
                    output = FindOutput(forced, key, originalId);
                    if (output == null)
                    {
                        AddDiagnostic(BookmarkChainDiagnosticCode.InvalidTargetRecipe, "Chosen recipe does not produce " + originalId + ".", key, forced.Id);
                        return null;
                    }
                    return forced;
                }

                var candidates = new List<Recipe>();
                foreach (Recipe recipe in _request.BookmarkedRecipes)
                    if (FindOutput(recipe, key, originalId) != null && !candidates.Contains(recipe)) candidates.Add(recipe);
                if (!_request.StrictGroupRecipes)
                {
                    foreach (Recipe recipe in _catalog.GetRecipesProducing(originalId))
                        if (FindOutput(recipe, key, originalId) != null && !candidates.Contains(recipe)) candidates.Add(recipe);
                    if (candidates.Count == 0)
                        foreach (Recipe recipe in _catalog.Recipes)
                            if (FindOutput(recipe, key, originalId) != null && !candidates.Contains(recipe)) candidates.Add(recipe);
                }
                candidates = candidates.Where(IsAllowedBookmark).OrderBy(RecipeRank).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
                if (candidates.Count == 0) return null;
                Recipe chosen = candidates[0];
                output = FindOutput(chosen, key, originalId);
                return chosen;
            }

            private bool IsAllowedBookmark(Recipe recipe)
            {
                if (!_request.StrictGroupRecipes) return true;
                return _bookmarkKeys.Contains(RecipeKey(recipe)) || _bookmarkKeys.Contains(recipe.Id) || _request.BookmarkedRecipes.Contains(recipe);
            }

            private int RecipeRank(Recipe recipe)
            {
                if (_bookmarkRank.TryGetValue(RecipeKey(recipe), out int rank)) return rank;
                return int.MaxValue;
            }

            private RecipeOutput? FindOutput(Recipe recipe, string key, string originalId)
            {
                return recipe.Outputs.FirstOrDefault(x => OutputMatches(x, key, originalId));
            }

            private bool OutputMatches(RecipeOutput output, string key, string originalId)
            {
                return string.Equals(output.BrowseId, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(output.ItemId, originalId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(output.ItemId, key, StringComparison.OrdinalIgnoreCase);
            }

            private string NormalizeItemId(string id)
            {
                if (string.IsNullOrWhiteSpace(id)) return string.Empty;
                string trimmed = id.Trim();
                if (IsRequirementKey(trimmed)) return trimmed;
                if (_catalog.TryGetItem(trimmed, out var item)) return item.BrowseId;
                if (trimmed.StartsWith("item:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("virtual:", StringComparison.OrdinalIgnoreCase)) return trimmed;
                return "item:" + trimmed;
            }

            private static string OutputKey(RecipeOutput output) => output.BrowseId;
            private static string RecipeKey(Recipe recipe) => "recipe/" + recipe.Source + "/" + recipe.Id;
            private static bool IsRequirementKey(string key) => key.StartsWith("quality:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("tag:", StringComparison.OrdinalIgnoreCase);
            private float InventoryStock(string key) => _stock.TryGetValue(key, out float value) ? value : 0f;
            private float GeneratedStock(string key) => _generated.TryGetValue(key, out float value) ? value : 0f;
            private float GetStock(string key) => InventoryStock(key) + GeneratedStock(key);

            private float TakeStock(string key, float requested)
            {
                if (requested <= Epsilon) return 0f;
                float planned = Math.Min(GeneratedStock(key), requested);
                float existing = Math.Min(InventoryStock(key), requested - planned);
                if (planned > Epsilon) _generated[key] -= planned;
                if (existing > Epsilon) _stock[key] -= existing;
                return planned + existing;
            }

            private void AddStock(string key, float amount)
            {
                if (amount <= Epsilon) return;
                _stock[key] = GetStock(key) + amount;
            }

            private void AddMissing(string key, float shortage, Recipe? recipe, bool requirement, bool liquid)
            {
                if (shortage <= Epsilon) return;
                AddRequirement(key, requirement ? new RecipeIngredient(string.Empty, qualityId: key) : new RecipeIngredient(key, isLiquid: liquid), 0f, 0f, shortage, requirement);
            }

            private void AddRequirement(string key, RecipeIngredient ingredient, float required, float available, float shortage, bool requirement)
            {
                if (!_requirements.TryGetValue(key, out RequirementTotals? total))
                {
                    total = new RequirementTotals(key, ingredient.IsLiquid, requirement, ingredient);
                    _requirements[key] = total;
                }
                total.Required += required;
                total.Available += available;
                total.Shortage += shortage;
                if (total.Ingredient == null) total.Ingredient = ingredient;
            }

            private void AddDiagnostic(BookmarkChainDiagnosticCode code, string message, string itemId = "", string recipeId = "")
            {
                _diagnostics.Add(new BookmarkChainDiagnostic(code, message, itemId, recipeId));
            }

            private sealed class RequirementTotals
            {
                internal RequirementTotals(string itemId, bool liquid, bool requirement, RecipeIngredient ingredient)
                {
                    ItemId = itemId; IsLiquid = liquid; IsRequirement = requirement; Ingredient = ingredient;
                }
                internal string ItemId { get; }
                internal bool IsLiquid { get; }
                internal bool IsRequirement { get; }
                internal RecipeIngredient? Ingredient { get; set; }
                internal float Required { get; set; }
                internal float Available { get; set; }
                internal float Shortage { get; set; }
            }
        }
    }
}
