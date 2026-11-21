using NotEnoughItems;
using UnityEngine;
#if RECIPE_TESTS
using ProviderRecipe = NotEnoughItems.Recipe;
using ProviderCategory = NotEnoughItems.RecipeCategory;
#endif

return RunTests();

static int RunTests()
{
    (string Name, Action Check)[] tests =
    [
        ("plain search combines case-insensitive terms", PlainSearch),
        ("prefixes limit search to their corresponding field", PrefixedSearch),
        ("quotes preserve phrases and can follow a prefix", QuotedSearch),
        ("minus excludes plain and prefixed terms", ExcludedSearch),
        ("screen rectangles round-trip between bottom-left and top-left", ScreenConversion),
        ("wide-screen panels avoid the host and each other", WideLayout),
        ("off-center host leaves layout asymmetric", OffCenterLayout),
        ("narrow-screen panels shrink without covering the host", NarrowLayout),
        ("recipe inspector is clamped to screen margins", InspectorLayout),
        ("screen footer never follows a moving inventory", FooterAnchoring),
        ("tall terminals keep search clear of native slots and quantity", TallTerminalFooter),
        ("legacy bookmarks migrate and grouped quantities round-trip", BookmarkMigration),
        ("reordering and clipboard import preserve independent groups", BookmarkGroups),
        ("GUI texture locations use a namespace and preserve legacy mappings", TextureLocations),
        ("full-width hosts retain both side grids and a separate inspector", FullWidthHostLayout),
        ("native drag releases over NEI cancel only without a real drop target", DragReleaseOwnership),
        ("closing the inventory cancels a drag without cancelling ordinary radial transitions", DragInventoryClose),
        ("missed native release is cleared without stealing the release frame", MissedDragRelease),
#if RECIPE_TESTS
        ("native inventory bounds are screen-anchored and scale with the canvas", NativeInventoryAnchor),
        ("the installed assembly contains all namespaced fallback GUI textures", EmbeddedGuiTextures),
        ("native tooltip entry points and original rich text passthrough", TooltipSmoke.Run),
        ("drag hooks match the installed game's native entry points", TooltipSmoke.DragEndpoints),
        ("bookmark cheat clicks preserve lookup, drag modifiers and recipe bookmarks", BookmarkCheatClicks),
        ("bookmark chain reports raw requirements and reuses byproducts across targets", BookmarkChainBalances),
        ("public recipe registrations appear in production and use indexes", RecipeRegistration),
        ("usage queries share ingredient matching semantics without false positives", RecipeUsageMatching),
        ("source replacement clears stale recipes without affecting other sources", RecipeSourceReplacement),
#endif
    ];

    int failures = 0;
    foreach (var (name, check) in tests)
    {
        try
        {
            check();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception error)
        {
            failures++;
            Console.Error.WriteLine($"FAIL {name}: {error}");
        }
    }

    Console.WriteLine($"{tests.Length - failures}/{tests.Length} checks passed.");
    return failures == 0 ? 0 : 1;
}

static bool Search(string query) => NeiSearch.Matches(query,
    text: "Heavy Plate steel armor",
    source: "Forge Guild",
    tags: "Metal Tools",
    tooltip: "heat resistant shell",
    id: "plate_heavy_02");

static void PlainSearch()
{
    Check(Search(""), "An empty query must match.");
    Check(Search(" \t\n"), "Whitespace-only queries must match.");
    Check(Search("  HEAVY\tsteel  "), "Plain terms must match regardless of case and whitespace separators.");
    Check(!Search("steel copper"), "All positive terms must match.");
}

static void PrefixedSearch()
{
    Check(Search("@FORGE $metal #shell &heavy_02"), "Each prefix should find its own matching field.");
    Check(!Search("@steel"), "A plain-text occurrence cannot satisfy a source term.");
    Check(!Search("$armor"), "A plain-text occurrence cannot satisfy a tag term.");
    Check(!Search("#metal"), "A tag cannot satisfy a tooltip term.");
    Check(!Search("&shell"), "Tooltip text cannot satisfy an ID term.");
}

