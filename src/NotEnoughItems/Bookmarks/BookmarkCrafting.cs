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
    private const float ReceiptWaitSeconds = 20f;
    private const float IngredientSyncWaitSeconds = 5f;
    private const float TogetherPollInterval = .2f;
    private static Harmony? _observer;
    private static Receipt? _pending;

    internal static IEnumerator Execute(BookmarkChainBalanceResult plan, Action<string> completed, Func<bool> contextValid,
        IReadOnlyList<BookmarkChainBalanceEntry> route, IReadOnlyCollection<string>? favorites = null)
    {
        string L(string key, params object[] args) => NeiText.Get(key, Plugin.Instance?.Chinese == true, args);
        if (plan.Diagnostics.Any(d => d.Code is BookmarkChainDiagnosticCode.Cycle or BookmarkChainDiagnosticCode.InvalidRecipe or BookmarkChainDiagnosticCode.MissingRecipe))
        {
            completed(L("bookmark.craft.plan_incomplete"));
            yield break;
        }
        var camera = PlayerCamera.main;
        if (!ValidContext())
        {
            completed(ContextChanged());
            yield break;
        }
        var session = NativePlaySession.Detect(camera.body);
        var together = session.Offline ? null : TogetherCrafting.Resolve(session);
        if (!session.Offline && together == null)
        {
            completed(L("bookmark.craft.session_unsupported"));
            yield break;
        }
        if (plan.Steps.Any(step => !step.Recipe.IsNative || step.Recipe.NativeRecipe is not { } native ||
            !GameSurface.CanOpenCrafting(native) || (together != null && native.index > ushort.MaxValue)))
        {
            completed(L("bookmark.craft.recipe_unavailable_or_locked"));
            yield break;
        }
        // Together chooses its own synchronized inputs and cannot enforce NEI's pins.
        // Fail the whole order before spending anything rather than use a different item.
        if (together != null && plan.Steps.Any(step => route[step.EntryIndex].PreferredIngredients?.Count > 0))
        {
            completed(L("bookmark.craft.together_pins_unsupported"));
            yield break;
        }
        var preferred = favorites ?? Array.Empty<string>();
        int made = 0, attempts = 0;
        string reason = "";
        foreach (var step in plan.Steps)
        {
            var native = step.Recipe.NativeRecipe;
            if (native == null) { reason = L("bookmark.craft.recipe_unavailable"); break; }
            var pins = route[step.EntryIndex].PreferredIngredients;
            for (int i = 0; i < step.Batches; i++)
            {
                if (attempts >= OrderLimit) { reason = L("bookmark.craft.order_limit"); break; }
                reason = CanCraft(native);
                if (reason.Length != 0) break;
                if (together != null)
                {
                    // Together's patched recipe preview uses synchronized, reachable stock.
                    // A receipt can arrive before the intermediate item or liquid does.
                    if (made > 0)
                    {
                        var syncDeadline = Time.realtimeSinceStartup + IngredientSyncWaitSeconds;
                        while (native.GetItemsForRecipe() == null && Time.realtimeSinceStartup < syncDeadline)
                        {
                            yield return new WaitForSecondsRealtime(TogetherPollInterval);
                            reason = CanCraft(native);
                            if (reason.Length != 0) break;
                        }
                        if (reason.Length != 0) break;
                    }
                    attempts++;
                    var reply = together.Submit(camera, native);
                    if (reply.Status == TogetherCraftStatus.Pending)
                    {
                        var receiptDeadline = Time.realtimeSinceStartup + ReceiptWaitSeconds;
                        do
                        {
                            yield return null;
                            if (!ValidSession())
                            {
                                reason = L("bookmark.craft.context_changed_pending");
                                break;
                            }
                            reply = together.Poll();
                        } while (reply.Status == TogetherCraftStatus.Pending && Time.realtimeSinceStartup < receiptDeadline);
                    }
                    if (reason.Length != 0) break;
                    if (reply.Status != TogetherCraftStatus.Accepted) { reason = TogetherStop(reply, step.Recipe.Name); break; }
                    made++;
                    yield return null;
                    continue;
                }
                if (NativeCraftSelection.Find(native, camera.body, pins, preferred) == null)
                { reason = L("bookmark.craft.missing_inputs", step.Recipe.Name); break; }
                if (!EnsureObserver()) { reason = L("bookmark.craft.verification_unavailable"); break; }

                camera.SelectRecipe(native.index);
                var receipt = new Receipt(native, camera.body, pins, preferred);
                _pending = receipt;
                try { attempts++; camera.TryCraft(); }
                finally { _pending = null; }
                if (!receipt.Produced)
                {
                    reason = receipt.Rejected ? L("bookmark.craft.inputs_changed") :
                        L("bookmark.craft.result_unconfirmed");
                    break;
                }
                made++;
                yield return null;
            }
            if (reason.Length > 0) break;
        }
        completed(L("bookmark.craft.produced", made, reason));

        bool ValidContext() => contextValid() && camera && camera.body && camera.body.alive && camera.body.conscious && !camera.dragItem &&
            camera.craftingPanel && camera.craftingPanel.activeInHierarchy && WorldGeneration.world && !WorldGeneration.world.cutsceneActive;

        bool ValidSession()
        {
            if (!ValidContext()) return false;
            var current = NativePlaySession.Detect(camera.body);
            return session.Matches(current) && (session.Offline || TogetherCrafting.IsPlayable(current));
        }

        string CanCraft(global::Recipe recipe)
        {
            if (!ValidSession()) return ContextChanged();
            if (!GameSurface.CanOpenCrafting(recipe) || (together != null && recipe.index > ushort.MaxValue))
                return L("bookmark.craft.recipe_no_longer_available");
            if (camera.body.skills.INT < recipe.INT)
                return L("bookmark.craft.intelligence_too_low");
            return "";
        }

        string ContextChanged() => L("bookmark.craft.context_changed");

        string TogetherStop(TogetherCraftReply reply, string recipe) => reply.Status switch
        {
            TogetherCraftStatus.Busy => L("bookmark.craft.together_busy"),
            TogetherCraftStatus.Pending => L("bookmark.craft.together_timeout"),
            TogetherCraftStatus.Interrupted => L("bookmark.craft.together_interrupted"),
            TogetherCraftStatus.Rejected when reply.Reason == "StillSyncing" => L("bookmark.craft.together_syncing", recipe),
            TogetherCraftStatus.Rejected when reply.Reason is "Missing" or "Taken" or "Unreachable" =>
                L("bookmark.craft.together_missing_inputs", recipe),
            _ => L("bookmark.craft.together_rejected", recipe),
        };
    }

    // TryMake resolves ingredients again immediately before UseItem. Validate THAT selection,
    // not a preview, and allow the original missing-material branch to handle a mismatch.
    private static void CheckSelection(global::Recipe __instance, ref List<Item>? __result)
    {
        if (_pending is not { } r || r.ResultStarted || !ReferenceEquals(r.Recipe, __instance)) return;
        __result = NativeCraftSelection.Find(__instance, r.Body, r.Pins, r.Favorites);
        r.Rejected = __result == null;
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

    internal static void Release() { _pending = null; _observer?.UnpatchSelf(); _observer = null; TogetherCrafting.Release(); }

    private static void BeforeResult(RecipeResult __instance)
    {
        if (_pending is { } r && ReferenceEquals(r.Recipe.result, __instance))
        {
            r.ResultStarted = true;
            r.Measure(before: true);
        }
    }
    private static void AfterResult(RecipeResult __instance)
    {
        if (_pending is { } r && ReferenceEquals(r.Recipe.result, __instance)) r.Measure(before: false);
    }

    private sealed class Receipt(global::Recipe recipe, Body body, IReadOnlyDictionary<int, string>? pins, IReadOnlyCollection<string> favorites)
    {
        internal readonly global::Recipe Recipe = recipe;
        internal Body Body => body;
        internal readonly IReadOnlyDictionary<int, string>? Pins = pins;
        internal readonly IReadOnlyCollection<string> Favorites = favorites;
        internal bool Produced, Rejected, ResultStarted;
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
