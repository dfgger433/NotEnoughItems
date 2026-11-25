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
        ("both shipped language files use the same keys and valid placeholders", LocalizationTests.ShippedFiles),
        ("loose language files override defaults and fall back per key or file", LocalizationTests.LooseFiles),
        ("optional pinyin search falls back without disrupting literal search", PinyinSearchTests.OptionalProvider),
        ("pinyin search preserves query operators and field isolation", PinyinSearchTests.SearchSyntax),
        ("plain search combines case-insensitive terms", PlainSearch),
        ("prefixes limit search to their corresponding field", PrefixedSearch),
        ("mod searches ignore spaces and recognize the base game's displayed name", ModSourceSearch),
        ("quotes preserve phrases and can follow a prefix", QuotedSearch),
        ("minus excludes plain and prefixed terms", ExcludedSearch),
        ("screen rectangles round-trip between bottom-left and top-left", ScreenConversion),
        ("shared overlay regions match the reference pixels and scale with the viewport", OverlayLayout),
        ("inspectors stay in the viewport and the recipe panel is vertically centered", InspectorLayout),
        ("options center on screen regardless of the inventory or terminal anchor", OptionsLayout),
        ("spawn mode choices share one row without overlapping at any settings scale", SpawnModeChoiceLayout),
        ("recipe pages keep rows reachable, with a 3×3 minimum and clipped actions", RecipeListLayoutTests.Run),
        ("catalog controls and history stay in the footer below the item grid", CatalogPanelLayout),
        ("history and quantity use the requested catalog footer bounds", HistoryCatalogTests.Run),
        ("only visible buttons intercept clicks inside the left tool area", LeftToolsLayout),
        ("item tooltips stay inside the scaled viewport and flip at its right and bottom edges", TooltipBounds),
        ("item and candidate tooltips follow the pointer over recipe navigation", RecipeNavigationTooltipBounds),
        ("nested tooltip previews stay under the pointer even over their parent", NestedTooltipBounds),
        ("liquid slot quantities use the bottom-left corner and fit with decimal suffixes", LiquidSlotAmounts),
        ("long supported-item lists page through every candidate without gaps", SupportedItemsPagination),
        ("legacy bookmarks migrate and grouped quantities round-trip", BookmarkMigration),
        ("reordering and clipboard import preserve independent groups", BookmarkGroups),
        ("dragging a bracket to a new page preserves its settings without merging IDs", () =>
        {
            var book = new BookmarkBook();
            var a = book.Add("A", "rope", 1, "g")!;
            var b = book.Add("A", "cloth", 2, "g")!;
            var other = book.Add("B", "hammer", 1, "g")!;
            book.ToggleChain("A", "g"); book.ToggleCollapsed("A", "g");
            Check(book.DropAtEnd(a, "B", true) && !book.Page("A").Any() && book.Page("B").SequenceEqual(new[] { other, a, b }) &&
                a.Group == b.Group && a.Group != other.Group && book.IsChainEnabled("B", a.Group) && book.IsCollapsed("B", a.Group), "Transfer the entire independent bracket.");
            Check(book.DropAtEnd(a, "B", false) && a.Group == "" && book.Page("B").Last() == a, "An empty-space drop removes just the selected entry from the bracket.");
        }),
        ("bookmark groups, saved recipe routes and crafting targets", BookmarkParityTests.Run),
        ("legacy Alt-click dissolves a bracket without changing the other bracket clicks", () =>
        {
            Check(BookmarkClicks.Bracket(0, true, false, false) == BookmarkBracketClick.Ungroup &&
                BookmarkClicks.Bracket(1, false, true, false) == BookmarkBracketClick.Ungroup &&
                BookmarkClicks.Bracket(0, false, false, false) == BookmarkBracketClick.ToggleLayout &&
                BookmarkClicks.Bracket(0, false, true, false) == BookmarkBracketClick.ToggleCollapse &&
                BookmarkClicks.Bracket(1, false, false, false) == BookmarkBracketClick.ToggleChain &&
                BookmarkClicks.Bracket(0, false, false, true) == BookmarkBracketClick.None,
                "Group clicks must not alias toggles or drag modifiers.");
        }),
        ("Alt-click distinguishes splitting a bookmark from expanding or folding a chain", () =>
        {
            Check(BookmarkClicks.Slot(0, true, false, true, false, true) == BookmarkSlotClick.Split &&
                BookmarkClicks.Slot(0, true, false, false, true, true) == BookmarkSlotClick.ExpandLane &&
                BookmarkClicks.Slot(0, true, true, true, false, true) == BookmarkSlotClick.ToggleRecipeCollapse &&
                BookmarkClicks.Slot(0, true, true, true, false, false) == BookmarkSlotClick.None,
                "Alt-click removes membership without deleting a bookmark; Shift+Alt must never split it.");
        }),
        ("craft, top-up, clipboard and native menu shortcuts never alias", () =>
        {
            Check(BookmarkKeys.Resolve(KeyCode.C, true, false) == BookmarkShortcut.Copy &&
                BookmarkKeys.Resolve(KeyCode.C, true, true) == BookmarkShortcut.CraftMissing &&
                BookmarkKeys.Resolve(KeyCode.C, false, true) == BookmarkShortcut.Craft &&
                BookmarkKeys.Resolve(KeyCode.C, false, false) == BookmarkShortcut.None &&
                BookmarkKeys.Resolve(KeyCode.V, true, false) == BookmarkShortcut.Paste &&
                BookmarkKeys.Resolve(KeyCode.V, false, true) == BookmarkShortcut.PullMissing &&
                BookmarkKeys.Resolve(KeyCode.S, false, true) == BookmarkShortcut.OpenNative, "Only explicit crafting chords can start an order.");
        }),
        ("bookmark shortcut help follows chain, page, crafting and rebound-key contexts", BookmarkShortcutHelp),
        ("long chain tooltip rows expose every ingredient including the last", () =>
        {
            var rows = BookmarkBoardPaging.Rows(new[] { 0, 100, 1 }, 3).ToArray();
            Check(rows.Length == 36 && rows[0] == (0, 0, 0) && rows[^2] == (1, 99, 1) && rows[^1] == (2, 0, 1), "A clipped step or note is not dropped.");
        }),
        ("GUI texture locations use a namespace and preserve legacy mappings", TextureLocations),
        ("ordinary buttons capture, release and cancel without a stray click", ButtonVisualTests.Input),
        ("mouse capture and release keep the same hover artwork", ButtonVisualTests.ClickKeepsHoverArtwork),
        ("sprites and built-in plus/minus share idle, hover and disabled artwork", ButtonVisualTests.Artwork),
        ("button bevel texels mirror at fractional device scales", ButtonBevelPixels),
        ("full-width hosts retain both side grids and a separate inspector", FullWidthHostLayout),
        ("native drag releases over NEI cancel only without a real drop target", DragReleaseOwnership),
        ("closing the inventory cancels a drag without cancelling ordinary radial transitions", DragInventoryClose),
        ("missed native release is cleared without stealing the release frame", MissedDragRelease),
        ("Together request receipts are owned, matched and collected at most once", TogetherCraftRequestTests.Run),
        ("Together permissions distinguish hosts, authorized clients, and unknown sessions", MultiplayerPermissions),
        ("multiplayer give only targets the caller and accepts bounded item tokens", MultiplayerGiveCommand),
        ("Tab key and character events do not transfer focus to NEI search", TabDoesNotFocusSearch),