static void QuotedSearch()
{
    Check(Search("\"heavy plate\" @\"forge guild\" #\"heat resistant\""), "Quoted prefixed terms must keep their spaces.");
    Check(!Search("\"plate heavy\""), "A phrase is not an unordered collection of words.");
    Check(!Search("@\"forge plate\""), "A phrase may not combine different fields.");
    Check(Search("\"heavy plate"), "An unfinished phrase should continue matching while the user types.");
}

static void ExcludedSearch()
{
    Check(Search("steel -brass -@outpost -$wood -#brittle -&plate_light"), "Absent excluded terms must not reject a match.");
    Check(!Search("steel -armor"), "A present excluded plain term must reject a match.");
    Check(!Search("steel -@\"forge guild\""), "A present excluded source phrase must reject a match.");
    Check(!Search("steel -$tools"), "A present excluded tag must reject a match.");
    Check(!Search("steel -#shell"), "A present excluded tooltip term must reject a match.");
    Check(!Search("steel -&heavy_02"), "A present excluded ID term must reject a match.");
}

static void ScreenConversion()
{
    var screen = new Rect(124, 73, 397, 184);
    Rect gui = NeiLayout.FromScreen(screen, 720);
    RectEqual(new Rect(124, 463, 397, 184), gui, "left and size must stay unchanged; y must use yMax, not yMin");
    RectEqual(screen, NeiLayout.FromScreen(gui, 720), "flipping twice must restore the original rectangle");
    RectEqual(new Rect(0, 0, 1280, 720), NeiLayout.FromScreen(new Rect(0, 0, 1280, 720), 720), "full-screen rectangle");
}

static void WideLayout()
{
    var center = new Rect(630, 32, 660, 1016);
    var (left, right) = NeiLayout.Sides(center, 1920, 1080, 1);
    RectEqual(new Rect(293, 32, 330, 1016), left, "left side caps its width at scale 1");
    RectEqual(new Rect(1297, 32, 330, 1016), right, "right side caps its width at scale 1");
    CheckSides(center, left, right, 1920, 1080);

    var scaled = NeiLayout.Sides(center, 1920, 1080, .75f);
    Equal(247.5f, scaled.Left.width, "left side width scales");
    Equal(247.5f, scaled.Right.width, "right side width scales");
    CheckSides(center, scaled.Left, scaled.Right, 1920, 1080);
}

static void OffCenterLayout()
{
    var center = new Rect(150, 100, 910, 430);
    var (left, right) = NeiLayout.Sides(center, 1280, 720, 1);
    Equal(135, left.width, "left side uses only the actual available width");
    Equal(205, right.width, "right side uses its own separate available width");
    CheckSides(center, left, right, 1280, 720);
}

static void NarrowLayout()
{
    var center = new Rect(29, 72, 584, 190);
    var (left, right) = NeiLayout.Sides(center, 640, 360, 1);
    Equal(14, left.width, "left side shrinks below the UI drawing threshold");
    Equal(12, right.width, "right side shrinks below the UI drawing threshold");
    Equal(352, left.yMax, "minimum requested height must not extend beyond the viewport");
    CheckSides(center, left, right, 640, 360);

    var fillsScreen = new Rect(0, 0, 640, 360);
    var (noLeft, noRight) = NeiLayout.Sides(fillsScreen, 640, 360, 1);
    Equal(0, noLeft.width, "left side collapses when no gap remains");
    Equal(0, noRight.width, "right side collapses when no gap remains");
}

static void InspectorLayout()
{
    var nearCorner = new Rect(540, 330, 80, 30);
    Rect panel = NeiLayout.Inspector(nearCorner, 640, 360, 1);
    RectEqual(new Rect(72, 8, 560, 344), panel, "corner anchor is clamped inside the viewport");

    var narrow = new Rect(30, 20, 200, 320);
    RectEqual(new Rect(8, 8, 244, 344), NeiLayout.Inspector(narrow, 260, 360, 1),
        "narrow screen constrains both dimensions without a negative rectangle");
}

