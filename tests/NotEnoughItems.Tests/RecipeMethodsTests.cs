using System.Reflection;
using NotEnoughItems;
using UnityEngine;
using Category = NotEnoughItems.RecipeCategory;
using Row = NotEnoughItems.Recipe;

internal static class RecipeMethodsTests
{
    internal static void AlternateRows()
    {
        // Two providers can use the same local recipe ID. Both alternatives belong in the result.
        var cast = new Row("gear", "metal", [new RecipeIngredient("ingot")], [new RecipeOutput("gear")], source: "a");
        var cut = new Row("gear", "METAL", [new RecipeIngredient("plate")], [new RecipeOutput("gear")], source: "b");
        var toolUse = new Row("hold", "metal", [new RecipeIngredient("ingot", isCatalyst: true)], [new RecipeOutput("die")]);
        var press = new Row("press", "press", [new RecipeIngredient("ingot")], [new RecipeOutput("gear")]);
        Category[] categories = [new("press", "Press"), new("metal", "Metalwork"), new("unused")];

        // These are the already-matched production and reverse-use index rows, not the whole catalog.
        var producesGear = RecipeMethods.Group([cut, press, cast], categories, chinese: true);
        var usesIngot = RecipeMethods.Group([press, cast, toolUse], categories, chinese: true);

        Check(producesGear.Select(m => m.Name).SequenceEqual(["Metalwork", "Press"]) &&
            producesGear[0].Recipes.SequenceEqual([cut, cast]) && producesGear[1].Recipes.SequenceEqual([press]),
            "Keep both recipes for one output, in index order, without adding an All or unused method.");
        Check(usesIngot.Select(m => m.Name).SequenceEqual(["Press", "Metalwork"]) &&
            usesIngot[0].Recipes.SequenceEqual([press]) && usesIngot[1].Recipes.SequenceEqual([cast, toolUse]),
            "A usage starts with its own first method and retains both material and tool uses.");

    }

    internal static void MethodNavigationIsIndependentOfRecipePages()
    {
        var assembled = new Row("assemble", "bench", [], [new RecipeOutput("gear")]);
        var repaired = new Row("repair", "bench", [], [new RecipeOutput("gear")]);
        var pressed = new Row("press", "press", [], [new RecipeOutput("gear")]);
        Category[] categories = [new("bench", "Workbench"), new("press", "Press")];
        var methods = RecipeMethods.Group([assembled, repaired], categories, chinese: false);
        var pages = new RecipePages();
        pages.Build(methods.Single().Recipes, _ => 100f, 100f);

        Check(!RecipeMethods.CanSwitch(0) && !RecipeMethods.CanSwitch(methods.Length) &&
            RecipeMethods.Cycle(0, -1, methods.Length) == 0 && methods[0].Name == "Workbench" && pages.Count == 2,
            "A single method keeps its name and cannot switch, even when its recipe pages can turn.");

        methods = RecipeMethods.Group([assembled, pressed], categories, chinese: false);
        int next = RecipeMethods.Cycle(0, 1, methods.Length);
        pages.Build(methods[next].Recipes, _ => 100f, 100f);

        Check(RecipeMethods.CanSwitch(methods.Length) && methods[next].Name == "Press" &&
            methods[next].Recipes.Single() == pressed && pages.Count == 1 && RecipeMethods.Cycle(next, 1, methods.Length) == 0,
            "Switching methods updates their name and rows even when there is only one recipe page.");
    }

