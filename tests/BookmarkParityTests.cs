using System;
using System.Collections.Generic;
using System.Linq;
using NotEnoughItems;
#if RECIPE_TESTS
using System.Collections;
using System.Reflection;
using ProviderRecipe = NotEnoughItems.Recipe;
#endif

internal static class BookmarkParityTests
{
    internal static void Run()
    {
        Action[] checks =
        [
            GroupDropsRespectBrackets,
            RangeAndResizePreserveBoundaries,
            DisplayStateIsScoped,
            RecipeFoldKeepsSharedDependenciesVisible,
            GroupQuantityChangesAreScoped,
            LegacyGroupedSaveMigrates,
            RecipeStateRoundTrips,
            ClipboardScopesAndRemapsGroups,
            BadClipboardIsAtomic,
#if RECIPE_TESTS
            OrdinaryAndGroupedToolsFollowCarriedStock,
            GroupBalanceStaysWithinSavedRecipes,
            GroupBalanceReusesProductionAndTools,
            GroupBalanceKeepsConditionAndTypeBoundaries,
            VariantSelectionKeepsSlotsAndTypes,
            ProductionUsesSelectedEntryAndCarriedStock,
            ProductionKeepsVariantsOfDuplicateRecipes,
            ProductionDoesNotDoubleCountStock,
            CircularProductionReportsCycle,
#endif
        ];

        var failures = new List<Exception>();
        foreach (Action check in checks)
        {
            try { check(); }
            catch (Exception error) { failures.Add(new InvalidOperationException(check.Method.Name, error)); }
        }
        if (failures.Count > 0) throw new AggregateException("Bookmark parity checks failed.", failures);
    }

    private static void GroupDropsRespectBrackets()
    {
        var book = new BookmarkBook();
        var a = Add(book, "page", "item:a", "first");
        var otherPage = Add(book, "other", "item:other", "first");
        var b = Add(book, "page", "item:b", "first");
        var c = Add(book, "page", "item:c", "second");
        var d = Add(book, "page", "item:d", "second");
        var loose = Add(book, "page", "item:loose");

        Check(book.Move(loose, b, reorderOnly: false, before: true, separate: true), "Drop before the bracket.");
        PageOrder(book, "page", loose, a, b, c, d);
        Check(loose.Group == "", "An outside drop keeps the entry ungrouped.");
        Check(book.Move(loose, c, reorderOnly: false, separate: true), "Drop after the bracket.");
        PageOrder(book, "page", a, b, c, d, loose);
        Check(book.MoveGroup("page", "first", d, before: true) == false, "A group already at the requested boundary stays put.");
        Check(book.MoveGroup("page", "first", c), "Move the first bracket past the entire second bracket.");
        PageOrder(book, "page", c, d, a, b, loose);
        Check(book.Move(loose, a, reorderOnly: false), "A regular drop joins the target bracket.");
        Check(loose.Group == "first", "The joined entry uses the existing group ID.");
        PageOrder(book, "page", c, d, a, loose, b);
        Check(book.Split(loose), "Split the middle member out of its group.");
        PageOrder(book, "page", c, d, a, b, loose);
        Check(loose.Group == "" && a.Group == b.Group, "Splitting moves the entry outside the remaining bracket.");
        Check(!book.Move(a, otherPage, reorderOnly: false) && !book.MoveGroup("page", "first", otherPage), "Moves stay on their source page.");
        Check(book.RemoveGroup("page", "first"), "Remove both members of one bracket.");
        PageOrder(book, "page", c, d, loose);
        PageOrder(book, "other", otherPage);
    }

    private static void RangeAndResizePreserveBoundaries()
    {
        var book = new BookmarkBook();
        var left = Add(book, "page", "item:left");
        var a = Add(book, "page", "item:a", "original");
        var b = Add(book, "page", "item:b", "original");
        var c = Add(book, "page", "item:c", "original");
        var d = Add(book, "page", "item:d", "original");
        var right = Add(book, "page", "item:right");
        Check(book.ToggleCompact("page", "original") && book.ToggleChain("page", "original"), "Set the original group's independent flags.");

        Check(book.GroupRange(c, b), "Reverse endpoints select the same inclusive range.");
        string middle = b.Group;
        Check(middle != "" && middle == c.Group && a.Group != middle && d.Group != middle && a.Group != d.Group,
            "Stealing the middle of a group leaves two separate brackets.");
        foreach (string group in new[] { a.Group, d.Group })
            Check(book.IsCompact("page", group) && book.IsChainEnabled("page", group), "Both remnants keep the original display and chain flags.");
        Check(!book.GroupRange(b, c), "Selecting exactly the current group is a no-op.");

        Check(book.ResizeGroup("page", middle, left, fromStart: true), "Extend the start across a neighboring group.");
        Check(left.Group == middle && a.Group == middle && b.Group == middle && c.Group == middle && d.Group != middle,
            "Only the slots inside the extended bracket are absorbed.");
        Check(book.ResizeGroup("page", middle, right, fromStart: false), "Extend the end through the last slot.");
        Check(d.Group == middle && right.Group == middle, "End extension includes the dropped-on slot.");
        Check(book.ResizeGroup("page", middle, b, fromStart: true, remove: true), "Shrink the start to the target slot.");
        Check(left.Group == "" && a.Group == "" && b.Group == middle, "Start shrink releases only preceding members.");
        Check(book.ResizeGroup("page", middle, c, fromStart: false, remove: true), "Shrink the end to the target slot.");
        Check(c.Group == middle && d.Group == "" && right.Group == "", "End shrink releases only following members.");
        Check(book.ResizeGroup("page", middle, d, fromStart: true, remove: true), "Pulling a handle past the opposite end dissolves the group.");
        Check(book.Page("page").All(entry => entry.Group == "") && !book.ToggleCompact("page", middle), "Dissolving leaves no orphan group state or members.");
        PageOrder(book, "page", left, a, b, c, d, right);
    }

