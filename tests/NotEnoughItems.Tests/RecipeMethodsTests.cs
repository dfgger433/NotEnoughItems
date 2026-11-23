using System.Reflection;
using NotEnoughItems;
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

        var method = producesGear[0];
        string Display(string id) => id == "item:plate" ? "金属板" : id;
        Check(RecipeMethods.Filter(method, "金属板", Display).SequenceEqual([cut]) &&
            RecipeMethods.Filter(method, "gear -金属板", Display).SequenceEqual([cast]) &&
            RecipeMethods.Filter(method, "missing", Display).Count == 0 &&
            ReferenceEquals(RecipeMethods.Filter(method, "", Display), method.Recipes),
            "Recipe search filters only the current method in order, using localized ingredient names, and clears to all of its rows.");
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
