using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private sealed class PullLine(string id, RecipeIngredient? input, bool retained)
    {
        internal readonly string Id = id;
        internal readonly RecipeIngredient? Input = input;
        internal readonly bool Retained = retained;
        internal double Target, Planned, Credit, Moved;
        internal double Remaining => Math.Max(0, Target - Planned - Credit - Moved);
    }

    // Mirrors GTNH NEI 2.8.171-GTNH ShortcutInputHandler.pullRecipeItems / BookmarkPanel.pullBookmarkItems.
    // A slot means that stack, even when it is a recipe result. Only a group invokes chain math.
    internal bool HandleBookmarkPull(Vector2 pointer, bool shift)
    {
        pointer /= _scale;
        if (!ShowLeft || _options || !_left.Contains(pointer)) return false;
        var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
        string? group = cell?.Entry.Group ?? _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
        if (group == null && _bookmarkNavigation.Contains(pointer)) group = string.Empty;
        if (group == null) return false;
        try { PullFromBookmark(cell, group, shift); }
        catch (Exception error)
        {
            Debug.LogWarning("NEI bookmark pull stopped: " + error);
            Status(L("bookmark.pull.inventory_verification_failed"));
        }
        return true;
    }

    private void PullFromBookmark(BookmarkCell? cell, string group, bool shift)
    {
        var camera = PlayerCamera.main;
        var body = camera ? camera.body : null;
        var source = camera ? camera.currentContainer : null;
        string? block;
        var held = new List<Item>();
        var stock = new List<Item>();
        bool stockKnown = false;
        try
        {
            bool? terminal = PullTerminalOpen(out var terminalApi);
            block = terminal == false ? PullBlock(camera, body, source) : terminal == true
                ? L("bookmark.pull.terminal_read_only")
                : L("bookmark.pull.terminal_state_unknown");
            if (terminal == true && terminalApi != null)
            {
                // Resolve only in the current open session. These references NEVER enter the native mover.
                stockKnown = ReadPullTerminal(terminalApi, held, stock);
            }
            else if (body)
            {
                bool validSource = terminal == false && camera && source && camera.containerMenu &&
                    camera.containerMenu.activeInHierarchy && PullLocalSource(body!, source!);
                if (validSource)
                {
                    stock.AddRange(PullSourceItems(source!));
                    stockKnown = true;
                }
                // A worn bag is part of GetAllItemsThorough. Its own children must not also count as held.
                held.AddRange(body!.GetAllItemsThorough().Where(i => i && (!validSource || i.transform.parent != source!.transform)));
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning("NEI pull snapshot unavailable: " + error.Message);
            stock.Clear(); held.Clear(); stockKnown = false;
            block = L("bookmark.pull.inventory_unverified");
        }

        var entries = BookmarkGroupEntries(group);
        var lines = new List<PullLine>();
        if (cell != null) AddCell(cell, cell.Amount == 0 ? BookmarkQuantity : cell.Amount);
        else if (_book.IsChainEnabled(_group, group) && entries.Any(e => e.IsRecipe))
        {
            var saved = entries.Select(BalanceEntry).ToArray();
            var supplies = PullAmounts(stock.Concat(shift ? held : Enumerable.Empty<Item>()));
            var route = new List<BookmarkChainBalanceEntry>(saved.Where(e => e.Recipe != null || e.ShowRecipe));
            // Upstream replaces initial bookmarked supplies with actual on-hand/container stacks
            // before refreshing the chain, keeping only the unrepresented part of each bookmark.
            var savedStock = saved.Where(e => e.Recipe == null && !e.ShowRecipe)
                .GroupBy(e => PullKey(e.Id), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(e => (double)e.Quantity), StringComparer.OrdinalIgnoreCase);
            foreach (var id in supplies.Keys.Concat(savedStock.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                supplies.TryGetValue(id, out double actual); savedStock.TryGetValue(id, out double planned);
                int units = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(actual, planned)));
                if (units > 0) route.Add(new BookmarkChainBalanceEntry(id, units));
            }
            var plan = BookmarkChainBalance.Calculate(_catalog, route);
            // Unbacked initial bookmarks come first in GTNH's math; they are NOT real items
            // we can pull. Retain them separately in the bill, so they never look like inventory.
            foreach (var material in plan.Inputs)
            {
                var line = Add(material.ItemId, material.Quantity, material.Ingredient, material.IsCatalyst);
                if (line == null) continue;
                supplies.TryGetValue(line.Id, out var actual);
                savedStock.TryGetValue(line.Id, out var planned);
                double reserved = Math.Min(material.Quantity, Math.Max(0, planned - actual));
                line.Planned += reserved;
                savedStock[line.Id] = planned - reserved;
            }
            if (plan.Diagnostics.Any(d => d.Code is BookmarkChainDiagnosticCode.Cycle or BookmarkChainDiagnosticCode.InvalidRecipe or BookmarkChainDiagnosticCode.MissingRecipe))
                block = L("bookmark.pull.chain_incomplete");
            // The summary coalesces predicates sharing an ID. Never auto-select from an ambiguous aggregate.
            if (plan.Steps.SelectMany(s => s.Inputs).GroupBy(i => i.ItemId, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Select(i => (i.Ingredient?.MinimumCondition, i.Ingredient?.NoMaxCondition, i.Ingredient?.IgnoredItemId)).Distinct().Skip(1).Any()))
                block = L("bookmark.pull.differing_conditions");
        }
        else
        {
            foreach (var entry in entries)
                foreach (var part in CellsForBookmark(entry, compact: false, previewCarried: false)) AddCell(part, part.Amount);
        }

        var available = held.Where(i => i).Distinct().ToList();
        var fluids = PullAmounts(available);
        foreach (var line in lines)
        {
            if (line.Id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase))
            {
                fluids.TryGetValue(line.Id, out var amount);
                line.Credit = shift ? Math.Min(line.Remaining, amount) : 0;
                fluids[line.Id] = amount - line.Credit;
            }
            else if (shift)
            {
                for (int index = available.Count - 1; index >= 0 && line.Remaining > 0; index--)
                    if (PullMatches(available[index], line)) { line.Credit++; available.RemoveAt(index); }
            }
        }

        if (block == null && camera && body && source)
        {
            try { Move(); }
            catch (Exception error)
            {
                Debug.LogWarning("NEI native bookmark pull interrupted: " + error);
                block = L("bookmark.pull.pickup_interrupted");
            }
        }
        ReportPull(lines, stock, stockKnown, shift, block);

        void Move()
        {
            // Take only instances in the initial snapshot. A hook introducing or removing another
            // child is not permission to continue on the changed stock.
            var expected = new HashSet<Item>(stock);
            foreach (var line in lines)
            {
                if (line.Remaining <= 0 || !line.Id.StartsWith("item:", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var item in stock)
                {
                    if (line.Remaining <= 0) break;
                    if (!expected.Contains(item) || !item || !PullMatches(item, line)) continue;
                    int slot = PullSlot(body!, item, source!);
                    if (slot < 0) continue;
                    block = PullBlock(camera!, body, source);
                    if (block != null) break;
                    if (PullTerminalOpen(out _) != false || !expected.SetEquals(PullSourceItems(source!)))
                    {
                        block = L("bookmark.pull.container_changed"); break;
                    }
                    if (!PullListed(camera!, item) || !body!.DoPickupCheck(item, noAlerts: true)) continue;
                    var previousSlots = body.slots.Select((_, index) => body.GetItem(index)).ToArray();
                    body.PickUpItem(item, slot, force: false); // Native call unloads from the actual parent itself.
                    if (item && body.GetItem(slot) == item && item.transform.parent == body.slots[slot].transform)
                    { line.Moved++; expected.Remove(item); }
                    else { block = L("bookmark.pull.pickup_unconfirmed"); break; }
                    if (camera!.currentContainer != source || !expected.SetEquals(PullSourceItems(source!)) ||
                        previousSlots.Length != body.slots.Length || previousSlots.Where((old, i) => i != slot && old != body.GetItem(i)).Any())
                    { block = L("bookmark.pull.inventory_changed"); break; }
                }
                if (block != null) break;
            }
            if (lines.Any(line => line.Moved > 0) && camera!.currentContainer == source && camera.containerMenu.activeInHierarchy)
                camera.RepopulateContainer();
        }

        void AddCell(BookmarkCell part, double amount)
        {
            var input = part.Ingredient >= 0 ? BookmarkedRecipe(part.Entry.Id)?.Inputs[part.Ingredient] : null;
            Add(part.Id, amount, input, input?.IsCatalyst == true);
        }
        PullLine? Add(string id, double amount, RecipeIngredient? input, bool tool)
        {
            if (!(amount > 0) || double.IsInfinity(amount)) return null;
            string key = PullKey(id);
            var line = lines.Find(l => l.Id == key && l.Input == input && l.Retained == tool);
            if (line == null) lines.Add(line = new PullLine(key, input, tool));
            line.Target += key.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? amount : Math.Ceiling(amount);
            return line;
        }
    }

    private string PullKey(string id) => _catalog.TryGetItem(id, out var item) ? item.BrowseId : id;

    private static bool PullUsable(Item item) => item && !item.favourited && !string.IsNullOrEmpty(item.id) &&
        item.Stats != null && (!item.Stats.destroyAtZeroCondition || item.condition > 0f) && (!item.container || item.container.itemCount == 0);

    private static bool PullMatches(Item item, PullLine line) => PullUsable(item) &&
        string.Equals("item:" + item.id, line.Id, StringComparison.Ordinal) &&
        (line.Input == null || (item.id != line.Input.IgnoredItemId && item.condition >= line.Input.MinimumCondition &&
            (!line.Input.NoMaxCondition || item.condition < .991f)));

    private static Dictionary<string, double> PullAmounts(IEnumerable<Item> items)
    {
        var amounts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Distinct().Where(PullUsable))
        {
            Count("item:" + item.id, 1);
            if (item.TryGetComponent<WaterContainerItem>(out var vessel) && vessel.stack != null)
                foreach (var portion in vessel.stack)
                    if (portion != null && !string.IsNullOrEmpty(portion.liquidId) && portion.amount > 0 && !float.IsInfinity(portion.amount))
                        Count("liquid:" + portion.liquidId, portion.amount);
        }
        return amounts;
        void Count(string key, double amount) { amounts.TryGetValue(key, out double old); amounts[key] = old + amount; }
    }

    private static bool? PullTerminalOpen(out Type? itemsApi)
    {
        itemsApi = null;
        var plugins = Chainloader.PluginInfos;
        if (plugins == null) return null;
        if (!plugins.TryGetValue(GameSurface.TerminalGuid, out var plugin)) return false;
        var assembly = plugin.Instance?.GetType().Assembly;
        itemsApi = assembly?.GetType("UnknownInventoryTerminal.TerminalItemsApi");
        return assembly?.GetType("UnknownInventoryTerminal.TerminalApi")?.GetProperty("IsOpen")?.GetValue(null) as bool?;
    }

    private static bool ReadPullTerminal(Type api, List<Item> held, List<Item> stock)
    {
        if (api.GetMethod("GetLoadout")?.Invoke(null, null) is IEnumerable<Item> loadout) held.AddRange(loadout);
        object?[] snapshot = { null, null };
        if (api.GetMethod("TryGetSnapshot")?.Invoke(null, snapshot) is not true || snapshot[0] is not IEnumerable entries) return false;
        var resolve = api.GetMethod("TryResolve");
        if (resolve == null) return false;
        foreach (var entry in entries)
        {
            object?[] args = { entry?.GetType().GetProperty("InstanceKey")?.GetValue(entry), null };
            if (args[0] is string && resolve.Invoke(null, args) is true && args[1] is Item item && item) stock.Add(item);
        }
        return true;
    }

    private string? PullBlock(PlayerCamera? camera, Body? body, Container? source)
    {
        var transfer = LiquidTransfer.instance;
        if (camera == null || body == null || camera != PlayerCamera.main || camera.body != body || !camera.isActiveAndEnabled ||
            !body.alive || !body.conscious || !WorldGeneration.world || !WorldGeneration.world.worldExists || WorldGeneration.world.cutsceneActive ||
            PauseHandler.paused || SettingsMenu.instance || (ConsoleScript.instance && ConsoleScript.instance.active) ||
            (MinigameBase.main && MinigameBase.main.currentMinigame) ||
            (transfer && transfer.isActiveAndEnabled && camera.mainCanvas && transfer.transform.IsChildOf(camera.mainCanvas.transform)) ||
            camera.blockItemUsage || camera.woundView.activeInHierarchy || camera.craftingPanel.activeInHierarchy ||
            camera.tradeMenu.activeInHierarchy || camera.itemCustomizer.activeInHierarchy || _producing ||
            camera.dragItem || _plugin.NativePointerActive || _bookmarkDrag != null ||
            Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            return L("bookmark.pull.inventory_unavailable");
        if (!source || camera.currentContainer != source || !camera.containerMenu || !camera.containerMenu.activeInHierarchy)
            return L("bookmark.pull.open_source");
        if (CheatSession.Detect(body).Access != CheatAccess.Offline)
            return L("bookmark.pull.offline_required");
        if (!PullLocalSource(body, source!))
            return L("bookmark.pull.source_out_of_reach");
        return null;
    }

    private static bool PullLocalSource(Body body, Container source) => source && source.isActiveAndEnabled &&
        source.TryGetComponent<Item>(out var bag) && bag && bag.condition > 0f &&
        (!bag.transform.parent || body.GetSurfaceInventoryItems().Contains(bag)) && body.DoPickupCheck(bag, noAlerts: true);

    private static IEnumerable<Item> PullSourceItems(Container source)
    {
        foreach (Transform child in source.transform)
            if (child.TryGetComponent<Item>(out var item)) yield return item;
    }

    private static bool PullListed(PlayerCamera camera, Item item) => item.transform.parent == camera.currentContainer.transform &&
        camera.containerUnloadButtons.Any(button => button && button.activeInHierarchy && button.TryGetComponent<InvButton>(out var slot) && slot.refItem == item);

    private static int PullSlot(Body body, Item item, Container source)
    {
        // Native pickup unloads first, so preflight the components that native unload/pickup dereference.
        if ((item.Stats.wearable && !item.Stats.wearableCanBeHeld) || !item.rb || !item.GetComponent<SpriteRenderer>() ||
            !source.GetComponent<SpriteRenderer>() || item.rb.simulated) return -1;
        float weight = item.totalWeight;
        // Conservative upper bound: do not assume a discount for the item leaving a worn bag.
        if (!(weight >= 0 && body.GetTotalEncumberance() + weight <= body.maxEncumberance)) return -1;
        for (int i = 0; i < body.slots.Length; i++)
        {
            var slot = body.slots[i];
            if (slot && slot.body == body && slot.limb && slot.canPickUp && !body.HoldingItem(i) && (!item.Stats.onlyHoldInHands || slot.isHand)) return i;
        }
        return -1;
    }

    private void ReportPull(List<PullLine> lines, List<Item> stock, bool stockKnown, bool shift, string? reason)
    {
        if (lines.Count == 0) { Status(reason ?? L("bookmark.pull.nothing_to_pull")); return; }
        var before = PullAmounts(stock);
        var report = new List<string>();
        foreach (var line in lines)
        {
            string id = line.Id;
            bool liquid = id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase);
            string name = line.Input is { MatchesAny: true } requirement && SameId(id, requirement.BrowseId)
                ? RequirementTitle(requirement) : Display(id);
            if (line.Retained) name = L("bookmark.pull.reusable", name);
            if (liquid) name = L("bookmark.pull.manual_pour", name);
            before.TryGetValue(id, out var stocked);
            var parts = new List<string> { L("bookmark.pull.target", name, BookmarkAmount(id, line.Target)) };
            if (line.Planned > 0) parts.Add(L("bookmark.pull.manual_supply", BookmarkAmount(id, line.Planned)));
            if (shift) parts.Add(L("bookmark.pull.held", BookmarkAmount(id, line.Credit)));
            if (stockKnown) parts.Add(L("bookmark.pull.source_total", BookmarkAmount(id, stocked)));
            if (line.Moved > 0) parts.Add(L("bookmark.pull.moved", BookmarkAmount(id, line.Moved)));
            parts.Add(L("bookmark.pull.remaining", BookmarkAmount(id, line.Remaining)));
            report.Add(string.Join(" · ", parts));
        }
        int moved = (int)lines.Sum(l => l.Moved);
        string title = reason ?? (lines.All(l => l.Remaining == 0)
            ? L("bookmark.pull.complete", moved)
            : L("bookmark.pull.partial", moved));
        // Do not overwrite the player's clipboard. The log retains the COMPLETE manual checklist.
        Debug.Log("NEI: " + title + "\n" + string.Join("\n", report));
        string more = report.Count > 4 ? "\n" + L("bookmark.pull.full_list") : "";
        Status(title + "\n" + string.Join("\n", report.Take(4)) + more);
        _statusUntil = Time.unscaledTime + 9f;
    }
}