    private static void DisplayStateIsScoped()
    {
        var book = new BookmarkBook();
        var a = Add(book, "page", "item:shared", "a");
        var b = Add(book, "page", "item:shared", "b");
        Add(book, "other", "item:shared", "a");
        Check(book.ToggleCompact("page", "a") && book.ToggleChain("page", "a"), "Change the first bracket independently.");
        Check(book.IsCompact("page", "a") && book.IsChainEnabled("page", "a") && !book.IsCompact("page") &&
            !book.IsChainEnabled("page") && !book.IsChainEnabled("page", "b") && !book.IsChainEnabled("other", "a"),
            "Page lanes, sibling brackets, and equal IDs on other pages have independent settings.");
        Check(book.ToggleChain("page") && book.IsChainEnabled("page") && !book.IsChainEnabled("page", "b"),
            "The default lane's toggle only changes that lane.");
        Add(book, "page", "item:later", "new");
        Check(!book.IsChainEnabled("page", "new") && !book.IsCompact("page", "new"), "New brackets have their own default display state.");
        Check(book.ToggleAllChains("page") && book.AreAllChainsEnabled("page"), "The aggregate toggle enables each lane on a mixed page.");
        Check(book.ToggleCollapsed("page", "a") && book.IsCollapsed("page", "a") &&
            !book.IsCollapsed("page") && !book.IsCollapsed("page", "b") && !book.IsCollapsed("other", "a"),
            "Collapse is scoped to the selected bracket.");
        Check(book.ToggleAllChains("page") && !book.IsChainEnabled("page", "a") && !book.IsChainEnabled("page", "new") &&
            book.IsCompact("page", "a") && book.IsCollapsed("page", "a"), "Disabling all page chains preserves compact and collapsed settings.");
        Check(book.Toggle("page", a.Id, 1, "a") && book.Find("page", a.Id, "a") == null && book.Find("page", b.Id, "b") == b,
            "Scoped toggle removes only the matching duplicate.");
        Add(book, "page", a.Id, "a");
        Check(!book.IsCompact("page", "a"), "Removing the last member discards stale group settings.");
    }

    private static void RecipeFoldKeepsSharedDependenciesVisible()
    {
        var book = new BookmarkBook();
        var first = Add(book, "page", "recipe/api/first", "chain");
        var second = Add(book, "page", "recipe/api/second", "chain");
        var plain = Add(book, "page", "item:plain", "chain");
        Check(!book.ToggleRecipeCollapsed(first), "Recipe folding requires an enabled chain.");
        Check(book.ToggleChain("page", "chain") && book.ToggleRecipeCollapsed(first) && first.IsRecipeCollapsed,
            "Folding marks the selected recipe on the enabled chain.");
        Check(!book.ToggleRecipeCollapsed(plain) && !second.IsRecipeCollapsed && !book.IsCollapsed("page", "chain"),
            "Folding leaves plain entries, neighboring recipes, and the entire group unchanged.");

        (int Producer, int Consumer)[] links = [(2, 0), (2, 1), (3, 2), (4, 0)];
        int[] results = [0, 1];
        var folded = new HashSet<int> { 0 };
        Check(BookmarkBook.HiddenCollapsedDependencies(5, folded, links, results).SetEquals([4]),
            "The shared producer and its transitive input remain visible through the second result.");
        folded.Add(1);
        Check(BookmarkBook.HiddenCollapsedDependencies(5, folded, links, results).SetEquals([2, 3, 4]),
            "Folding both consumers hides all upstream entries while keeping both result anchors visible.");
        Check(book.ToggleRecipeCollapsed(first) && !first.IsRecipeCollapsed, "Toggling again unfolds the selected recipe.");
    }