    internal static void DebugPreviewNavigatesOnePageAtATime()
    {
        var window = typeof(Catalog).Assembly.GetType("NotEnoughItems.NeiWindow", throwOnError: true)!;
        var toggle = window.GetField("_previewMethodTabs", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(!(bool)toggle.GetValue(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(window))!,
            "The preview is disabled by default and is not loaded from config.");

        foreach (int width in new[] { 104, 140, 180, 228 })
        {
            var panel = new RecipePanelLayout(new UnityEngine.Rect(0, 0, width, 400));
            int capacity = panel.TabsPerPage;
            var methods = RecipeMethods.PreviewTabs(capacity, chinese: true);
            var english = RecipeMethods.PreviewTabs(capacity, chinese: false);
            int pageCount = panel.TabPageCount(methods.Length);
            Check(methods.Length == capacity * 2 && pageCount == 2 &&
                methods.All(method => method.Recipes.Count == 0 && method.MachineId.Length == 0),
                "Provide exactly two pages of preview tabs without exposing fake recipe or machine actions.");
            Check(methods.Select(method => method.TabLabel).Distinct().Count() == methods.Length &&
                methods.Select(method => method.Id).SequenceEqual(english.Select(method => method.Id)) &&
                methods[0].Name == "测试方式 01" && english[0].Name == "Test method 01",
                "Every preview tab is distinguishable without changing the selection key when changing language.");
            Check(panel.TabPageStart(0) == 0 && panel.TabPageEnd(0, methods.Length) == capacity &&
                panel.TabPageStart(1) == capacity && panel.TabPageEnd(1, methods.Length) == methods.Length &&
                RecipeMethods.Cycle(0, 1, methods.Length) == 1 && RecipeMethods.Cycle(0, -1, methods.Length) == methods.Length - 1,
                "Tab arrows change the visible page, while method arrows remain a distinct one-method selection.");
            var pager = new RecipeMethodTabPager();
            int selected = RecipeMethods.SelectedIndex(methods, id: null);
            pager.Show(panel, methods, selected);
            var (left, right) = panel.TabArrows(pager.Page, methods.Length);
            var next = right.center;
            var previous = left.center;
            Check(!pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 1, next) &&
                !pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, panel.NextMethod.center) &&
                !pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, new Vector2(next.x, right.yMax)) &&
                pager.Page == 0, "Right-click, the lower method arrow and the panel seam are not tab-page hits.");
            Check(pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, next) && pager.Page == 1,
                "Next must display the next page immediately, even when no MouseUp reaches the inspector.");
            Check(!pager.TurnOnPress(panel, methods.Length, EventType.MouseUp, 0, next) &&
                !pager.TurnOnPress(panel, methods.Length, EventType.Repaint, 0, next), "Release and repaint do not turn the page again.");
            pager.Show(panel, methods, selected);
            pager.Show(panel, english, selected); // A language change preserves the browsing position.
            Check(pager.Page == 1 && panel.TabPageStart(pager.Page) == capacity && selected == 0,
                "Next holds the second page across redraws without switching the current method.");
            Check(pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, previous), "Previous accepts a left press.");
            pager.Show(panel, methods, selected);
            Check(pager.Page == 0, "Previous holds the first page on redraw.");
            Check(pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, previous) && pager.Page == 1,
                "Previous wraps from the first page to the last.");
            Check(pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, next) && pager.Page == 0,
                "Next wraps from the last page to the first.");
            pager.TurnOnPress(panel, methods.Length, EventType.MouseDown, 0, next);
            pager.Show(panel, methods, RecipeMethods.Cycle(selected, 1, methods.Length), reveal: true);
            Check(pager.Page == 0, "Changing the method with the lower arrow reveals its tab.");
            pager.Show(panel, methods, capacity);
            Check(pager.Page == 1, "Selecting an off-page method reveals its page.");
            pager.Show(panel, [methods[0]], 0);
            Check(pager.Page == 0 && panel.TabPageCount(1) == 1, "A shrinking result set cannot leave an empty page visible.");
            Check(!pager.TurnOnPress(panel, methodCount: 1, EventType.MouseDown, 0, next) && pager.Page == 0,
                "A single page cannot turn or consume the press.");
            pager.Show(panel, [], 0);
            Check(pager.Page == 0, "An empty lookup resets tab paging.");

            var partialThirdPage = methods.Append(new RecipeMethod("third", "Third page", [], string.Empty, null)).ToArray();
            pager.Show(panel, partialThirdPage, selected);
            Check(pager.TurnOnPress(panel, partialThirdPage.Length, EventType.MouseDown, 0, previous) && pager.Page == 2 &&
                panel.TabPageEnd(pager.Page, partialThirdPage.Length) - panel.TabPageStart(pager.Page) == 1,
                "Previous from the first page wraps backward to a partial third page, not forward to the second.");
            var (_, partialNext) = panel.TabArrows(pager.Page, partialThirdPage.Length);
            Check(!pager.TurnOnPress(panel, partialThirdPage.Length, EventType.MouseDown, 0, next) &&
                !pager.TurnOnPress(panel, partialThirdPage.Length, EventType.MouseDown, 0, panel.Tab(0).center) && pager.Page == 2,
                "The old full-page button position and the adjacent tab are not hits on a partial page.");
            Check(pager.TurnOnPress(panel, partialThirdPage.Length, EventType.MouseDown, 0, partialNext.center) && pager.Page == 0,
                "The relocated next button beside the last visible tab wraps to the first page.");
        }
    }

    internal static void NativeAndMachineMethods()
    {
        var tools = NativeRow("hammer", "Tools");
        var food = NativeRow("ration", "Food");
        var cooker = new Row(RecipeMethods.CookingRecipeId, "native.cooking", [], [new RecipeOutput("steak")], machineId: "heater.cooker", source: "native");
        var inherited = new Row("press", "Tools", [], []);
        var explicitDefault = new Row("press-two", "tools", [], [], machineId: "PRESS");
        var mill = new Row("grind", "tools", [], [], machineId: "mill");
        Category[] categories = [new("Tools", "Tool bench", "press"), new("native.cooking", "Cooking", "heater.cooker"), new("Food")];
        Row[] rows = [tools, cooker, mill, food, inherited, explicitDefault];

        var methods = RecipeMethods.Group(rows, categories, chinese: true);

        Check(methods.Select(m => m.Name).SequenceEqual(["制作栏", "烹饪", "Tool bench · mill", "Tool bench"]),
            "Native categories share one crafting panel, but cooking and provider machines are actual, distinct methods.");
        Check(methods[0].Recipes.SequenceEqual([tools, food]) && methods[1].Recipes.SequenceEqual([cooker]) &&
            methods[2].Recipes.SequenceEqual([mill]) && methods[3].Recipes.SequenceEqual([inherited, explicitDefault]),
            "Do not absorb the native-source heater or provider rows sharing a native category. Inherit category machines case-insensitively.");
        var localized = RecipeMethods.Group(rows, categories, chinese: false);
        Check(localized[0].Name == "Crafting" && localized[1].Name == "Cooking" &&
            localized.Select(m => m.Id).SequenceEqual(methods.Select(m => m.Id)) &&
            methods.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == methods.Length,
            "Every selection needs a distinct ID; changing the display language must not reset it.");
        int first = RecipeMethods.SelectedIndex(methods, "retired-method");
        int next = RecipeMethods.Cycle(first, 1, methods.Length);
        int last = RecipeMethods.Cycle(first, -1, methods.Length);
        Check(first == 0 && next == 1 && last == methods.Length - 1 && RecipeMethods.Cycle(last, 1, methods.Length) == 0 &&
            RecipeMethods.SelectedIndex(localized, methods[next].Id.ToUpperInvariant()) == next &&
            localized[next].Recipes.Single() == cooker && RecipeMethods.Cycle(0, 1, 1) == 0,
            "Previous/next wrap and a locale change preserves the selected method and recipe rows.");
        Check(methods.Select(method => method.MachineId).SequenceEqual(["", "heater.cooker", "mill", "press"]),
            "The tab icon and catalyst rail use the same method-local machine key.");
    }

    internal static void BuiltInCookingLocalization()
    {
        const string categoryId = "native.cooking";
        const string recipeId = "native.process.cook-meat";
        var pluginType = typeof(Catalog).Assembly.GetType("NotEnoughItems.Plugin", throwOnError: true)!;
        var instance = pluginType.GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var prior = instance.GetValue(null);
        var plugin = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(pluginType);
        var config = new BepInEx.Configuration.ConfigFile(Path.Combine(Path.GetTempPath(), $"nei-cooker-{Guid.NewGuid():N}.cfg"),
            saveOnInit: false) { SaveOnConfigSet = false };
        var language = config.Bind("NotEnoughItems", "Language", "en");
        pluginType.GetField("_language", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, language);
        instance.SetValue(null, plugin);
        try
        {
            var catalog = new Catalog();
            catalog.RegisterItem(new CatalogItem("steak", "Game steak"));
            string? methodId = null;
            foreach (bool chinese in new[] { false, true, false })
            {
                language.Value = chinese ? "zh-CN" : "en";
                catalog.Refresh();
                var native = catalog.GetRecipesProducing("item:steak").Single(recipe => recipe.Id == recipeId);
                var method = RecipeMethods.Group([native], catalog.RecipeCategories, chinese).Single();
                methodId ??= method.Id;
                string material = chinese ? "带肉类标签的物品" : "Meat-tagged item";
                Check(method.Name == (chinese ? "烹饪" : "Cooking") && native.Name == "Game steak" && method.Id == methodId &&
                    native.CategoryId == categoryId && native.MachineId == "heater.cooker" && native.Inputs.Single().BrowseId == "tag:meat" &&
                    native.Inputs.Single().DisplayName == material && native.Description.Contains(chinese ? "耐久" : "condition"),
                    "Translate NEI's own cooker labels without changing the game-provided name, method key, or ingredient ID.");
                Check(catalog.TryGetItem("tag:meat", out var item) && item.DisplayName == material &&
                    item.Description.Contains(chinese ? "烹饪加热器" : "cooker"), "The virtual requirement tooltip uses the same language.");
            }
        }
        finally { instance.SetValue(null, prior); }
    }

    internal static void MissingMetadataAndEmptyMatches()
    {
        Category[] categories = [new("unused")];
        Check(RecipeMethods.Group([], categories, chinese: true).Length == 0, "Empty lookups have no methods, even with registered categories.");

        var first = new Row("a", "x:y", [], [], machineId: "z");
        var second = new Row("b", "x", [], [], machineId: "y:z");
        var third = new Row("c", "crafting", [], []);
        var methods = RecipeMethods.Group([first, second, third], categories, chinese: true);
        Check(methods.Select(m => m.Name).SequenceEqual(["z", "y:z", "crafting"]) &&
            methods.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 &&
            methods.SelectMany(m => m.Recipes).SequenceEqual([first, second, third]),
            "Unregistered processes fall back to machine or category IDs, including IDs with separators.");
    }

    // Exercise actual normalized native rows, but do not call the game's crafting/localization code
    // or change Recipes.recipes in the headless process. The live handle is unnecessary for grouping.
    private static Row NativeRow(string id, string category) => (Row)Activator.CreateInstance(typeof(Row),
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        [id, category, Array.Empty<RecipeIngredient>(), new[] { new RecipeOutput(id) }, "", id, "", "native",
            null, -1, true, false, false, false, 0], null)!;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
