using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using CUCoreLib.Registries;

namespace NotEnoughItems
{
    /// <summary>Identifies the kind of an entry in the item catalog.</summary>
    public enum CatalogItemKind
    {
        /// <summary>A normal game item.</summary>
        Item,
        /// <summary>A game liquid.</summary>
        Liquid,
        /// <summary>An entry supplied by a content provider without a game item.</summary>
        Virtual,
    }

    /// <summary>A searchable item, liquid, or virtual item presented by NEI.</summary>
    public sealed class CatalogItem
    {
        /// <summary>Creates a catalog item.</summary>
        public CatalogItem(
            string id,
            string? displayName = null,
            string? description = null,
            string? categoryId = null,
            CatalogItemKind kind = CatalogItemKind.Item,
            Sprite? icon = null,
            bool isVirtual = false,
            global::ItemInfo? nativeItemInfo = null,
            IEnumerable<string>? tags = null,
            string? source = null,
            IEnumerable<global::CraftingQuality>? qualities = null,
            Color? iconColor = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("An item ID is required.", nameof(id));

            Id = id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Description = description ?? string.Empty;
            CategoryId = string.IsNullOrWhiteSpace(categoryId) ? "other" : categoryId.Trim();
            Kind = kind;
            Icon = icon;
            IconColor = iconColor ?? Color.white;
            IsVirtual = isVirtual || kind == CatalogItemKind.Virtual;
            NativeItemInfo = nativeItemInfo;
            Source = string.IsNullOrWhiteSpace(source) ? (kind == CatalogItemKind.Virtual ? "api" : "native") : source.Trim();
            Qualities = Snapshot(qualities);
            Tags = Snapshot(tags);
            SearchText = BuildSearchText(Id, DisplayName, Description, CategoryId, Source, Tags, Qualities);
        }

        /// <summary>Stable raw ID used by native recipes.</summary>
        public string Id { get; }
        /// <summary>Type-qualified ID used for bookmarks and unambiguous lookups.</summary>
        public string BrowseId => IsVirtual && (Id.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) || Id.StartsWith("quality:", StringComparison.OrdinalIgnoreCase))
            ? Id : (IsVirtual ? "virtual:" : IsLiquid ? "liquid:" : "item:") + Id;
        /// <summary>Localized display name, when one is available.</summary>
        public string DisplayName { get; }
        /// <summary>Localized or provider-supplied description.</summary>
        public string Description { get; }
        /// <summary>Provider/category identifier used by item filters.</summary>
        public string CategoryId { get; }
        /// <summary>Whether this is an item, liquid, or virtual entry.</summary>
        public CatalogItemKind Kind { get; }
        /// <summary>Whether the entry is not backed by a real game item.</summary>
        public bool IsVirtual { get; }
        /// <summary>Icon to display, if one was available.</summary>
        public Sprite? Icon { get; }
        /// <summary>Tint applied to the icon, useful for liquids.</summary>
        public Color IconColor { get; }
        /// <summary>Tint for an item icon; white for untinted icons.</summary>
        public Color IconTint => IconColor;
        /// <summary>The game ItemInfo for a native item, or null for liquids/virtual entries.</summary>
        public global::ItemInfo? NativeItemInfo { get; }
        /// <summary>Registration owner or native game source, searchable with the @source query.</summary>
        public string Source { get; }
        /// <summary>Crafting qualities exposed by this item or liquid.</summary>
        public IReadOnlyList<global::CraftingQuality> Qualities { get; }
        /// <summary>Provider tags associated with the entry.</summary>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Pre-combined, case-insensitive search text.</summary>
        public string SearchText { get; }
        /// <summary>True when this entry represents a liquid.</summary>
        public bool IsLiquid => Kind == CatalogItemKind.Liquid;