static void FooterAnchoring()
{
    var center = new Rect(430, 100, 420, 500);
    var (tools, search) = NeiLayout.BottomRail(1280, 720, center, default);
    var moved = NeiLayout.BottomRail(1280, 720, new Rect(470, 150, 420, 486), default);
    RectEqual(tools, moved.Tools, "The left buttons are anchored to the screen.");
    RectEqual(search, moved.Search, "The normal center search does not follow the inventory.");
    RectEqual(new Rect(54, 689, 49, 23), tools, "Left rail clears the vanilla HUD and uses a fixed screen margin.");
    Equal(640, search.center.x, "The search is centered.");
}

static void TallTerminalFooter()
{
    var terminal = new Rect(480, 8, 320, 704);
    var (_, right) = NeiLayout.Sides(terminal, 1280, 720, 1);
    var (tools, search) = NeiLayout.BottomRail(1280, 720, terminal, right);
    Check(!search.Overlaps(terminal), "Search must not cover any part of tall central terminal.");
    Check(!tools.Overlaps(terminal) && !tools.Overlaps(search), "Left controls are clear of the terminal and search.");
    Check(search.xMin > right.xMin && search.xMax < right.xMax && search.yMax < right.yMax - 30,
        "Tall terminal search is placed  immediately above the right quantity controls.");
}

static void BookmarkMigration()
{
    var book = new BookmarkBook();
    Check(book.Load("0:item%3Arope|布%3A料:recipe%2Fnative%2Frope|bad"), "Legacy entries load without dropping playable bookmarks.");
    Check(book.Find("0", "item:rope")?.Quantity == 1, "Legacy entries use amount one.");
    Check(book.Find("布:料", "recipe/native/rope") != null, "Escaped page names and recipe keys survive.");
    Check(book.Toggle("0", "item:leaf", 3), "Create a second bookmark.");
    var leaf = book.Find("0", "item:leaf")!;
    Check(book.Move(leaf, book.Find("0", "item:rope")!, false) && leaf.Group.Length > 0, "Dragging onto an entry creates a group.");
    Check(book.Adjust(leaf, 4), "Quantity control updates target amount.");
    var restored = new BookmarkBook();
    Check(restored.Load(book.Serialize()) && restored.Entries.Count == 3, "The new format round-trips all entries.");
    Check(restored.Find("0", "item:leaf")?.Quantity == 7 && restored.Find("0", "item:leaf")?.Group == leaf.Group,
        "Groups and amounts stay intact after reload.");
}

static void BookmarkGroups()
{
    var book = new BookmarkBook();
    book.Toggle("0", "item:first", 1);
    book.Toggle("0", "item:second", 2);
    var first = book.Find("0", "item:first")!;
    var second = book.Find("0", "item:second")!;
    Check(book.Move(first, second, true) && first.Group.Length == 0 && book.Entries[1] == first,
        "Reordering does not silently create a group.");
    Check(book.Move(first, second, false) && first.Group == second.Group, "Group creation preserves both entries.");
    string group = first.Group;
    Check(book.Load(book.Serialize(book.Page("0")), "Imported"), "Paste a compatible page.");
    var imported = book.Find("Imported", "item:first")!;
    Check(imported.Group.Length > 0 && imported.Group != group && imported.Group == book.Find("Imported", "item:second")!.Group,
        "Imported groups remain separate from the original.");
    Check(book.Ungroup("0", group) && first.Group == "" && imported.Group != "", "Ungroup only affects the chosen page.");
    Check(!book.Load("0:item%3Atest", "Imported"), "Clipboard import only accepts the documented format.");
}

static void TextureLocations()
{
    Check(NeiAssets.RelativePath(NeiAssets.Modernity("nei_sprites.png")) == "assets/notenoughitems/textures/gui/modernity/nei_sprites.png",
        "Modernity uses lowercase paths below the namespace.");
    Check(NeiAssets.ResourceName(NeiAssets.Background("")) == "assets/notenoughitems/textures/gui/backgrounds/default.png",
        "The embedded fallback uses the same resource path as the disk override.");
    Check(NeiAssets.LegacyPath(NeiAssets.Background("recipe")) == "Textures/background-recipe.png" &&
        NeiAssets.LegacyPath(NeiAssets.Modernity("widgets.png")) == null, "Legacy backgrounds stay readable, not obsolete control art.");
    Check(ModernityFrames.Caption("previous") == "<" && ModernityFrames.Caption("next") == ">" &&
        ModernityFrames.Find("options_cheat") is { X: 64, Y: 0, File: "nei_tabbed_sprites.png" }, "Official sprite positions and paging glyphs.");
}

