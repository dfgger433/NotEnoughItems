using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void DrawInspector(Rect p)
    {
        NeiSkin.Background(p, "recipe");
        var all = _item == null ? _catalog.Recipes : _usage ? _catalog.GetRecipesUsing(_item) : _catalog.GetRecipesProducing(_item);
        if (_usage && _lookupContents.Length > 0)
            all = all.Concat(_lookupContents.SelectMany(_catalog.GetRecipesUsing)).Distinct().ToArray();
        var categories = (_item == null ? _catalog.RecipeCategories : _catalog.RecipeCategories.Where(c => all.Any(r => SameId(r.CategoryId, c.Id)))).ToArray();
        _catalog.TryGetItem(_item ?? string.Empty, out var subject);
        _activeRecipeKey = null;
        string name = subject?.DisplayName ?? _item ?? L("Bookmarked recipe", "收藏的配方");
        Header(p, name, _item == null ? "NEI" : _usage ? L("Uses", "用途") : L("Recipes", "配方"));
        float x = p.x + 8, w = p.width - 16, y = p.y + 31;
        if (NeiSkin.Button(new Rect(x, y, 64, 23), L("Back", "返回"))) { Back(); return; }
        if (NeiSkin.IconButton(new Rect(p.xMax - 31, y, 23, 23), "close", L("Close", "关闭"))) { _open = false; _back.Clear(); return; }
        float mid = x + 68;
        if (_item != null && w >= 245)
        {
            float half = (w - 138) / 2;
            if (NeiSkin.Tab(new Rect(mid, y, half, 23), L("Recipes", "配方"), !_usage)) { Lookup(_item, false, _lookupContents); return; }
            if (NeiSkin.Tab(new Rect(mid + half + 4, y, half, 23), L("Uses", "用途"), _usage)) { Lookup(_item, true, _lookupContents); return; }
        }
        y += 28;
        int categoryIndex = Array.FindIndex(categories, c => SameId(c.Id, _category));
        if (_category != null && categoryIndex < 0) _category = null;
        int choice = categoryIndex + 1;
        if (NeiSkin.IconButton(new Rect(x, y, 24, 23), "previous", L("Previous category", "上一个类别"), categories.Length != 0)) SetCategory(categories, Wrap(choice - 1, categories.Length + 1));
        if (NeiSkin.IconButton(new Rect(p.xMax - 32, y, 24, 23), "next", L("Next category", "下一个类别"), categories.Length != 0)) SetCategory(categories, Wrap(choice + 1, categories.Length + 1));
        var selectedCategory = categories.FirstOrDefault(c => SameId(c.Id, _category));
        if (NeiSkin.Button(new Rect(x + 28, y, w - 56, 23), selectedCategory?.Name ?? L("All recipe categories", "全部配方类别"), categories.Length != 0, true))
            SetCategory(categories, Wrap(choice + 1, categories.Length + 1));
        y += 29;
        _searchRect = new Rect(x, y, w, 22);
        string filter = NeiSkin.TextField(_searchRect, _recipeQuery, SearchControl, 160);
        if (filter != _recipeQuery) { _recipeIndex = 0; _recipeScroll = Vector2.zero; _recipeQuery = filter; }
        if (_recipeQuery.Length == 0 && GUI.GetNameOfFocusedControl() != SearchControl)
            NeiSkin.Label(new Rect(x + 6, y, w - 12, 22), L("Find a recipe…", "搜索配方…"), muted: true);
        if (_searchRect.Contains(Event.current.mousePosition))
            Tip(L("Recipe search", "配方搜索"), L("< input   > output   ^ machine or tool   ~ name or description", "< 输入   > 产物   ^ 机器或工具   ~ 名称或描述"));
        y += 27;
        IReadOnlyList<Recipe> recipes = _category == null ? all : _item == null ? _catalog.GetRecipesForCategory(_category) : all.Where(r => SameId(r.CategoryId, _category)).ToArray();
        if (_recipeQuery.Length > 0) recipes = recipes.Where(MatchesRecipe).ToArray();
        if (_craftableOnly) recipes = recipes.Where(IsCraftable).ToArray();
        if (_selectedRecipeBookmark != null)
        {
            for (int i = 0; i < recipes.Count; i++)
                if (RecipeKey(recipes[i]) == _selectedRecipeBookmark) { _recipeIndex = i; break; }
            _selectedRecipeBookmark = null;
        }
        if (recipes.Count == 0)
        {
            NeiSkin.Label(new Rect(x, y + 20, w, 26), L("No matching recipes", "找不到匹配的配方"), TextAnchor.MiddleCenter);
            if (_craftableOnly && NeiSkin.Button(new Rect(x + w * .25f, y + 56, w * .5f, 23), L("Show all recipes", "显示全部配方"))) _craftableOnly = false;
            else if (_item != null && !_craftableOnly) ItemSlot(new Rect(p.center.x - 20, y + 55, 40, 40), _item, false);
            return;
        }
        _recipeIndex = Mathf.Clamp(_recipeIndex, 0, recipes.Count - 1);
        if (NeiSkin.IconButton(new Rect(x, y, 24, 23), "previous", L("Previous recipe", "上一条配方"), recipes.Count > 1)) StepRecipe(-1, recipes.Count);
        if (NeiSkin.IconButton(new Rect(p.xMax - 32, y, 24, 23), "next", L("Next recipe", "下一条配方"), recipes.Count > 1)) StepRecipe(1, recipes.Count);
        Recipe recipe = recipes[_recipeIndex];
        NeiSkin.Label(new Rect(x + 25, y, w - 50, 23), $"{_recipeIndex + 1}/{recipes.Count}  ·  {recipe.Name}", TextAnchor.MiddleCenter);
        y += 27;
        _activeRecipeKey = RecipeKey(recipe);
        float third = w / 3;
        if (NeiSkin.Button(new Rect(x, y, third - 2, 22), L("Bookmark recipe", "收藏配方"), selected: _book.Find(_group, _activeRecipeKey) != null))
            ToggleBookmark(_activeRecipeKey);
        if (NeiSkin.Button(new Rect(x + third, y, third - 2, 22), L("Order from bookmarks", "用已收藏配方建组")))
            BookmarkOrder(recipe, 1);
        if (NeiSkin.Button(new Rect(x + 2 * third, y, third, 22), L("Craftable", "可制作"), selected: _craftableOnly)) { _craftableOnly = !_craftableOnly; _recipeIndex = 0; }
        y += 27;
        bool native = recipe.IsNative && recipe.NativeIndex >= 0;
        float footer = native ? 33 : 9;
        var viewport = new Rect(x, y, w, p.yMax - y - footer);
        if (viewport.height >= 35) DrawRecipeBody(viewport, recipe);
        if (native && recipe.NativeRecipe is { } definition)
        {
            bool known = _plugin.CanOpenCrafting(definition);
            if (NeiSkin.Button(new Rect(x, p.yMax - 29, w, 23), known ? L("Open in game's crafting menu", "在游戏制作菜单中打开") :
                L("Not available in the current crafting menu", "当前游戏制作菜单尚不可用"), known) && _plugin.OpenCrafting(definition)) _open = false;
        }
    }

    private bool MatchesRecipe(Recipe recipe)
    {
        string names = recipe.Name + " " + recipe.Description + " " + recipe.Id + " " + recipe.CategoryId;
        return NeiSearch.MatchesFields(_recipeQuery, names, type => type switch
        {
            '<' => string.Join(" ", recipe.Inputs.Select(IngredientSearchText)),
            '>' => string.Join(" ", recipe.Outputs.Select(o => o.ItemId + " " + Display(o.BrowseId) + " " + o.DisplayName)),
            '^' => recipe.MachineId + " " + Display(recipe.MachineId) + " " + string.Join(" ", recipe.Inputs.Where(i => i.IsCatalyst).Select(IngredientSearchText)),
            '~' => names,
            '@' => recipe.Source,
            _ => null
        });
    }

    private string IngredientSearchText(RecipeIngredient ingredient)
    {
        if (_ingredientSearch.TryGetValue(ingredient, out string text)) return text;
        string title = ingredient.MatchesAny ? RequirementTitle(ingredient) : Display(ingredient.BrowseId);
        text = $"{ingredient.ItemId} {title} {ingredient.DisplayName} {ingredient.QualityId} {ingredient.RequiredTag}";
        if (ingredient.MatchesAny)
            text += " " + string.Join(" ", _catalog.GetMatchingItems(ingredient, includeBelowRequirement: true)
                .Select(candidate => $"{candidate.Id} {candidate.DisplayName}"));
        return _ingredientSearch[ingredient] = text;
    }

    private string Display(string id) => _catalog.TryGetItem(id, out var item) ? item!.DisplayName : id;
    private static string RecipeKey(Recipe recipe) => "recipe/" + recipe.Source + "/" + recipe.Id;
    private static bool SameId(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private bool IsCraftable(Recipe recipe)
    {
        if (recipe.NativeRecipe is not { } native) return false;
        if (Time.unscaledTime >= _craftableUntil)
        {
            _craftable.Clear();
            _craftableUntil = Time.unscaledTime + .8f;
        }
        if (!_craftable.TryGetValue(native, out var ready))
        {
            try { ready = GameSurface.CanOpenCrafting(native) && native.GetItemsForRecipe() != null; }
            catch (Exception) { ready = false; }
            _craftable.Add(native, ready);
        }
        return ready;
    }

    private void DrawRecipeBody(Rect viewport, Recipe recipe)
    {
        const float cell = 37;
        UpdateRequirements(recipe);
        var inputs = recipe.Inputs;
        var outputs = recipe.Outputs;
        int cols = Mathf.Max(1, (int)((viewport.width - 26) / cell));
        int requirementCols = Mathf.Max(1, (int)((viewport.width - 26) / 86));
        float requirementWidth = (viewport.width - 26) / requirementCols;
        const float requirementHeight = 54;
        int materials = inputs.Count(input => !input.IsCatalyst);
        int tools = inputs.Count - materials;
        int expanded = _expandedRequirement;
        var candidates = expanded >= 0 && expanded < inputs.Count ? Alternatives(expanded, inputs[expanded]).Related : Array.Empty<CatalogItem>();
        float extra = string.IsNullOrWhiteSpace(recipe.Description) ? 0 : 69;
        float h = 27 + Rows(outputs.Count, cols) * cell + extra;
        if (materials > 0 || tools == 0) h += 27 + Rows(materials, requirementCols) * requirementHeight;
        if (tools > 0) h += 27 + Rows(tools, requirementCols) * requirementHeight;
        if (candidates.Count > 0) h += 29 + Rows(candidates.Count, cols) * cell;
        if (!string.IsNullOrEmpty(recipe.MachineId)) h += cell + 11;
        var content = new Rect(0, 0, viewport.width - 19, Mathf.Max(viewport.height, h));
        _recipeScroll = GUI.BeginScrollView(viewport, _recipeScroll, content, false, h > viewport.height);
        var offset = viewport.position - _recipeScroll;
        try
        {
            float y = 0;
            if (!string.IsNullOrEmpty(recipe.MachineId))
            {
                ItemSlot(new Rect(4, y + 2, cell - 2, cell - 2), recipe.MachineId!, false, clipping: viewport, globalOffset: offset);
                NeiSkin.Label(new Rect(cell + 6, y + 3, content.width - cell - 10, cell), L("Machine / catalyst", "机器／催化剂"));
                y += cell + 11;
            }
            if (materials > 0 || tools == 0)
                y = DrawIngredients(inputs, false, L("Ingredients", "原料与性质"), y, requirementWidth, requirementHeight, requirementCols, viewport, offset);
            if (tools > 0)
                y = DrawIngredients(inputs, true, L("Tools · not consumed", "工具 · 不消耗"), y, requirementWidth, requirementHeight, requirementCols, viewport, offset);
            if (candidates.Count > 0)
            {
                var requirement = inputs[expanded];
                string title = RequirementTitle(requirement);
                NeiSkin.Label(new Rect(4, y, viewport.width - 26, 22), $"{title} · {L("Matching items", "匹配物品")} ({candidates.Count})");
                y += 22;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var rect = IngredientRect(i, y, cell, cols);
                    float top = rect.y + offset.y;
                    if (top + rect.height <= viewport.yMin || top >= viewport.yMax) continue;
                    var candidate = candidates[i];
                    bool below = !requirement.Matches(candidate);
                    ItemSlot(rect, candidate.BrowseId, false, clipping: viewport, globalOffset: offset,
                        details: below ? L("Has this quality, but below this recipe's requirement", "具有该性质，但未达本配方要求") : null,
                        belowRequirement: below);
                }
                y += Rows(candidates.Count, cols) * cell + 7;
            }
            NeiSkin.Label(new Rect(4, y, viewport.width - 26, 19), L("Outputs", "产物"));
            y += 20;
            if (outputs.Count == 0) NeiSkin.Label(new Rect(4, y, viewport.width - 26, cell), "—", muted: true);
            for (int i = 0; i < outputs.Count; i++)
            {
                var output = outputs[i];
                ItemSlot(IngredientRect(i, y, cell, cols), output.BrowseId, false,
                    amount: Amount(output.Amount, output.IsLiquid), fallback: output.DisplayName,
                    clipping: viewport, globalOffset: offset,
                    details: !output.IsLiquid && output.Condition < 1f ? L("Condition: ", "耐久：") + output.Condition.ToString("P0", CultureInfo.InvariantCulture) : null);
            }
            y += Rows(outputs.Count, cols) * cell + 7;
            if (extra > 0)
            {
                NeiSkin.Text(new Rect(4, y, content.width - 8, extra), recipe.Description);
            }
        }
        finally { GUI.EndScrollView(); }
    }

    private float DrawIngredients(IReadOnlyList<RecipeIngredient> items, bool tools, string heading, float y, float width, float height, int cols, Rect viewport, Vector2 offset)
    {
        NeiSkin.Label(new Rect(4, y, viewport.width - 26, 19), heading);
        y += 20;
        int slot = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var ingredient = items[i];
            if (ingredient.IsCatalyst != tools) continue;
            float left = 4 + slot % cols * width;
            float top = y + slot / cols * height;
            slot++;
            var rect = new Rect(left, top, 35, 35);
            string caption = ingredient.MatchesAny ? RequirementTitle(ingredient) :
                string.IsNullOrEmpty(ingredient.DisplayName) ? Display(ingredient.BrowseId) : ingredient.DisplayName;
            NeiSkin.Label(new Rect(left, top + 35, width - 3, 16), caption);
            string? detail = tools ? L("Tool / not consumed", "工具／不消耗") : null;
            if (ingredient.MinimumCondition > 0 && !ingredient.IsLiquid)
                detail = (detail == null ? "" : detail + "\n") + L("Minimum condition: ", "最低耐久：") + ingredient.MinimumCondition.ToString("P0", CultureInfo.InvariantCulture);
            if (ingredient.NoMaxCondition && !ingredient.IsLiquid)
                detail = (detail == null ? "" : detail + "\n") + L("Must not be at full condition", "不可为满耐久");
            if (!ingredient.MatchesAny)
            {
                ItemSlot(rect, ingredient.BrowseId, false, amount: Amount(ingredient.Amount, ingredient.IsLiquid),
                    fallback: ingredient.DisplayName, clipping: viewport, globalOffset: offset, details: detail);
                continue;
            }

            var matches = Alternatives(i, ingredient);
            var display = PreferredMatch(i, ingredient, matches.Eligible) ?? matches.Related.FirstOrDefault();
            string requirementTitle = RequirementTitle(ingredient);
            string required = requirementTitle + " · " + ingredient.Amount.ToString("0.##", CultureInfo.InvariantCulture);
            if (display != null)
            {
                ItemSlot(rect, display.BrowseId, false, clipping: viewport, globalOffset: offset,
                    details: required + "\n" + L($"{matches.Related.Count} items with this property", $"{matches.Related.Count} 个物品具有该性质") +
                        (matches.Eligible.Count == 0 ? "\n" + L("None meet this recipe's requirement", "没有物品达到本配方要求") : "") +
                        (detail == null ? "" : "\n" + detail), requirement: i, belowRequirement: !ingredient.Matches(display));
            }
            else
            {
                bool hover = rect.Contains(Event.current.mousePosition) && viewport.Contains(Event.current.mousePosition + offset);
                NeiSkin.Slot(rect, hover);
                NeiSkin.Label(rect, "?", TextAnchor.MiddleCenter);
                if (hover) Tip(required, L("No matching items in the current catalog", "当前目录中没有匹配物品"));
            }
        }
        if (slot == 0) NeiSkin.Label(new Rect(4, y, viewport.width - 26, height), "—", muted: true);
        return y + Rows(slot, cols) * height + 7;
    }

    private string RequirementTitle(RecipeIngredient ingredient)
    {
        if (!string.IsNullOrWhiteSpace(ingredient.DisplayName)) return ingredient.DisplayName;
        if (!string.IsNullOrWhiteSpace(ingredient.RequiredTag)) return ingredient.RequiredTag;
        if (!string.IsNullOrWhiteSpace(ingredient.QualityId))
        {
            var key = "cq" + ingredient.QualityId;
            if (Locale.currentLang?.other is { } terms && terms.TryGetValue(key, out var name)) return name;
            return ingredient.QualityId;
        }
        return L("Any matching item", "任意匹配物品");
    }

    private void UpdateRequirements(Recipe recipe)
    {
        int revision = _catalog.Revision;
        if (!ReferenceEquals(_requirementRecipe, recipe) || revision != _requirementRevision)
        {
            // A catalog refresh may recreate the same recipe. Keep its open row, but refresh the contents.
            if (_requirementRecipe == null || RecipeKey(_requirementRecipe) != RecipeKey(recipe)) _expandedRequirement = -1;
            _requirementRecipe = recipe;
            _requirementRevision = revision;
            _alternatives.Clear();
            _nativePreview = null;
            _previewUntil = 0;
        }
        if (Time.unscaledTime < _previewUntil || !recipe.Inputs.Any(input => input.MatchesAny)) return;
        _previewUntil = Time.unscaledTime + 1f;
        if (recipe.NativeRecipe is { } native && PlayerCamera.main && PlayerCamera.main.body)
        {
            try { _nativePreview = native.GetItemsForRecipeThorough(); }
            catch (Exception) { _nativePreview = null; }
        }
    }

    private (IReadOnlyList<CatalogItem> Related, IReadOnlyList<CatalogItem> Eligible) Alternatives(int index, RecipeIngredient ingredient)
    {
        if (!_alternatives.TryGetValue(index, out var matches))
        {
            var related = _catalog.GetMatchingItems(ingredient, includeBelowRequirement: true);
            matches = (related, related.Where(ingredient.Matches).ToArray());
            _alternatives.Add(index, matches);
        }
        return matches;
    }

    private CatalogItem? PreferredMatch(int index, RecipeIngredient ingredient, IReadOnlyList<CatalogItem> matches)
    {
        if (_nativePreview != null && index < _nativePreview.Count && _nativePreview[index] is { } native && native)
        {
            if (!ingredient.IsLiquid)
            {
                string key = "item:" + native.id;
                var exact = matches.FirstOrDefault(item => SameId(item.BrowseId, key));
                if (exact != null) return exact;
            }
            else if (native.TryGetComponent<WaterContainerItem>(out var container) && container.stack != null)
            {
                var inputDefinitions = _requirementRecipe?.NativeRecipe?.items;
                var quality = inputDefinitions != null && index < inputDefinitions.Count ? inputDefinitions[index]?.quality : null;
                foreach (var liquid in container.stack)
                {
                    var match = matches.FirstOrDefault(item => SameId(item.Id, liquid.liquidId));
                    if (match != null &&
                        (quality == null || Liquids.Registry.TryGetValue(liquid.liquidId, out var type) &&
                            Item.GetQualityThatMeetsCriteria(quality, type.GetScaledQualities(liquid.amount)) != null)) return match;
                }
            }
        }
        if (ingredient.QualityId.Length > 0 && !ingredient.IsLiquid)
        {
            // Prefer the game's own examples if no eligible instance is currently available.
            foreach (var example in Recipes.QualityExamples)
            {
                if (!SameId(example.Key.id, ingredient.QualityId)) continue;
                var candidate = matches.FirstOrDefault(item => SameId(item.Id, example.Value));
                if (candidate != null) return candidate;
            }
        }
        return matches.FirstOrDefault();
    }

    private static int Rows(int count, int columns) => Math.Max(1, (count + columns - 1) / columns);

    private static Rect IngredientRect(int i, float y, float cell, int cols) =>
        new(4 + i % cols * cell, y + i / cols * cell, cell - 2, cell - 2);
    private static string? Amount(float amount, bool liquid) => amount == 1 && !liquid ? null :
        amount.ToString("0.##", CultureInfo.InvariantCulture) + (liquid ? "mL" : "");

    private void SetCategory(RecipeCategory[] categories, int choice)
    {
        _category = choice == 0 ? null : categories[choice - 1].Id;
        _recipeIndex = 0; _recipeScroll = Vector2.zero;
    }
    private void StepRecipe(int delta, int count) { _recipeIndex = Wrap(_recipeIndex + delta, count); _recipeScroll = Vector2.zero; }
}
