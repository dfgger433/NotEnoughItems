using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NotEnoughItems;

internal sealed class BookmarkBook
{
    internal const string Format = "nei3";
    internal const int MaximumQuantity = 9999;
    private const string PreviousFormat = "nei2";
    private const string RecipePrefix = "recipe/";
    private const int Limit = 2048;
    private const int NameLimit = 128;
    private const int IdLimit = 256;
    private const int SlotLimit = 128;
    private const int VariantLimit = 4096;
    private const int PayloadLimit = 32_000_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    // Page and group settings are independent; the empty group denotes the page's ungrouped lane.
    private readonly Dictionary<string, DisplayState> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Page, string Group), DisplayState> _groups = new();
    private readonly HashSet<string> _favorites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _favoriteRoutes = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<Entry> Entries = new();

    private readonly struct DisplayState(bool compact, bool chain, bool collapsed)
    {
        internal bool Compact => compact;
        internal bool Chain => chain;
        internal bool Collapsed => collapsed;
        internal int Flags => (compact ? 1 : 0) | (chain ? 2 : 0) | (collapsed ? 4 : 0);
    }

    internal sealed class Entry(string page, string group, string id, int quantity)
    {
        internal string Page = page;
        internal string Group = group;
        internal string Id { get; } = id;
        internal int Quantity = quantity;
        internal bool IsRecipe => IsRecipeKey(Id);
        internal string? RecipeId => IsRecipe ? Id.Substring(RecipePrefix.Length) : null;
        internal bool ShowRecipe { get; set; } = IsRecipeKey(id);
        internal bool ShowQuantity { get; set; } = quantity > 1;
        internal bool LegacyRecipeQuantity { get; set; }
        internal bool IsRecipeCollapsed { get; set; }
        internal string? ProductId { get; set; }
        internal readonly Dictionary<int, string> VariantChoices = new();
        internal IReadOnlyDictionary<int, string> Variants => VariantChoices;
        internal IReadOnlyList<int> IngredientOrder { get; set; } = Array.Empty<int>();
        internal string? Variant(int slot) => VariantChoices.TryGetValue(slot, out var item) ? item : null;
    }

    internal IEnumerable<string> PageNames => _pages.Keys.Concat(Entries.Select(entry => entry.Page)).Distinct(StringComparer.Ordinal);
    internal IEnumerable<string> Favorites => _favorites.Where(IsRecipeKey);
    internal IEnumerable<string> FavoriteMaterials => _favorites.Where(IsMaterialKey);
    internal IEnumerable<KeyValuePair<string, string>> FavoriteRoutes => _favoriteRoutes;
    internal string? FavoriteRoute(string product) => _favoriteRoutes.TryGetValue(product, out var recipe) ? recipe : null;

    internal bool SetFavoriteRoute(string product, string? recipe)
    {
        if (!IsMaterialKey(product) || !ValidId(product)) return false;
        if (recipe == null) return _favoriteRoutes.Remove(product);
        if (!TryRecipeKey(recipe, out var key) || (!_favoriteRoutes.ContainsKey(product) && _favoriteRoutes.Count >= Limit)) return false;
        _favoriteRoutes[product] = key;
        return true;
    }
    internal IEnumerable<Entry> Page(string page) => Entries.Where(entry => entry.Page == page);
    internal Entry? Find(string page, string id) => Entries.Find(entry => entry.Page == page && entry.Id == id);
    internal Entry? Find(string page, string id, string group) => Entries.Find(entry => entry.Page == page && entry.Id == id && entry.Group == group);

    internal bool EnsurePage(string page) => ValidText(page, NameLimit) && _pages.Count < Limit && _pages.TryAdd(page, default);

    internal bool IsFavorite(string recipeKey) => TryRecipeKey(recipeKey, out var key) && _favorites.Contains(key);

    internal bool ToggleFavorite(string recipeKey)
    {
        if (!TryRecipeKey(recipeKey, out var key)) return false;
        return _favorites.Remove(key) || (_favorites.Count < Limit && _favorites.Add(key));
    }

    internal bool IsFavoriteMaterial(string id) => IsMaterialKey(id) && _favorites.Contains(id);

    internal bool ToggleFavoriteMaterial(string id) => ValidId(id) && IsMaterialKey(id) &&
        (_favorites.Remove(id) || (_favorites.Count < Limit && _favorites.Add(id)));

    private static bool IsMaterialKey(string id) => id.StartsWith("item:", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) || id.StartsWith("virtual:", StringComparison.OrdinalIgnoreCase);

    internal Entry? Add(string page, string id, int quantity, string group = "", bool showRecipe = true,
        string? productId = null, bool showQuantity = false)
    {
        if (Entries.Count >= Limit || !ValidText(page, NameLimit) || !ValidText(group, NameLimit, true) || !ValidId(id) ||
            (productId != null && (!IsRecipeKey(id) || !ValidText(productId, IdLimit)))) return null;
        if (!_pages.ContainsKey(page) && !EnsurePage(page)) return null;
        int amount = Math.Clamp(quantity, 0, MaximumQuantity);
        var entry = new Entry(page, group, id, amount)
        {
            ShowRecipe = IsRecipeKey(id) && showRecipe,
            ProductId = productId,
            ShowQuantity = showQuantity || amount != 1
        };
        int last = group.Length == 0 ? -1 : Entries.FindLastIndex(item => item.Page == page && item.Group == group);
        if (last < 0) Entries.Add(entry);
        else Entries.Insert(last + 1, entry);
        if (group.Length > 0) _groups.TryAdd((page, group), GroupState(page, group));
        return entry;
    }

    // The original overload searches across the page, for callers without a specific group target.
    internal bool Toggle(string page, string id, int quantity, bool showQuantity = false)
        => Find(page, id) is { } entry ? Remove(entry) : Add(page, id, quantity, showQuantity: showQuantity) != null;

    internal bool Toggle(string page, string id, int quantity, string group, bool showQuantity = false)
        => Find(page, id, group) is { } entry ? Remove(entry) : Add(page, id, quantity, group, showQuantity: showQuantity) != null;

    internal bool ToggleRecipe(string page, string recipeKey, int quantity, bool showRecipe, string? productId = null,
        string group = "", bool showQuantity = false)
    {
        if (!TryRecipeKey(recipeKey, out var id) || (productId != null && !ValidText(productId, IdLimit))) return false;
        var entry = Entries.Find(item => item.Page == page && item.Group == group && item.Id == id && item.ProductId == productId);
        if (entry == null) return Add(page, id, quantity, group, showRecipe, productId, showQuantity) != null;
        if (entry.ShowRecipe == showRecipe && (!showQuantity || entry.ShowQuantity)) return Remove(entry);
        entry.ShowRecipe = showRecipe;
        if (showQuantity) SetQuantity(entry, quantity);
        return true;
    }

    internal bool Remove(Entry entry)
    {
        if (!Entries.Remove(entry)) return false;
        PruneGroup(entry.Page, entry.Group);
        return true;
    }

    internal bool RemoveGroup(string page, string group)
    {
        if (Entries.RemoveAll(entry => entry.Page == page && entry.Group == group) == 0) return false;
        if (group.Length > 0) _groups.Remove((page, group));
        return true;
    }

    internal bool Adjust(Entry entry, int delta) =>
        SetQuantity(entry, (int)Math.Clamp((long)entry.Quantity + delta, 0, MaximumQuantity));

    internal bool StepQuantity(Entry entry, int delta, int? legacyBatches = null) => delta != 0 &&
        SetAdjustedQuantity(entry, (int)Math.Min(MaximumQuantity, ShiftMultiplier(legacyBatches ?? entry.Quantity, delta, 0)), legacyBatches.HasValue);

    internal bool AdjustGroup(string page, string group, int delta, IReadOnlyDictionary<Entry, int>? recipeBatches = null,
        ISet<Entry>? chainTargets = null, IReadOnlyDictionary<Entry, int>? recipeUnits = null)
    {
        if (delta == 0) return false;
        var members = Page(page).Where(entry => entry.Group == group);
        bool changed = false;
        if (chainTargets != null)
        {
            foreach (var entry in members)
                if (chainTargets.Contains(entry)) changed |= StepQuantity(entry, delta, GetLegacyBatches(entry));
            return changed;
        }

        // A normal group adjusts a common multiplier. Convert recipe batches to result units
        // for the ratio, then round each adjusted quantity up to a whole recipe batch.
        var amounts = members.Select(entry =>
        {
            int? legacy = GetLegacyBatches(entry);
            int units = recipeUnits != null && recipeUnits.TryGetValue(entry, out int output) ? Math.Max(1, output) : 1;
            return (Entry: entry, ResolvedLegacy: legacy.HasValue, UnitsPerBatch: units, Amount: (long)(legacy ?? entry.Quantity) * units);
        }).Where(member => member.Amount > 0).ToArray();
        if (amounts.Length == 0) return false;

        long common = amounts.Select(member => member.Amount).Aggregate(GreatestCommonDivisor);
        long multiplier = ShiftMultiplier(common, delta, 1);
        // Respect the saved-quantity bound without distorting the ratio near the limit.
        multiplier = Math.Min(multiplier, amounts.Min(member => (long)MaximumQuantity * member.UnitsPerBatch / (member.Amount / common)));
        foreach (var member in amounts)
        {
            long amount = member.Amount / common * multiplier;
            int quantity = (int)((amount + member.UnitsPerBatch - 1) / member.UnitsPerBatch);
            changed |= SetAdjustedQuantity(member.Entry, quantity, member.ResolvedLegacy);
        }
        return changed;

        int? GetLegacyBatches(Entry entry) => entry.LegacyRecipeQuantity && recipeBatches != null && recipeBatches.TryGetValue(entry, out int batches)
            ? batches : null;
    }

    private static long ShiftMultiplier(long multiplier, int delta, int minimum)
    {
        long step = Math.Abs((long)delta);
        long shifted = (multiplier + delta) / step * step;
        if (shifted <= 0 && multiplier > 1) shifted = 1;
        return Math.Max(minimum, shifted);
    }

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    private bool SetAdjustedQuantity(Entry entry, int quantity, bool resolveLegacy)
    {
        bool changed = SetQuantity(entry, quantity);
        if (!Entries.Contains(entry) || !resolveLegacy || !entry.LegacyRecipeQuantity) return changed;
        entry.LegacyRecipeQuantity = false;
        return true;
    }

    internal bool SetQuantity(Entry entry, int quantity)
    {
        int amount = Math.Clamp(quantity, 0, MaximumQuantity);
        if (!Entries.Contains(entry) || (entry.Quantity == amount && entry.ShowQuantity)) return false;
        entry.Quantity = amount;
        entry.ShowQuantity = true;
        return true;
    }

    internal bool SetRecipeProduct(Entry entry, string? productId)
    {
        if (!entry.IsRecipe || !Entries.Contains(entry) || entry.ProductId == productId ||
            (productId != null && !ValidText(productId, IdLimit))) return false;
        entry.ProductId = productId;
        return true;
    }

    internal bool SetShowRecipe(Entry entry, bool showRecipe)
    {
        if (!entry.IsRecipe || !Entries.Contains(entry) || entry.ShowRecipe == showRecipe) return false;
        entry.ShowRecipe = showRecipe;
        return true;
    }

    internal bool ToggleRecipeCollapsed(Entry entry)
    {
        if (!entry.IsRecipe || !IsChainEnabled(entry.Page, entry.Group) || !Entries.Contains(entry)) return false;
        entry.IsRecipeCollapsed = !entry.IsRecipeCollapsed;
        return true;
    }

    internal static HashSet<int> HiddenCollapsedDependencies(int count, ISet<int> folded,
        IEnumerable<(int Producer, int Consumer)> links, IEnumerable<int> resultIndices)
    {
        var producers = links.ToLookup(link => link.Consumer, link => link.Producer);
        var candidates = new HashSet<int>(folded);
        var pending = new Queue<int>(folded);
        while (pending.Count > 0)
            foreach (int producer in producers[pending.Dequeue()])
                if (candidates.Add(producer)) pending.Enqueue(producer);

        // Visible recipes keep their own transitive prerequisites. A collapsed recipe is
        // a visible anchor, but doesn't keep its upstream recipes visible by itself.
        var visible = Enumerable.Range(0, count).Where(index => !candidates.Contains(index) || folded.Contains(index))
            .Concat(resultIndices).ToHashSet();
        foreach (int index in visible) pending.Enqueue(index);
        while (pending.Count > 0)
        {
            int consumer = pending.Dequeue();
            if (folded.Contains(consumer)) continue;
            foreach (int producer in producers[consumer])
                if (visible.Add(producer)) pending.Enqueue(producer);
        }

        candidates.ExceptWith(visible);
        return candidates;
    }

    internal bool SetVariant(Entry entry, int slot, string? productId)
    {
        if (!entry.IsRecipe || !Entries.Contains(entry) || !ValidSlot(slot) || entry.Variant(slot) == productId) return false;
        if (productId == null) return entry.VariantChoices.Remove(slot);
        if (!ValidText(productId, IdLimit)) return false;
        int length = SlotRecordLength(slot, productId) + entry.Variants.Where(pair => pair.Key != slot).Sum(pair => SlotRecordLength(pair.Key, pair.Value));
        if (length > VariantLimit) return false;
        entry.VariantChoices[slot] = productId;
        return true;
    }

    internal bool SetIngredientOrder(Entry entry, IReadOnlyList<int> order)
    {
        if (!entry.IsRecipe || !Entries.Contains(entry) || order.Count > SlotLimit || !order.All(ValidSlot) ||
            order.Distinct().Count() != order.Count || order.SequenceEqual(entry.IngredientOrder)) return false;
        entry.IngredientOrder = order.ToArray();
        return true;
    }

    internal bool IsCompact(string page, string group = "") => GroupState(page, group).Compact;
    internal bool IsChainEnabled(string page, string group = "") => GroupState(page, group).Chain;
    internal bool IsCollapsed(string page, string group = "") => GroupState(page, group).Collapsed;

    internal bool ToggleCompact(string page, string group = "")
    {
        if (!CanChangeState(page, group)) return false;
        var state = GroupState(page, group);
        SetGroupState(page, group, new DisplayState(!state.Compact, state.Chain, state.Collapsed));
        return true;
    }

    internal bool ToggleCollapsed(string page, string group = "")
    {
        if (!CanChangeState(page, group)) return false;
        var state = GroupState(page, group);
        SetGroupState(page, group, new DisplayState(state.Compact, state.Chain, !state.Collapsed));
        return true;
    }

    internal bool AreAllChainsEnabled(string page) => IsChainEnabled(page) &&
        Page(page).Select(entry => entry.Group).Distinct().All(group => IsChainEnabled(page, group));

    internal bool ToggleChain(string page, string group = "")
    {
        if (!CanChangeState(page, group)) return false;
        var state = GroupState(page, group);
        SetGroupState(page, group, new DisplayState(state.Compact, !state.Chain, state.Collapsed));
        return true;
    }

    internal bool ToggleAllChains(string page)
    {
        if (!CanChangeState(page, "")) return false;
        bool enable = !AreAllChainsEnabled(page);
        foreach (string group in Page(page).Select(entry => entry.Group).Append("").Distinct())
        {
            var state = GroupState(page, group);
            SetGroupState(page, group, new DisplayState(state.Compact, enable, state.Collapsed));
        }
        return true;
    }

    private DisplayState GroupState(string page, string group)
    {
        if (group.Length == 0) return _pages.TryGetValue(page, out var state) ? state : default;
        return _groups.TryGetValue((page, group), out var explicitState) ? explicitState : default;
    }

    private void SetGroupState(string page, string group, DisplayState state)
    {
        if (group.Length == 0) _pages[page] = state;
        else _groups[(page, group)] = state;
    }

    private bool CanChangeState(string page, string group) => group.Length == 0 ? _pages.ContainsKey(page) || EnsurePage(page) :
        Page(page).Any(entry => entry.Group == group);

    // A normal drop joins the target group (creating one if needed). A separate drop inserts outside its entire bracket.
    internal bool Move(Entry source, Entry target, bool reorderOnly, bool before = false, bool separate = false)
    {
        if (source == target || source.Page != target.Page || !Entries.Contains(source) || !Entries.Contains(target)) return false;
        string page = source.Page, previousGroup = source.Group;
        bool create = !separate && !reorderOnly && target.Group.Length == 0;
        if (create)
        {
            target.Group = NewGroup();
            _groups.Add((page, target.Group), GroupState(page, target.Group));
        }
        source.Group = separate ? "" : target.Group;
        int previousIndex = Entries.IndexOf(source);
        Entries.RemoveAt(previousIndex);
        int index = separate ? GroupAnchor(target, before) : Entries.IndexOf(target);
        int insert = index + (before ? 0 : 1);
        Entries.Insert(insert, source);
        PruneGroup(page, previousGroup);
        return create || previousIndex != insert || previousGroup != source.Group;
    }

    internal bool Split(Entry entry)
    {
        if (entry.Group.Length == 0 || !Entries.Contains(entry)) return false;
        string group = entry.Group;
        var remaining = Page(entry.Page).Where(item => item.Group == group && item != entry).ToArray();
        entry.Group = "";
        if (remaining.Length > 0 && Entries.IndexOf(entry) > Entries.IndexOf(remaining[0]))
        {
            Entries.Remove(entry);
            Entries.Insert(Entries.IndexOf(remaining[remaining.Length - 1]) + 1, entry);
        }
        PruneGroup(entry.Page, group);
        return true;
    }

    // Dropping on empty space or on a neighboring page's arrow has no target slot.
    // Keep whole brackets ordered and remap their IDs when crossing page boundaries.
    internal bool DropAtEnd(Entry source, string page, bool entireGroup)
    {
        if (!Entries.Contains(source) || !ValidText(page, NameLimit) || (entireGroup && source.Group.Length == 0)) return false;
        string oldPage = source.Page, oldGroup = source.Group;
        if (!_pages.ContainsKey(page) && !EnsurePage(page)) return false;
        var moved = entireGroup ? Page(oldPage).Where(e => e.Group == oldGroup).ToArray() : new[] { source };
        if (oldPage == page && (entireGroup || oldGroup.Length == 0) && Page(page).LastOrDefault() == moved[^1]) return false;
        var state = GroupState(oldPage, oldGroup);
        string group = entireGroup ? oldPage == page ? oldGroup : NewGroup() : "";
        foreach (var entry in moved)
        {
            Entries.Remove(entry);
            entry.Page = page;
            entry.Group = group;
            Entries.Add(entry);
        }
        if (entireGroup) SetGroupState(page, group, state);
        PruneGroup(oldPage, oldGroup);
        return true;
    }

    internal bool MoveGroup(string page, string group, Entry target, bool before = false)
    {
        if (string.IsNullOrEmpty(group) || target.Page != page || target.Group == group || !Entries.Contains(target)) return false;
        var moved = Page(page).Where(entry => entry.Group == group).ToArray();
        if (moved.Length == 0) return false;
        var previous = Page(page).ToArray();
        Entries.RemoveAll(entry => entry.Page == page && entry.Group == group);
        Entries.InsertRange(GroupAnchor(target, before) + (before ? 0 : 1), moved);
        return !Page(page).SequenceEqual(previous);
    }

    private int GroupAnchor(Entry target, bool before)
    {
        if (target.Group.Length == 0) return Entries.IndexOf(target);
        return before ? Entries.FindIndex(item => item.Page == target.Page && item.Group == target.Group) :
            Entries.FindLastIndex(item => item.Page == target.Page && item.Group == target.Group);
    }

    internal bool Ungroup(string page, string group)
    {
        if (string.IsNullOrEmpty(group)) return false;
        bool changed = false;
        foreach (var entry in Page(page))
        {
            if (entry.Group != group) continue;
            entry.Group = "";
            changed = true;
        }
        if (changed) _groups.Remove((page, group));
        return changed;
    }

    internal bool GroupRange(Entry first, Entry last)
    {
        if (first.Page != last.Page) return false;
        var entries = Page(first.Page).ToArray();
        int start = Array.IndexOf(entries, first), end = Array.IndexOf(entries, last);
        if (start < 0 || end < 0) return false;
        if (start > end) (start, end) = (end, start);
        string group = entries[start].Group;
        if (group.Length > 0 && entries.Skip(start).Take(end - start + 1).All(entry => entry.Group == group) &&
            entries.Count(entry => entry.Group == group) == end - start + 1) return false;
        group = NewGroup();
        _groups.Add((first.Page, group), GroupState(first.Page, group));
        for (int i = start; i <= end; i++) entries[i].Group = group;
        NormalizeRuns(first.Page);
        return true;
    }

    // Both handles live in page order. A shrink keeps the target slot; dragging past the opposite end dissolves the group.
    internal bool ResizeGroup(string page, string group, Entry target, bool fromStart, bool remove = false)
    {
        if (string.IsNullOrEmpty(group) || target.Page != page) return false;
        var entries = Page(page).ToArray();
        int start = Array.FindIndex(entries, entry => entry.Group == group), end = Array.FindLastIndex(entries, entry => entry.Group == group);
        int index = Array.IndexOf(entries, target);
        if (start < 0 || index < 0) return false;
        if (remove)
        {
            if (fromStart ? index > end : index < start) return Ungroup(page, group);
            if (fromStart ? index <= start : index >= end) return false;
            for (int i = fromStart ? start : index + 1; i <= (fromStart ? index - 1 : end); i++) entries[i].Group = "";
        }
        else
        {
            if (fromStart ? index >= start : index <= end) return false;
            for (int i = fromStart ? index : end + 1; i <= (fromStart ? start - 1 : index); i++) entries[i].Group = group;
        }
        NormalizeRuns(page);
        return true;
    }

    // A selection may split an existing group in two. Keep the two brackets distinct without losing either group's flags.
    private void NormalizeRuns(string page)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string last = "", replacement = "";
        foreach (var entry in Page(page))
        {
            string group = entry.Group;
            if (group != last)
            {
                replacement = group;
                if (group.Length > 0 && !seen.Add(group))
                {
                    replacement = NewGroup();
                    _groups[(page, replacement)] = GroupState(page, group);
                }
            }
            entry.Group = replacement;
            last = group;
        }
        var live = Page(page).Select(entry => entry.Group).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _groups.Keys.Where(key => key.Page == page && !live.Contains(key.Group)).ToArray()) _groups.Remove(key);
    }

    private void PruneGroup(string page, string group)
    {
        if (group.Length > 0 && !Page(page).Any(entry => entry.Group == group)) _groups.Remove((page, group));
    }

    internal string CopyEntry(Entry entry) => Entries.Contains(entry) ? Serialize(new[] { entry }) : "";
    internal string CopyGroup(string page, string group)
    {
        var selected = Page(page).Where(entry => entry.Group == group).ToArray();
        if (selected.Length == 0) return "";
        return SerializeCore(selected, null, false, group.Length == 0 ? NewGroup() : null);
    }
    internal string CopyPage(string page) => PageNames.Contains(page) ? SerializeCore(Page(page), new[] { page }, false) : "";

    // v3 records: P (page state), G (group state), E (entry), F (favorite); structural fields never contain unescaped delimiters.
    internal string Serialize(IEnumerable<Entry>? entries = null) => entries == null
        ? SerializeCore(Entries, PageNames, true)
        : SerializeCore(entries, null, false);

    private string SerializeCore(IEnumerable<Entry> entries, IEnumerable<string>? pages, bool includeFavorites, string? sharedDefaultGroup = null)
    {
        var selected = entries.ToArray();
        var text = new StringBuilder(Format);
        foreach (string page in pages ?? Enumerable.Empty<string>())
            text.Append("|P:").Append(Escape(page)).Append(':').Append(Number(GroupState(page, "").Flags));
        foreach (var group in selected.Select(entry => (entry.Page, entry.Group)).Distinct())
        {
            string serializedGroup = group.Group.Length == 0 ? sharedDefaultGroup ?? "" : group.Group;
            if (serializedGroup.Length > 0)
                text.Append("|G:").Append(Escape(group.Page)).Append(':').Append(Escape(serializedGroup)).Append(':')
                    .Append(Number(GroupState(group.Page, group.Group).Flags));
        }
        foreach (var entry in selected)
        {
            int flags = (entry.ShowRecipe ? 1 : 0) | (entry.LegacyRecipeQuantity ? 2 : 0) | (entry.ShowQuantity ? 4 : 0) |
                (entry.IsRecipeCollapsed ? 8 : 0);
            string serializedGroup = entry.Group.Length == 0 ? sharedDefaultGroup ?? "" : entry.Group;
            text.Append("|E:").Append(Escape(entry.Page)).Append(':').Append(Escape(serializedGroup)).Append(':').Append(Escape(entry.Id))
                .Append(':').Append(Number(entry.Quantity)).Append(':').Append(Number(flags)).Append(':').Append(Escape(entry.ProductId ?? ""))
                .Append(':').Append(string.Join(",", entry.Variants.OrderBy(pair => pair.Key).Select(pair => Number(pair.Key) + "=" + Escape(pair.Value))))
                .Append(':').Append(string.Join(",", entry.IngredientOrder.Select(Number)));
        }
        if (includeFavorites)
        {
            foreach (string favorite in _favorites.OrderBy(id => id, StringComparer.Ordinal)) text.Append("|F:").Append(Escape(favorite));
            foreach (var pair in _favoriteRoutes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                text.Append("|F:").Append(Escape(pair.Value)).Append(':').Append(Escape(pair.Key));
        }
        return text.ToString();
    }

    // Save loading tolerates damaged individual records. Clipboard imports are validated atomically and never merge group IDs.
    internal bool Load(string? data, string? importToPage = null) => importToPage != null ? Import(data, importToPage) : Read(data, null, false);
    internal bool Import(string? data, string? targetPage = null) => Read(data, targetPage, true);

    private bool Read(string? data, string? targetPage, bool import)
    {
        if (string.IsNullOrWhiteSpace(data) || data!.Length > PayloadLimit || (targetPage != null && !ValidText(targetPage, NameLimit))) return false;
        string[] records = data.Split(new[] { '|' }, Limit * 5 + 2);
        if (records.Length > Limit * 5 + 1) return false;
        bool v3 = records[0] == Format, v2 = records[0] == PreviousFormat;
        if (import && !v3 && !v2) return false;
        var pages = new Dictionary<string, DisplayState>(StringComparer.Ordinal);
        var groups = new Dictionary<(string Page, string Group), DisplayState>();
        var favorites = new HashSet<string>(StringComparer.Ordinal);
        var routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<Entry>();
        var legacyKeys = new HashSet<(string Page, string Group, string Id)>();
        for (int i = v3 || v2 ? 1 : 0; i < records.Length; i++)
        {
            string[] fields = records[i].Split(new[] { ':' }, 10);
            bool valid;
            if (v3)
            {
                switch (fields[0])
                {
                    case "P":
                        valid = fields.Length == 3 && Decode(fields[1], NameLimit, out var page) && ReadState(fields[2], out var pageState) &&
                            pages.Count < Limit && pages.TryAdd(page, pageState);
                        break;
                    case "G":
                        valid = fields.Length == 4 && Decode(fields[1], NameLimit, out var groupPage) && Decode(fields[2], NameLimit, out var group) &&
                            ReadState(fields[3], out var groupState) && groups.Count < Limit && groups.TryAdd((groupPage, group), groupState);
                        break;
                    case "F":
                        valid = false;
                        if (fields.Length >= 2 && Decode(fields[1], IdLimit, out var favorite))
                        {
                            if (fields.Length == 2 && (IsRecipeKey(favorite) || IsMaterialKey(favorite)))
                                valid = favorites.Count < Limit && favorites.Add(favorite);
                            else if (!import && fields.Length == 3 && IsRecipeKey(favorite) && Decode(fields[2], IdLimit, out var product) && IsMaterialKey(product))
                                valid = routes.Count < Limit && routes.TryAdd(product, favorite);
                        }
                        break;
                    case "E":
                        var current = entries.Count < Limit ? ReadEntry(fields) : null;
                        valid = current != null;
                        if (current != null) entries.Add(current);
                        break;
                    default:
                        valid = false;
                        break;
                }
            }
            else
            {
                var legacy = ReadLegacy(fields, v2, import);
                valid = legacy != null && entries.Count < Limit;
                if (legacy != null && valid && legacyKeys.Add((legacy.Page, legacy.Group, legacy.Id))) entries.Add(legacy);
            }
            if (import && !valid) return false;
        }

        var liveGroups = entries.Where(entry => entry.Group.Length > 0).Select(entry => (entry.Page, entry.Group)).ToHashSet();
        if (import && (groups.Keys.Any(key => !liveGroups.Contains(key)) || Entries.Count + entries.Count > Limit ||
            _favorites.Count + favorites.Count(key => !_favorites.Contains(key)) > Limit)) return false;
        var sourcePages = pages.Keys.Concat(entries.Select(entry => entry.Page)).Distinct(StringComparer.Ordinal).ToArray();
        var targetPages = sourcePages.Select(page => targetPage ?? page).Distinct(StringComparer.Ordinal).ToArray();
        if (import && _pages.Count + targetPages.Count(page => !_pages.ContainsKey(page)) > Limit) return false;

        bool changed = false;
        var pageStatesApplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (string source in sourcePages)
        {
            string page = targetPage ?? source;
            changed |= EnsurePage(page);
            if (!_pages.ContainsKey(page) || !pages.TryGetValue(source, out var state) || !pageStatesApplied.Add(page)) continue;
            changed |= GroupState(page, "").Flags != state.Flags;
            SetGroupState(page, "", state);
        }
        foreach (string favorite in favorites)
            if (_favorites.Count < Limit) changed |= _favorites.Add(favorite);
        foreach (var pair in routes)
            if (_favoriteRoutes.Count < Limit) changed |= _favoriteRoutes.TryAdd(pair.Key, pair.Value);

        var replacements = new Dictionary<(string Page, string Group), string>();
        var seenLegacy = !v3 ? Entries.Select(entry => (entry.Page, entry.Group, entry.Id)).ToHashSet() : null;
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (Entries.Count >= Limit) break;
            string page = targetPage ?? entry.Page;
            if (!_pages.ContainsKey(page)) continue;
            if (entry.Group.Length > 0)
            {
                var source = (entry.Page, entry.Group);
                if (import)
                {
                    if (!replacements.TryGetValue(source, out var replacement)) replacements.Add(source, replacement = NewGroup());
                    entry.Group = replacement;
                }
                if (groups.TryGetValue(source, out var state)) SetGroupState(page, entry.Group, state);
                else _groups.TryAdd((page, entry.Group), GroupState(page, entry.Group));
            }
            entry.Page = page;
            if (seenLegacy != null && !seenLegacy.Add((page, entry.Group, entry.Id))) continue;
            Entries.Add(entry);
            touched.Add(page);
            changed = true;
        }
        foreach (string page in touched) RestoreGroupOrder(page);
        return changed;
    }

    // Old saves could interleave group members with ungrouped entries. Keep each group together at its first saved occurrence.
    private void RestoreGroupOrder(string page)
    {
        var entries = Page(page).ToArray();
        var groups = entries.Where(entry => entry.Group.Length > 0).ToLookup(entry => entry.Group);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new Queue<Entry>();
        foreach (var entry in entries)
            if (entry.Group.Length == 0) ordered.Enqueue(entry);
            else if (seen.Add(entry.Group)) foreach (var member in groups[entry.Group]) ordered.Enqueue(member);
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].Page == page) Entries[i] = ordered.Dequeue();
    }

    private static Entry? ReadEntry(string[] fields)
    {
        if (fields.Length != 9 || !Decode(fields[1], NameLimit, out var page) || !Decode(fields[2], NameLimit, out var group, true) ||
            !Decode(fields[3], IdLimit, out var id) || !ValidId(id) || !TryNumber(fields[4], out int quantity) ||
            quantity > MaximumQuantity || !TryNumber(fields[5], out int flags) || flags > 15 || !Decode(fields[6], IdLimit, out var product, true)) return null;
        var entry = new Entry(page, group, id, quantity)
        {
            ShowRecipe = (flags & 1) != 0,
            LegacyRecipeQuantity = (flags & 2) != 0,
            ShowQuantity = (flags & 4) != 0,
            IsRecipeCollapsed = (flags & 8) != 0,
            ProductId = product.Length == 0 ? null : product
        };
        if (!entry.IsRecipe && ((flags & 11) != 0 || product.Length != 0 || fields[7].Length != 0 || fields[8].Length != 0)) return null;
        if (fields[7].Length > VariantLimit || fields[8].Length > SlotLimit * 4) return null;
        if (fields[7].Length > 0)
            foreach (string choice in fields[7].Split(','))
            {
                var parts = choice.Split('=');
                if (parts.Length != 2 || !TryNumber(parts[0], out int index) || !ValidSlot(index) || !Decode(parts[1], IdLimit, out var variant) ||
                    !entry.VariantChoices.TryAdd(index, variant)) return null;
            }
        if (fields[8].Length > 0)
        {
            var slots = new List<int>();
            var seen = new HashSet<int>();
            foreach (string slot in fields[8].Split(','))
            {
                if (!TryNumber(slot, out int index) || !ValidSlot(index) || !seen.Add(index)) return null;
                slots.Add(index);
            }
            entry.IngredientOrder = slots.ToArray();
        }
        return entry;
    }

    private static Entry? ReadLegacy(string[] fields, bool v2, bool strict)
    {
        if (fields.Length != (v2 ? 4 : 2) || !Decode(fields[0], NameLimit, out var page) ||
            !Decode(fields[v2 ? 2 : 1], IdLimit, out var id) || !ValidId(id)) return null;
        string group = "";
        int quantity = 1;
        if (v2)
        {
            if (!Decode(fields[1], NameLimit, out group, true)) return null;
            if (TryNumber(fields[3], out int amount) && (!strict || (amount >= 1 && amount <= MaximumQuantity)))
                quantity = Math.Clamp(amount, 1, MaximumQuantity);
            else if (strict) return null;
        }
        return new Entry(page, group, id, quantity) { LegacyRecipeQuantity = IsRecipeKey(id) };
    }

    private static bool ReadState(string text, out DisplayState state)
    {
        bool valid = TryNumber(text, out int flags) && flags <= 7;
        state = valid ? new DisplayState((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0) : default;
        return valid;
    }

    private static bool IsRecipeKey(string id) => id.Length > RecipePrefix.Length && id.StartsWith(RecipePrefix, StringComparison.Ordinal);
    private static bool ValidId(string id) => ValidText(id, IdLimit) && id != RecipePrefix;
    private static bool TryRecipeKey(string recipe, out string key)
    {
        key = "";
        if (!ValidText(recipe, IdLimit)) return false;
        key = recipe.StartsWith(RecipePrefix, StringComparison.Ordinal) ? recipe : RecipePrefix + recipe;
        return IsRecipeKey(key) && key.Length <= IdLimit;
    }

    private static string NewGroup() => Guid.NewGuid().ToString("N");
    private static bool ValidSlot(int slot) => slot >= 0 && slot < SlotLimit;
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static bool TryNumber(string value, out int number) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    private static string Escape(string text) => Uri.EscapeDataString(text);
    private static int SlotRecordLength(int slot, string productId) => Number(slot).Length + Escape(productId).Length + 2;

    private static bool ValidText(string? text, int limit, bool empty = false)
    {
        if (text == null || text.Length > limit || (!empty && string.IsNullOrWhiteSpace(text))) return false;
        if (empty && text.Length > 0 && string.IsNullOrWhiteSpace(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsControl(text[i]) || char.IsLowSurrogate(text[i])) return false;
            if (char.IsHighSurrogate(text[i]) && (++i >= text.Length || !char.IsLowSurrogate(text[i]))) return false;
        }
        return true;
    }

    private static bool Decode(string field, int limit, out string value, bool empty = false)
    {
        value = "";
        if (field.Length > limit * 9) return false;
        // Uri.UnescapeDataString is deliberately permissive; reject bad percent triplets and invalid UTF-8 first.
        int start = field.IndexOf('%');
        if (start >= 0)
        {
            var bytes = new byte[field.Length / 3];
            try
            {
                for (int i = start; i < field.Length; i++)
                {
                    if (field[i] != '%') continue;
                    int count = 0;
                    do
                    {
                        if (i + 2 >= field.Length || !byte.TryParse(field.Substring(i + 1, 2), NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture, out bytes[count])) return false;
                        count++; i += 3;
                    } while (i < field.Length && field[i] == '%');
                    StrictUtf8.GetCharCount(bytes, 0, count);
                    i--;
                }
            }
            catch (DecoderFallbackException) { return false; }
        }
        value = Uri.UnescapeDataString(field);
        return ValidText(value, limit, empty);
    }
}
