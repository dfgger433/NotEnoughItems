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
        var producesGear = RecipeMethods.Group([cut, press, cast], categories, "制作栏");
        var usesIngot = RecipeMethods.Group([press, cast, toolUse], categories, "制作栏");

        Check(producesGear.Select(m => m.Name).SequenceEqual(["Metalwork", "Press"]) &&
            producesGear[0].Recipes.SequenceEqual([cut, cast]) && producesGear[1].Recipes.SequenceEqual([press]),
            "Keep both recipes for one output, in index order, without adding an All or unused method.");
        Check(usesIngot.Select(m => m.Name).SequenceEqual(["Press", "Metalwork"]) &&
            usesIngot[0].Recipes.SequenceEqual([press]) && usesIngot[1].Recipes.SequenceEqual([cast, toolUse]),
            "A usage starts with its own first method and retains both material and tool uses.");
    }

    internal static void NativeAndMachineMethods()
    {
        var tools = NativeRow("hammer", "Tools");
        var food = NativeRow("ration", "Food");
        var cooker = new Row("sear", "native.cooking", [], [new RecipeOutput("steak")], machineId: "heater.cooker", source: "native");
        var inherited = new Row("press", "Tools", [], []);
        var explicitDefault = new Row("press-two", "tools", [], [], machineId: "PRESS");
        var mill = new Row("grind", "tools", [], [], machineId: "mill");
        Category[] categories = [new("Tools", "Tool bench", "press"), new("native.cooking", "Cooking", "heater.cooker"), new("Food")];
        Row[] rows = [tools, cooker, mill, food, inherited, explicitDefault];

        var methods = RecipeMethods.Group(rows, categories, "制作栏");

        Check(methods.Select(m => m.Name).SequenceEqual(["制作栏", "Cooking", "Tool bench · mill", "Tool bench"]),
            "Native categories share one crafting panel, but cooking and provider machines are actual, distinct methods.");
        Check(methods[0].Recipes.SequenceEqual([tools, food]) && methods[1].Recipes.SequenceEqual([cooker]) &&
            methods[2].Recipes.SequenceEqual([mill]) && methods[3].Recipes.SequenceEqual([inherited, explicitDefault]),
            "Do not absorb the native-source heater or provider rows sharing a native category. Inherit category machines case-insensitively.");
        var localized = RecipeMethods.Group(rows, categories, "Crafting");
        Check(localized[0].Name == "Crafting" && localized.Select(m => m.Id).SequenceEqual(methods.Select(m => m.Id)) &&
            methods.Select(m => m.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == methods.Length,
            "Every selection needs a distinct ID; changing the display language must not reset it.");
    }

    internal static void MissingMetadataAndEmptyMatches()
    {
        Category[] categories = [new("unused")];
        Check(RecipeMethods.Group([], categories, "制作栏").Length == 0, "Empty lookups have no methods, even with registered categories.");

        var first = new Row("a", "x:y", [], [], machineId: "z");
        var second = new Row("b", "x", [], [], machineId: "y:z");
        var third = new Row("c", "crafting", [], []);
        var methods = RecipeMethods.Group([first, second, third], categories, "制作栏");
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
