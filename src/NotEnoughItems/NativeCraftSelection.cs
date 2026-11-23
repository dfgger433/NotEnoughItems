using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

// Slot-ordered, bounded backtracking. NEI may reorder eligible instances, but the
// original game still checks item quality/condition and performs every consumption.
internal static class NativeCraftSelection
{
    private const int WorkLimit = 20_000;
    private const int SlotLimit = 64;

    internal static List<Item>? Find(global::Recipe recipe, Body body, IReadOnlyDictionary<int, string>? pins,
        IReadOnlyCollection<string> favorites)
    {
        if (recipe.items.Count > SlotLimit || pins?.Keys.Any(slot => slot < 0 || slot >= recipe.items.Count) == true) return null;
        var stock = NativeCraftStock.Items(body, carried: true, nearby: true).Where(item => !item.favourited).ToArray();
        var used = new HashSet<Item>();
        var amountsUsed = new Dictionary<(Item Vessel, string Id), float>();
        var assigned = new Item[recipe.items.Count];
        var single = new List<Item>(1) { null! };
        var empty = new List<Item>();
        var preferred = new HashSet<string>(favorites, StringComparer.OrdinalIgnoreCase);
        int work = 0;
        return Search(0) ? assigned.ToList() : null;

        bool Search(int index)
        {
            if (index >= assigned.Length) return true;
            var input = recipe.items[index];
            string? pin = pins?.TryGetValue(index, out var id) == true ? id : null;
            var matches = new List<(Item Item, string? Liquid, float Amount, bool Preferred)>();
            foreach (var item in stock)
            {
                if (++work > WorkLimit) return false;
                if (used.Contains(item)) continue;
                if (input.isLiquid)
                {
                    if (!Liquid(input, item, out string liquid, out float amount) ||
                        (pin != null && !string.Equals(pin, "liquid:" + liquid, StringComparison.OrdinalIgnoreCase))) continue;
                    matches.Add((item, liquid, amount, preferred.Contains("liquid:" + liquid)));
                    continue;
                }
                string key = "item:" + item.id;
                if (pin != null && !string.Equals(pin, key, StringComparison.OrdinalIgnoreCase)) continue;
                single[0] = item;
                if (input.GetMatchingItem(single, empty) == item) matches.Add((item, null, 0, preferred.Contains(key)));
            }
            foreach (var match in matches.OrderByDescending(match => match.Preferred))
            {
                assigned[index] = match.Item;
                bool consumesItem = !input.isLiquid && input.destroyItem;
                if (consumesItem) used.Add(match.Item);
                float prior = 0;
                if (match.Liquid != null)
                {
                    amountsUsed.TryGetValue((match.Item, match.Liquid), out prior);
                    amountsUsed[(match.Item, match.Liquid)] = prior + match.Amount;
                }
                if (Search(index + 1)) return true;
                if (consumesItem) used.Remove(match.Item);
                if (match.Liquid != null) amountsUsed[(match.Item, match.Liquid)] = prior;
                if (work >= WorkLimit) return false;
            }
            return false;
        }

        bool Liquid(RecipeItem input, Item item, out string id, out float amount)
        {
            id = string.Empty;
            amount = 0f;
            if (!item.TryGetComponent<WaterContainerItem>(out var vessel) || vessel.stack == null) return false;
            if (input.specific)
            {
                id = input.specificId;
                amount = input.minimumCondition;
                return amount > 0f && Available(vessel.AmountOf(id), id) >= amount;
            }
            foreach (var stack in vessel.stack)
            {
                if (stack == null || !Liquids.Registry.TryGetValue(stack.liquidId, out var type)) continue;
                float volume = Available(stack.amount, stack.liquidId);
                if (volume <= 0f) continue;
                var quality = Item.GetQualityThatMeetsCriteria(input.quality, type.GetScaledQualities(volume));
                if (quality == null || quality.amount <= 0f) continue;
                // UseItem drains the first eligible stack in the vessel, not any
                // alternative stack that happens to match the selected favorite.
                id = stack.liquidId;
                amount = volume * Math.Min(1f, input.quality.amount / quality.amount);
                return amount > 0f;
            }
            return false;

            float Available(float total, string liquid) => total - (amountsUsed.TryGetValue((item, liquid), out float value) ? value : 0f);
        }
    }
}