    private static void GroupQuantityChangesAreScoped()
    {
        var book = new BookmarkBook();
        var root = Add(book, "page", "recipe/api/root", "chain");
        var intermediate = Add(book, "page", "recipe/api/intermediate", "chain");
        var raw = Add(book, "page", "item:raw", "chain");
        var neighbor = Add(book, "page", "recipe/api/neighbor", "other");
        var plain = Add(book, "page", "item:loose");
        Check(book.SetQuantity(root, 6) && book.SetQuantity(intermediate, 2), "Set the saved recipe amounts.");
        root.LegacyRecipeQuantity = true;
        Check(book.ToggleChain("page", "chain"), "Enable the selected chain.");
        var resolvedBatches = new Dictionary<BookmarkBook.Entry, int> { [root] = 2, [intermediate] = 2 };
        var targets = new HashSet<BookmarkBook.Entry> { root };

        Check(book.AdjustGroup("page", "chain", 1, resolvedBatches, targets), "Increase the marked target by one batch.");
        Check(root.Quantity == 3 && !root.LegacyRecipeQuantity && intermediate.Quantity == 2 && raw.Quantity == 1 &&
            neighbor.Quantity == 1 && plain.Quantity == 1, "Chain adjustment converts legacy units and changes only the selected result recipe.");
        Check(book.SetQuantity(root, 2) && book.AdjustGroup("page", "chain", 4, chainTargets: targets) && root.Quantity == 4 &&
            book.AdjustGroup("page", "chain", 4, chainTargets: targets) && root.Quantity == 8 &&
            book.AdjustGroup("page", "chain", -4, chainTargets: targets) && root.Quantity == 4,
            "Chain targets advance to the next multiple of the scroll step in either direction.");
        Check(book.AdjustGroup("page", "chain", int.MaxValue, chainTargets: targets) && root.Quantity == BookmarkBook.MaximumQuantity &&
            book.AdjustGroup("page", "chain", int.MinValue, chainTargets: targets) && root.Quantity == 1 &&
            book.AdjustGroup("page", "chain", -1, chainTargets: targets) && root.Quantity == 0 && root.ShowQuantity,
            "Large changes clamp safely, with a separate decrement from one to an explicit zero.");
        Check(book.ToggleChain("page", "chain") && book.SetQuantity(raw, 4), "Set up an ordinary bracket with a 2:4 ratio and an explicit zero.");
        Check(book.AdjustGroup("page", "chain", 1) && root.Quantity == 0 && intermediate.Quantity == 3 && raw.Quantity == 6,
            "Increasing the ordinary bracket scales its positive amounts proportionally and preserves the explicit zero.");
        Check(book.AdjustGroup("page", "chain", -1) && root.Quantity == 0 && intermediate.Quantity == 2 && raw.Quantity == 4 &&
            neighbor.Quantity == 1 && plain.Quantity == 1, "Decreasing restores the original ratio and leaves neighboring bookmarks unchanged.");
        Check(book.SetQuantity(raw, 6), "Set up equal displayed amounts when a recipe yields three units per batch.");
        var units = new Dictionary<BookmarkBook.Entry, int> { [intermediate] = 3 };
        Check(book.AdjustGroup("page", "chain", 1, recipeUnits: units) && raw.Quantity == 7 && intermediate.Quantity == 3,
            "The ratio uses product units and rounds the adjusted recipe quantity up to a whole batch.");
    }

    private static void LegacyGroupedSaveMigrates()
    {
        const string oldSave = "nei2|work:g:recipe%2Fapi%2Fgear:6|work::item%3Aloose:bad|" +
            "work:g:item%3Aore:20000|work:g:item%3Aore:4|work::item%3Abad%FF:2";
        var book = new BookmarkBook();
        Check(book.Load(oldSave), "Read playable records from a nei2 save containing damage.");
        var entries = book.Page("work").ToArray();
        Check(entries.Select(entry => entry.Id).SequenceEqual(new[] { "recipe/api/gear", "item:ore", "item:loose" }),
            "Migration restores the bracket at its first occurrence and drops the duplicate and invalid UTF-8 record.");
        Check(entries[0] is { Quantity: 6, ShowRecipe: true, ShowQuantity: true, LegacyRecipeQuantity: true } &&
            entries[1].Quantity == BookmarkBook.MaximumQuantity && entries[2].Quantity == 1,
            "Keep legacy recipe-unit semantics and recover malformed or out-of-range quantities.");
        var restored = new BookmarkBook();
        Check(restored.Load(book.Serialize()) && restored.Find("work", "recipe/api/gear") is { LegacyRecipeQuantity: true, Quantity: 6 },
            "Saving after migration retains the marker needed to resolve old recipe amounts.");
    }

    private static void RecipeStateRoundTrips()
    {
        const string page = "炉:料|%😀";
        const string group = "组:0|%";
        const string key = "recipe/api/press:01|%";
        const string product = "liquid:oil|%";
        const string variant = "item:滤布,=|%";
        var book = new BookmarkBook();
        Check(book.ToggleRecipe(page, key, 1, showRecipe: false, productId: product, group: group, showQuantity: true), "Save a product-only recipe with a visible amount of one.");
        var recipe = book.Find(page, key, group) ?? throw new InvalidOperationException("Expected the recipe bookmark.");
        Check(book.SetVariant(recipe, 127, variant) && book.SetVariant(recipe, 0, "item:fiber"), "First and last supported ingredient slots accept variants.");
        Check(!book.SetVariant(recipe, 128, variant) && !book.SetIngredientOrder(recipe, [0, 0]), "Reject an out-of-bounds slot and duplicate ingredient order.");
        Check(book.SetIngredientOrder(recipe, [127, 0]), "Persist a sparse, reordered slot list.");
        Check(book.ToggleFavorite(key) && book.IsFavorite("api/press:01|%"), "Favorites normalize the optional recipe prefix.");
        Check(book.ToggleCompact(page) && book.ToggleChain(page, group) && book.ToggleCollapsed(page, group) && book.EnsurePage("empty"),
            "Persist page and group flags and an empty page.");
        Check(book.ToggleRecipeCollapsed(recipe), "Persist the recipe's independent fold state.");
        var plain = Add(book, page, "item:plain");
        Check(book.Adjust(plain, int.MaxValue) && plain.Quantity == BookmarkBook.MaximumQuantity &&
            book.Adjust(plain, int.MinValue) && plain.Quantity == 0 && plain.ShowQuantity, "Quantity adjustments clamp safely at both limits.");

        string data = book.Serialize();
        var restored = new BookmarkBook();
        Check(data.StartsWith("nei3|", StringComparison.Ordinal) && restored.Load(data), "Round-trip the current save format.");
        var entry = restored.Find(page, key, group);
        Check(entry is { IsRecipe: true, Quantity: 1, ShowRecipe: false, ShowQuantity: true, LegacyRecipeQuantity: false } && entry.ProductId == product,
            "The current format restores product identity and recipe and quantity display independently.");
        Check(entry!.Variant(127) == variant && entry.Variant(0) == "item:fiber" && entry.IngredientOrder.SequenceEqual(new[] { 127, 0 }),
            "Escaped variant IDs and ingredient order round-trip independently.");
        Check(restored.Find(page, plain.Id) is { Quantity: 0, ShowQuantity: true }, "An explicit zero amount survives a save round-trip.");
        Check(restored.IsFavorite(key) && restored.PageNames.Contains("empty") && restored.IsCompact(page) &&
            !restored.IsCompact(page, group) && !restored.IsChainEnabled(page) && restored.IsChainEnabled(page, group) &&
            restored.IsCollapsed(page, group) && !restored.IsCollapsed(page), "Full saves restore favorites, empty pages, and independent flags.");
        Check(restored.Serialize() == data, "The save payload is stable after a full round-trip.");
    }

