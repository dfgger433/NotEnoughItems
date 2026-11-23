using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    // A request owns a snapshot of a particular page, catalog and native crafting panel.
    // Invalidate it even if the player closes and reopens the same page between batches.
    private int _productionContext;
    private bool _producing;

    internal bool OpenBookmarkInCrafting(Vector2 pointer)
    {
        pointer /= _scale;
        if (!ShowLeft || _options || !_left.Contains(pointer)) return false;
        var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
        if (cell == null || !cell.Entry.IsRecipe) return false;
        if (BookmarkedRecipe(cell.Entry.Id)?.NativeRecipe is { } native && _plugin.OpenCrafting(native)) _open = false;
        else Status(L("This recipe is not available in the game's crafting menu.", "游戏制作菜单不支持此配方或尚未解锁。"));
        return true;
    }

    internal bool HandleBookmarkCraft(Vector2 pointer, bool topUp)
    {
        pointer /= _scale;
        if (!ShowLeft || _options || !_left.Contains(pointer)) return false;
        var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
        string? group = cell?.Entry.Group ?? _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
        if (group == null && _bookmarkNavigation.Contains(pointer)) group = string.Empty;
        if (group == null) return false;
        if (_book.IsCollapsed(_group, group)) cell = null;
        if (cell == null && !_book.IsChainEnabled(_group, group))
        {
            Status(L("Enable this chain with a right-click first.", "请先右键启用这条合成链。"));
            return true;
        }
        var camera = PlayerCamera.main;
        if (!camera || !camera.craftingPanel || !camera.craftingPanel.activeInHierarchy)
        {
            Status(L("Open the game's crafting panel first (Shift+S on a saved recipe).", "请先打开原生制作面板（对配方书签按 Shift＋S）。"));
            return true;
        }
        if (_producing)
        {
            Status(L("A bookmark order is already running.", "已有书签订单正在制作。"));
            return true;
        }
        var entries = BookmarkGroupEntries(group);
        var route = entries.Select(BalanceEntry).ToArray();
        int? target = null;
        if (cell != null)
        {
            // An ingredient slot refers to its own producer, not the consumer recipe it is drawn on.
            if (cell.Ingredient < 0 && cell.Entry.IsRecipe)
            {
                target = Array.IndexOf(entries, cell.Entry);
                if (target >= 0 && route[target.Value].Recipe is { } recipe && recipe.Outputs.Any(o => SameId(o.BrowseId, cell.Id)))
                {
                    // For a co-product slot, top up the pointed-at product, not the first output.
                    var selectedEntry = route[target.Value];
                    route[target.Value] = new BookmarkChainBalanceEntry(cell.Id, selectedEntry.Quantity, recipe,
                        selectedEntry.ShowRecipe, selectedEntry.PreferredIngredients, selectedEntry.PreferredRecipe);
                }
            }
            else
            {
                var consumer = cell.Ingredient >= 0 ? BookmarkedRecipe(cell.Entry.Id) : null;
                var input = consumer?.Inputs[cell.Ingredient];
                target = Array.FindIndex(route, entry => entry.Recipe?.Outputs.Any(output =>
                    SameId(output.BrowseId, cell.Id) && (input == null || OutputMatches(input, output))) == true);
            }
            if (target < 0 || route[target.Value].Recipe == null)
            {
                Status(L("No saved recipe in this group produces that item.", "本组没有制作该物品的可用配方。"));
                return true;
            }
        }
        if (target is int selected && !_book.IsChainEnabled(_group, group))
        {
            // Independent recipe bookmarks do not implicitly run neighboring recipes.
            route = new[] { route[selected] };
            target = 0;
        }
        var plan = BookmarkChainBalance.PlanCraft(_catalog, route, target, topUp, NativeCraftStock.Quantities(camera.body),
            PreferredMaterials, NativeCraftStock.Quantities(camera.body, nearby: true));
        if (plan.Steps.Count == 0 && plan.Diagnostics.Count == 0)
        {
            Status(topUp ? L("Order already covered by carried stock.", "随身库存已满足目标数量。") : L("No recipe steps.", "没有制作步骤。"));
            return true;
        }
        int generation = _productionContext;
        string page = _group;
        var body = camera.body;
        int revision = _catalog.Revision;
        _producing = true;
        _plugin.StartCoroutine(Run());
        return true;

        IEnumerator Run()
        {
            var job = BookmarkCrafting.Execute(plan, Status, () => generation == _productionContext && _group == page && ShowLeft &&
                _plugin.isActiveAndEnabled && !_plugin.NativePointerActive && camera && PlayerCamera.main == camera &&
                camera.body == body && camera.craftingPanel && camera.craftingPanel.activeInHierarchy && !PauseHandler.paused && _catalog.Revision == revision, route, PreferredMaterials);
            try
            {
                while (true)
                {
                    bool more;
                    try { more = job.MoveNext(); }
                    catch (Exception error)
                    {
                        _plugin.LogCraftFailure(error);
                        Status(L("Native crafting stopped; check the game log.", "原生制作已停止，请查看游戏日志。"));
                        break;
                    }
                    if (!more) break;
                    yield return job.Current;
                }
            }
            finally { (job as IDisposable)?.Dispose(); _producing = false; }
        }
    }

    private bool OutputMatches(RecipeIngredient input, RecipeOutput output) => input.IsLiquid == output.IsLiquid &&
        (output.IsLiquid || (output.Condition >= input.MinimumCondition && (!input.NoMaxCondition || output.Condition < .991f))) &&
        (input.MatchesAny ? _catalog.TryGetItem(output.BrowseId, out var item) && input.Matches(item) : SameId(output.BrowseId, input.BrowseId));


}
