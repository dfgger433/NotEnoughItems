using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

internal sealed class MissingMaterialSpawn(CatalogItem item, int amount)
{
    internal CatalogItem Item { get; } = item;
    internal int Amount { get; } = amount;
}

internal static class MissingMaterialSpawnPolicy
{
    // A null capacity provider means this session cannot deliver filled bottles.
    internal static string? Build(Catalog catalog, BookmarkChainBalanceResult plan, int maximum, Func<float>? getBottleCapacity,
        Func<string, object[], string> text, out List<MissingMaterialSpawn> batch)
    {
        batch = new();
        if (plan.Missing.Any(row => row.IsRequirement)) return text("chain.spawn.choose_material", Array.Empty<object>());
        if (plan.Diagnostics.Any(issue => issue.Code != BookmarkChainDiagnosticCode.MissingMaterial))
            return text("chain.spawn.invalid_plan", Array.Empty<object>());
        if (plan.Missing.Count == 0) return text("chain.spawn.nothing_missing", Array.Empty<object>());
        if (getBottleCapacity == null && plan.Missing.Any(row => row.IsLiquid))
            return text("chain.spawn.together_liquid", Array.Empty<object>());

        var prepared = new List<MissingMaterialSpawn>();
        int remaining = maximum;
        float? bottleCapacity = null;
        foreach (var rows in plan.Missing.GroupBy(row => row.ItemId, StringComparer.OrdinalIgnoreCase))
        {
            bool liquid = rows.First().IsLiquid;
            var kind = liquid ? CatalogItemKind.Liquid : CatalogItemKind.Item;
            if (!catalog.TryGetItem(rows.Key, out var item) || item.Kind != kind || item.IsVirtual || rows.Any(row => row.IsLiquid != liquid))
                return text("chain.spawn.unsupported_item", new object[] { rows.Key });

            // The summary retains only the first ingredient for each ID. Check each
            // short slot so a later damaged-only slot cannot be masked by the first.
            if (plan.Steps.SelectMany(step => step.Inputs).Any(input =>
                string.Equals(input.ItemId, rows.Key, StringComparison.OrdinalIgnoreCase) &&
                input.Quantity - input.AvailableQuantity > 1e-7 && input.Ingredient is { } ingredient &&
                (!ingredient.Matches(item) || (!liquid && (ingredient.NoMaxCondition || !(ingredient.MinimumCondition <= 1f))))))
                return text("chain.spawn.unsupported_item", new object[] { item.DisplayName });

            double units = rows.Sum(row => row.Quantity);
            if (liquid)
            {
                float capacity = bottleCapacity ??= getBottleCapacity!();
                if (!float.IsFinite(capacity) || capacity <= 0f)
                    return text("actions.error.temporary_bottle_unavailable", Array.Empty<object>());
                units /= capacity;
            }
            // The cap counts physical objects, not mL. Every liquid bottle is full.
            double amount = Math.Ceiling(units);
            if (!(amount >= 1 && amount <= remaining)) return text("chain.spawn.limit", new object[] { maximum });
            prepared.Add(new MissingMaterialSpawn(item, (int)amount));
            remaining -= (int)amount;
        }
        batch = prepared;
        return null;
    }
}