    private static void ClipboardScopesAndRemapsGroups()
    {
        var source = new BookmarkBook();
        var one = Add(source, "source", "item:one", "same-id");
        Add(source, "source", "item:two", "same-id");
        Add(source, "source", "item:loose");
        Add(source, "other", "item:other", "same-id");
        Check(source.ToggleCompact("source", "same-id") && source.ToggleChain("source", "same-id") && source.ToggleFavorite("api/favorite"),
            "Set source metadata for scope checks.");
        Check(source.EnsurePage("empty") && source.ToggleCompact("empty"), "An empty page still has clipboard state.");
        Check(source.ToggleChain("source") && source.ToggleCollapsed("source"), "Set default-lane metadata separately.");

        var dest = new BookmarkBook();
        var original = Add(dest, "dest", "item:original", "same-id");
        Check(dest.ToggleCompact("dest") && dest.ToggleCompact("single"), "Destination pages have their own settings before paste.");
        Check(dest.Import(source.CopyGroup("source", "same-id"), "dest") && dest.Import(source.CopyGroup("source", "same-id"), "dest"), "Repeat-paste the same bracket onto a page with an existing matching group ID.");
        var groups = dest.Page("dest").Where(entry => entry != original).GroupBy(entry => entry.Group).ToArray();
        Check(groups.Length == 2 && groups.All(group => group.Key.Length > 0 && group.Key != original.Group &&
            group.Select(entry => entry.Id).SequenceEqual(new[] { "item:one", "item:two" }) &&
            dest.IsCompact("dest", group.Key) && dest.IsChainEnabled("dest", group.Key)), "Each paste preserves membership, order, and flags under its own group ID.");
        Check(dest.IsCompact("dest") && !dest.IsCompact("dest", original.Group) && !dest.IsFavorite("api/favorite"),
            "Group copies preserve destination page and group settings and exclude favorites.");
        Check(dest.Import(source.CopyEntry(one), "single") && dest.Page("single").Single().Id == one.Id && dest.IsCompact("single"),
            "Entry copy includes exactly one entry and preserves destination page settings.");
        Check(dest.Import(source.CopyGroup("source", ""), "dest"), "Copy the default lane as its own bracket.");
        var copiedDefault = dest.Find("dest", "item:loose");
        Check(copiedDefault is { Group.Length: > 0 } && dest.IsChainEnabled("dest", copiedDefault.Group) &&
            dest.IsCollapsed("dest", copiedDefault.Group) && !dest.IsChainEnabled("dest") && !dest.IsCollapsed("dest"),
            "The default lane's copied settings belong to the imported bracket.");
        Check(dest.Import(source.CopyPage("source")) && dest.Page("source").Count() == 3 && !dest.PageNames.Contains("other") &&
            dest.IsChainEnabled("source") && dest.IsCollapsed("source"), "Page copy preserves its default lane and excludes neighboring pages.");
        Check(dest.Import(source.CopyPage("empty")) && dest.IsCompact("empty") && !dest.Page("empty").Any(), "An empty page can be pasted with its display state.");
        Check(dest.Import("nei2|v2:g:item%3Av2:3", "imported-v2") && dest.Find("imported-v2", "item:v2") is { Quantity: 3 }, "Clipboard remains compatible with nei2.");
    }