#if RECIPE_TESTS
        ("NEI exposes only recipe and cheat modes", NeiModeContract),
        ("the installed JUC dictionary provides catalog pinyin", () =>
        {
            string provider = Path.Combine(AppContext.BaseDirectory, "JustUnknownCharacters.dll");
            if (File.Exists(provider)) PinyinSearchTests.InstalledProvider(provider);
        }),
        ("NEI resolves inventory, terminal and bookmark-only crafting with native blockers first", SurfaceSmoke.Run),
        ("native inventory bounds are screen-anchored and scale with the canvas", NativeInventoryAnchor),
        ("catalog search preserves definition order and keeps all matching types", CatalogListingTests.Run),
        ("catalog tooltip previews start dry without using a live item", TooltipSmoke.DryPreview),
        ("the built assembly contains all namespaced fallback GUI textures", EmbeddedGuiTextures),
        ("embedded Back keeps its two-tone art and crafting tabs use states atlas frames", GuiArtworkTests.BackAndRectangularTabs),
        ("the embedded tooltip backplate is opaque across its entire frame", GuiArtworkTests.TooltipBackground),
        ("native tooltip suppression is bounded by visible NEI regions", TooltipSmoke.Run),
        ("item and liquid tooltips keep native data separate from IDs and mod origins", TooltipSmoke.ItemContent),
        ("tooltip IDs default on, persist, and copy only on Shift-left-click", TooltipSmoke.IdSettingAndInput),
        ("item tooltip text retains native TMP colors, alpha and line breaks", TooltipSmoke.Colors),
        ("ingredient tooltips list all and only supported items", TooltipSmoke.SupportedIngredientNames),
        ("drag hooks match the installed game's native entry points", TooltipSmoke.DragEndpoints),
        ("bookmark crafting and pickup hooks match the installed game", TooltipSmoke.BookmarkNativeEndpoints),
        ("item and liquid cheat clicks preserve lookup, drag modifiers and recipe bookmarks", BookmarkCheatClicks),
        ("catalog and bookmark giving share a 1–64 count and right click stays at one", SharedSlotGiveAmount),
        ("multi-digit quantities parse, clamp and survive config reload", CatalogQuantityTests.Run),
        ("spawn mode defaults to NEI and persists both choices in BepInEx config", SpawnModeConfig),
        ("vanilla spawning uses the player position for items and preserves NEI liquid delivery", SpawnPlacement),
        ("temporary bottles hold only the selected liquid at native capacity", FullTemporaryBottle),
        ("bookmark chain reports raw requirements and reuses byproducts across targets", BookmarkChainBalances),
        ("chain tooltip and dependency tree follow explicit language changes", BookmarkParityTests.ChainNotesFollowInterfaceLanguage),
        ("production and usage methods retain alternative rows in index order", RecipeMethodsTests.AlternateRows),
        ("method navigation is independent of recipe pagination", RecipeMethodsTests.MethodNavigationIsIndependentOfRecipePages),
        ("debug preview pages overflowing crafting methods", RecipeMethodsTests.DebugPreviewNavigatesOnePageAtATime),
        ("native crafting, cooking and provider machines are distinct lookup methods", RecipeMethodsTests.NativeAndMachineMethods),
        ("built-in cooker labels are bilingual without changing recipe identity", RecipeMethodsTests.BuiltInCookingLocalization),
        ("method groups handle empty lookups and missing process metadata", RecipeMethodsTests.MissingMetadataAndEmptyMatches),
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

