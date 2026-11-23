using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private RecipeMethod[]? _recipeMethods;
    private int _methodRevision = -1;
    private bool _methodChinese;
    private readonly RecipePages _recipePages = new();
    private RecipeMethod? _pagedMethod;
    private float _pageWidth, _pageHeight, _pageIconScale;
    private int _recipeIndex;
    private Vector2 _recipeScroll;
    private int _methodTabPage, _tabPageCapacity;
    private IReadOnlyList<Recipe> _pageRows = Array.Empty<Recipe>();
    private readonly List<(Rect Row, Rect Heart, Recipe Recipe)> _recipeTargets = new();
    private readonly List<RecipeDiagramPlan> _pagePlans = new();

    private void DrawInspector(RecipePanelLayout panel)
    {
        if (panel.TabsPerPage == 0) return;
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button is 1 or 3 && panel.MethodTitle.Contains(e.mousePosition))
        {
            NavigateBack();
            e.Use();
            return;
        }
        NeiSkin.RecipePanel(panel.Frame);
        NeiSkin.RecipeBar(panel.Method);
        NeiSkin.RecipeBar(panel.Pages);
        if (NeiSkin.RecipeTab(panel.Back, ModernityFrames.Caption("back"), tooltip: L("Back · Backspace", "返回 · 退格键")))
        {
            NavigateBack();
            return;
        }
        if (_propertyQuery is { } property)
        {
            DrawPropertyResults(panel, property);
            return;
        }
        EnsureDiagramRevision();
        var methods = GetRecipeMethods();

        int jump = -1;
        int ingredientToExpand = _selectedRecipeIngredient;
        _selectedRecipeIngredient = -1;
        int current = RecipeMethods.SelectedIndex(methods, _method);
        if (_selectedRecipeBookmark is { } bookmark)
        {
            for (int m = 0; m < methods.Length && jump < 0; m++)
                for (int r = 0; r < methods[m].Recipes.Count; r++)
                    if (RecipeKey(methods[m].Recipes[r]) == bookmark) { current = m; jump = r; break; }
            _selectedRecipeBookmark = null;
        }

        // Keep the method arrows visible like the page arrows, even when there is nothing to switch to.
        int methodDelta = DrawArrowPair(panel.PreviousMethod, panel.NextMethod, RecipeMethods.CanSwitch(methods.Length),
            L("Previous crafting method", "上一种制作方式"), L("Next crafting method", "下一种制作方式"));
        if (methods.Length == 0)
        {
            DrawPageArrows(panel, false);
            NeiSkin.RecipeMethodTitle(panel.MethodTitle, _item is { } subject ? Display(subject) : L("Recipes", "配方"));
            DrawPageTitle(panel, 0, 0);
            DrawNoRecipe(panel.Content);
            return;
        }

        int selected = RecipeMethods.Cycle(current, methodDelta, methods.Length);
        selected = DrawMethodTabs(panel, methods, selected, jump >= 0 || methodDelta != 0);
        if (selected != current) jump = -1;
        RecipeMethod method = methods[selected];
        if (!SameId(method.Id, _method))
        {
            _method = method.Id;
            ResetRecipePosition();
        }
        NeiSkin.RecipeMethodTitle(panel.MethodTitle, method.Name);
        if (panel.MethodTitle.Contains(e.mousePosition))
            Tip(method.Name, L("Right-click to go back", "右键返回"));

        float width = RecipePanelLayout.DiagramArea(panel.Content, 0).width;
        bool rowsChanged = !ReferenceEquals(_pagedMethod, method);
        if (rowsChanged) _pageRows = method.Recipes;
        if (rowsChanged || _pageWidth != width || _pageHeight != panel.Content.height || _pageIconScale != IconScale)
        {
            _recipePages.Build(_pageRows, recipe => RecipeRowHeight(recipe, width), panel.Content.height);
            _pagedMethod = method;
            _pageWidth = width;
            _pageHeight = panel.Content.height;
            _pageIconScale = IconScale;
        }
        int page = _recipePages.PageOf(jump >= 0 ? jump : _recipeIndex);
        int delta = DrawPageArrows(panel, _recipePages.Count > 1);
        if (_recipePages.Count == 0)
        {
            DrawPageTitle(panel, 0, 0);
            NeiSkin.Label(panel.Content, L("No recipes available", "没有可用配方"), TextAnchor.MiddleCenter, muted: true);
            return;
        }
        DrawRecipeCatalyst(panel.Catalyst, method);
        if (!_open || !SameId(method.Id, _method)) return; // the rail opens another lookup on mouse-down
        if (delta != 0) page = NavigateRecipePage(page, delta);
        else if (jump >= 0)
        {
            _recipeIndex = jump;
            var recipe = _pageRows[jump];
            if (ingredientToExpand >= 0 && ingredientToExpand < recipe.Inputs.Count && recipe.Inputs[ingredientToExpand].MatchesAny)
                _expandedRequirement = RequirementToken(recipe, ingredientToExpand);
        }
        float height = PrepareRecipePage(panel.Content, page, jump >= 0 && delta == 0 && ingredientToExpand >= 0 ? jump : -1);
        // Candidate popups handle input before the inspector. Resolve the favorite
        // button ahead of paging and Unity's scroll view using the same clipped rect as its tooltip.
        if (e.type == EventType.ScrollWheel && e.delta.y != 0 && !HandleRecipeFavoriteWheel(panel.Content, page, e))
        {
            delta = panel.PageWheelDirection(e.mousePosition, e.delta.y, _recipePages.Count, height);
            if (delta != 0)
            {
                e.Use();
                page = NavigateRecipePage(page, delta);
                height = PrepareRecipePage(panel.Content, page, -1);
            }
        }
        DrawPageTitle(panel, page + 1, _recipePages.Count);
        DrawRecipePage(panel.Content, page, height);
    }

    private void DrawNoRecipe(Rect viewport)
    {
        const float labelHeight = 22f, gap = 4f;
        const float action = RecipeGridLayout.ActionSize, actionWidth = action + 3f;
        float labelY = viewport.center.y - labelHeight * .5f;
        if (_item is { } item && _catalog.TryGetItem(item, out _))
        {
            float size = Mathf.Min(36f, viewport.width - actionWidth, viewport.height - labelHeight - gap);
            if (size >= 22f)
            {
                var slot = new Rect(viewport.center.x - (size + actionWidth) * .5f,
                    viewport.center.y - (size + gap + labelHeight) * .5f, size, size);
                ItemSlot(slot, item, diagram: true);
                var favorite = new Rect(slot.xMax + 3f, slot.center.y - action * .5f, action, action);
                bool selected = _book.IsFavoriteMaterial(item);
                bool hover = favorite.Contains(Event.current.mousePosition);
                if (selected || hover) NeiSkin.FavoriteMark(slot, selected, hover);
                if (DrawFavorite(favorite, item, selected, hover, L("Favorite material", "收藏为首选原料")) &&
                    _book.ToggleFavoriteMaterial(item)) SaveBookmarks();
                labelY = slot.yMax + gap;
            }
        }
        NeiSkin.Label(new Rect(viewport.x, labelY, viewport.width, labelHeight), L("No matching recipes", "找不到匹配的配方"), TextAnchor.MiddleCenter, true);
    }

    private bool DrawFavorite(Rect button, string product, bool selected, bool hovered, string hint)
    {
        bool clicked = NeiSkin.IconButton(button, "favorite", string.Empty, selected: selected, backed: true, maxIconSize: 10f);
        if (hovered) Tip(Display(product), hint);
        return clicked;
    }

    private void ResetRecipeResults()
    {
        _favoriteProductRecipe = _favoriteProductChoice = null;
        _recipeMethods = null;
        _pagedMethod = null;
        _pageRows = Array.Empty<Recipe>();
        _methodTabPage = _tabPageCapacity = 0;
        ResetRecipePosition();
    }

    private void ResetRecipePosition()
    {
        _recipeIndex = 0;
        _recipeTargets.Clear();
        _recipeScroll = Vector2.zero;
        CollapseRecipeRequirement();
        ClearSupportedItemsTip();
    }

    private int NavigateRecipePage(int page, int direction)
    {
        int nextPage = Wrap(page + direction, _recipePages.Count);
        ResetRecipePosition();
        _recipeIndex = _recipePages.Range(nextPage).First;
        return nextPage;
    }

    private RecipeMethod[] GetRecipeMethods()
    {
        int revision = _catalog.Revision;
        bool chinese = _plugin.Chinese;
        if (_recipeMethods != null && _methodRevision == revision && _methodChinese == chinese) return _recipeMethods;
        _methodChinese = chinese;
        _methodRevision = revision;
        IEnumerable<Recipe> matches = _item == null ? _catalog.Recipes : _usage ? _catalog.GetRecipesUsing(_item) : _catalog.GetRecipesProducing(_item);
        if (_usage && _lookupContents.Length > 0)
            matches = matches.Concat(_lookupContents.SelectMany(_catalog.GetRecipesUsing)).Distinct();
        return _recipeMethods = RecipeMethods.Group(matches, _catalog.RecipeCategories, chinese);
    }

    private int DrawPageArrows(RecipePanelLayout panel, bool enabled, bool property = false) => DrawArrowPair(
        panel.PreviousPage, panel.NextPage, enabled,
        property ? L("Previous property result page", "上一页性质结果") : L("Previous recipe page", "上一页配方"),
        property ? L("Next property result page", "下一页性质结果") : L("Next recipe page", "下一页配方"));

    private int DrawArrowPair(Rect left, Rect right, bool enabled, string previousTip, string nextTip)
    {
        bool previous = NeiSkin.IconButton(left, "previous", previousTip, enabled);
        bool next = NeiSkin.IconButton(right, "next", nextTip, enabled);
        var pointer = Event.current.mousePosition;
        if (left.Contains(pointer)) Tip(previousTip, null, boxed: true);
        else if (right.Contains(pointer)) Tip(nextTip, null, boxed: true);
        return previous ? -1 : next ? 1 : 0;
    }

    private int DrawMethodTabs(RecipePanelLayout panel, RecipeMethod[] methods, int selected, bool reveal)
    {
        int perPage = panel.TabsPerPage;
        int pageCount = panel.TabPageCount(methods.Length);
        if (reveal || _tabPageCapacity != perPage || !SameId(methods[selected].Id, _method))
            _methodTabPage = selected / perPage;
        _tabPageCapacity = perPage;
        _methodTabPage = Math.Clamp(_methodTabPage, 0, pageCount - 1);

        if (pageCount > 1)
        {
            int delta = DrawArrowPair(panel.PreviousTabs, panel.NextTabs, true,
                L("Previous method tabs", "上一页合成方式页签"), L("Next method tabs", "下一页合成方式页签"));
            _methodTabPage = Wrap(_methodTabPage + delta, pageCount);
        }
        int start = _methodTabPage * perPage;
        for (int i = start; i < Math.Min(start + perPage, methods.Length); i++)
        {
            Rect rect = panel.Tab(i - start);
            RecipeMethod candidate = methods[i];
            if (NeiSkin.RecipeTab(rect, string.Empty, selected: i == selected, tooltip: candidate.Name)) selected = i;
            if (Event.current.type != EventType.Repaint) continue;
            var inner = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
            CatalogItem? machine = null;
            if (candidate.MachineId.Length > 0) _catalog.TryGetItem(Catalog.ItemKey(candidate.MachineId, false), out machine);
            Sprite? icon = machine?.Icon;
            if (!icon) icon = candidate.Icon;
            if (!icon && candidate.Id == RecipeMethods.CraftingId) icon = NativeCraftingButtonSprite();
            if (icon) DrawSprite(inner, icon, machine?.IconTint ?? Color.white, null);
            else NeiSkin.Label(rect, candidate.Name.Substring(0, Math.Min(2, candidate.Name.Length)), TextAnchor.MiddleCenter);
        }
        return selected;
    }

    // Recipe rows are no longer filtered in the recipe inspector; the page index is
    // always an index in the current method's complete recipe list.
    private int UnfilteredRecipeIndex() => _recipeIndex;

    private void DrawPageTitle(RecipePanelLayout panel, int page, int count) => NeiSkin.Label(
        panel.PageTitle, L($"Page {page}/{count}", $"第 {page}/{count} 页"), TextAnchor.MiddleCenter);

    private void DrawRecipeCatalyst(Rect slot, RecipeMethod method)
    {
        if (method.MachineId.Length == 0) return;
        string id = Catalog.ItemKey(method.MachineId, false);
        ItemSlot(slot, id, fallback: Display(id), diagram: true);
    }

    private float RecipeRowHeight(Recipe recipe, float width) =>
        new RecipeGridLayout(new Rect(0, 0, width, 0), recipe.Inputs.Count, recipe.Outputs.Count, IconScale).Plate.height
        + RecipeDiagramBottom;

    private float PrepareRecipePage(Rect viewport, int page, int revealRecipe)
    {
        var (first, end) = _recipePages.Range(page);
        float height = 0;
        _pagePlans.Clear();
        for (int i = first; i < end; i++)
        {
            if (i == revealRecipe) _recipeScroll.y = height;
            _pagePlans.Add(BuildRecipeDiagramPlan(_pageRows[i], RecipePanelLayout.DiagramArea(viewport, height)));
            height += _recipePages.Height(i) + _pagePlans[^1].ExtraHeight;
        }
        _recipeScroll.y = Mathf.Clamp(_recipeScroll.y, 0, Mathf.Max(0, height - viewport.height));
        return height;
    }

    private void DrawRecipePage(Rect viewport, int page, float height)
    {
        var (first, end) = _recipePages.Range(page);
        float scrollWidth = viewport.width - RecipePanelLayout.ScrollbarAllowance;
        _recipeScroll = GUI.BeginScrollView(viewport, _recipeScroll, new Rect(0, 0, scrollWidth, height), false, height > viewport.height);
        try
        {
            Vector2 offset = viewport.position - _recipeScroll;
            for (int i = first; i < end; i++)
            {
                var plan = _pagePlans[i - first];
                Rect area = RecipePanelLayout.DiagramArea(viewport, plan.Grid.Plate.y);
                var row = new Rect(area.x, area.y, area.width, _recipePages.Height(i) + plan.ExtraHeight);
                if (RecipeListLayout.VisibleHit(row, viewport, offset).height <= 0) continue;
                Recipe recipe = _pageRows[i];
                DrawRecipeDiagram(area, plan, recipe, viewport, offset);
                if (_pagedMethod == null) break; // A slot click can open a new lookup on MouseDown.
                DrawRecipeActions(row, plan.Grid, recipe, viewport, offset);
                if (_pagedMethod == null) break; // The native crafting button can close the inspector.
            }
        }
        finally { GUI.EndScrollView(); }
    }

    private void DrawRecipeActions(Rect row, RecipeGridLayout grid, Recipe recipe, Rect viewport, Vector2 offset)
    {
        Rect heart = recipe.Outputs.Count > 0 ? grid.Favorite : default;
        var pointer = Event.current.mousePosition + offset;
        var heartHit = RecipeListLayout.VisibleHit(heart, viewport, offset);
        bool hover = heartHit.Contains(pointer);
        string? product = FavoriteProduct(recipe);
        if (Event.current.type == EventType.Repaint)
        {
            _recipeTargets.Add((RecipeListLayout.VisibleHit(row, viewport, offset), heartHit, recipe));
            var clip = new Rect(viewport.position - offset, viewport.size);
            var (first, end) = grid.VisibleOutputs(clip, recipe.Outputs.Count);
            for (int i = first; i < Math.Min(end, recipe.Outputs.Count); i++)
            {
                string output = recipe.Outputs[i].BrowseId;
                bool preview = hover && SameId(output, product);
                bool selected = ReferenceEquals(FavoriteForProduct(output), recipe);
                if (preview || selected) NeiSkin.FavoriteMark(grid.Output(i), selected, preview);
            }
        }
        if (product is not null)
        {
            bool favorite = ReferenceEquals(FavoriteForProduct(product), recipe);
            if (DrawFavorite(heart, product, favorite, hover,
                L($"Click: favorite · Wheel: switch output · Shift+{_plugin.BookmarkKey}: order",
                    $"点击：收藏 · 滚轮：切换产物 · Shift＋{_plugin.BookmarkKey}：下单"))) ToggleFavorite(product, RecipeKey(recipe));
        }
        // The host has no ingredient-fill API on this surface. Its native crafting opener is the transfer equivalent.
        Rect add = grid.Transfer;
        var native = recipe.NativeRecipe;
        bool enabled = native is not null && _plugin.CanOpenCrafting(native);
        if (NeiSkin.IconButton(add, "add", L("Open in game's crafting menu", "在游戏制作菜单中打开"), enabled) && native is not null && _plugin.OpenCrafting(native))
            CloseRecipe();
        if (!enabled && RecipeListLayout.VisibleHit(add, viewport, offset).Contains(pointer))
            Tip(L("Not available in this crafting menu", "当前无法在游戏制作菜单中打开"), null);
    }

    private string? _favoriteProductRecipe, _favoriteProductChoice;

    private string? FavoriteProduct(Recipe recipe)
    {
        string? selected = SameId(_favoriteProductRecipe, RecipeKey(recipe)) ? _favoriteProductChoice : _item;
        return recipe.Outputs.FirstOrDefault(output => SameId(output.BrowseId, selected))?.BrowseId ?? recipe.Outputs.FirstOrDefault()?.BrowseId;
    }

    private bool HandleRecipeFavoriteWheel(Rect viewport, int page, Event e)
    {
        if (!viewport.Contains(e.mousePosition)) return false;
        Vector2 offset = viewport.position - _recipeScroll;
        var (first, end) = _recipePages.Range(page);
        for (int i = first; i < end; i++)
        {
            Recipe recipe = _pageRows[i];
            Rect heart = _pagePlans[i - first].Grid.Favorite;
            if (recipe.Outputs.Count < 2 || !RecipeListLayout.VisibleHit(heart, viewport, offset).Contains(e.mousePosition)) continue;
            var products = recipe.Outputs.Select(output => output.BrowseId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (products.Count < 2) return false;
            string? selected = FavoriteProduct(recipe);
            _favoriteProductRecipe = RecipeKey(recipe);
            _favoriteProductChoice = products[Wrap(products.FindIndex(product => SameId(product, selected)) + Math.Sign(e.delta.y), products.Count)];
            e.Use();
            return true;
        }
        return false;
    }

    private static Sprite? NativeCraftingButtonSprite()
    {
        var camera = PlayerCamera.main;
        var button = camera ? camera.craftButton : null;
        if (button == null) return null;
        // The stock button may be a wrapper around an Image. Read it without touching the native UI.
        var image = button.GetComponentInChildren<UnityEngine.UI.Image>(includeInactive: true);
        return image == null ? null : image.overrideSprite ? image.overrideSprite : image.sprite;
    }

    private string Display(string id) => _catalog.TryGetItem(id, out var item) ? item!.DisplayName : id;
    private static string RecipeKey(Recipe recipe) => "recipe/" + recipe.Source + "/" + recipe.Id;
    private static bool SameId(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

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

    private static int Rows(int count, int columns) => Math.Max(1, (count + columns - 1) / columns);
    private static SlotQuantity? Amount(double amount, bool liquid) => amount == 1 && !liquid ? null : new SlotQuantity(amount, liquid);
}
