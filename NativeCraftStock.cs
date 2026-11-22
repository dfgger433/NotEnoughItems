using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

// The game's crafting screen searches carried items and reachable items on the ground.
internal static class NativeCraftStock
{
    internal static IEnumerable<Item> Items(Body body, bool carried, bool nearby)
    {
        var seen = new HashSet<Item>();
        foreach (var item in body.GetAllItemsThorough())
            if (item && seen.Add(item) && carried) yield return item;
        if (!nearby) yield break;
        foreach (var hit in Physics2D.OverlapCircleAll(body.transform.position, 10f, LayerMask.GetMask("Item")))
            if (hit && hit.TryGetComponent<Item>(out var item) && item && seen.Add(item) && body.DoPickupCheck(item, noAlerts: true))
                yield return item;
    }

    internal static Dictionary<string, int> Quantities(Body? body, bool nearby = false)
    {
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (body && body is not null)
            foreach (var item in Items(body, carried: !nearby, nearby: nearby))
            {
                if (item.favourited || string.IsNullOrEmpty(item.id)) continue;
                if ((!item.Stats.destroyAtZeroCondition || item.condition > 0f) &&
                    (!item.TryGetComponent<Container>(out var storage) || storage.itemCount == 0)) Count("item:" + item.id, 1);
                if (item.TryGetComponent<WaterContainerItem>(out var vessel) && vessel.stack != null)
                    foreach (var stack in vessel.stack)
                        if (stack != null && stack.amount > 0f && !float.IsInfinity(stack.amount) && !string.IsNullOrWhiteSpace(stack.liquidId))
                            Count("liquid:" + stack.liquidId, stack.amount);
            }
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in totals) result[pair.Key] = (int)Math.Min(int.MaxValue, Math.Floor(pair.Value));
        return result;

        void Count(string key, double amount)
        {
            totals.TryGetValue(key, out double old);
            totals[key] = old + amount;
        }
    }
}