        private static IReadOnlyList<string> Snapshot(IEnumerable<string>? values)
        {
            var result = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                        result.Add(value.Trim());
                }
            }
            return new ReadOnlyCollection<string>(result);
        }

        private static IReadOnlyList<global::CraftingQuality> Snapshot(IEnumerable<global::CraftingQuality>? values)
        {
            return new ReadOnlyCollection<global::CraftingQuality>(values == null
                ? new List<global::CraftingQuality>()
                : values.Where(value => value != null).Distinct().ToList());
        }

        private static string BuildSearchText(string id, string name, string description, string category, string source, IReadOnlyList<string> tags, IReadOnlyList<global::CraftingQuality> qualities)
        {
            var builder = new StringBuilder();
            builder.Append(id).Append(' ').Append(name).Append(' ').Append(description).Append(' ').Append(category).Append(' ').Append(source);
            foreach (string tag in tags)
                builder.Append(' ').Append(tag);
            foreach (global::CraftingQuality quality in qualities)
            {
                if (quality == null)
                    continue;
                builder.Append(' ').Append(quality.id);
            }
            return builder.ToString();
        }
    }

    /// <summary>A machine/category grouping for independently registered recipes.</summary>
    public sealed class RecipeCategory
    {
        /// <summary>Creates a recipe category. MachineId is descriptive and never resolved to a live object.</summary>
        public RecipeCategory(string id, string? name = null, string? machineId = null, string? description = null, Sprite? icon = null, string? source = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A category ID is required.", nameof(id));

            Id = id.Trim();
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            MachineId = machineId ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
            Source = string.IsNullOrWhiteSpace(source) ? "api" : source.Trim();
        }

        /// <summary>Stable category identifier.</summary>
        public string Id { get; }
        /// <summary>Display name for the category.</summary>
        public string Name { get; }
        /// <summary>Optional machine identifier; no machine instance is required.</summary>
        public string MachineId { get; }
        /// <summary>Optional category description.</summary>
        public string Description { get; }
        /// <summary>Optional category icon.</summary>
        public Sprite? Icon { get; }
        /// <summary>Registration source used by source-scoped replacement/clear.</summary>
        public string Source { get; }
    }

    /// <summary>An ingredient or catalyst in a recipe.</summary>
    public sealed class RecipeIngredient
    {
        /// <summary>Creates an ingredient. An empty ItemId describes a quality/tag requirement.</summary>
        public RecipeIngredient(
            string itemId,
            float amount = 1f,
            bool isLiquid = false,
            bool isCatalyst = false,
            bool matchesAny = false,
            string? qualityId = null,
            float minimumCondition = 0f,
            string? ignoredItemId = null,
            string? displayName = null,
            string? requiredTag = null)
        {
            ItemId = itemId?.Trim() ?? string.Empty;
            Amount = amount;
            IsLiquid = isLiquid;
            IsCatalyst = isCatalyst;
            MatchesAny = matchesAny || string.IsNullOrWhiteSpace(ItemId);
            QualityId = qualityId?.Trim() ?? string.Empty;
            MinimumCondition = minimumCondition;
            IgnoredItemId = ignoredItemId?.Trim() ?? string.Empty;
            DisplayName = displayName?.Trim() ?? string.Empty;
            RequiredTag = requiredTag?.Trim() ?? string.Empty;
        }

        /// <summary>Referenced item or liquid ID; empty for a generic quality requirement.</summary>
        public string ItemId { get; }
        /// <summary>Browseable stable pseudo-entry ID for generic inputs, such as quality:cloth:2.</summary>
        public string BrowseId => !MatchesAny ? Catalog.ItemKey(ItemId, IsLiquid) :
            (RequiredTag.Length > 0 ? "tag:" + RequiredTag : "quality:" + QualityId) +
            (RequiredTag.Length > 0 ? string.Empty : (IsLiquid ? ":liquid:" : ":item:") + Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        /// <summary>Required amount or quality amount.</summary>
        public float Amount { get; }
        /// <summary>True when ItemId refers to a liquid.</summary>
        public bool IsLiquid { get; }
        /// <summary>True when the input is retained rather than consumed.</summary>
        public bool IsCatalyst { get; }
        /// <summary>True when any item satisfying QualityId may be used.</summary>
        public bool MatchesAny { get; }
        /// <summary>Optional crafting quality identifier.</summary>
        public string QualityId { get; }
        /// <summary>Minimum condition for a specific item input.</summary>
        public float MinimumCondition { get; }
        /// <summary>Native recipe item ID that is ignored when selecting candidates.</summary>
        public string IgnoredItemId { get; }
        /// <summary>Whether a native non-liquid candidate must be below 99.1% condition.</summary>
        public bool NoMaxCondition { get; internal set; }
        /// <summary>Optional provider display label.</summary>
        public string DisplayName { get; }
        /// <summary>Required native item tag for a tag-matched process input.</summary>
        public string RequiredTag { get; }
        /// <summary>True when this ingredient is consumed by the recipe.</summary>
        public bool IsConsumed => !IsCatalyst;

        /// <summary>
        /// Returns whether this ingredient can use the supplied catalog entry. A catalog describes
        /// item types, not live item condition, container contents, or the player's exclusions.
        /// </summary>
        public bool Matches(CatalogItem item) => Matches(item, false);

        internal bool Matches(CatalogItem item, bool includeBelowRequirement)
        {
            if (item == null || item.IsLiquid != IsLiquid ||
                (!IsLiquid && Catalog.SameId(IgnoredItemId, item.Id)))
                return false;
            if (!MatchesAny)
                return Catalog.SameId(BrowseId, item.BrowseId);
            if (item.IsVirtual)
                return false;

            if (RequiredTag.Length > 0 && !HasTag(item, RequiredTag))
                return false;
            if (QualityId.Length == 0)
                return RequiredTag.Length > 0;

            // RecipeItem.DoesUseItemType is intentionally broader than GetMatchingItem:
            // usage/expanded queries show every matching quality kind, regardless of strength.
            return item.Qualities.Any(q => Catalog.SameId(q.id, QualityId) &&
                (includeBelowRequirement || (IsLiquid ? q.amount > 0f : q.amount >= Amount)));
        }

        private static bool HasTag(CatalogItem item, string tag)
        {
            if (item.NativeItemInfo == null)
                return item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

            // Native ItemInfo.HasTag uses Enumerable.Contains(string), which is ordinal and
            // case-sensitive. Keep that rule rather than broadening native tag matches.
            try
            {
                return item.NativeItemInfo.HasTag(tag);
            }
            catch
            {
                return (item.NativeItemInfo.tags ?? string.Empty).Split(',').Contains(tag, StringComparer.Ordinal);
            }
        }


    }

    /// <summary>An output item or liquid produced by a recipe.</summary>
    public sealed class RecipeOutput
    {
        /// <summary>Creates a recipe output.</summary>
        public RecipeOutput(string itemId, int amount = 1, bool isLiquid = false, float condition = 1f, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An output ID is required.", nameof(itemId));
            ItemId = itemId.Trim();
            Amount = amount;
            IsLiquid = isLiquid;
            Condition = condition;
            DisplayName = displayName?.Trim() ?? string.Empty;
        }

        /// <summary>Produced item or liquid ID.</summary>
        public string ItemId { get; }
        /// <summary>Type-qualified stable identifier used for browsing and lookup.</summary>
        public string BrowseId => Catalog.ItemKey(ItemId, IsLiquid);
        /// <summary>Produced quantity.</summary>
        public int Amount { get; }
        /// <summary>True when ItemId is a liquid.</summary>
        public bool IsLiquid { get; }
        /// <summary>Condition of a produced item, when applicable.</summary>
        public float Condition { get; }
        /// <summary>Optional provider display label.</summary>
        public string DisplayName { get; }
    }

    /// <summary>A normalized native or provider recipe shown by NEI.</summary>
    public sealed class Recipe
    {
        /// <summary>Creates a provider recipe. It does not register or inject anything into the game.</summary>
        public Recipe(
            string id,
            string categoryId,
            IEnumerable<RecipeIngredient> inputs,
            IEnumerable<RecipeOutput> outputs,
            string? machineId = null,
            string? name = null,
            string? description = null,
            string? source = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A recipe ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(categoryId))
                throw new ArgumentException("A category ID is required.", nameof(categoryId));

            Id = id.Trim();
            CategoryId = categoryId.Trim();
            MachineId = machineId ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            Description = description ?? string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "api" : source.Trim();
            Inputs = Snapshot(inputs);
            Outputs = Snapshot(outputs);
            IsNative = false;
            _capturedNativeIndex = -1;
            NativeRecipe = null;
        }

        private Recipe(
            string id,
            string categoryId,
            IEnumerable<RecipeIngredient> inputs,
            IEnumerable<RecipeOutput> outputs,
            string machineId,
            string name,
            string description,
            string source,
            global::Recipe nativeRecipe,
            int nativeIndex,
            bool visible,
            bool hasMadeBefore,
            bool specialKnown,
            bool isRepair,
            int intelligence)
            : this(id, categoryId, inputs, outputs, machineId, name, description, source)
        {
            NativeRecipe = nativeRecipe;
            _capturedNativeIndex = nativeIndex;
            IsNative = true;
            IsVisible = visible;
            HasMadeBefore = hasMadeBefore;
            IsKnown = specialKnown;
            IsRepair = isRepair;
            Intelligence = intelligence;
        }

        /// <summary>Stable recipe ID.</summary>
        public string Id { get; }
        /// <summary>Category ID used for grouping.</summary>
        public string CategoryId { get; }
        /// <summary>Optional machine ID; it is metadata only.</summary>
        public string MachineId { get; }
        /// <summary>Display name.</summary>
        public string Name { get; }
        /// <summary>Optional description.</summary>
        public string Description { get; }
        /// <summary>Registration source, or native for a game recipe.</summary>
        public string Source { get; }
        /// <summary>Normalized inputs, including catalysts and generic quality requirements.</summary>
        public IReadOnlyList<RecipeIngredient> Inputs { get; }
        /// <summary>Normalized outputs.</summary>
        public IReadOnlyList<RecipeOutput> Outputs { get; }
        /// <summary>True when this wraps a recipe from Recipes.recipes.</summary>
        public bool IsNative { get; }
        /// <summary>True when the native recipe is visible to the original crafting UI.</summary>
        public bool IsVisible { get; }
        /// <summary>Native hasMadeBefore flag.</summary>
        public bool HasMadeBefore { get; }
        /// <summary>Native specialKnown flag.</summary>
        public bool IsKnown { get; }
        /// <summary>Native repair flag.</summary>
        public bool IsRepair { get; }
        /// <summary>Native intelligence requirement.</summary>
        public int Intelligence { get; }
        /// <summary>Safe current index to pass to the original crafting UI, or -1 if the native recipe is no longer live.</summary>
        public int NativeIndex => NativeRecipeIndex;
        /// <summary>Safe current index to pass to the original crafting UI, or -1 if the native recipe is no longer live.</summary>
        public int NativeRecipeIndex
        {
            get
            {
                if (!IsNative || NativeRecipe == null)
                    return -1;
                try
                {
                    List<global::Recipe> live = global::Recipes.recipes;
                    if (live == null)
                        return -1;
                    if (_capturedNativeIndex >= 0 && _capturedNativeIndex < live.Count
                        && ReferenceEquals(live[_capturedNativeIndex], NativeRecipe))
                        return _capturedNativeIndex;
                    for (int i = 0; i < live.Count; i++)
                        if (ReferenceEquals(live[i], NativeRecipe))
                            return i;
                }
                catch { }
                return -1;
            }
        }
        private readonly int _capturedNativeIndex;
        /// <summary>Live native recipe object, or null for provider recipes.</summary>
        public global::Recipe? NativeRecipe { get; }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(values == null ? new List<T>() : values.Where(v => v != null).ToList());
        }

        internal static Recipe? FromNative(global::Recipe recipe, int listIndex)
        {
            if (recipe == null || recipe.result == null || string.IsNullOrWhiteSpace(recipe.result.id))
                return null;

            int nativeIndex = recipe.index >= 0 ? recipe.index : listIndex;
            string categoryId = recipe.category.ToString();
            var inputs = new List<RecipeIngredient>();
            if (recipe.items != null)
            {
                foreach (global::RecipeItem item in recipe.items)
                {
                    if (item == null)
                        continue;
                    string qualityId = string.Empty;
                    float amount = 1f;
                    try
                    {
                        if (item.quality != null)
                        {
                            qualityId = item.quality.id ?? string.Empty;
                            amount = item.quality.amount;
                        }
                    }
                    catch
                    {
                    }

                    inputs.Add(new RecipeIngredient(
                        item.specific ? item.specificId : string.Empty,
                        item.isLiquid && item.specific ? item.minimumCondition : amount,
                        item.isLiquid,
                        !item.isLiquid && !item.destroyItem,
                        !item.specific,
                        qualityId,
                        item.minimumCondition,
                        item.ignoredId)
                    {
                        NoMaxCondition = item.noMaxCondition
                    });
                }
            }

            var outputs = new[]
            {
                new RecipeOutput(recipe.result.id, recipe.result.amount, recipe.result.isLiquid, recipe.result.resultCondition)
            };
            string name = SafeRecipeName(recipe, nativeIndex);
            return new Recipe(
                "native:" + nativeIndex + ":" + recipe.result.id,
                categoryId,
                inputs,
                outputs,
                string.Empty,
                name,
                string.Empty,
                "native",
                recipe,
                nativeIndex,
                SafeBool(() => recipe.visible, true),
                SafeBool(() => recipe.hasMadeBefore, false),
                SafeBool(() => recipe.specialKnown, false),
                SafeBool(() => recipe.isRepair, false),
                SafeInt(() => recipe.INT, 0));
        }

        private static string SafeRecipeName(global::Recipe recipe, int index)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(recipe.simpleName))
                    return recipe.simpleName;
            }
            catch { }
            try
            {
                if (!string.IsNullOrWhiteSpace(recipe.fullName))
                    return recipe.fullName;
            }
            catch { }
            return "Recipe " + index;
        }

        private static bool SafeBool(Func<bool> getter, bool fallback)
        {
            try { return getter(); } catch { return fallback; }
        }

        private static int SafeInt(Func<int> getter, int fallback)
        {
            try { return getter(); } catch { return fallback; }
        }
    }

    /// <summary>A refreshable catalog of game items and normalized recipes.</summary>
    public sealed class Catalog
    {
        private const string NativeSource = "native";
        private readonly Dictionary<string, CatalogItem> _providerItems = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecipeCategory> _providerCategories = new Dictionary<string, RecipeCategory>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, Recipe>> _providerRecipes = new Dictionary<string, Dictionary<string, Recipe>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CatalogItem> _items = new List<CatalogItem>();
        private readonly List<RecipeCategory> _categories = new List<RecipeCategory>();
        private readonly List<Recipe> _recipes = new List<Recipe>();
        private readonly Dictionary<string, CatalogItem> _itemsById = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CatalogItem> _itemsByBrowseId = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<CatalogItem> _itemSnapshot = Array.Empty<CatalogItem>();
        private IReadOnlyList<Recipe> _recipeSnapshot = Array.Empty<Recipe>();
        private IReadOnlyList<RecipeCategory> _categorySnapshot = Array.Empty<RecipeCategory>();
        private readonly Dictionary<string, List<Recipe>> _producedBy = new Dictionary<string, List<Recipe>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Recipe>> _usedIn = new Dictionary<string, List<Recipe>>(StringComparer.OrdinalIgnoreCase);
        private int _lastItemCount = -1;
        private int _lastLiquidCount = -1;
        private int _lastRecipeCount = -1;
        private int _lastRegistrySignature;
        private int _lastLocaleSignature;
        private long _nextFreshnessCheck;
        private bool _built;
        private int _revision;

        /// <summary>The process-wide catalog used by <see cref="RecipeApi"/>.</summary>
        public static Catalog Shared { get; } = new Catalog();

        /// <summary>Creates an independent catalog instance.</summary>
        public Catalog()
        {
        }

        /// <summary>Gets the current item snapshot, refreshing when game counts changed.</summary>
        public IReadOnlyList<CatalogItem> Items { get { EnsureFresh(); return _itemSnapshot; } }
        /// <summary>Gets native and provider recipe categories.</summary>
        public IReadOnlyList<RecipeCategory> RecipeCategories { get { EnsureFresh(); return _categorySnapshot; } }
        /// <summary>Gets native and provider recipes.</summary>
        public IReadOnlyList<Recipe> Recipes { get { EnsureFresh(); return _recipeSnapshot; } }
        /// <summary>Increments whenever the normalized catalog is rebuilt.</summary>
        public int Revision { get { EnsureFresh(); return _revision; } }

        /// <summary>Rebuilds native game entries while retaining provider registrations.</summary>
        public void Refresh()
        {
            _items.Clear();
            _categories.Clear();
            _recipes.Clear();
            _itemsById.Clear();
            _itemsByBrowseId.Clear();
            _producedBy.Clear();
            _usedIn.Clear();

            BuildNativeItems();
            foreach (CatalogItem item in _providerItems.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
                AddItem(item);

            var categoriesById = new Dictionary<string, RecipeCategory>(StringComparer.OrdinalIgnoreCase);
            BuildNativeRecipes(categoriesById);
            BuildNativeProcesses(categoriesById);
            foreach (RecipeCategory category in _providerCategories.Values)
                categoriesById[category.Id] = category;
            foreach (var source in _providerRecipes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                foreach (Recipe recipe in source.Value.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
                {
                    if (!categoriesById.ContainsKey(recipe.CategoryId))
                        categoriesById[recipe.CategoryId] = new RecipeCategory(recipe.CategoryId, machineId: recipe.MachineId, source: source.Key);
                    _recipes.Add(recipe);
                }
            }
            foreach (Recipe recipe in _recipes)
                IndexRecipe(recipe, categoriesById.TryGetValue(recipe.CategoryId, out var category) ? category.MachineId : string.Empty);
            _categories.AddRange(categoriesById.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase));
            _itemSnapshot = Array.AsReadOnly(_items.ToArray());
            _recipeSnapshot = Array.AsReadOnly(_recipes.ToArray());
            _categorySnapshot = Array.AsReadOnly(_categories.ToArray());
            _lastItemCount = global::Item.GlobalItems?.Count ?? 0;
            _lastLiquidCount = global::Liquids.Registry?.Count ?? 0;
            _lastRecipeCount = global::Recipes.recipes?.Count ?? 0;
            _lastRegistrySignature = ReadRegistrySignature();
            _lastLocaleSignature = ReadLocaleSignature();
            _nextFreshnessCheck = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency;
            _built = true;
            _revision++;
        }

        /// <summary>Registers or replaces an item independently of the game registry.</summary>
        public void RegisterItem(CatalogItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            _providerItems[item.Id] = item;
            Refresh();
        }

        /// <summary>Registers a virtual item that has no live game object.</summary>
        public void RegisterVirtualItem(string id, string? displayName = null, string? description = null, string? categoryId = null, Sprite? icon = null, IEnumerable<string>? tags = null)
        {
            RegisterItem(new CatalogItem(id, displayName, description, categoryId, CatalogItemKind.Virtual, icon, true, null, tags));
        }

        /// <summary>Registers or replaces a provider category without requiring a machine object.</summary>
        public void RegisterCategory(RecipeCategory category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            _providerCategories[category.Id] = category;
            Refresh();
        }

        /// <summary>Registers or replaces a provider recipe without requiring a machine object.</summary>
        public void RegisterRecipe(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            string source = string.IsNullOrWhiteSpace(recipe.Source) ? "api" : recipe.Source;
            if (!_providerRecipes.TryGetValue(source, out Dictionary<string, Recipe> recipes))
            {
                recipes = new Dictionary<string, Recipe>(StringComparer.OrdinalIgnoreCase);
                _providerRecipes[source] = recipes;
            }
            recipes[recipe.Id] = recipe;
            Refresh();
        }

        /// <summary>Replaces all category and recipe registrations owned by a source.</summary>
        public void ReplaceSource(string source, IEnumerable<RecipeCategory> categories, IEnumerable<Recipe> recipes)
        {
            ClearSource(source, false);
            string owner = NormalizeSource(source);
            if (categories != null)
            {
                foreach (RecipeCategory category in categories)
                {
                    if (category != null)
                        _providerCategories[category.Id] = WithSource(category, owner);
                }
            }
            if (recipes != null)
            {
                foreach (Recipe recipe in recipes)
                {
                    if (recipe == null)
                        continue;
                    if (!_providerRecipes.TryGetValue(owner, out Dictionary<string, Recipe> owned))
                    {
                        owned = new Dictionary<string, Recipe>(StringComparer.OrdinalIgnoreCase);
                        _providerRecipes[owner] = owned;
                    }
                    owned[recipe.Id] = recipe;
                }
            }
            Refresh();
        }

        /// <summary>Clears all provider categories and recipes owned by a source.</summary>
        public void ClearSource(string source)
        {
            ClearSource(source, true);
        }

        /// <summary>Looks up an item by stable ID.</summary>
        public bool TryGetItem(string? id, [NotNullWhen(true)] out CatalogItem? item)
        {
            EnsureFresh();
            item = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            string key = id.Trim();
            return _itemsByBrowseId.TryGetValue(key, out item) || _itemsById.TryGetValue(key, out item);
        }

        /// <summary>Gets recipes in a category. An empty list is returned for an unknown category.</summary>
        public IReadOnlyList<Recipe> GetRecipesForCategory(string categoryId)
        {
            EnsureFresh();
            if (string.IsNullOrWhiteSpace(categoryId))
                return new ReadOnlyCollection<Recipe>(new List<Recipe>());
            return new ReadOnlyCollection<Recipe>(_recipes.Where(x => string.Equals(x.CategoryId, categoryId.Trim(), StringComparison.OrdinalIgnoreCase)).ToList());
        }

        /// <summary>Gets recipes that produce an item or liquid.</summary>
        public IReadOnlyList<Recipe> GetRecipesProducing(string itemId)
        {
            EnsureFresh();
            return GetIndexed(_producedBy, ResolveIndexKey(itemId), itemId);
        }

        /// <summary>Gets recipes requiring an item as material, catalyst, or machine, including every matching property type.</summary>
        /// <param name="itemId">A raw or type-qualified catalog ID.</param>
        /// <returns>All indexed recipes that use the type, independent of available inventory or unlocked recipes.</returns>
        public IReadOnlyList<Recipe> GetRecipesUsing(string itemId)
        {
            EnsureFresh();
            return GetIndexed(_usedIn, ResolveIndexKey(itemId), itemId);
        }

        /// <summary>Alias for <see cref="GetRecipesUsing"/>.</summary>
        public IReadOnlyList<Recipe> GetItemUsages(string itemId)
        {
            return GetRecipesUsing(itemId);
        }

        /// <summary>
        /// Gets catalog item types that can satisfy an ingredient. Set includeBelowRequirement to true
        /// to include same-quality items below the requested amount, as recipe-usage queries do.
        /// Results describe types only: live condition, containers, exclusions from a player's crafting
        /// selection, and liquid quantities cannot be established from the catalog.
        /// </summary>
        /// <param name="ingredient">The material or tool requirement being inspected.</param>
        /// <param name="includeBelowRequirement">Include entries sharing the quality even if it is too weak to satisfy the recipe.</param>
        /// <returns>The matching catalog entries, excluding placeholder entries for generic requirements.</returns>
        public IReadOnlyList<CatalogItem> GetMatchingItems(RecipeIngredient ingredient, bool includeBelowRequirement = false)
        {
            if (ingredient == null)
                throw new ArgumentNullException(nameof(ingredient));
            EnsureFresh();
            return Array.AsReadOnly(_items.Where(item => ingredient.Matches(item, includeBelowRequirement)).ToArray());
        }

        /// <summary>Finds the live native recipe index used by the original crafting panel.</summary>
        public bool TryGetNativeRecipeIndex(string recipeId, out int index)
        {
            EnsureFresh();
            index = -1;
            if (string.IsNullOrWhiteSpace(recipeId))
                return false;
            Recipe? recipe = _recipes.FirstOrDefault(x => x.IsNative && string.Equals(x.Id, recipeId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (recipe == null)
                return false;
            index = recipe.NativeIndex;
            return index >= 0;
        }

        /// <summary>Forces the next query to compare live game registry counts again.</summary>
        public void Invalidate()
        {
            _built = false;
        }

        private void EnsureFresh()
        {
            if (!_built || (global::Item.GlobalItems?.Count ?? 0) != _lastItemCount ||
                (global::Liquids.Registry?.Count ?? 0) != _lastLiquidCount || (global::Recipes.recipes?.Count ?? 0) != _lastRecipeCount)
            {
                Refresh();
                return;
            }

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now < _nextFreshnessCheck) return;
            _nextFreshnessCheck = now + System.Diagnostics.Stopwatch.Frequency;
            if (ReadRegistrySignature() != _lastRegistrySignature || ReadLocaleSignature() != _lastLocaleSignature)
                Refresh();
        }

        private void BuildNativeItems()
        {
            var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var liquidIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (global::Item.GlobalItems is { } gameItems) itemIds.UnionWith(gameItems.Keys);
            if (global::Liquids.Registry is { } gameLiquids) liquidIds.UnionWith(gameLiquids.Keys);
            itemIds.UnionWith(ItemRegistry.GetRegisteredItemIds());
            liquidIds.UnionWith(LiquidRegistry.GetRegisteredLiquidIds());

            foreach (string id in itemIds.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                ItemRegistry.TryGetCustomInfo(id, out var custom);
                global::ItemInfo? info = null;
                global::Item.GlobalItems?.TryGetValue(id, out info);
                info ??= custom;
                if (info == null) continue;
                string source = ItemRegistry.TryGetOwnerModGuid(id, out string owner) ? owner : custom != null ? "CUCoreLib" : NativeSource;
                AddItem(new CatalogItem(id, ReadLocale(id, false) ?? Fallback(info.fullName, id),
                    ReadLocale(id + "dsc", false) ?? info.description, info.category, CatalogItemKind.Item,
                    LoadIcon(id), nativeItemInfo: info, tags: ReadTags(info), source: source, qualities: info.qualities));
            }

            Sprite? droplet = liquidIds.Count > 0 ? LoadDroplet() : null;
            foreach (string id in liquidIds.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                global::LiquidType? type = null;
                global::Liquids.Registry?.TryGetValue(id, out type);
                LiquidRegistry.TryGetCustomInfo(id, out var custom);
                if (type == null && custom == null) continue;
                string key = Fallback(type?.localeName, id);
                string source = LiquidRegistry.TryGetOwnerModGuid(id, out string owner) ? owner : custom != null ? "CUCoreLib" : NativeSource;
                var tags = new List<string>();
                if (type?.injectable == true || custom?.injectable == true) tags.Add("injectable");
                if (type?.healthUsable == true || custom?.healthUsable == true) tags.Add("health");
                if (global::Liquids.DangerList?.Contains(id) == true) tags.Add("danger");
                AddItem(new CatalogItem(id, ReadLocale(key, type?.localeFromItem != true) ?? Fallback(custom?.name, key),
                    ReadLocale(id + "dsc", true) ?? custom?.description, "Liquid", CatalogItemKind.Liquid, droplet,
                    tags: tags, source: source, qualities: type?.qualities ?? custom?.qualities, iconColor: type?.color ?? custom?.color));
            }
        }

        private static string Fallback(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string? ReadLocale(string key, bool other)
        {
            var dictionary = other ? global::Locale.currentLang?.other : global::Locale.currentLang?.main;
            return dictionary != null && dictionary.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text) ? text : null;
        }

        private void BuildNativeRecipes(Dictionary<string, RecipeCategory> categoriesById)
        {
            try
            {
                if (global::Recipes.recipes == null)
                    return;
                for (int i = 0; i < global::Recipes.recipes.Count; i++)
                {
                    global::Recipe native = global::Recipes.recipes[i];
                    Recipe? recipe = Recipe.FromNative(native, i);
                    if (recipe == null)
                        continue;
                    _recipes.Add(recipe);
                    if (!categoriesById.ContainsKey(recipe.CategoryId))
                        categoriesById[recipe.CategoryId] = new RecipeCategory(recipe.CategoryId, recipe.CategoryId, string.Empty, string.Empty, null, NativeSource);
                }
            }
            catch
            {
            }
        }

        private void AddItem(CatalogItem item)
        {
            if (!_itemsByBrowseId.TryAdd(item.BrowseId, item)) return;
            _items.Add(item);
            _itemsById.TryAdd(item.Id, item);
        }

        internal static bool SameId(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        internal static string ItemKey(string id, bool liquid)
        {
            if (id.StartsWith("item:", StringComparison.OrdinalIgnoreCase) || id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("virtual:", StringComparison.OrdinalIgnoreCase) || id.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("quality:", StringComparison.OrdinalIgnoreCase)) return id;
            return (liquid ? "liquid:" : "item:") + id;
        }

        private string ResolveIndexKey(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            string key = id.Trim();
            if (_itemsByBrowseId.ContainsKey(key) || key.StartsWith("item:", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("virtual:", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("quality:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                return key;
            bool hasItem = _itemsByBrowseId.ContainsKey("item:" + key);
            bool hasLiquid = _itemsByBrowseId.ContainsKey("liquid:" + key);
            bool hasVirtual = _itemsByBrowseId.ContainsKey("virtual:" + key);
            return hasItem && !hasLiquid && !hasVirtual && _itemsById.TryGetValue(key, out var item)
                ? item.BrowseId
                : "raw:" + key;
        }

        private void BuildNativeProcesses(Dictionary<string, RecipeCategory> categories)
        {
            const string category = "native.cooking";
            if (!_itemsByBrowseId.TryGetValue("item:steak", out var steak)) return;
            if (!_itemsByBrowseId.ContainsKey("tag:meat"))
                AddItem(new CatalogItem("tag:meat", "Meat-tagged item", "Any item whose native ItemInfo.HasTag(\"meat\") is accepted by a cooker.",
                    "Process", CatalogItemKind.Virtual, source: NativeSource, tags: new[] { "meat" }, isVirtual: true));
            var input = new RecipeIngredient(string.Empty, ignoredItemId: "steak", displayName: "Meat-tagged item", requiredTag: "meat");
            // Heater.OnCollisionEnter2D, cooker=true: replace a meat-tagged item other than steak; retain 30% of input condition.
            categories[category] = new RecipeCategory(category, "Cooking", "heater.cooker", source: NativeSource, icon: steak.Icon);
            _recipes.Add(new Recipe("native.process.cook-meat", category, new[] { input }, new[] { new RecipeOutput("steak") },
                machineId: "heater.cooker", name: steak.DisplayName,
                description: "Place raw meat against a cooking heater. The steak retains 30% of the input's condition.", source: NativeSource));
        }

        private void IndexRecipe(Recipe recipe, string categoryMachineId)
        {
            foreach (RecipeOutput output in recipe.Outputs)
                AddIndex(_producedBy, ItemKey(output.ItemId, output.IsLiquid), recipe);
            string machineId = string.IsNullOrWhiteSpace(recipe.MachineId) ? categoryMachineId : recipe.MachineId;
            if (!string.IsNullOrWhiteSpace(machineId)) AddIndex(_usedIn, ItemKey(machineId, false), recipe);
            foreach (RecipeIngredient input in recipe.Inputs)
            {
                if (!input.MatchesAny)
                {
                    if (input.IsLiquid || !SameId(input.IgnoredItemId, input.ItemId))
                        AddIndex(_usedIn, input.BrowseId, recipe);
                    continue;
                }
                // Match the expanded list: every advertised quality type, even when its
                // strength isn't high enough to craft. Game-defined exclusions still apply.
                AddIndex(_usedIn, input.BrowseId, recipe);
                foreach (CatalogItem item in _items)
                    if (input.Matches(item, includeBelowRequirement: true))
                        AddIndex(_usedIn, item.BrowseId, recipe);
            }
        }

        private static void AddIndex(Dictionary<string, List<Recipe>> index, string id, Recipe recipe)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;
            if (!index.TryGetValue(id, out List<Recipe> list))
            {
                list = new List<Recipe>();
                index[id] = list;
            }
            if (!list.Contains(recipe))
                list.Add(recipe);
        }

        private static IReadOnlyList<Recipe> GetIndexed(Dictionary<string, List<Recipe>> index, string key, string rawId)
        {
            if (index.TryGetValue(key, out var recipes)) return recipes.AsReadOnly();
            if (key.StartsWith("raw:", StringComparison.OrdinalIgnoreCase))
            {
                string id = rawId.Trim();
                return Array.AsReadOnly(new[] { "item:", "liquid:", "virtual:" }
                    .Where(prefix => index.ContainsKey(prefix + id))
                    .SelectMany(prefix => index[prefix + id]).Distinct().ToArray());
            }
            return Array.Empty<Recipe>();
        }

        private void ClearSource(string source, bool refresh)
        {
            string owner = NormalizeSource(source);
            var categoryIds = _providerCategories.Values.Where(x => string.Equals(x.Source, owner, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
            foreach (string categoryId in categoryIds)
                _providerCategories.Remove(categoryId);
            _providerRecipes.Remove(owner);
            if (refresh)
                Refresh();
        }

        private static RecipeCategory WithSource(RecipeCategory category, string source)
        {
            return new RecipeCategory(category.Id, category.Name, category.MachineId, category.Description, category.Icon, source);
        }

        private static string NormalizeSource(string source)
        {
            return string.IsNullOrWhiteSpace(source) ? "api" : source.Trim();
        }

        private static Sprite? LoadIcon(string id)
        {
            try
            {
                if (ItemRegistry.TryGetIcon(id, out Sprite sprite) && sprite != null)
                    return sprite;
            }
            catch { }
            try
            {
                GameObject prefab = Resources.Load<GameObject>(id);
                SpriteRenderer? renderer = prefab == null ? null : prefab.GetComponent<SpriteRenderer>();
                return renderer == null ? null : renderer.sprite;
            }
            catch
            {
                return null;
            }
        }

        private static Sprite? LoadDroplet()
        {
            try { return Resources.Load<Sprite>("Sprites/droplet"); } catch { return null; }
        }

        private static IEnumerable<string> ReadTags(global::ItemInfo info)
        {
            if (info == null)
                return Array.Empty<string>();
            try
            {
                string[] tags = info.GetTags();
                return tags ?? Array.Empty<string>();
            }
            catch
            {
                return string.IsNullOrWhiteSpace(info.tags) ? Array.Empty<string>() : info.tags.Split(',');
            }
        }

        private static string SafeString(Func<string> getter, string fallback)
        {
            try
            {
                string value = getter();
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private static int ReadRegistrySignature()
        {
            unchecked
            {
                int hash = 17;
                if (global::Item.GlobalItems != null)
                    foreach (var pair in global::Item.GlobalItems.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        hash = hash * 31 + pair.Key.GetHashCode(StringComparison.OrdinalIgnoreCase);
                        global::ItemInfo info = pair.Value;
                        if (info == null) continue;
                        hash = hash * 31 + (info.fullName ?? string.Empty).GetHashCode(StringComparison.Ordinal);
                        hash = hash * 31 + (info.description ?? string.Empty).GetHashCode(StringComparison.Ordinal);
                        hash = hash * 31 + (info.tags ?? string.Empty).GetHashCode(StringComparison.Ordinal);
                        if (info.qualities != null)
                            foreach (global::CraftingQuality quality in info.qualities)
                                hash = hash * 31 + (quality?.id ?? string.Empty).GetHashCode(StringComparison.OrdinalIgnoreCase);
                    }
                if (global::Liquids.Registry != null)
                    foreach (var pair in global::Liquids.Registry.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        hash = hash * 31 + pair.Key.GetHashCode(StringComparison.OrdinalIgnoreCase);
                        hash = hash * 31 + (pair.Value?.localeName ?? string.Empty).GetHashCode(StringComparison.Ordinal);
                    }
                if (global::Recipes.recipes != null)
                    foreach (global::Recipe recipe in global::Recipes.recipes)
                        hash = hash * 31 + RuntimeHelpers.GetHashCode(recipe);
                return hash;
            }
        }

        private static int ReadLocaleSignature()
        {
            try { return StringComparer.OrdinalIgnoreCase.GetHashCode(global::Locale.currentLangName ?? string.Empty); }
            catch { return 0; }
        }

        private static int NativeItemCount()
        {
            try { return (global::Item.GlobalItems?.Count ?? 0) + ItemRegistry.GetRegisteredItemIds().Count(); } catch { return 0; }
        }

        private static int NativeRecipeCount()
        {
            try { return global::Recipes.recipes?.Count ?? 0; } catch { return 0; }
        }
    }

    /// <summary>Process-wide registration facade for future and optional machine mods.</summary>
    public static class RecipeApi
    {
        /// <summary>The catalog receiving registrations.</summary>
        public static Catalog Catalog => NotEnoughItems.Catalog.Shared;

        /// <summary>Registers or replaces a category without requiring its machine to be installed.</summary>
        public static void RegisterCategory(RecipeCategory category)
        {
            Catalog.RegisterCategory(category);
        }

        /// <summary>Registers or replaces a recipe without requiring its machine to be installed.</summary>
        public static void RegisterRecipe(Recipe recipe)
        {
            Catalog.RegisterRecipe(recipe);
        }

        /// <summary>Replaces all registrations from one provider source.</summary>
        public static void ReplaceSource(string source, IEnumerable<RecipeCategory> categories, IEnumerable<Recipe> recipes)
        {
            Catalog.ReplaceSource(source, categories, recipes);
        }

        /// <summary>Clears all registrations from one provider source.</summary>
        public static void ClearSource(string source)
        {
            Catalog.ClearSource(source);
        }

        /// <summary>Forces native item and recipe data to be rescanned.</summary>
        public static void Refresh()
        {
            Catalog.Refresh();
        }
    }
}