    private static void BadClipboardIsAtomic()
    {
        var book = new BookmarkBook();
        Add(book, "existing", "item:existing", "kept");
        string before = book.Serialize();
        const string validPrefix = "nei3|P:existing:3|F:recipe%2Fapi%2Ftest|E:existing::item%3Avalid:1:0:::";
        foreach (string invalid in new[]
        {
            "|E:existing::item%3Abad%:1:0:::",
            "|E:existing::item%3Abad%C0%AF:1:0:::",
            "|E:existing::item%3Abad%0A:1:0:::",
            "|E:existing::recipe%2Fapi%2Ftest:1:1::0=item%3Aa,0=item%3Ab:",
            "|E:existing::recipe%2Fapi%2Ftest:1:1:::0,0",
            "|E:existing::item%3Ainvalid:10000:0:::",
            "|E:existing::item%3Anegative:-1:4:::",
            "|G:existing:orphan:2"
        })
        {
            Check(!book.Import(validPrefix + invalid, "existing"), $"Reject damaged clipboard suffix {invalid}.");
            Check(book.Serialize() == before, "An invalid clipboard does not append entries, favorites, or overwrite page flags.");
        }
        Check(!book.Import("nei2|existing::item%3Avalid:2|existing::item%3Abad:0") && book.Serialize() == before,
            "Out-of-range v2 clipboard amounts are rejected atomically.");
        Check(!book.Import("existing:item%3Alegacy") && !book.Import(null) && book.Serialize() == before, "Clipboard requires an explicit supported format.");
        var recovered = new BookmarkBook();
        Check(recovered.Load(validPrefix + "|broken") && recovered.Find("existing", "item:valid") != null,
            "Save loading still recovers intact records around damage.");
    }

#if RECIPE_TESTS
    private static void GroupBalanceStaysWithinSavedRecipes()
    {
        var catalog = new Catalog();
        var outside = Recipe("outside", [], [new RecipeOutput("core", 20)]);
        catalog.RegisterRecipe(outside);
        var coreUser = Recipe("core-user", [new RecipeIngredient("core", 2)], [new RecipeOutput("frame", 3)]);
        var entries = Entries(
            BalanceEntry("item:frame", 2, coreUser),
            BalanceEntry("item:core"),
            BalanceEntry("recipe/api/unresolved"));
        var stock = new Dictionary<string, int> { ["ITEM:CORE"] = 1, ["item:unrelated"] = 9 };

        object plan = Calculate(catalog, entries, stock);

        var step = Rows(plan, "Steps").Single();
        Check(Property<ProviderRecipe>(step, "Recipe") == coreUser && Property<int>(step, "Batches") == 2,
            "Carried stock annotates the group bill without changing its saved batch amount or pulling producers out of the catalog.");
        Amount(plan, "Results", "item:frame", 6);
        Amount(plan, "Inputs", "item:core", 4);
        Amount(plan, "Inputs", "item:core", 2, "AvailableQuantity");
        Amount(plan, "Missing", "item:core", 2);
        Check(Rows(plan, "Surplus").Length == 0 && HasDiagnostic(plan, "MissingRecipe"),
            "Unrelated stock contributes no surplus, and an unresolved recipe contributes a diagnostic rather than stock.");
    }

    private static void GroupBalanceReusesProductionAndTools()
    {
        var smelt = Recipe("smelt", [new RecipeIngredient("ore", 2)], [new RecipeOutput("ingot", 3), new RecipeOutput("slag", 2)]);
        var gear = Recipe("gear", [new RecipeIngredient("ingot", 2), new RecipeIngredient("tool", isCatalyst: true),
            new RecipeIngredient("tool", isCatalyst: true)], [new RecipeOutput("gear")]);
        var brick = Recipe("brick", [new RecipeIngredient("slag"), new RecipeIngredient("tool", isCatalyst: true)], [new RecipeOutput("brick")]);
        var entries = Entries(BalanceEntry("item:gear", 2, gear), BalanceEntry("item:brick", 1, brick), BalanceEntry("item:ingot", 1, smelt));

        object plan = Calculate(new Catalog(), entries, new Dictionary<string, int> { ["tool"] = 1, ["ore"] = 1 });

        var steps = Rows(plan, "Steps");
        Check(steps.Select(step => Property<ProviderRecipe>(step, "Recipe")).SequenceEqual(new[] { smelt, gear, brick }),
            "The shared producer precedes its consumers even when saved last.");
        Check(Property<int>(steps[0], "Batches") == 2, "Two whole smelt batches cover four ingots and the coproduct requirement.");
        Amount(plan, "Results", "item:gear", 2);
        Amount(plan, "Results", "item:brick", 1);
        Amount(plan, "Surplus", "item:ingot", 2);
        Amount(plan, "Surplus", "item:slag", 3);
        Amount(plan, "Inputs", "item:ore", 4);
        Amount(plan, "Missing", "item:ore", 3);
        Amount(plan, "Inputs", "item:tool", 2);
        Amount(plan, "Missing", "item:tool", 1);
        Check(Property<bool>(Material(plan, "Inputs", "item:tool"), "IsCatalyst") &&
            Rows(plan, "Inputs").Length == 2, "Two retained tool slots require separate copies, reusable across batches and other steps.");
        Amount(steps[1], "Inputs", "item:ingot", 4);
        Amount(steps[1], "Inputs", "item:ingot", 0, "NetQuantity");
        Amount(steps[0], "Outputs", "item:slag", 4);
        Amount(steps[0], "Outputs", "item:slag", 3, "NetQuantity");
        Check(Rows(plan, "Links").Any(link => Property<int?>(link, "ProducerEntryIndex") == 2 &&
            Property<int?>(link, "ConsumerEntryIndex") == 0 && Property<string>(link, "ItemId") == "item:ingot" &&
            Property<double>(link, "Quantity") == 4), "Tree and folded subchains receive actual allocations, not catalog guesses.");
    }