static void ModSourceSearch()
{
    static bool Native(string term) => NeiSearch.Matches(term, "Rope", "native", "", "", "rope");
    foreach (string query in new[] { "@CasualtiesUnknown", "@casualtiesunknown", "@\"Casualties: Unknown\"", "@\"Casualties Unknown\"", "@native" })
        Check(Native(query), $"The base game must match '{query}'.");
    Check(Search("@FORGEGUILD steel") && Search("@\"Forge\tGuild\" steel") && !Search("-@ForgeGuild") &&
        Search("@WrongSource | @forgeguild"), "Normalize only the mod field, without changing AND, OR or exclusion.");
    Check(!Search("@forge guild") && !Search("heavyplate") && !Search("$metaltools") && !Search("#heatresistant") &&
        !Search("&plateheavy02") && !Search("@\"   \""), "Other fields and unquoted query separators must not change.");
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

static void LiquidSlotAmounts()
{
    string Fit(double volume, int width) => new SlotQuantity(volume, true).Fit(text => text.Length <= width);
    Check(Fit(1250, 6) == "1250mL" && Fit(1250, 5) == "1.25k" && Fit(1250, 3) == "1k",
        "Keep mL when it fits; otherwise keep as much precision as possible in the abbreviated value.");
    Check(Fit(999, 2) == "999mL" && Fit(.125, 3) == "0.13mL" && Fit(0, 3) == "0mL",
        "Small quantities preserve units and are scaled by the renderer rather than rounded into a different tier.");
    Check(Fit(1_500_000, 4) == "1.5m" && Fit(2_000_000_000, 2) == "2g" &&
        Fit(3_000_000_000_000, 2) == "3t" && Fit(4_000_000_000_000_000, 2) == "4p",
        "Suffixes represent successive powers of 1000, not powers of 1024.");
    Check(Fit(999_999, 3) == "1m" && Fit(1e33, 8) == "1e+33",
        "Promote a rounded overflow and use bounded scientific notation beyond the suffix table.");
    var slot = new Rect(30, 50, 25, 24);
    var size = new Vector2(12, 10);
    RectEqual(new Rect(31, 63, 12, 10), new SlotQuantity(1250, true).LabelBounds(slot, size), "liquid label is bottom-left");
    var solid = new SlotQuantity(1500, false);
    Check(solid.Fit(_ => false) == "1500", "Solid counts do not change notation.");
    RectEqual(new Rect(42, 63, 12, 10), solid.LabelBounds(slot, size), "solid label stays bottom-right");
}

static void TooltipBounds()
{
    // A 1280×720 screen at 150% UI scale has an ~853×480 logical viewport.
    var viewport = new Rect(4, 4, 845, 472);
    RectEqual(new Rect(112, 115, 200, 60),
        TooltipPlacement.AtPointer(new Vector2(100, 100), new Vector2(200, 60), viewport), "follow the pointer when there is room");
    RectEqual(new Rect(630, 402, 200, 60),
        TooltipPlacement.AtPointer(new Vector2(838, 470), new Vector2(200, 60), viewport), "flip inward at the edge");
    RectEqual(viewport, TooltipPlacement.AtPointer(new Vector2(70, 100), new Vector2(900, 800), viewport),
        "an oversized tooltip is clamped to the logical viewport");
    var resultArea = new Rect(30, 40, 120, 100);
    var loneResult = TooltipPlacement.CenteredGridCell(resultArea, 30, 4, 1, 0);
    RectEqual(new Rect(75, 75, 30, 30), loneResult, "A lone property result is centered horizontally and vertically.");
    var shortTail = TooltipPlacement.CenteredGridCell(resultArea, 30, 4, 6, 4);
    var lastResult = TooltipPlacement.CenteredGridCell(resultArea, 30, 4, 6, 5);
    RectEqual(new Rect(60, 90, 30, 30), shortTail, "An incomplete second row is centered horizontally.");
    RectEqual(new Rect(90, 90, 30, 30), lastResult, "The last item shares the centered incomplete row.");
    foreach (var anchor in new[] { new Vector2(100, 100), new Vector2(838, 470) })
    {
        var source = new Rect(anchor - new Vector2(8, 8), new Vector2(16, 16));
        var box = TooltipPlacement.AtPointer(anchor, new Vector2(200, 60), viewport);
        var idRow = new Vector2(box.center.x, box.yMax - 20);
        for (int i = 0; i <= 20; i++)
        {
            Vector2 pointer = Vector2.Lerp(anchor, idRow, i / 20f);
            Check(TooltipPlacement.KeepItemTip(pointer, anchor, source, box, interactive: true),
                "Shift must hold the tip while moving across the gap into the ID row, even when flipped.");
            Check(!TooltipPlacement.KeepItemTip(pointer, anchor, source, box, interactive: false),
                "Without Shift an old hover must never stay pinned, even while the pointer is inside the tooltip.");
        }
        Check(!TooltipPlacement.KeepItemTip(new Vector2(12, 450), anchor, source, box, interactive: true),
            "Moving away from the source and popup must dismiss the pinned item.");
    }
}

static void RecipeNavigationTooltipBounds()
{
    var viewport = new Rect(4, 4, 1306, 730);
    var panel = new RecipePanelLayout(new Rect(390, 8, 548, 722));
    var pointer = new Vector2(485, 137);
    foreach (var size in new[] { new Vector2(260, 165), new Vector2(338, 260) })
    {
        Rect preview = TooltipPlacement.AtPointer(pointer, size, viewport);
        Check(preview.Overlaps(panel.Method) && preview.Overlaps(panel.Pages),
            "The preview may cover the method bar and paging buttons.");
        RectEqual(new Rect(497, 152, size.x, size.y), preview, "The preview keeps its normal offset below the mouse.");
        var movement = new Vector2(14, 9);
        RectEqual(new Rect(preview.position + movement, size), TooltipPlacement.AtPointer(pointer + movement, size, viewport),
            "The preview follows the mouse while it overlaps the navigation area.");
    }

    var source = new Rect(480, 159, 26, 26);
    var pinned = TooltipPlacement.AtSlot(source, new Vector2(360, 260), viewport);
    Check(pinned.Overlaps(panel.NavigationBounds), "A Shift-pinned candidate list may also overlap navigation.");
    RectEqual(new Rect(512, 159, 360, 260), pinned, "A pinned candidate list stays beside its source slot.");
}

static void NestedTooltipBounds()
{
    var viewport = new Rect(4, 4, 792, 472);
    var chain = new Rect(258, 72, 350, 295);
    var first = new Rect(280, 215, 30, 30);
    var adjacent = new Rect(311, 246, 30, 30);
    foreach (var size in new[] { new Vector2(160, 80), new Vector2(170, 225) })
    {
        Rect preview = TooltipPlacement.AtPointer(first.center, size, viewport);
        RectEqual(new Rect(307, 245, size.x, size.y), preview, "A nested preview keeps the current pointer offset.");
        Check(preview.Overlaps(adjacent) && preview.Overlaps(chain) && ContainsRect(viewport, preview),
            "A second-level preview can cover adjacent slots rather than move outside its parent frame.");
    }
}

static void TabDoesNotFocusSearch()
{
    Check(TabFocusPolicy.Consume(true, KeyCode.Tab, '\0') && TabFocusPolicy.Consume(true, KeyCode.None, '\t'),
        "The NEI overlay consumes both physical Tab and character-only Tab before Unity's default focus traversal.");
    Check(!TabFocusPolicy.Consume(false, KeyCode.Tab, '\0') && !TabFocusPolicy.Consume(false, KeyCode.None, '\t') &&
        !TabFocusPolicy.Consume(true, KeyCode.F, 'f') && !TabFocusPolicy.Consume(true, KeyCode.None, ' '),
        "Tab release, other keys and ordinary text input retain native behavior.");
}

static void SupportedItemsPagination()
{
    const int count = 103;
    var visible = new List<int>();
    for (int i = 0; i < TooltipPlacement.ItemListPage(count, 0, 265, 18).Pages; i++)
    {
        var page = TooltipPlacement.ItemListPage(count, i, 265, 18);
        Check(page.Index == i, "Every page index must be reachable.");
        visible.AddRange(Enumerable.Range(page.Start, page.End - page.Start));
    }
    Check(visible.SequenceEqual(Enumerable.Range(0, count)), "Every supported candidate must appear exactly once across pages.");
    Check(TooltipPlacement.ItemListPage(2, 12, 80, 18) == (0, 1, 0, 2), "A stale scroll position must not hide a short list.");

    var viewport = new Rect(4, 4, 500, 350);
    RectEqual(new Rect(124, 72, 132, 110), TooltipPlacement.AtSlot(new Rect(90, 72, 28, 28), new Vector2(132, 110), viewport),
        "A pinned popup must stay beside its source slot.");
    RectEqual(new Rect(332, 244, 132, 110), TooltipPlacement.AtSlot(new Rect(470, 326, 28, 28), new Vector2(132, 110), viewport),
        "A pinned popup should flip and clamp at the right and bottom edges.");
    var grid = new Rect(200, 100, 128, 64);
    var last = TooltipPlacement.CandidateCell(grid, 4, 32, 5);
    Check(TooltipPlacement.CandidateAt(grid, 4, 32, 16, 22, last.center) == 21 &&
        TooltipPlacement.CandidateAt(grid, 4, 32, 16, 22, new Vector2(295, 145)) == -1 &&
        TooltipPlacement.CandidateAt(grid, 4, 32, 16, 22, new Vector2(199, 106)) == -1,
        "Candidate clicks must respect the page offset and cannot select blank or out-of-tray cells.");
}

static void ScreenConversion()
{
    var screen = new Rect(124, 73, 397, 184);
    Rect gui = NeiLayout.FromScreen(screen, 720);
    RectEqual(new Rect(124, 463, 397, 184), gui, "left and size must stay unchanged; y must use yMax, not yMin");
    RectEqual(screen, NeiLayout.FromScreen(gui, 720), "flipping twice must restore the original rectangle");
    RectEqual(new Rect(0, 0, 1280, 720), NeiLayout.FromScreen(new Rect(0, 0, 1280, 720), 720), "full-screen rectangle");
}

static void OverlayLayout()
{
    foreach (float zoom in new[] { .7f, 1f, 1.4f })
    {
        Verify(2560, 1440, new(10, 135, 741, 1025), new(1831, 135, 719, 1042),
            new(104, 1198, 640, 96), new(1831, 1178, 719, 184));
        Verify(1280, 720, new(5, 67.5f, 370.5f, 512.5f), new(915.5f, 67.5f, 359.5f, 521),
            new(52, 599, 320, 48), new(915.5f, 589, 359.5f, 92));
        Verify(1920, 1080, new(7.5f, 101.25f, 555.75f, 768.75f), new(1373.25f, 101.25f, 539.25f, 781.5f),
            new(78, 898.5f, 480, 72), new(1373.25f, 883.5f, 539.25f, 138));
        Verify(1280, 1080, new(5, 101.25f, 370.5f, 768.75f), new(915.5f, 101.25f, 359.5f, 781.5f),
            new(52, 898.5f, 320, 72), new(915.5f, 883.5f, 359.5f, 138));

        void Verify(float width, float height, Rect left, Rect right, Rect tools, Rect footer)
        {
            float scale = NeiLayout.GuiScale(width, height, zoom);
            var layout = NeiLayout.Overlay(width / scale, height / scale);
            Rect Pixels(Rect rect) => new(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
            RectEqual(left, Pixels(layout.Bookmarks), "Bookmark bounds in top-left screen pixels");
            RectEqual(right, Pixels(layout.Catalog), "Catalog bounds in top-left screen pixels");
            RectEqual(tools, Pixels(layout.Tools), "Tool placement area in top-left screen pixels");
            RectEqual(footer, Pixels(layout.CatalogControls), "Search and history area in top-left screen pixels");
            Check(!left.Overlaps(tools) && !left.Overlaps(right) && footer.yMin > right.yMax, "Regions must not collide.");
        }
    }
}

static void InspectorLayout()
{
    var nearCorner = new Rect(540, 330, 80, 30);
    Rect panel = NeiLayout.Inspector(nearCorner, 640, 360, 1);
    RectEqual(new Rect(72, 8, 560, 344), panel, "corner anchor is clamped inside the viewport");

    var narrow = new Rect(30, 20, 200, 320);
    RectEqual(new Rect(8, 8, 244, 344), NeiLayout.Inspector(narrow, 260, 360, 1),
        "narrow screen constrains both dimensions without a negative rectangle");
    var displaced = new RecipePanelLayout(new Rect(150, 8, 300, 300), centerX: 280);
    var clamped = new RecipePanelLayout(new Rect(150, 8, 300, 300), centerX: 100);
    Check(displaced.Bounds.center.x == 280 && clamped.Bounds.xMin == 150,
        "Dragging sidebar panels keeps the recipe centered until the available boundary requires a clamp.");
    foreach (float height in new[] { 300f, 432f, 720f })
    {
        var recipe = new RecipePanelLayout(new Rect(100, 8, 245, height - 16));
        Check(Mathf.Abs(recipe.Bounds.center.y - height / 2) < .01f, "Recipe window must center on screen, not on the inventory anchor.");
        float inset = NeiAssets.RecipeBackgroundSlice;
        var inner = Rect.MinMaxRect(recipe.Frame.xMin + inset, recipe.Frame.yMin + inset,
            recipe.Frame.xMax - inset, recipe.Frame.yMax - inset);
        Check(ContainsRect(inner, recipe.Method) && ContainsRect(inner, recipe.Pages) && ContainsRect(inner, recipe.Content) &&
            recipe.Method.xMin == inner.xMin && recipe.Method.xMax == inner.xMax &&
            recipe.Pages.xMin == inner.xMin && recipe.Pages.xMax == inner.xMax &&
            recipe.Content.xMin == inner.xMin && recipe.Content.xMax == inner.xMax &&
            recipe.Method.yMax < recipe.Pages.yMin && recipe.Pages.yMax < recipe.Content.yMin,
            "The inset method and page controls align with the diagram area below them.");
        var firstTab = recipe.Tab(0);
        var lastTab = recipe.Tab(recipe.TabsPerPage - 1);
        Check(ContainsRect(recipe.NavigationBounds, recipe.Back) && ContainsRect(recipe.NavigationBounds, firstTab) &&
            ContainsRect(recipe.NavigationBounds, lastTab) && ContainsRect(recipe.Frame, recipe.Method) &&
            !recipe.Back.Overlaps(firstTab) && !recipe.Back.Overlaps(recipe.Method) &&
            recipe.PreviousMethod.xMin == recipe.Pages.xMin && recipe.NextMethod.xMax == recipe.Pages.xMax,
            "The lower arrows share inset edges; the tabs and Back stay in the protected header.");
        var diagram = RecipePanelLayout.DiagramArea(recipe.Content, 0);
        Check(Mathf.Abs(recipe.Content.x + diagram.center.x - inner.center.x) < .01f &&
            recipe.Content.x + diagram.xMin > recipe.Catalyst.xMax &&
            diagram.xMax < recipe.Content.width - RecipePanelLayout.ScrollbarAllowance,
            "Catalyst and scrollbar cannot move or overlap the centered diagram.");
    }
}

static void CatalogPanelLayout()
{
    foreach (var (width, height) in new[] { (2560, 1440), (1280, 720), (640, 480), (360, 640), (3440, 1440) })
    foreach (float zoom in new[] { .7f, 1f, 1.4f })
    {
        float scale = NeiLayout.GuiScale(width, height, zoom);
        var (_, catalog, _, footer) = NeiLayout.Overlay(width / scale, height / scale);
        var layout = new NeiLayout.CatalogPanel(catalog, footer);
        Rect[] regions = [layout.Search, layout.Quantity, layout.Increase, layout.Decrease, layout.History];
        for (int i = 0; i < regions.Length; i++)
        {
            Check(ContainsRect(footer, regions[i]), "Each editor, stepper and history slot must fit inside the footer.");
            for (int j = i + 1; j < regions.Length; j++)
                Check(!regions[i].Overlaps(regions[j]), "Footer controls must not overlap.");
        }
        Check(layout.Search.width >= 40 && layout.Quantity.width >= 40 && layout.Search.height >= 15 && layout.History.height >= 15,
            "Both editors and the history strip remain usable in compact layouts.");
        Check(ContainsRect(catalog, layout.Grid) && layout.Grid.yMax <= footer.yMin && layout.Grid.height >= 28 && layout.Grid.width >= 28,
            "The item grid fits above the footer, independent of whether history is visible.");
        Check(new[] { layout.Previous, layout.Page, layout.Next }.All(button => ContainsRect(catalog, button) && button.yMax < layout.Grid.yMin),
            "Page navigation stays above the item grid, not in the footer.");
    }
}

static void LeftToolsLayout()
{
    var (bookmarks, _, area, _) = NeiLayout.Overlay(1280, 720);
    var buttons = new NeiLayout.ToolButtons(area);
    Check(ContainsRect(area, buttons.Settings) && ContainsRect(area, buttons.Bookmarks) && !area.Overlaps(bookmarks),
        "Both buttons fit in the separate tool area.");
    Check(buttons.Settings.size == new Vector2(30, 30) && buttons.Settings.size == buttons.Bookmarks.size &&
        buttons.Settings.y == buttons.Bookmarks.y && NeiLayout.BookmarkCellSize == 34f && NeiLayout.BookmarkControlSize == 28f,
        "Settings and bookmarks have enlarged, aligned tools, with larger bookmark cells and page controls.");
    var compact = new NeiLayout.ToolButtons(new Rect(0, 0, 45, 19));
    Check(compact.Settings.size == new Vector2(19, 19) && compact.Bookmarks.xMax <= 45,
        "Both tools scale down when the reserved area is compact.");
    Check(!buttons.Settings.Overlaps(buttons.Bookmarks) && buttons.Hit(buttons.Settings.center, false) &&
        buttons.Hit(buttons.Bookmarks.center, true) && !buttons.Hit(buttons.Bookmarks.center, false) && !buttons.Hit(area.center, true) &&
        !buttons.Hit(new Vector2(buttons.Settings.xMax + 1, buttons.Settings.center.y), true),
        "Unused space and a hidden bookmark button do not capture the pointer.");
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

static void BookmarkShortcutHelp()
{
    var ordinary = BookmarkHelp.Rows(page: false, hasEntries: true, chain: false, crafting: false, chinese: false, bookmarkKey: "Q").Split('\n');
    var active = BookmarkHelp.Rows(page: false, hasEntries: true, chain: true, crafting: false, chinese: false, bookmarkKey: "Q").Split('\n');
    Check(ordinary.Contains("Right click: enable chain") && !ordinary.Any(line => line.StartsWith("T:")), "Only an enabled chain opens a tree.");
    Check(active.Contains("Right click: disable chain") && active.Contains("T: inspect the crafting tree") &&
        active.Contains("Hold Shift: pin totals and show steps"), "Enabled chains retain shortcut help alongside their totals view.");
    Check(active.All(line => line.Count(character => character == ':') == 1) &&
        active.Contains("Alt+left click: ungroup without deleting") && active.Contains("Ctrl+right click: ungroup without deleting"),
        "Each gesture has a separate display row, even when two gestures do the same thing.");
    Check(active.Contains("Shift+Q: remove group") && !active.Any(line => line.StartsWith("Shift+A:")), "Display the current bookmark binding.");

    var page = BookmarkHelp.Rows(page: true, hasEntries: true, chain: true, crafting: true, chinese: true, bookmarkKey: null).Split('\n');
    Check(page.Contains("右键：关闭未分组合成链") && page.Contains("Ctrl＋右键：切换整页合成链") &&
        page.Contains("Ctrl＋C：复制整页"), "Do not confuse ungrouped chain actions with entire-page actions.");
    Check(page.Contains("Shift＋C：制作目标") && page.Contains("Ctrl＋Shift＋C：补齐目标数量") &&
        !page.Any(line => line.StartsWith("T：") || line.Contains("删除")), "Crafting uses production shortcuts; unbound actions are hidden.");
    var empty = BookmarkHelp.Rows(page: true, hasEntries: false, chain: true, crafting: false, chinese: false, bookmarkKey: "A");
    Check(empty == "Left click: switch grid / queue\nCtrl+V: paste into this page", "Empty pages still allow paste, but no chain actions.");
}

static void TextureLocations()
{
    Check(NeiAssets.ResourceName(NeiAssets.StatesAtlas) == "assets/notenoughitems/textures/gui/sprites/interface_states.png" &&
        NeiAssets.ExpectedSize(NeiAssets.StatesAtlas) == (256, 256), "Use the supplied 256×256 sheet under textures/gui/sprites.");
    Check(NeiAssets.ResourceName(NeiAssets.RecipeBackgroundTexture) == "assets/notenoughitems/textures/gui/panels/recipe_background.png",
        "Use the canonical background under textures/gui/panels.");
    Check(NeiAssets.RelativePath(NeiAssets.Background("")) == "assets/notenoughitems/textures/gui/backgrounds/default.png" &&
        NeiAssets.LegacyPath(NeiAssets.Background("recipe")) == "Textures/background-recipe.png", "Optional background overrides remain readable.");
    foreach (var (name, x, y, width, height) in new[]
    {
        ("bookmark", 0, 0, 32, 32), ("bookmark_selected", 32, 0, 32, 32),
        ("options", 64, 0, 32, 32), ("options_cheat", 128, 0, 32, 32),
        ("favorite", 32, 32, 16, 16), ("favorite_selected", 48, 32, 16, 16),
        ("copy", 0, 64, 16, 16), ("paste", 16, 64, 16, 16), ("close", 16, 80, 16, 16),
        ("previous", 48, 64, 16, 16), ("next", 32, 64, 16, 16),
        ("recipe_arrow", 32, 64, 16, 16), ("back", 0, 80, 16, 16),
        ("recipe_slot", 161, 209, 18, 18), ("scroll_horizontal_track", 161, 161, 22, 16),
        ("scroll_vertical_track", 192, 161, 16, 22), ("scroll_recipe_track", 209, 161, 26, 42),
        ("scroll_horizontal_thumb", 161, 193, 18, 12), ("scroll_vertical_thumb", 192, 192, 12, 18)
    })
    {
        Check(ModernityFrames.Find(name) is { ResourcePath: NeiAssets.StatesAtlas } frame &&
            (frame.X, frame.Y, frame.Width, frame.Height) == (x, y, width, height), "Incorrect atlas frame: " + name);
    }
    Check(new[] { "add", "remove" }.All(name => BuiltInControlArt.IsQuantitySymbol(name) && ModernityFrames.Find(name) == null) &&
        !BuiltInControlArt.IsQuantitySymbol("back") && ModernityFrames.Find("small_button") == null &&
        new[] { "back", "previous", "next", "close" }.All(ModernityFrames.HasWidgetBackplate),
        "Plus/minus are built in; Back is the original sprite on a normal widget button.");
    Check(ModernityFrames.IconGraphic("bookmark", false) == "bookmark" &&
        ModernityFrames.IconGraphic("bookmark", true) == "bookmark_selected" &&
        ModernityFrames.IconGraphic("options", true) == "options" &&
        ModernityFrames.IconGraphic("options_cheat", true) == "options_cheat",
        "Keep the semantic bookmark and wrench sprites independent of transient button styling.");
    Color[] grid = [(Color)new Color32(255, 0, 170, 255), (Color)new Color32(255, 190, 170, 255)];
    Check(!ModernityFrames.HasBackArtwork(grid) && !ModernityFrames.HasBackArtwork(new Color[4]) &&
        ModernityFrames.HasBackArtwork([grid[0], Color.white, default]),
        "The new Back frame alone falls back to bundled artwork if missing from an older atlas.");

    RectEqual(new Rect(12, 27, 6, 8), ModernityFrames.FitButtonSprite(new Rect(10, 20, 10, 22), 6, 8, float.PositiveInfinity, false),
        "The narrow tab rail centers the non-square arrow without stretching it");
    RectEqual(new Rect(1, 8, 4, 2), ModernityFrames.FitButtonSprite(new Rect(0, 0, 6, 18), 8, 4, float.PositiveInfinity, false),
        "A compact control scales both sprite axes by the same factor");
    Check(ModernityFrames.FitsButtonBorders(new Rect(0, 0, 10, 22), 2, 2, 2, 2) &&
        !ModernityFrames.FitsButtonBorders(new Rect(0, 0, 3, 22), 2, 2, 2, 2),
        "Only use fixed-corner slicing when the full borders and center fit.");
    Check(ModernityFrames.Find("recipe_search") == null && ModernityFrames.Find("search") == null,
        "The inspector no longer exposes the recipe-search icon.");
    var ink = new Color[18 * 20];
    for (int y = 4; y <= 17; y++)
        for (int x = 3; x <= 16; x++) ink[y * 18 + x] = new Color(1, 1, 1, 1);
    Check(ModernityFrames.InkBounds(18, 20, ink) == (3, 4, 14, 14) &&
        ModernityFrames.InkBounds(18, 20, new Color[18 * 20]) == (0, 0, 18, 20),
        "The favorite button centers visible ink despite asymmetric transparency.");
    Check(ModernityFrames.Find("search_field") is { ResourcePath: NeiAssets.TextFieldAtlas, X: 0, Y: 12, Width: 128, Height: 12 } &&
        ModernityFrames.Find("search_field_focused") is { ResourcePath: NeiAssets.TextFieldAtlas, X: 0, Y: 0, Width: 128, Height: 12 } &&
        ModernityFrames.Find("tooltip_background") is { ResourcePath: NeiAssets.TextFieldAtlas, X: 0, Y: 0, Width: 128, Height: 12 },
        "Search and tooltip surfaces use individual rows of the supplied atlas, not the transparent 128×128 canvas.");
    Check(ModernityFrames.Find("recipe_method_title") == null,
        "The method title is a label, not a beveled atlas button.");
    Check(NeiAssets.ExpectedSize(NeiAssets.DropDownAtlas) == (256, 256), "Keep the supplied dropdown atlas canvas.");
    foreach (var (inside, enabled, triggerY, triggerHeight, optionY, optionHeight, offset) in new[]
    {
        (false, true,   0, 20, 128, 19, 0),
        (true,  true,  64, 19, 192, 18, 1),
        (true,  false, 32, 18, 160, 17, 2)
    })
    {
        var state = ModernityFrames.DropDownState(ButtonVisual.Resolve(enabled, inside));
        Check(state.Offset == offset, "Shorter dropdown faces move the face without moving the row's hit target.");
        CheckDropDownFrame("dropdown_closed" + state.Suffix, 0, triggerY, triggerHeight);
        CheckDropDownFrame("dropdown_open" + state.Suffix, 112, triggerY, triggerHeight);
        CheckDropDownFrame("dropdown_option" + state.Suffix, 0, optionY, optionHeight);
    }
    Check(ModernityFrames.DropDownState(ButtonVisual.Resolve(true, false), selected: true) == ("_hover", 0) &&
        ModernityFrames.DropDownState(ButtonVisual.Resolve(true, true), selected: true) == ("_hover", 1),
        "A selected option keeps its highlight at rest and the normal hover geometry under the pointer.");

    static void CheckDropDownFrame(string name, int x, int y, int height) =>
        Check(ModernityFrames.Find(name) is { ResourcePath: NeiAssets.DropDownAtlas, Width: 112 } frame &&
            (frame.X, frame.Y, frame.Height) == (x, y, height), "Incorrect dropdown atlas crop: " + name);
    Check(NeiAssets.ExpectedSize(NeiAssets.CheckboxAtlas) == (64, 64) &&
        NeiAssets.TextureSources(NeiAssets.CheckboxAtlas, true).Last() == (NeiAssets.ResourceName(NeiAssets.CheckboxAtlas), true),
        "Checkbox replacements require the original atlas dimensions and have an embedded fallback.");
    foreach (var (name, x, y) in new[] { ("checkbox_off", 0, 28), ("checkbox_off_hover", 22, 28),
        ("checkbox_on", 0, 40), ("checkbox_on_hover", 22, 40) })
        Check(ModernityFrames.Find(name) is { ResourcePath: NeiAssets.CheckboxAtlas, Width: 22, Height: 12 } frame &&
            (frame.X, frame.Y) == (x, y), "Incorrect NEI switch atlas crop: " + name);
    Check(ModernityFrames.CheckboxFace(on: false, hovered: true) == "checkbox_off_hover" &&
        ModernityFrames.CheckboxFace(on: true, hovered: false) == "checkbox_on",
        "Use the supplied idle and hover frames for regular NEI switches.");
    Check(ModernityFrames.Find(ModernityFrames.SquareCheckboxFace(false, false)) is
        { ResourcePath: NeiAssets.CheckboxAtlas, X: 29, Y: 1, Width: 12, Height: 12 } &&
        ModernityFrames.Find(ModernityFrames.SquareCheckboxFace(false, true)) is
        { ResourcePath: NeiAssets.CheckboxAtlas, X: 43, Y: 1, Width: 12, Height: 12 } &&
        ModernityFrames.Find(ModernityFrames.SquareCheckboxFace(true, false)) is
        { ResourcePath: NeiAssets.CheckboxAtlas, X: 29, Y: 15, Width: 12, Height: 12 } &&
        ModernityFrames.Find(ModernityFrames.SquareCheckboxFace(true, true)) is
        { ResourcePath: NeiAssets.CheckboxAtlas, X: 43, Y: 15, Width: 12, Height: 12 },
        "Spawn mode uses all four supplied square checkbox states.");
    Check(NeiAssets.ExpectedSize(NeiAssets.RecipeBackgroundTexture) == (256, 256) &&
        ModernityFrames.Find("recipe_background") is { ResourcePath: NeiAssets.RecipeBackgroundTexture, X: 0, Y: 0, Width: 184, Height: 174 } &&
        NeiAssets.RecipeBackgroundSlice == 9 &&
        ModernityFrames.Find(NeiAssets.RecipeTabSelected) is { ResourcePath: NeiAssets.StatesAtlas, X: 160, Y: 112, Width: 48, Height: 47 } &&
        ModernityFrames.Find(NeiAssets.RecipeTabCandidate) is { ResourcePath: NeiAssets.StatesAtlas, X: 208, Y: 112, Width: 48, Height: 47 } &&
        NeiAssets.RecipeTabStripWidth == 96 && NeiAssets.ExpectedSize(NeiAssets.StatesAtlas) == (256, 256),
        "The recipe inspector crops the adjacent method-tab frames to their 48x47 painted bounds.");
    Check(ModernityFrames.Find("button") is { ResourcePath: NeiAssets.WidgetsAtlas, Y: 66 } &&
        ModernityFrames.Find("button_disabled") is { ResourcePath: NeiAssets.WidgetsAtlas, Y: 46 } &&
        ModernityFrames.Find("button_hover") is { ResourcePath: NeiAssets.WidgetsAtlas, Y: 86 },
        "Use the relocated widget backplates without changing state coordinates.");
    foreach (var (texture, previousPaths) in new (string, string[])[]
    {
        (NeiAssets.StatesAtlas, ["guis/states.png"]),
        (NeiAssets.RecipeBackgroundTexture, ["guis/recipebg.png", "gui/modernity/recipebg.png"]),
        (NeiAssets.WidgetsAtlas, ["gui/sprites/widgets.png", "gui/modernity/widgets.png"]),
        (NeiAssets.TextFieldAtlas, ["gui/sprites/text_field.png", "gui/controls/text_field.png"]),
        (NeiAssets.DropDownAtlas, ["gui/sprites/drop_down_list.png", "gui/controls/drop_down_list.png"]),
        (NeiAssets.CheckboxAtlas, ["gui/sprites/checkbox.png", "gui/controls/checkbox.png"])
    })
    {
        string canonical = NeiAssets.RelativePath(texture);
        var defaults = new[] { (canonical, false), (canonical, true) };
        var pack = new[] { texture }.Concat(previousPaths)
            .Select(path => (NeiAssets.ActivePackDirectory + "/" + NeiAssets.RelativePath(path), false));
        Check(NeiAssets.TextureSources(texture, true).SequenceEqual(pack.Concat(defaults)) &&
            NeiAssets.TextureSources(texture, false).SequenceEqual(defaults),
            "Prefer the new active-pack path, then older pack paths, then the canonical loose and embedded assets: " + texture);
    }
}

static void ButtonBevelPixels()
{
    // At 1.25× the two side texels cover three pixels. The extra pixel belongs to
    // the inner texel on both sides, not to opposite colors at a sampling tie.
    Check(ButtonSlicePixels.Column(0, 29, 200, 2, 1.25f) == (0, 1, 0, 1) &&
        ButtonSlicePixels.Column(1, 29, 200, 2, 1.25f) == (1, 3, 1, 2) &&
        ButtonSlicePixels.Column(4, 29, 200, 2, 1.25f) == (28, 29, 199, 200),
        "A fractional bevel repeats the same corresponding texels on either side.");

    const int sourceWidth = 200, side = ModernityFrames.ButtonSlice;
    foreach (float scale in new[] { .45f, .9f, 1f, 1.25f, 1.5f, 1.8f, 2f, 2.4f })
        foreach (int width in new[] { 11, 12, 23, 24, 29, 120, 161 })
        {
            int end = 0, sourceEnd = 0;
            for (int i = 0; i <= 2 * side; i++)
            {
                var band = ButtonSlicePixels.Column(i, width, sourceWidth, side, scale);
                var opposite = ButtonSlicePixels.Column(2 * side - i, width, sourceWidth, side, scale);
                Check(band.Start == end && band.SourceStart == sourceEnd && band.End >= band.Start &&
                    band.SourceEnd > band.SourceStart, "Slices must meet without gaps, overlaps or inverted UVs.");
                Check(band.Start == width - opposite.End && band.End == width - opposite.Start &&
                    band.SourceStart == sourceWidth - opposite.SourceEnd && band.SourceEnd == sourceWidth - opposite.SourceStart,
                    $"Mismatched source or device pixels at width {width}, scale {scale}, band {i}.");
                (end, sourceEnd) = (band.End, band.SourceEnd);
            }
            Check(end == width && sourceEnd == sourceWidth, "The slices must cover the whole face.");
        }
}

static void DragReleaseOwnership()
{
    var release = DragInputPolicy.ReleaseOverOverlay;
    Check(release(true, false, true) == DragInputPolicy.Release.Native,
        "A real native drop target at an overlapping rectangle still receives the original release.");
    Check(release(true, false, false) == DragInputPolicy.Release.CancelDrag,
        "A preview over a NEI-only field is cancelled, not dropped into the world.");
    Check(release(false, true, false) == DragInputPolicy.Release.OverlayClick,
        "A NEI click must suppress native release without cancelling the MouseUp required by IMGUI.");
    Check(release(false, false, false) == DragInputPolicy.Release.Native,
        "A completed native click must not be mistaken for a NEI click.");
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

static void MultiplayerPermissions()
{
    Check(CheatPermission.ForTogether(false, false, null, null, null, null) == CheatAccess.Offline, "No active transport is offline.");
    Check(CheatPermission.ForTogether(false, true, false, true, true, true) == CheatAccess.Unavailable, "A connecting transport is not offline.");
    Check(CheatPermission.ForTogether(null, false, null, null, true, true) == CheatAccess.Unavailable, "An unreadable network state fails closed.");
    Check(CheatPermission.ForTogether(true, true, true, false, true, null) == CheatAccess.Host, "A host follows the server cheat rule.");
    Check(CheatPermission.ForTogether(true, true, false, true, true, true) == CheatAccess.AdminClient, "An authorized client can submit through Together's admin dispatcher.");
    Check(CheatPermission.ForTogether(true, true, false, true, true, false) == CheatAccess.Unavailable, "A client-cheats rule is not an admin grant.");
    Check(CheatPermission.ForTogether(true, true, false, true, false, true) == CheatAccess.Unavailable, "Admin alone does not override a disabled cheat rule.");
    Check(CheatPermission.ForTogether(true, true, true, true, true, true) == CheatAccess.Unavailable, "Inconsistent roles fail closed.");
}

static void MultiplayerGiveCommand()
{
    Check(CheatPermission.GiveCommand("food/energy-bar", 2, 64) == "give @m food/energy-bar food/energy-bar",
        "Every item goes to the command caller as one bounded batch.");
    foreach (string id in new[] { "", "rope 1", "@a", "rope\nheal", "rope;heal", "../rope", "//rope" })
        Check(CheatPermission.GiveCommand(id, 1, 64) == null, $"Reject non-item or unsafe command token {id}.");
    Check(CheatPermission.GiveCommand("rope", 0, 64) == null && CheatPermission.GiveCommand("rope", 65, 64) == null,
        "Only quantities from 1 to 64 are valid.");
    Check(CheatPermission.GiveCommand("rope", 64, 64)?.Split(' ').Skip(2).SequenceEqual(Enumerable.Repeat("rope", 64)) == true,
        "A full batch sends all 64 items to the caller.");
}

#if RECIPE_TESTS
static void NeiModeContract()
{
    var assembly = typeof(Catalog).Assembly;
    var actions = assembly.GetType("NotEnoughItems.NeiActions", throwOnError: true)!;
    var mode = actions.GetNestedType("LocalMode", System.Reflection.BindingFlags.NonPublic)!;
    var names = Enum.GetNames(mode);
    Check(names.SequenceEqual(new[] { "Recipe", "Cheat" }), "The removed Utility mode must not remain in the mode contract.");
    Check(actions.GetMethod("Heal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) == null &&
        actions.GetMethod("SetSpeed", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) == null,
        "Healing and game-speed actions must not remain as hidden Utility-mode entry points.");
}
#endif

static void OptionsLayout()
{
    foreach (var (pixelWidth, pixelHeight, zoom) in new[] { (2560, 1440, 1f), (480, 410, 1.4f), (1920, 1080, .7f), (3440, 1440, 1f) })
    {
        float scale = NeiLayout.GuiScale(pixelWidth, pixelHeight, zoom);
        float width = pixelWidth / scale, height = pixelHeight / scale;
        var (left, right, _, _) = NeiLayout.Overlay(width, height);
        foreach (var host in new[] { new Rect(40, 0, 300, 40), new Rect(width - 500, height - 70, 400, 70) })
        {
            var (options, optionsZoom) = NeiLayout.OptionsBetween(host, left, right, width, height);
            var content = NeiLayout.OptionsContentBounds(options, optionsZoom);
            Equal(pixelHeight * .5f, options.center.y * scale, "Complete options frame centered in screen pixels");
            Check(ContainsRect(new Rect(8, 8, width - 16, height - 16), options) &&
                !options.Overlaps(left) && !options.Overlaps(right), "Settings fit in the viewport between both sidebars.");
            Check(content.position == options.position && content.width >= 244.99f && content.height >= 419.99f &&
                optionsZoom > 0f && optionsZoom <= NeiLayout.OptionsZoom, "Scaled drawing keeps its pivot and room for all rows.");
            if (pixelWidth >= 1920) Equal(1.2f, optionsZoom, "Settings and text grow by 20% when there is room");
        }
    }
}

static void SpawnModeChoiceLayout()
{
    foreach (float width in new[] { 221f, 360f, 536f })
    {
        var row = new Rect(12f, 174f, width, 54f);
        var (nei, toPlayer) = NeiLayout.SpawnModeChoices(row);
        Check(ContainsRect(row, nei) && ContainsRect(row, toPlayer) && nei.y == toPlayer.y &&
            nei.size == toPlayer.size && nei.xMax < toPlayer.xMin && !nei.Overlaps(toPlayer),
            "Both spawn choices should fit side-by-side as independent targets, even at the minimum panel width.");
    }
}

static void FullWidthHostLayout()
{
    float scale = NeiLayout.GuiScale(480, 410, 1.4f);
    float width = 480 / scale, height = 410 / scale;
    var (left, right, tools, _) = NeiLayout.Overlay(width, height);
    var viewport = new Rect(0, 0, width, height);
    Check(ContainsRect(viewport, left) && ContainsRect(viewport, right) && ContainsRect(viewport, tools),
        "All overlay regions must remain inside a narrow viewport.");
    foreach (var host in new[] { viewport, new Rect(width * .6f, 36, 200, 400), new Rect(0, 115, 290, 380) })
    {
        var inspector = NeiLayout.InspectorBetween(host, left, right, width, height);
        Check(!inspector.Overlaps(left) && !inspector.Overlaps(right) && inspector.width >= 244.999f,
            "Wide or off-center hosts must leave room for the inspector without moving either sidebar.");
    }
}

static bool ContainsRect(Rect bounds, Rect rect) => rect.width > 0 && rect.height > 0 &&
    rect.xMin >= bounds.xMin && rect.yMin >= bounds.yMin && rect.xMax <= bounds.xMax && rect.yMax <= bounds.yMax;

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
    foreach (string path in new[] { NeiAssets.StatesAtlas, NeiAssets.WidgetsAtlas, NeiAssets.TextFieldAtlas,
        NeiAssets.DropDownAtlas, NeiAssets.RecipeBackgroundTexture, NeiAssets.CheckboxAtlas })
    {
        using var png = assembly.GetManifestResourceStream(NeiAssets.ResourceName(path));
        Check(png != null, "Missing namespaced resource: " + path);
        png!.ReadExactly(header);
        Check(header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Fallback is not PNG: " + path);
        int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..]);
        int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..]);
        Check(NeiAssets.ExpectedSize(path) == (width, height), "Bundled sheet has incorrect size: " + path);
    }
}

static void BookmarkCheatClicks()
{
    var window = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NeiWindow", throwOnError: true)!;
    var check = window.GetMethod("CanGiveSlot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
        null, [typeof(bool), typeof(string), typeof(CatalogItem), typeof(int), typeof(bool), typeof(bool), typeof(bool)], null)!;
    bool Can(CatalogItem? item, string? id = null, int button = 0, bool shift = false, bool alt = false, bool cheat = true, bool source = true) =>
        (bool)check.Invoke(null, [source, id ?? item?.BrowseId ?? "missing", item, button, shift, alt, cheat])!;
    foreach (var item in new[] { new CatalogItem("water"), new CatalogItem("water", kind: CatalogItemKind.Liquid) })
    {
        Check(Can(item) && Can(item, button: 1), "Both physical and liquid entries accept left and right clicks in Cheat mode.");
        Check(!Can(item, shift: true) && !Can(item, alt: true) && !Can(item, button: 2), "Lookup/drag and bookmark gestures never give either kind.");
        Check(!Can(item, cheat: false) && !Can(item, source: false) && !Can(item, id: "recipe/native/water"),
            "Recipe browsing, other menus and recipe bookmarks never give either kind.");
    }
    Check(!Can(null) && !Can(new CatalogItem("water", kind: CatalogItemKind.Liquid, isVirtual: true)),
        "Missing and virtual liquid entries cannot be given.");
}

static void SharedSlotGiveAmount()
{
    var actions = typeof(Catalog).Assembly.GetType("NotEnoughItems.NeiActions", throwOnError: true)!;
    var slotAmount = actions.GetMethod("SlotGiveAmount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    int Amount(int button, int value) => (int)slotAmount.Invoke(null, [button, value])!;
    for (int count = 1; count <= 64; count++)
        Check(Amount(0, count) == count && Amount(1, count) == 1, "Left gives the selected count; right gives one.");
    Check(Amount(0, 0) == 1 && Amount(0, 65) == 64, "Invalid counts cannot give outside the configured 1–64 range.");
}

static void SpawnModeConfig()
{
    // Keep scratch config in the writable test output rather than touching the live BepInEx config.
    string path = Path.Combine(AppContext.BaseDirectory, $"nei-spawn-mode-{Guid.NewGuid():N}.cfg");
    var plugin = typeof(Catalog).Assembly.GetType("NotEnoughItems.Plugin", throwOnError: true)!;
    var bind = plugin.GetMethod("BindSpawnMode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    BepInEx.Configuration.ConfigEntryBase Reload() => (BepInEx.Configuration.ConfigEntryBase)bind.Invoke(null,
        [new BepInEx.Configuration.ConfigFile(path, saveOnInit: false)])!;

    try
    {
        var entry = Reload();
        Check(entry.BoxedValue.ToString() == "Nei", "Existing installations without this setting keep NEI pickup.");
        Check(Enum.GetNames(entry.SettingType).SequenceEqual(new[] { "Nei", "SpawnToPlayer" }), "Offer exactly the two spawn modes.");
        foreach (string mode in new[] { "SpawnToPlayer", "Nei" })
        {
            entry.BoxedValue = Enum.Parse(entry.SettingType, mode);
            entry = Reload();
            Check(entry.BoxedValue.ToString() == mode, $"BepInEx reload restores {mode}.");
        }
    }
    finally { File.Delete(path); }
}

static void SpawnPlacement()
{
    var assembly = typeof(Catalog).Assembly;
    var mode = assembly.GetType("NotEnoughItems.SpawnMode", throwOnError: true)!;
    var actions = assembly.GetType("NotEnoughItems.NeiActions", throwOnError: true)!;
    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    var pickup = actions.GetMethod("UsesVanillaPickup", flags)!;
    bool UsesVanilla(string choice, CatalogItemKind kind) => (bool)pickup.Invoke(null, [Enum.Parse(mode, choice), kind])!;
    Check(UsesVanilla("SpawnToPlayer", CatalogItemKind.Item) && !UsesVanilla("Nei", CatalogItemKind.Item) &&
        !UsesVanilla("SpawnToPlayer", CatalogItemKind.Liquid), "Only physical items use the vanilla pickup path when selected.");

    var spawnPosition = actions.GetMethod("SpawnPosition", flags)!;
    var player = new Vector2(12f, -8f);
    for (int i = 0; i < 9; i++)
    {
        Vector2 Position(bool vanilla) => (Vector2)spawnPosition.Invoke(null, [player, i, vanilla])!;
        var original = Position(vanilla: false);
        var native = Position(vanilla: true);
        Equal(player.x + (i % 7 - 3) * .10f, original.x, "NEI spawn spreads multiple drops horizontally");
        Equal(player.y + .4f, original.y, "NEI spawn stays above the player position");
        Equal(player.x, native.x, "Vanilla spawn uses same player X for all items");
        Equal(player.y, native.y, "Vanilla spawn uses the player Y");
    }
    Check(typeof(Body).GetMethod("AutoPickUpItem", [typeof(Item)])?.ReturnType == typeof(void),
        "The installed game exposes the native automatic pickup endpoint.");
}

static void FullTemporaryBottle()
{
    const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    const System.Reflection.BindingFlags privateStatic = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    var actions = typeof(Catalog).Assembly.GetType("NotEnoughItems.NeiActions", throwOnError: true)!;
    var fill = actions.GetMethod("FillTemporaryBottle", privateStatic)!;
    var prefabId = (string)actions.GetField("TemporaryBottleId", privateStatic)!.GetRawConstantValue()!;
    Equal(300f, (float)actions.GetField("TemporaryBottleLifetime", privateStatic)!.GetRawConstantValue()!, "Native crafting bottle lifetime");
    Check(prefabId == "craftingbottle", "Liquid giving uses the native temporary crafting bottle.");

    // Use the game's managed liquid methods without requiring a Unity scene or native constructors.
    var item = (Item)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Item));
    item.id = prefabId;
    var bottle = (WaterContainerItem)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WaterContainerItem));
    typeof(WaterContainerItem).GetField("item", privateInstance)!.SetValue(bottle, item);
    var previous = Item.GlobalItems;
    var stats = new LiquidItemInfo { capacity = 750f };
    Item.GlobalItems = new Dictionary<string, ItemInfo> { [item.id] = stats };
    try
    {
        bottle.stack = [new LiquidStack("other", 12f), new LiquidStack("water", 25f)];
        fill.Invoke(null, [bottle, "water"]);
        Check(bottle.stack.Count == 1 && bottle.stack[0].liquidId == "water", "The bottle must not retain default or mixed contents.");
        Equal(stats.capacity, bottle.AmountOf("water"), "The selected liquid fills the whole current capacity");
        Equal(0f, bottle.SpaceLeft, "No space remains");
        Equal(1f, item.condition, "Native liquid condition reports a full bottle");

        stats.capacity = 0f;
        try
        {
            fill.Invoke(null, [bottle, "water"]);
            throw new Exception("A zero-capacity bottle must not be delivered.");
        }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
    }
    finally { Item.GlobalItems = previous; }
}

static void NativeInventoryAnchor()
{
    var adapter = typeof(Catalog).Assembly.GetType("NotEnoughItems.GameSurface", throwOnError: true)!;
    var anchor = adapter.GetMethod("AnchorInventory", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    var radial = new Rect(-297, -292, 594, 601);
    Rect Resolve(float screenWidth, float screenHeight, float scale) =>
        (Rect)anchor.Invoke(null, [radial, screenWidth, screenHeight, scale])!;
    RectEqual(new Rect(343, 68, 594, 601), Resolve(1280, 720, 1), "Centered vanilla inventory");
    RectEqual(new Rect(514.5f, 102, 891, 901.5f), Resolve(1920, 1080, 1.5f), "Screen/UI scaling");
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
