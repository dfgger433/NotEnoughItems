using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private BookmarkChainPlan? _chainPlan;
    private readonly Dictionary<(string Page, string Group, string Requirement), string> _ingredientChoices = new();

    private void OpenChain(string group, bool details)
    {
        _chainGroup = group;
        _chainDetails = details;
        _chainScroll = Vector2.zero;
        _chainPlan = CalculateChain(group);
        _bookmarkDrag = null;
        _skipFocusFrame = true;
    }

    private BookmarkChainPlan CalculateChain(string group)
    {
        var entries = _book.Page(_group).Where(entry => group.Length == 0 || entry.Group == group).ToArray();
        var recipeLookup = _catalog.Recipes.GroupBy(RecipeKey).ToDictionary(recipes => recipes.Key, recipes => recipes.First());
        var selected = entries.Where(entry => entry.IsRecipe && recipeLookup.ContainsKey(entry.Id))
            .Select(entry => (Entry: entry, Recipe: recipeLookup[entry.Id])).ToArray();
        var targets = new List<BookmarkChainTarget>();
        foreach (var entry in entries.Where(entry => !entry.IsRecipe)) targets.Add(new BookmarkChainTarget(entry.Id, entry.Quantity));
        var outputs = selected.Select(candidate => (candidate.Entry, candidate.Recipe, Output: candidate.Recipe.Outputs.FirstOrDefault()))
            .Where(candidate => candidate.Output != null).ToArray();
        var sinks = outputs.Where(candidate => !selected.Any(other => other.Recipe != candidate.Recipe && other.Recipe.Inputs.Any(input =>
            !input.IsCatalyst && !input.MatchesAny && SameId(input.BrowseId, candidate.Output!.BrowseId)))).ToArray();
        foreach (var candidate in sinks.Length == 0 ? outputs.Take(1) : sinks)
            targets.Add(new BookmarkChainTarget(candidate.Output!.BrowseId, candidate.Entry.Quantity, candidate.Recipe));
        var choices = _ingredientChoices.Where(pair => pair.Key.Page == _group && pair.Key.Group == group)
            .ToDictionary(pair => pair.Key.Requirement, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return BookmarkChain.Plan(new BookmarkChainRequest(_catalog, targets, selected.Select(entry => entry.Recipe),
            available: InventoryForChain(), preferredIngredients: choices));
    }

    private static IReadOnlyDictionary<string, int> InventoryForChain()
    {
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var liquids = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var camera = PlayerCamera.main;
        if (!camera || !camera.body) return quantities;
        foreach (var item in camera.body.GetAllItemsThorough().Distinct())
        {
            if (!item || item.favourited || string.IsNullOrEmpty(item.id)) continue;
            if (!item.TryGetComponent<Container>(out var storage) || storage.itemCount == 0)
            {
                var key = "item:" + item.id;
                quantities.TryGetValue(key, out int before);
                quantities[key] = before + 1;
            }
            if (!item.TryGetComponent<WaterContainerItem>(out var container) || container.stack == null) continue;
            var local = container.stack.Where(stack => stack != null && stack.amount > 0 && !string.IsNullOrWhiteSpace(stack.liquidId))
                .GroupBy(stack => stack.liquidId, StringComparer.OrdinalIgnoreCase);
            foreach (var contents in local)
            {
                var key = "liquid:" + contents.Key;
                double amount = contents.Sum(stack => (double)stack.amount);
                liquids.TryGetValue(key, out double before);
                // One native ingredient must fit in one container; don't add separate stacks together.
                liquids[key] = Math.Max(before, amount);
            }
        }
        foreach (var liquid in liquids) quantities[liquid.Key] = (int)Math.Min(int.MaxValue, Math.Floor(liquid.Value));
        return quantities;
    }

    private void DrawChain(Rect panel)
    {
        NeiSkin.Background(panel, "recipe");
        Header(panel, _chainGroup == string.Empty ? L("Bookmark page chain", "整页书签合成链") : L("Bookmark group chain", "书签组合成链"), "NEI");
        float x = panel.x + 8, y = panel.y + 27, w = panel.width - 16;
        if (NeiSkin.Button(new Rect(x, y, w / 3 - 3, 23), L("Back to bookmarks", "返回书签"))) { _chainGroup = null; return; }
        if (NeiSkin.Button(new Rect(x + w / 3, y, w / 3 - 3, 23), L("Recipe steps", "配方步骤"), selected: _chainDetails))
        { _chainDetails = !_chainDetails; _chainScroll = Vector2.zero; }
        if (NeiSkin.Button(new Rect(x + 2 * w / 3, y, w / 3, 23), L("Recalculate", "重新计算"))) _chainPlan = CalculateChain(_chainGroup!);
        y += 27;
        NeiSkin.Label(new Rect(x, y, w, 21), L("Planning only · availability estimated from carried items", "仅作规划 · 持有量按随身物品估算"), muted: true);
        y += 23;
        var plan = _chainPlan;
        if (plan == null) return;
        var rows = new List<(string Label, string? Id, string? Amount, RecipeIngredient? Choice)>();
        void Heading(string en, string zh) => rows.Add((L(en, zh), null, null, null));
        void Row(string id, double amount, string? label = null, RecipeIngredient? choice = null) => rows.Add((label ?? Display(id), id,
            amount.ToString("0.##", CultureInfo.InvariantCulture) + (id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase) ? " mL" : ""), choice));
        Heading("Targets", "目标产物");
        foreach (var target in plan.Targets) Row(target.ItemId, target.Quantity);
        Heading("Required (net)", "所需材料（净用量）");
        foreach (var input in plan.Inputs.Where(input => input.RequiredQuantity > 0))
            Row(input.ItemId, input.RequiredQuantity, input.IsRequirement ? RequirementTitle(input.Ingredient) : null,
                input.Ingredient.MatchesAny ? input.Ingredient : null);
        Heading("Missing", "缺少材料");
        foreach (var missing in plan.Missing) Row(missing.ItemId, missing.Shortage);
        if (plan.Missing.Count == 0) rows.Add((L("No simulated shortages", "没有模拟缺料"), null, null, null));
        if (plan.Surplus.Count > 0) Heading("Surplus", "剩余产物");
        foreach (var surplus in plan.Surplus) Row(surplus.ItemId, surplus.Surplus);
        if (_chainDetails)
        {
            Heading("Recipe steps", "配方步骤");
            foreach (var step in plan.Nodes)
            {
                Row(RecipeKey(step.Recipe), step.ProducedQuantity, $"{step.Recipe.Name} ×{step.Batches}");
                foreach (var input in step.Inputs)
                    Row(input.ItemId, input.RequiredQuantity, "↳ " + (input.IsRequirement ? RequirementTitle(input.Ingredient) : Display(input.ItemId)),
                        input.Ingredient.MatchesAny ? input.Ingredient : null);
            }
        }
        if (plan.Diagnostics.Count > 0) Heading("Notes", "计算提示");
        foreach (var issue in plan.Diagnostics) rows.Add((issue.Message, null, null, null));
        if (plan.Targets.Count == 0) rows.Add((L("Bookmark recipe outputs or items first.", "请先收藏配方产物或物品。"), null, null, null));
        var viewport = new Rect(x, y, w, Mathf.Max(1, panel.yMax - y - 7));
        var content = new Rect(0, 0, w - 19, Mathf.Max(viewport.height, rows.Count * 28));
        var inputEvent = Event.current;
        if (inputEvent.type == EventType.ScrollWheel && inputEvent.shift && viewport.Contains(inputEvent.mousePosition))
        {
            Vector2 local = inputEvent.mousePosition - viewport.position + _chainScroll;
            int index = (int)(local.y / 28);
            if (local.x >= 2 && local.x < 27 && index >= 0 && index < rows.Count && rows[index].Choice is { } choice)
            {
                CycleChainIngredient(choice, inputEvent.delta.y < 0 ? 1 : -1);
                inputEvent.Use();
            }
        }
        _chainScroll = GUI.BeginScrollView(viewport, _chainScroll, content, false, content.height > viewport.height);
        try
        {
            Vector2 offset = viewport.position - _chainScroll;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float top = i * 28;
                if (top + offset.y + 28 <= viewport.yMin || top + offset.y >= viewport.yMax) continue;
                var icon = new Rect(2, top + 1, 25, 25);
                if (!string.IsNullOrEmpty(row.Id)) ItemSlot(icon, row.Id, clipping: viewport, globalOffset: offset,
                    details: row.Choice != null ? L("Shift+scroll changes the ingredient variant", "Shift＋滚轮选择匹配的材料") : null);
                NeiSkin.Label(new Rect(31, top, content.width - 100, 25), row.Label);
                if (row.Amount != null) NeiSkin.Label(new Rect(content.width - 68, top, 66, 25), row.Amount, TextAnchor.MiddleRight);
            }
        }
        finally { GUI.EndScrollView(); }
    }

    private void CycleChainIngredient(RecipeIngredient ingredient, int delta)
    {
        var choices = _catalog.GetMatchingItems(ingredient);
        if (choices.Count == 0 || _chainGroup == null) return;
        var key = (_group, _chainGroup, ingredient.BrowseId);
        _ingredientChoices.TryGetValue(key, out var current);
        int index = current == null ? -1 : Array.FindIndex(choices.ToArray(), choice => SameId(choice.BrowseId, current));
        _ingredientChoices[key] = choices[Wrap(index + delta + (index < 0 && delta < 0 ? 1 : 0), choices.Count)].BrowseId;
        _chainPlan = CalculateChain(_chainGroup);
    }

    private void BookmarkOrder(Recipe root, int quantity)
    {
        var recipeIndex = _catalog.Recipes.GroupBy(RecipeKey).ToDictionary(set => set.Key, set => set.First());
        var available = _book.Entries.Where(e => e.IsRecipe && recipeIndex.ContainsKey(e.Id)).Select(e => recipeIndex[e.Id]).Distinct().ToArray();
        var picked = new List<Recipe>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(Recipe recipe)
        {
            if (picked.Count >= 128 || !seen.Add(RecipeKey(recipe))) return;
            picked.Add(recipe);
            foreach (var input in recipe.Inputs.Where(input => !input.MatchesAny && !input.IsCatalyst))
            {
                var chosen = available.FirstOrDefault(candidate => candidate.Outputs.Any(output => SameId(output.BrowseId, input.BrowseId)));
                if (chosen != null) Visit(chosen);
            }
        }
        Visit(root);
        // Use a new page so existing recipe bookmarks keep their groups and quantities.
        var page = root.Name.Length > 28 ? root.Name.Substring(0, 28) : root.Name;
        var original = page;
        for (int n = 2; _book.Page(page).Any(); n++) page = $"{original} {n}";
        string group = Guid.NewGuid().ToString("N");
        foreach (var recipe in picked)
        {
            string key = RecipeKey(recipe);
            if (_book.Toggle(page, key, recipe == root ? quantity : 1) && _book.Find(page, key) is { } entry) entry.Group = group;
        }
        SaveBookmarks();
        SelectBookmarkGroup(page);
        _open = false;
        OpenChain(group, false);
    }
}