    private static void GroupBalanceKeepsConditionAndTypeBoundaries()
    {
        var lowGrade = Recipe("low-grade", [], [new RecipeOutput("metal", condition: .25f)]);
        var liquid = Recipe("liquid-metal", [], [new RecipeOutput("metal", isLiquid: true)]);
        var press = Recipe("press", [new RecipeIngredient("metal", minimumCondition: .75f)], [new RecipeOutput("plate")]);
        var entries = Entries(BalanceEntry("item:plate", 1, press), BalanceEntry("item:metal", 1, lowGrade), BalanceEntry("liquid:metal", 1, liquid));

        object plan = Calculate(new Catalog(), entries);

        Amount(plan, "Missing", "item:metal", 1);
        Amount(plan, "Results", "item:metal", 1);
        Amount(plan, "Results", "liquid:metal", 1);
        Amount(plan, "Results", "item:plate", 1);
        Check(Rows(plan, "Surplus").Length == 0, "Incompatible condition or type leaves both producers' own results intact.");
    }

    private static void VariantSelectionKeepsSlotsAndTypes()
    {
        const string material = "parity-medium";
        const string alternate = "parity-alternate";
        const string quality = "parity-quality";
        var catalog = new Catalog();
        catalog.RegisterItem(new CatalogItem(material, "Cloth", qualities: [new global::CraftingQuality(quality, 3)]));
        catalog.RegisterItem(new CatalogItem(alternate, "Alternative cloth", qualities: [new global::CraftingQuality(quality, 4)]));
        catalog.RegisterItem(new CatalogItem(material, "Conditioner", kind: CatalogItemKind.Liquid,
            qualities: [new global::CraftingQuality(quality, 4)]));
        string itemId = "item:" + material;
        string liquidId = "liquid:" + material;
        Check(catalog.TryGetItem(itemId, out var item) && !item.IsLiquid &&
            catalog.TryGetItem(liquidId, out var liquid) && liquid.IsLiquid,
            "Typed catalog lookups distinguish item and liquid registrations sharing one raw ID.");
        var missing = new RecipeIngredient("", requiredTag: "parity-missing-tag");
        var wash = Recipe("wash", [new RecipeIngredient("", 2, qualityId: quality),
            new RecipeIngredient("", 6, isLiquid: true, qualityId: quality), missing], [new RecipeOutput("washed")]);
        var variants = new Dictionary<int, string> { [0] = itemId, [1] = liquidId };
        var stock = new Dictionary<string, int> { ["item:" + alternate] = 100, [itemId] = 1, [liquidId] = 2 };

        object plan = Calculate(catalog, Entries(BalanceEntry("item:washed", 2, wash, variants: variants)), stock);

        var inputs = Rows(Rows(plan, "Steps").Single(), "Inputs");
        Check(inputs.Select(row => Property<int>(row, "IngredientIndex")).SequenceEqual(new[] { 0, 1, 2 }), "Each input keeps its source slot.");
        Amount(plan, "Inputs", itemId, 2);
        Amount(plan, "Inputs", liquidId, 3);
        Amount(plan, "Missing", itemId, 1);
        Amount(plan, "Missing", liquidId, 1);
        Check(!Property<bool>(inputs[0], "IsLiquid") && Property<bool>(inputs[1], "IsLiquid") &&
            Property<bool>(inputs[2], "IsRequirement") && HasDiagnostic(plan, "RequirementUnresolved"),
            "A chosen item uses quality as a threshold, a liquid uses it as strength, and an unmatched tag remains generic.");
    }

    private static void ProductionUsesSelectedEntryAndCarriedStock()
    {
        var makeCore = Recipe("make-core", [new RecipeIngredient("ore")], [new RecipeOutput("core", 3)]);
        var makeFrame = Recipe("make-frame", [new RecipeIngredient("core", 2)], [new RecipeOutput("frame", 3)]);
        var entries = Entries(BalanceEntry("item:frame", 2, makeFrame), BalanceEntry("item:core", 1, makeCore));
        var catalog = new Catalog();
        var stock = new Dictionary<string, int> { ["item:frame"] = 4, ["item:core"] = 2 };

        object append = PlanCraft(catalog, entries, 0, topUp: false, stock);
        object topUp = PlanCraft(catalog, entries, 0, topUp: true, stock);

        var steps = Rows(append, "Steps");
        Check(steps.Select(step => Property<int>(step, "EntryIndex")).SequenceEqual(new[] { 1, 0 }) &&
            Property<int>(steps[0], "Batches") == 1 && Property<int>(steps[1], "Batches") == 2,
            "Append retains both target batches and grows the prerequisite by only the shortage after carried cores.");
        Amount(append, "Results", "item:frame", 6);
        var toppedUp = Rows(topUp, "Steps").Single();
        Check(Property<int>(toppedUp, "EntryIndex") == 0 && Property<int>(toppedUp, "Batches") == 1,
            "Top-up from four to six frames rounds the two-unit shortage to one batch, fully supported by carried cores.");
        Amount(topUp, "Results", "item:frame", 3);
        var upstream = Rows(PlanCraft(catalog, entries, 1, topUp: false, stock), "Steps").Single();
        Check(Property<ProviderRecipe>(upstream, "Recipe") == makeCore, "Selecting the intermediate never schedules its downstream consumer.");
        Check(Rows(PlanCraft(catalog, entries, 0, topUp: true, new Dictionary<string, int> { ["frame"] = 6 }), "Steps").Length == 0,
            "A target already at its saved quantity needs no craft.");

        var press = Recipe("press-coproduct", [], [new RecipeOutput("cake", 5), new RecipeOutput("oil", 2, isLiquid: true)]);
        var oilTarget = Entries(BalanceEntry("liquid:oil", 2, press));
        var pressed = Rows(PlanCraft(catalog, oilTarget, 0, topUp: true,
            new Dictionary<string, int> { ["item:cake"] = 100, ["liquid:oil"] = 3 }), "Steps").Single();
        Check(Property<int>(pressed, "Batches") == 1, "Top-up uses the selected coproduct's yield and typed stock.");
        Amount(pressed, "Outputs", "liquid:oil", 2);
        var zeroTarget = Entries(BalanceEntry("liquid:oil", 0, press));
        Check(Rows(PlanCraft(catalog, zeroTarget, 0, topUp: false, stock), "Steps").Length == 0,
            "An explicit zero multiplier schedules no production.");
    }