static void DragReleaseOwnership()
{
    Check(!DragInputPolicy.CancelRelease(true, false, true), "A real native drop target at an overlapping rectangle still receives the original release.");
    Check(DragInputPolicy.CancelRelease(true, false, false), "A preview over a NEI-only field is cancelled, not dropped into the world.");
    Check(DragInputPolicy.CancelRelease(false, true, false), "A NEI click release must not close the radial menu.");
    Check(!DragInputPolicy.CancelRelease(false, false, false), "A completed native click must not be mistaken for a NEI click.");
}

static void DragInventoryClose()
{
    Check(DragInputPolicy.CancelOnClose(true, false), "An explicit close clears the original drag before the release handler.");
    Check(!DragInputPolicy.CancelOnClose(true, true), "An ignored close key cannot interrupt a held item.");
    Check(!DragInputPolicy.CancelOnClose(false, false), "The radial menu can still auto-close while dragging outside its normal bounds.");
}

static void MissedDragRelease()
{
    Check(!DragInputPolicy.IsStale(true, false), "An ordinary drag remains live across frames.");
    Check(!DragInputPolicy.IsStale(false, true), "The native and terminal release handlers keep the original release edge.");
    Check(DragInputPolicy.IsStale(false, false), "An unhandled release or focus loss cannot leave a stale preview next frame.");
}

static void FullWidthHostLayout()
{
    foreach (var host in new[] { new Rect(0, 0, 480, 410), new Rect(280, 36, 200, 400), new Rect(0, 115, 290, 380) })
    {
        var (left, right) = NeiLayout.VisibleSides(host, 480, 410);
        var inspector = NeiLayout.InspectorBetween(host, left, right, 480, 410);
        Check(left.width >= 104 && right.width >= 104 && left.height >= 180 && right.height >= 180,
            "A wide or off-center native panel must not make either grid collapse.");
        Check(!left.Overlaps(right) && !inspector.Overlaps(left) && !inspector.Overlaps(right) && inspector.width >= 245,
            "Both grids and recipe controls remain visible independently.");
        Check(left.xMin >= 8 && right.xMax <= 472 && right.yMax <= 402, "All controls remain inside the viewport.");
    }
    var normal = new Rect(410, 260, 460, 400);
    var panels = NeiLayout.VisibleSides(normal, 1280, 720);
    CheckSides(normal, panels.Left, panels.Right, 1280, 720);
}

static void CheckSides(Rect host, Rect left, Rect right, float width, float height)
{
    Check(left.width > 0 && right.width > 0, "Fixture must leave room for both sides.");
    Check(!left.Overlaps(host) && !right.Overlaps(host) && !left.Overlaps(right),
        $"Panels overlap: host {host}, left {left}, right {right}.");
    Check(left.xMin >= 8 && right.xMax <= width - 8, "Panels must stay inside the horizontal screen margins.");
    Check(left.yMin >= 8 && right.yMin >= 8 && left.yMax <= height - 8 && right.yMax <= height - 8,
        "Panels must stay inside the vertical screen margins.");
}

static void RectEqual(Rect expected, Rect actual, string message)
{
    Equal(expected.x, actual.x, $"{message} (x)");
    Equal(expected.y, actual.y, $"{message} (y)");
    Equal(expected.width, actual.width, $"{message} (width)");
    Equal(expected.height, actual.height, $"{message} (height)");
}

static void Equal(float expected, float actual, string message) =>
    Check(float.IsFinite(actual) && Math.Abs(expected - actual) < .001f, $"{message}: expected {expected}, got {actual}.");

static void Check(bool passed, string message)
{
    if (!passed)
        throw new InvalidOperationException(message);
}

