using System;
using System.Collections.Generic;
using System.Globalization;

namespace NotEnoughItems;

/// <summary>A method that has at least one recipe in the current lookup.</summary>
internal sealed class RecipeMethod
{
    internal RecipeMethod(string id, string name, IReadOnlyList<Recipe> recipes)
    {
        Id = id;
        Name = name;
        Recipes = recipes;
    }

    /// <summary>Opaque, locale-independent selection key; compare using OrdinalIgnoreCase.</summary>
    internal string Id { get; }
    /// <summary>Display name of the crafting panel, registered process or machine.</summary>
    internal string Name { get; }
    /// <summary>Original rows in lookup order, including alternative recipes.</summary>
    internal IReadOnlyList<Recipe> Recipes { get; }
}

internal static class RecipeMethods
{
    /// <summary>Partitions an already-matched recipe or usage lookup without consulting the live game.</summary>
    /// <param name="matching">Every row from the lookup, in its desired display order.</param>
    /// <param name="categories">Category metadata used for names and inherited machine IDs.</param>
    /// <param name="craftingLabel">Localized name of the game's crafting panel.</param>
    /// <returns>Only available methods, in order of first occurrence.</returns>
    internal static RecipeMethod[] Group(IEnumerable<Recipe> matching, IReadOnlyList<RecipeCategory> categories, string craftingLabel)
    {
        if (matching == null) throw new ArgumentNullException(nameof(matching));
        if (categories == null) throw new ArgumentNullException(nameof(categories));
        if (craftingLabel == null) throw new ArgumentNullException(nameof(craftingLabel));

        var metadata = new Dictionary<string, RecipeCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories) metadata[category.Id] = category;

        var groups = new List<RecipeMethod>();
        var rowsByMethod = new Dictionary<string, List<Recipe>>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in matching)
        {
            // Source == "native" also covers cooker processes; only actual crafting-panel rows set IsNative.
            var (id, name) = recipe.IsNative ? ("crafting", craftingLabel) : DescribeProcess(recipe, metadata);
            if (!rowsByMethod.TryGetValue(id, out var rows))
            {
                rows = new List<Recipe>();
                rowsByMethod.Add(id, rows);
                groups.Add(new RecipeMethod(id, name, rows.AsReadOnly()));
            }
            rows.Add(recipe);
        }
        return groups.ToArray();
    }

    private static (string Id, string Name) DescribeProcess(Recipe recipe, Dictionary<string, RecipeCategory> metadata)
    {
        metadata.TryGetValue(recipe.CategoryId, out var category);
        string categoryId = category?.Id ?? recipe.CategoryId;
        string defaultMachine = category?.MachineId.Trim() ?? string.Empty;
        string machine = string.IsNullOrWhiteSpace(recipe.MachineId) ? defaultMachine : recipe.MachineId.Trim();
        // An ID may itself contain separators. Prefix the category's length to keep the pair unambiguous.
        string id = "process:" + categoryId.Length.ToString(CultureInfo.InvariantCulture) + ":" + categoryId + ":" + machine;
        if (category == null) return (id, machine.Length == 0 ? categoryId : machine);
        string name = machine.Length > 0 && !StringComparer.OrdinalIgnoreCase.Equals(machine, defaultMachine)
            ? category.Name + " · " + machine : category.Name;
        return (id, name);
    }
}
