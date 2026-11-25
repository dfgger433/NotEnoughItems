using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>A crafting method displayed in the recipe inspector.</summary>
internal sealed class RecipeMethod
{
    internal RecipeMethod(string id, string name, IReadOnlyList<Recipe> recipes, string machineId, Sprite? icon, string? tabLabel = null)
    {
        Id = id;
        Name = name;
        Recipes = recipes;
        MachineId = machineId;
        Icon = icon;
        TabLabel = tabLabel ?? name.Substring(0, Math.Min(2, name.Length));
    }

    /// <summary>Opaque, locale-independent selection key; compare using OrdinalIgnoreCase.</summary>
    internal string Id { get; }
    /// <summary>Display name of the crafting panel, registered process or machine.</summary>
    internal string Name { get; }
    /// <summary>Original rows in lookup order, including alternative recipes.</summary>
    internal IReadOnlyList<Recipe> Recipes { get; }
    internal string MachineId { get; }
    internal Sprite? Icon { get; }
    internal string TabLabel { get; }
}

/// <summary>Keeps the visible tab page independent of the selected crafting method.</summary>
internal sealed class RecipeMethodTabPager
{
    private string? _selection;
    private int _capacity;
    internal int Page { get; private set; }

    internal void Show(RecipePanelLayout panel, RecipeMethod[] methods, int selected, bool reveal = false)
    {
        int pageCount = panel.TabPageCount(methods.Length);
        if (pageCount == 0) { Reset(); return; }

        string selection = methods[selected].Id;
        if (reveal || _capacity != panel.TabsPerPage || !StringComparer.OrdinalIgnoreCase.Equals(_selection, selection))
            Page = selected / panel.TabsPerPage;
        Page = Math.Clamp(Page, 0, pageCount - 1);
        _capacity = panel.TabsPerPage;
        _selection = selection;
    }

    internal bool TurnOnPress(RecipePanelLayout panel, int methodCount, EventType type, int button, Vector2 pointer)
    {
        if (type != EventType.MouseDown || button != 0) return false;
        int pageCount = panel.TabPageCount(methodCount);
        if (pageCount < 2) return false;
        var (previous, next) = panel.TabArrows(Page, methodCount);
        int direction = previous.Contains(pointer) ? -1 : next.Contains(pointer) ? 1 : 0;
        if (direction == 0) return false;
        Page = RecipeMethods.Cycle(Page, direction, pageCount);
        return true;
    }

    internal void Reset()
    {
        _selection = null;
        _capacity = Page = 0;
    }
}

internal static class RecipeMethods
{
    internal const string CookingRecipeId = "native.process.cook-meat";
    internal const string CraftingId = "crafting";

    // Preview methods have no recipe rows and never enter the catalog, bookmarks or crafting actions.
    internal static RecipeMethod[] PreviewTabs(int tabCapacity, bool chinese)
    {
        var methods = new RecipeMethod[tabCapacity * 2];
        for (int i = 0; i < methods.Length; i++)
        {
            string number = (i + 1).ToString("00", CultureInfo.InvariantCulture);
            methods[i] = new RecipeMethod("debug:method-tab:" + number,
                NeiText.Get("method.preview_test", chinese, number), Array.Empty<Recipe>(), string.Empty, null, number);
        }
        return methods;
    }

    internal static int SelectedIndex(RecipeMethod[] methods, string? id) =>
        Math.Max(0, Array.FindIndex(methods, method => StringComparer.OrdinalIgnoreCase.Equals(method.Id, id)));

    internal static bool CanSwitch(int count) => count > 1;

    internal static int Cycle(int selected, int step, int count) => CanSwitch(count) ? (selected + step + count) % count : 0;

    /// <summary>Partitions an already-matched recipe or usage lookup without consulting the live game.</summary>
    /// <param name="matching">Every row from the lookup, in its desired display order.</param>
    /// <param name="categories">Category metadata used for names and inherited machine IDs.</param>
    /// <param name="chinese">Whether to use NEI's Simplified Chinese labels for built-in methods.</param>
    /// <returns>Only available methods, in order of first occurrence.</returns>
    internal static RecipeMethod[] Group(IEnumerable<Recipe> matching, IReadOnlyList<RecipeCategory> categories, bool chinese)
    {
        if (matching == null) throw new ArgumentNullException(nameof(matching));
        if (categories == null) throw new ArgumentNullException(nameof(categories));

        var metadata = new Dictionary<string, RecipeCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories) metadata[category.Id] = category;

        var groups = new List<RecipeMethod>();
        var rowsByMethod = new Dictionary<string, List<Recipe>>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in matching)
        {
            // The native crafting menu uses ingredient requirements, not a shaped Minecraft grid.
            // "native" also covers non-crafting processes such as the heater.
            var (id, name, machine, icon) = recipe.IsNative
                ? (CraftingId, NeiText.Get("method.crafting", chinese), string.Empty, null)
                : DescribeProcess(recipe, metadata, chinese);
            if (!rowsByMethod.TryGetValue(id, out var rows))
            {
                rows = new List<Recipe>();
                rowsByMethod.Add(id, rows);
                groups.Add(new RecipeMethod(id, name, rows.AsReadOnly(), machine, icon));
            }
            rows.Add(recipe);
        }
        return groups.ToArray();
    }

    private static (string Id, string Name, string Machine, Sprite? Icon) DescribeProcess(Recipe recipe,
        Dictionary<string, RecipeCategory> metadata, bool chinese)
    {
        metadata.TryGetValue(recipe.CategoryId, out var category);
        string categoryId = category?.Id ?? recipe.CategoryId;
        string defaultMachine = category?.MachineId.Trim() ?? string.Empty;
        string machine = string.IsNullOrWhiteSpace(recipe.MachineId) ? defaultMachine : recipe.MachineId.Trim();
        // An ID may itself contain separators. Prefix the category's length to keep the pair unambiguous.
        string id = "process:" + categoryId.Length.ToString(CultureInfo.InvariantCulture) + ":" + categoryId + ":" + machine;
        if (category == null) return (id, machine.Length == 0 ? categoryId : machine, machine, null);
        string label = recipe.Id == CookingRecipeId && recipe.Source == "native"
            ? NeiText.Get("method.cooking", chinese) : category.Name;
        string name = machine.Length > 0 && !StringComparer.OrdinalIgnoreCase.Equals(machine, defaultMachine)
            ? label + " · " + machine : label;
        return (id, name, machine, category.Icon);
    }
}
