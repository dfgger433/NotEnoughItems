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
    internal static string? Build(Catalog catalog, BookmarkChainBalanceResult plan, int maximum, Func<string, object[], string> text,
        out List<MissingMaterialSpawn> batch)
    {
        batch = new();
        if (plan.Missing.Any(row => row.IsRequirement)) return text("chain.spawn.choose_material", Array.Empty<object>());
        if (plan.Diagnostics.Any(issue => issue.Code != BookmarkChainDiagnosticCode.MissingMaterial))
            return text("chain.spawn.invalid_plan", Array.Empty<object>());
        if (plan.Missing.Count == 0) return text("chain.spawn.nothing_missing", Array.Empty<object>());

        int remaining = maximum;
        foreach (var rows in plan.Missing.GroupBy(row => row.ItemId, StringComparer.OrdinalIgnoreCase))
        {
            if (rows.Any(row => row.IsLiquid)) return text("chain.spawn.solid_only", new object[] { rows.Key });
            if (!catalog.TryGetItem(rows.Key, out var item) || item.Kind != CatalogItemKind.Item || item.IsVirtual)
                return text("chain.spawn.unsupported_item", new object[] { rows.Key });

            // The summary retains only the first ingredient for each ID. Check each
            // short slot so a later damaged-only slot cannot be masked by the first.
            if (plan.Steps.SelectMany(step => step.Inputs).Any(input =>
                string.Equals(input.ItemId, rows.Key, StringComparison.OrdinalIgnoreCase) &&
                input.Quantity - input.AvailableQuantity > 1e-7 && input.Ingredient is { } ingredient &&
                (ingredient.NoMaxCondition || !(ingredient.MinimumCondition <= 1f) || !ingredient.Matches(item))))
                return text("chain.spawn.unsupported_item", new object[] { item.DisplayName });

            double amount = Math.Ceiling(rows.Sum(row => row.Quantity));
            if (!(amount >= 1 && amount <= remaining)) return text("chain.spawn.limit", new object[] { maximum });
            batch.Add(new MissingMaterialSpawn(item, (int)amount));
            remaining -= (int)amount;
        }
        return null;
    }
}