#if RECIPE_TESTS
static void EmbeddedGuiTextures()
{
    var assembly = typeof(Catalog).Assembly;
    Span<byte> header = stackalloc byte[24];
    foreach (string path in new[] { NeiAssets.Modernity("widgets.png"), NeiAssets.Modernity("nei_tabbed_sprites.png"),
        NeiAssets.Background("recipe"), NeiAssets.Modernity("nei_sprites.png") })
    {
        using var png = assembly.GetManifestResourceStream(NeiAssets.ResourceName(path));
        Check(png != null, "Missing namespaced resource: " + path);
        png!.ReadExactly(header);
        Check(header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Fallback is not PNG: " + path);
        uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
        Check(width > 0 && (width & (width - 1)) == 0 && height > 0 && (height & (height - 1)) == 0,
            "Texture canvas dimensions must be powers of two: " + path);
    }
}

static void BookmarkCheatClicks()
{
    var window = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NeiWindow", throwOnError: true)!;
    var check = window.GetMethod("CanGiveSlot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
        null, [typeof(bool), typeof(string), typeof(CatalogItem), typeof(int), typeof(bool), typeof(bool), typeof(bool)], null)!;
    var item = new CatalogItem("rope", "Rope");
    bool Can(string id = "item:rope", int button = 0, bool shift = false, bool alt = false, bool cheat = true, bool source = true) =>
        (bool)check.Invoke(null, [source, id, item, button, shift, alt, cheat])!;
    Check(Can() && Can(button: 1), "Both ordinary bookmark click actions reach item giving in Cheat mode.");
    Check(!Can(shift: true) && !Can(alt: true) && !Can(button: 2), "Lookup/drag and bookmark gestures do not give items.");
    Check(!Can(cheat: false) && !Can(source: false) && !Can(id: "recipe/native/rope"), "Recipe browsing, other menus and recipe bookmarks never give an item.");
}

static void NativeInventoryAnchor()
{
    var adapter = typeof(Catalog).Assembly.GetType("NotEnoughItems.GameSurface", throwOnError: true)!;
    var anchor = adapter.GetMethod("AnchorInventory", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    var radial = new Rect(-297, -292, 594, 601);
    var container = new Rect(390, -345, 200, 690);
    Rect Resolve(Rect? currentContainer, float screenWidth, float screenHeight, float scale) =>
        (Rect)anchor.Invoke(null, [radial, currentContainer, screenWidth, screenHeight, scale])!;
    RectEqual(new Rect(343, 68, 594, 601), Resolve(null, 1280, 720, 1), "Centered vanilla inventory");
    var withContainer = Resolve(container, 1280, 720, 1);
    Check(withContainer.xMin <= 343 && withContainer.xMax >= 1230, "The open container reserves the right side at full size.");
    RectEqual(new Rect(514.5f, 102, 891, 901.5f), Resolve(null, 1920, 1080, 1.5f), "Screen/UI scaling");
}

static void BookmarkChainBalances()
{
    var assembly = typeof(Catalog).Assembly;
    Type TypeOf(string name) => assembly.GetType("NotEnoughItems." + name, throwOnError: true)!;
    object New(string name, params object?[] args) => Activator.CreateInstance(TypeOf(name),
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
        null, args, null)!;
    object Read(object o, string property) => o.GetType().GetProperty(property)!.GetValue(o)!;
    object[] Rows(object o, string property) => ((System.Collections.IEnumerable)Read(o, property)).Cast<object>().ToArray();
    float Amount(object plan, string property, string id, string amount)
    {
        var row = Rows(plan, property).SingleOrDefault(item => (string)Read(item, "ItemId") == "item:" + id);
        return row == null ? 0f : Convert.ToSingle(Read(row, amount));
    }
    var catalog = new Catalog();
    var smelt = new ProviderRecipe("test.smelt", "test.work", [new RecipeIngredient("ore", 2), new RecipeIngredient("tongs", isCatalyst: true)],
        [new RecipeOutput("ingot", 3), new RecipeOutput("slag", 2)]);
    var gear = new ProviderRecipe("test.gear", "test.work", [new RecipeIngredient("ingot", 2)], [new RecipeOutput("gear")]);
    var bricks = new ProviderRecipe("test.bricks", "test.work", [new RecipeIngredient("slag")], [new RecipeOutput("brick")]);
    var targets = Array.CreateInstance(TypeOf("BookmarkChainTarget"), 2);
    targets.SetValue(New("BookmarkChainTarget", "item:gear", 1, gear), 0);
    targets.SetValue(New("BookmarkChainTarget", "item:brick", 1, bricks), 1);
    var request = New("BookmarkChainRequest", catalog, targets, new[] { smelt, gear, bricks }, null,
        new Dictionary<string, int> { ["item:ore"] = 1, ["item:tongs"] = 1, ["item:unrelated"] = 7 }, true, null);
    object plan = TypeOf("BookmarkChain").GetMethod("Plan")!.Invoke(null, [request])!;
    Equal(2, Amount(plan, "Inputs", "ore", "RequiredQuantity"), "Only one batch of ore should be required.");
    Equal(1, Amount(plan, "Missing", "ore", "Shortage"), "Missing bill reports only the additional raw ore.");
    Equal(0, Amount(plan, "Missing", "ingot", "Shortage"), "Intermediate must not be double-consumed or reported missing.");
    Equal(0, Amount(plan, "Missing", "gear", "Shortage"), "The planned target is not reported as a raw material.");
    Equal(1, Amount(plan, "Surplus", "ingot", "Surplus"), "Batch rounding leaves one ingot.");
    Equal(1, Amount(plan, "Surplus", "slag", "Surplus"), "The other target reuses one of the two produced byproducts.");
    Equal(0, Amount(plan, "Surplus", "unrelated", "Surplus"), "Untouched inventory is not produced surplus.");
}

static void RecipeRegistration()
{
    const string source = "nei.tests.registration";
    var category = new ProviderCategory("test.forge", "Test forge", "missing-machine", source: source);
    var recipe = new ProviderRecipe("test.smelt", category.Id,
        [new RecipeIngredient("test.ore"), new RecipeIngredient("test.tongs", isCatalyst: true)],
        [new RecipeOutput("test.ingot", 2), new RecipeOutput("test.byproduct", isLiquid: true)],
        machineId: category.MachineId, source: source);

    try
    {
        RecipeApi.RegisterCategory(category);
        RecipeApi.RegisterRecipe(recipe);
        var catalog = RecipeApi.Catalog;
        Check(catalog.GetRecipesForCategory("TEST.FORGE").Single() == recipe, "Category lookup must be case-insensitive.");
        Check(catalog.GetRecipesProducing("test.ingot").Single() == recipe, "Primary output must be indexed.");
        Check(catalog.GetRecipesProducing("test.byproduct").Single() == recipe, "Additional outputs must be indexed.");
        Check(catalog.GetItemUsages("test.tongs").Single() == recipe, "Catalysts must count as usages.");
        Check(catalog.GetItemUsages(category.MachineId).Single() == recipe, "Machine IDs must remain discoverable without a loaded machine.");
        var inherited = new ProviderRecipe("test.inherited-machine", category.Id, [], [new RecipeOutput("test.other-output")], source: source);
        RecipeApi.RegisterRecipe(inherited);
        Check(catalog.GetItemUsages(category.MachineId).Contains(inherited), "Recipes inherit their registered category's machine requirement when no machine ID was specified.");
        Check(!catalog.TryGetNativeRecipeIndex(recipe.Id, out _), "A provider recipe is not a live game recipe.");
    }
    finally
    {
        RecipeApi.ClearSource(source);
    }
}

static void RecipeUsageMatching()
{
    var catalog = new Catalog();
    var qualityItem = new global::CraftingQuality("test.fabric", 3f);
    var insufficientQuality = new global::CraftingQuality("test.fabric", 1f);
    var items = new[]
    {
        new CatalogItem("test.material", "Material", qualities: [new global::CraftingQuality("test.fabric", 3f)]),
        new CatalogItem("test.tool", "Retained tool"),
        new CatalogItem("test.fiber", "Fabric fiber", tags: ["cloth"], qualities: [qualityItem]),
        new CatalogItem("test.low-quality", "Insufficient fabric", tags: ["cloth"], qualities: [insufficientQuality]),
        new CatalogItem("test.wrong-tag", "Wrong tag", qualities: [new global::CraftingQuality("test.metal", 3f)]),
        new CatalogItem("test.ignored", "Ignored cloth", tags: ["cloth"], qualities: [qualityItem]),
        new CatalogItem("test.liquid", "Test liquid", kind: CatalogItemKind.Liquid,
            qualities: [new global::CraftingQuality("test.fabric", 3f)])
    };
    foreach (CatalogItem item in items)
        catalog.RegisterItem(item);

    var category = new ProviderCategory("test.usage-category");
    var exactInput = new RecipeIngredient("test.material");
    var toolInput = new RecipeIngredient("test.tool", isCatalyst: true);
    var qualityInput = new RecipeIngredient(string.Empty, amount: 2f, qualityId: "test.fabric");
    var tagInput = new RecipeIngredient(string.Empty, requiredTag: "cloth", ignoredItemId: "test.ignored");
    var liquidQualityInput = new RecipeIngredient(string.Empty, amount: 2f, isLiquid: true, qualityId: "test.fabric");
    var recipes = new[]
    {
        new ProviderRecipe("test.use-material", category.Id, [exactInput], [new RecipeOutput("test.output-material")]),
        new ProviderRecipe("test.use-tool", category.Id, [toolInput], [new RecipeOutput("test.output-tool")]),
        new ProviderRecipe("test.use-quality", category.Id, [qualityInput], [new RecipeOutput("test.output-quality")]),
        new ProviderRecipe("test.use-tag", category.Id, [tagInput], [new RecipeOutput("test.output-tag")]),
        new ProviderRecipe("test.use-liquid-quality", category.Id, [liquidQualityInput], [new RecipeOutput("test.output-liquid")])
    };
    foreach (ProviderRecipe recipe in recipes)
        catalog.RegisterRecipe(recipe);

    IReadOnlyList<CatalogItem> Matches(RecipeIngredient ingredient, bool includeBelowRequirement = false) =>
        catalog.GetMatchingItems(ingredient, includeBelowRequirement);
    IReadOnlyList<ProviderRecipe> Uses(string id) => catalog.GetRecipesUsing(id);
    void HasMatch(RecipeIngredient ingredient, string browseId, string message) =>
        Check(Matches(ingredient).Any(item => string.Equals(item.BrowseId, browseId, StringComparison.OrdinalIgnoreCase)), message);
    void LacksMatch(RecipeIngredient ingredient, string browseId, string message) =>
        Check(Matches(ingredient).All(item => !string.Equals(item.BrowseId, browseId, StringComparison.OrdinalIgnoreCase)), message);
    void HasUse(string itemId, ProviderRecipe recipe, string message) =>
        Check(Uses(itemId).Contains(recipe), message);
    void LacksUse(string itemId, ProviderRecipe recipe, string message) =>
        Check(!Uses(itemId).Contains(recipe), message);

    HasMatch(exactInput, "item:test.material", "A concrete ingredient must match its exact item type.");
    LacksMatch(exactInput, "item:test.tool", "A concrete ingredient must not match unrelated items.");
    HasMatch(toolInput, "item:test.tool", "A non-consumed catalyst must still be a valid matching item.");
    HasMatch(qualityInput, "item:test.fiber", "A quality wildcard must include items meeting the requested amount.");
    LacksMatch(qualityInput, "item:test.low-quality", "A quality wildcard must reject insufficient quality amount by default.");
    Check(Matches(qualityInput, includeBelowRequirement: true).Any(item => item.BrowseId == "item:test.low-quality"),
        "Expanded quality matching must include same-quality types below the amount threshold.");
    LacksMatch(qualityInput, "item:test.wrong-tag", "A quality wildcard must not accept items solely due to a different property.");
    LacksMatch(qualityInput, "liquid:test.liquid", "An item quality wildcard must not match a liquid with the same quality ID.");
    HasMatch(tagInput, "item:test.fiber", "A property wildcard must include items with the required tag.");
    LacksMatch(tagInput, "item:test.ignored", "A property wildcard must exclude its ignored item ID.");
    LacksMatch(tagInput, "item:test.wrong-tag", "A property wildcard must reject items missing the required tag.");
    HasMatch(liquidQualityInput, "liquid:test.liquid", "A liquid quality wildcard must use the liquid catalog type.");
    LacksMatch(liquidQualityInput, "item:test.material", "A liquid quality wildcard must not produce item candidates.");

    HasUse("TEST.MATERIAL", recipes[0], "Concrete uses must be case-insensitive.");
    LacksUse("test.tool", recipes[0], "A concrete use must not become a false-positive usage.");
    HasUse("item:test.tool", recipes[1], "Catalysts are still recipe uses.");
    HasUse("item:test.fiber", recipes[2], "Quality matches must appear in reverse-use queries.");
    HasUse("item:test.low-quality", recipes[2], "Reverse usage is a type predicate and must not impose candidate amount filtering.");
    LacksUse("item:test.wrong-tag", recipes[2], "Quality uses must not include an item lacking the quality.");
    HasUse("item:test.fiber", recipes[3], "Required-tag inputs must appear as usages for matching items.");
    LacksUse("item:test.ignored", recipes[3], "Required-tag reverse usage must exclude ignored item types.");
    LacksUse("item:test.wrong-tag", recipes[3], "Required-tag reverse usage must reject items without the tag.");
    HasUse("liquid:test.liquid", recipes[4], "Liquid quality uses must preserve liquid type.");
    LacksUse("item:test.material", recipes[4], "Liquid quality uses must not match same-quality item types.");

    foreach (var (ingredient, recipe) in new[]
    {
        (exactInput, recipes[0]), (toolInput, recipes[1]), (qualityInput, recipes[2]),
        (tagInput, recipes[3]), (liquidQualityInput, recipes[4])
    })
    {
        foreach (CatalogItem item in items)
        {
            bool expectedUse = ingredient.Matches(item);
            bool indexedUse = Uses(item.BrowseId).Contains(recipe);
            if (ingredient.MatchesAny && ingredient.QualityId.Length > 0 && !ingredient.IsLiquid && item.Id == "test.low-quality")
                Check(indexedUse && !expectedUse,
                    "Reverse uses intentionally follow native type/quality-ID semantics rather than candidate amount requirements.");
            else
                Check(indexedUse == expectedUse,
                    $"Usage index and ingredient matching must agree for {recipe.Id} / {item.BrowseId}.");
        }
    }
}

static void RecipeSourceReplacement()
{
    const string source = "nei.tests.replace";
    const string neighboringSource = "nei.tests.neighbor";
    var oldCategory = new ProviderCategory("test.old-category");
    var newCategory = new ProviderCategory("test.new-category");
    var neighboringCategory = new ProviderCategory("test.neighbor-category");
    var oldRecipe = new ProviderRecipe("test.old", oldCategory.Id, [], [new RecipeOutput("test.old-output")]);
    var newRecipe = new ProviderRecipe("test.new", newCategory.Id, [], [new RecipeOutput("test.new-output")]);
    var neighbor = new ProviderRecipe("test.neighbor", neighboringCategory.Id, [], [new RecipeOutput("test.neighbor-output")]);

    try
    {
        RecipeApi.ReplaceSource(source, [oldCategory], [oldRecipe]);
        RecipeApi.ReplaceSource(neighboringSource, [neighboringCategory], [neighbor]);
        RecipeApi.ReplaceSource(source.ToUpperInvariant(), [newCategory], [newRecipe]);
        var catalog = RecipeApi.Catalog;
        Check(catalog.GetRecipesProducing("test.old-output").Count == 0, "Replacing a source must remove stale recipe indexes.");
        Check(catalog.RecipeCategories.All(c => c.Id != oldCategory.Id), "Replacing a source must remove stale categories.");
        Check(catalog.GetRecipesProducing("test.new-output").Single() == newRecipe, "Replacement must index the new recipe.");
        RecipeApi.ClearSource(source);
        Check(catalog.GetRecipesProducing("test.new-output").Count == 0, "Clearing a source must remove replacement recipes.");
        Check(catalog.GetRecipesProducing("test.neighbor-output").Single() == neighbor, "Clearing one source must preserve other sources.");
    }
    finally
    {
        RecipeApi.ClearSource(source);
        RecipeApi.ClearSource(neighboringSource);
    }
}
#endif