    private static void ProductionKeepsVariantsOfDuplicateRecipes()
    {
        var catalog = new Catalog();
        catalog.RegisterItem(new CatalogItem("parity-hemp", "Hemp", tags: ["parity-fiber"]));
        catalog.RegisterItem(new CatalogItem("parity-flax", "Flax", tags: ["parity-fiber"]));
        var weave = Recipe("weave", [new RecipeIngredient("", requiredTag: "parity-fiber")], [new RecipeOutput("fabric")]);
        var entries = Entries(
            BalanceEntry("item:fabric", 1, weave, variants: new Dictionary<int, string> { [0] = "item:parity-hemp" }),
            BalanceEntry("item:fabric", 2, weave, variants: new Dictionary<int, string> { [0] = "item:parity-flax" }));

        object plan = PlanCraft(catalog, entries, 1, topUp: false, new Dictionary<string, int> { ["parity-flax"] = 2 });

        var step = Rows(plan, "Steps").Single();
        Check(Property<int>(step, "EntryIndex") == 1 && Property<int>(step, "Batches") == 2, "Production uses the selected entry even when the recipe ID is shared.");
        Amount(step, "Inputs", "item:parity-flax", 2);
        Check(Rows(plan, "Missing").Length == 0, "The selected duplicate uses its own ingredient slot choice.");
    }

    private static void ProductionDoesNotDoubleCountStock()
    {
        var first = Recipe("first", [], [new RecipeOutput("same")]);
        var second = Recipe("second", [], [new RecipeOutput("same")]);
        var catalog = new Catalog();
        var entries = Entries(BalanceEntry("item:same", 2, first), BalanceEntry("item:same", 2, second));
        var stock = new Dictionary<string, int> { ["item:same"] = 3 };

        object plan = PlanCraft(catalog, entries, null, topUp: true, stock);
        Check(Rows(plan, "Steps").Sum(step => Property<int>(step, "Batches")) == 1,
            "Two separate two-unit targets with three carried units still need one craft.");
        Check(stock.Count == 1 && stock["item:same"] == 3, "Production planning never mutates the carried snapshot.");

        var assembly = Recipe("assembly", [new RecipeIngredient("part", 5)], [new RecipeOutput("assembly")]);
        var withPlainBookmark = Entries(BalanceEntry("item:assembly", 1, assembly), BalanceEntry("item:part", 5));
        object production = PlanCraft(catalog, withPlainBookmark, 0, topUp: false, new Dictionary<string, int> { ["part"] = 2 });
        Amount(production, "Missing", "item:part", 3);
    }

    private static void CircularProductionReportsCycle()
    {
        var makeA = Recipe("cycle-a", [new RecipeIngredient("cycle-b")], [new RecipeOutput("cycle-a"), new RecipeOutput("badge")]);
        var makeB = Recipe("cycle-b", [new RecipeIngredient("cycle-a")], [new RecipeOutput("cycle-b")]);
        var entries = Entries(BalanceEntry("item:badge", 1, makeA), BalanceEntry("item:cycle-b", 1, makeB));

        object plan = PlanCraft(new Catalog(), entries, 0, topUp: false, new Dictionary<string, int>());

        Check(HasDiagnostic(plan, "Cycle"), "A mutually dependent production order with no initial supply must report a cycle.");
    }

