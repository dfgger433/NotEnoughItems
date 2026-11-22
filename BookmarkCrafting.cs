using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Runs bookmark batches through the open native crafting panel. Never spawns or consumes items directly.</summary>
internal static class BookmarkCrafting
{
    private const int OrderLimit = 10000;
    private static Harmony? _observer;
    private static Receipt? _pending;

    internal static IEnumerator Execute(BookmarkChainBalanceResult plan, Action<string> completed, Func<bool> contextValid,
        IReadOnlyList<BookmarkChainBalanceEntry> route)
    {
        string L(string en, string zh) => Plugin.Instance?.Chinese == true ? zh : en;
        if (plan.Diagnostics.Any(d => d.Code is BookmarkChainDiagnosticCode.Cycle or BookmarkChainDiagnosticCode.InvalidRecipe or BookmarkChainDiagnosticCode.MissingRecipe))
        {
            completed(L("Incomplete or cyclic plan. No materials were spent.", "合成链不完整或存在循环，未消耗材料。"));
            yield break;
        }
        var camera = PlayerCamera.main;
        int made = 0, attempts = 0, unsupported = 0, missing = 0;
        string reason = "";
        foreach (var step in plan.Steps)
        {
            var native = step.Recipe.NativeRecipe;
            if (native == null || !step.Recipe.IsNative) { unsupported++; continue; }
            var pins = route[step.EntryIndex].PreferredIngredients;
            for (int i = 0; i < step.Batches; i++)
            {
                if (attempts >= OrderLimit) { reason = L("Order limit reached.", "本次订单已达批数上限。"); break; }
                if (!contextValid() || !camera || !camera.body || !camera.body.alive || !camera.body.conscious || camera.dragItem ||
                    !camera.craftingPanel || !camera.craftingPanel.activeInHierarchy || !WorldGeneration.world || WorldGeneration.world.cutsceneActive)
                { reason = L("Crafting context changed.", "制作界面或角色状态已改变。"); break; }
                if (CheatSession.Detect(camera.body).Access != CheatAccess.Offline)
                { reason = L("Auto-craft requires a verified offline session.", "仅在可确认的单机状态下自动制作。"); break; }
                int index = step.Recipe.NativeRecipeIndex;
                if (index < 0 || !GameSurface.CanOpenCrafting(native)) { unsupported++; break; }
                if (native.GetItemsForRecipe() == null) { missing++; break; }
                if (!EnsureObserver()) { reason = L("Native craft verification unavailable.", "无法核验原生制作结果。"); break; }

                camera.SelectRecipe(index);
                var receipt = new Receipt(native, camera.body, pins);
                _pending = receipt;
                try { attempts++; camera.TryCraft(); }
                finally { _pending = null; }
                if (!receipt.Produced)
                {
                    reason = receipt.Rejected ? L("Native selection differs from a pinned material; not consumed.", "原生选料与指定材料不符，本批未消耗。") :
                        L("Native result not confirmed; stopped. The game may have spent materials.", "未确认原生产出，已停止。按游戏规则可能已消耗材料。");
                    break;
                }
                made++;
                yield return null;
            }
            if (reason.Length > 0) break;
        }
        completed(L($"Produced {made} batches. Missing inputs: {missing} steps; unavailable: {unsupported}. ",
            $"已制作 {made} 批。缺料 {missing} 步，不支持或未解锁 {unsupported} 步。") + reason);
    }

    // TryMake resolves ingredients again immediately before UseItem. Validate THAT selection,
    // not a preview, and allow the original missing-material branch to handle a mismatch.
    private static void CheckSelection(global::Recipe __instance, ref List<Item>? __result)
    {
        if (_pending is not { } r || !ReferenceEquals(r.Recipe, __instance) || __result == null || r.Pins == null) return;
        foreach (var (slot, expected) in r.Pins)
        {
            if (slot >= 0 && slot < __result.Count && __result[slot] is { } item && item)
            {
                if (expected.StartsWith("item:", StringComparison.OrdinalIgnoreCase) && string.Equals("item:" + item.id, expected, StringComparison.OrdinalIgnoreCase)) continue;
                // In a mixed vessel, the native quality matcher may consume another liquid.
                // Decline ambiguous pins rather than overriding the game's selection.
                if (expected.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) && item.TryGetComponent<WaterContainerItem>(out var vessel) &&
                    vessel.stack != null && vessel.stack.Any(s => s != null && s.amount > 0) &&
                    vessel.stack.Where(s => s != null && s.amount > 0).All(s => string.Equals("liquid:" + s.liquidId, expected, StringComparison.OrdinalIgnoreCase))) continue;
            }
            r.Rejected = true;
            __result = null;
            return;
        }
    }

    private static bool EnsureObserver()
    {
        if (_observer != null) return true;
        var harmony = new Harmony(Plugin.Guid + ".bookmarkcrafting");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(global::Recipe), nameof(global::Recipe.GetItemsForRecipe)),
                postfix: new HarmonyMethod(typeof(BookmarkCrafting), nameof(CheckSelection)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(RecipeResult), nameof(RecipeResult.SpawnResult), new[] { typeof(int) }),
                prefix: new HarmonyMethod(typeof(BookmarkCrafting), nameof(BeforeResult)), postfix: new HarmonyMethod(typeof(BookmarkCrafting), nameof(AfterResult)));
            _observer = harmony;
            return true;
        }
        catch (Exception error) { harmony.UnpatchSelf(); Debug.LogWarning("NEI bookmark observer: " + error.Message); return false; }
    }

    internal static void Release() { _pending = null; _observer?.UnpatchSelf(); _observer = null; }

    private static void BeforeResult(RecipeResult __instance)
    {
        if (_pending is { } r && ReferenceEquals(r.Recipe.result, __instance)) r.Measure(before: true);
    }
    private static void AfterResult(RecipeResult __instance)
    {
        if (_pending is { } r && ReferenceEquals(r.Recipe.result, __instance)) r.Measure(before: false);
    }

    private sealed class Receipt(global::Recipe recipe, Body body, IReadOnlyDictionary<int, string>? pins)
    {
        internal readonly global::Recipe Recipe = recipe;
        internal readonly IReadOnlyDictionary<int, string>? Pins = pins;
        internal bool Produced, Rejected;
        private bool _measured;
        private readonly HashSet<int> _itemIds = new();
        private double _liquidBefore;

        internal void Measure(bool before)
        {
            try
            {
                if (!before && !_measured) return;
                var stock = new HashSet<Item>(body.GetAllItemsThorough());
                foreach (var hit in Physics2D.OverlapCircleAll(body.transform.position, 10f, LayerMask.GetMask("Item")))
                    if (hit && hit.TryGetComponent<Item>(out var item)) stock.Add(item);
                double volume = 0;
                foreach (var item in stock)
                {
                    if (!item) continue;
                    if (Recipe.result.isLiquid)
                    {
                        if (item.TryGetComponent<WaterContainerItem>(out var vessel) && vessel.stack != null)
                            foreach (var s in vessel.stack) if (s != null && s.liquidId == Recipe.result.id) volume += s.amount;
                    }
                    else if (item.id == Recipe.result.id)
                    {
                        if (before) _itemIds.Add(item.GetInstanceID());
                        else if (!_itemIds.Contains(item.GetInstanceID())) Produced = true;
                    }
                }
                if (before) { _liquidBefore = volume; _measured = true; }
                else if (Recipe.result.isLiquid && volume > _liquidBefore + 0.0001) Produced = true;
            }
            catch (Exception error) { _measured = Produced = false; Debug.LogWarning("NEI could not confirm craft: " + error.Message); }
        }
    }
}