    private static void OrdinaryAndGroupedToolsFollowCarriedStock()
    {
        const string quality = "preview-grip";
        const string example = "item:preview-a", carried = "item:preview-b", other = "item:preview-c";
        var catalog = new Catalog();
        foreach (string id in new[] { example, carried, other })
            catalog.RegisterItem(new CatalogItem(id.Substring(5), id, qualities: [new CraftingQuality(quality, 2)]));
        var tool = new RecipeIngredient("", 2, isCatalyst: true, qualityId: quality, minimumCondition: .5f);
        var material = new RecipeIngredient("", 2, qualityId: quality);
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var windowType = PluginType("NeiWindow");
        var config = new BepInEx.Configuration.ConfigFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "nei-bookmark-test-" + Guid.NewGuid() + ".cfg"), saveOnInit: false) { SaveOnConfigSet = false };
        object window = Activator.CreateInstance(windowType, instance, null, [null, config], null)!;
        windowType.GetField("_catalog", instance)!.SetValue(window, catalog);
        var book = windowType.GetField("_book", instance)!.GetValue(window)!;
        var add = book.GetType().GetMethod("Add", instance)!;
        var loose = add.Invoke(book, ["0", "recipe/api/tools-preview", 3, "", true, null, true]);
        var grouped = add.Invoke(book, ["0", "recipe/api/tools-preview", 3, "group", true, null, true]);
        var layout = windowType.GetField("_bookmarkLayout", instance)!;
        var update = windowType.GetMethod("UpdateCarriedBookmarkTools", instance)!;
        var select = windowType.GetMethod("SelectedBookmarkVariant", instance)!;

        void Update(params (string Id, float Condition)[] stock) => update.Invoke(window, [stock.ToHashSet()]);
        string? Select(object? entry, int slot, RecipeIngredient ingredient, bool preview = true) =>
            (string?)select.Invoke(window, [entry, slot, ingredient, preview]);
        void Expect(string expectedTool, string groupedTool) =>
            Check(Select(loose, 0, tool) == expectedTool && Select(grouped, 0, tool) == groupedTool,
                "Both ordinary and grouped slots must follow carried stock unless explicitly pinned.");

        Expect(example, example);
        object cached = Activator.CreateInstance(layout.FieldType)!;
        layout.SetValue(window, cached);
        Update((carried, .8f), (other, .4f));
        Check(layout.GetValue(window) == null, "New carried tools invalidate the cached bookmark rows.");
        Expect(carried, carried);
        Check(Select(loose, 1, material) == example && Select(loose, 0, tool, preview: false) == example,
            "Consumed materials and group pull targets keep their original selection.");
        layout.SetValue(window, cached);
        Update((other, .4f), (carried, .8f));
        Check(ReferenceEquals(cached, layout.GetValue(window)), "Unchanged stock must not invalidate the bookmark rows.");
        Update((carried, .4f), (other, .8f));
        Check(layout.GetValue(window) == null, "Durability changes can change which carried tool qualifies.");
        Expect(other, other);
        book.GetType().GetMethod("SetVariant", instance)!.Invoke(book, [grouped, 0, carried]);
        Update();
        Expect(example, carried);
        Update((other, .9f));
        Expect(other, carried);
    }

    private static ProviderRecipe Recipe(string id, RecipeIngredient[] inputs, RecipeOutput[] outputs) =>
        new("bookmark-parity." + id, "bookmark-parity", inputs, outputs);

    // The test project links BookmarkBook source but references the built plugin for recipes.
    private static Type PluginType(string name) => typeof(Catalog).Assembly.GetType("NotEnoughItems." + name, throwOnError: true)!;

    private static object BalanceEntry(string id, int quantity = 1, ProviderRecipe? recipe = null, IReadOnlyDictionary<int, string>? variants = null) =>
        Activator.CreateInstance(PluginType("BookmarkChainBalanceEntry"), [id, quantity, recipe, false, variants])!;

    private static Array Entries(params object[] entries)
    {
        var typed = Array.CreateInstance(PluginType("BookmarkChainBalanceEntry"), entries.Length);
        for (int index = 0; index < entries.Length; index++) typed.SetValue(entries[index], index);
        return typed;
    }

    private static object Calculate(Catalog catalog, Array entries, IReadOnlyDictionary<string, int>? carried = null) =>
        Balance("Calculate", catalog, entries, carried);

    private static object PlanCraft(Catalog catalog, Array entries, int? targetIndex, bool topUp, IReadOnlyDictionary<string, int> carried) =>
        Balance("PlanCraft", catalog, entries, targetIndex, topUp, carried);

    private static object Balance(string method, params object?[] args) =>
        (PluginType("BookmarkChainBalance").GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("BookmarkChainBalance", method)).Invoke(null, args)!;

    private static object[] Rows(object result, string collection) => Property<IEnumerable>(result, collection).Cast<object>().ToArray();

    private static T Property<T>(object instance, string name) => (T)(instance.GetType().GetProperty(name,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new MissingMemberException(instance.GetType().Name, name)).GetValue(instance)!;

    private static bool HasDiagnostic(object plan, string code) =>
        Rows(plan, "Diagnostics").Any(issue => Property<object>(issue, "Code").ToString() == code);

    private static object Material(object result, string collection, string id) =>
        Rows(result, collection).SingleOrDefault(row => StringComparer.OrdinalIgnoreCase.Equals(Property<string>(row, "ItemId"), id))
            ?? throw new InvalidOperationException($"Missing {id} in {collection}.");

    private static void Amount(object result, string collection, string id, double expected, string field = "Quantity")
    {
        double actual = Property<double>(Material(result, collection, id), field);
        Check(double.IsFinite(actual) && Math.Abs(expected - actual) < .0001, $"Expected {collection}/{id}/{field} to be {expected}, got {actual}.");
    }
#endif

    private static BookmarkBook.Entry Add(BookmarkBook book, string page, string id, string group = "") =>
        book.Add(page, id, 1, group) ?? throw new InvalidOperationException($"Could not add fixture {page}/{group}/{id}.");

    private static void PageOrder(BookmarkBook book, string page, params BookmarkBook.Entry[] expected) =>
        Check(book.Page(page).SequenceEqual(expected), $"Unexpected order for {page}. Expected {string.Join(", ", expected.Select(entry => entry.Id))}, got {string.Join(", ", book.Page(page).Select(entry => entry.Id))}.");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
