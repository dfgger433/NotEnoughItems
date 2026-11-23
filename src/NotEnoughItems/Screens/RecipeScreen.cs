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
    private readonly Dictionary<string, string> _categoryMachines = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Rect Row, Rect Heart, Recipe Recipe)> _recipeTargets = new();
    private readonly List<RecipeDiagramPlan> _pagePlans = new();

    private void DrawInspector(RecipePanelLayout panel)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 3 && panel.MethodTitle.Contains(e.mousePosition) && panel.Bounds.Contains(e.mousePosition))
        {
            Back();
            e.Use();
            return;
        }
        NeiSkin.RecipePanel(panel.Frame);
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
        NeiSkin.RecipeBar(panel.Method);
        NeiSkin.RecipeBar(panel.Pages);

        const float nav = RecipePanelLayout.RowNavWidth;
        var pageLabel = RecipePanelLayout.RowTitle(panel.Pages);

        int jump = -1;
        int ingredientToExpand = _selectedRecipeIngredient;
        _selectedRecipeIngredient = -1;
        int current = Array.FindIndex(methods, method => SameId(method.Id, _method));
        if (_selectedRecipeBookmark is { } bookmark)
        {
            for (int m = 0; m < methods.Length && jump < 0; m++)
                for (int r = 0; r < methods[m].Recipes.Count; r++)
                    if (RecipeKey(methods[m].Recipes[r]) == bookmark) { current = m; jump = r; break; }
            _selectedRecipeBookmark = null;
        }

        if (methods.Length == 0)
        {
            DrawMethodArrows(panel, false, L("crafting method", "种合成方式"));
            DrawRecipeArrows(panel.Pages, nav, false, L("recipe page", "配方页"));
            NeiSkin.RecipeMethodTitle(panel.MethodTitle, _item is { } subject ? Display(subject) : L("Recipes", "配方"));
            NeiSkin.Label(pageLabel, "0 / 0", TextAnchor.MiddleCenter);
            DrawNoRecipe(panel.Content);
            return;
        }

        current = DrawMethodTabs(panel, methods, Math.Max(0, current));
        RecipeMethod method = methods[current];
        if (!SameId(method.Id, _method))
        {
            _method = method.Id;
            ResetRecipePosition();
        }
        DrawRecipeCatalyst(panel.Catalyst, method);
        if (!_open || !SameId(method.Id, _method)) return; // the rail opens another lookup on mouse-down

        float width = RecipePanelLayout.DiagramArea(panel.Content, 0).width;
        if (!ReferenceEquals(_pagedMethod, method) || _pageWidth != width || _pageHeight != panel.Content.height || _pageIconScale != IconScale)
        {
            _recipePages.Build(method.Recipes, recipe => RecipeRowHeight(recipe, width), panel.Content.height);
            _pagedMethod = method;
            _pageWidth = width;
            _pageHeight = panel.Content.height;
            _pageIconScale = IconScale;
        }
        int page = _recipePages.PageOf(jump >= 0 ? jump : _recipeIndex);
        int delta = DrawRecipeArrows(panel.Pages, nav, _recipePages.Count > 1, L("recipe page", "配方页"));
        if (delta != 0) page = NavigateRecipePage(page, delta);
        else if (jump >= 0)
        {
            _recipeIndex = jump;
            var recipe = method.Recipes[jump];
            if (ingredientToExpand >= 0 && ingredientToExpand < recipe.Inputs.Count && recipe.Inputs[ingredientToExpand].MatchesAny)
                _expandedRequirement = RequirementToken(recipe, ingredientToExpand);
        }
        float height = PrepareRecipePage(panel.Content, method, page, jump >= 0 && delta == 0 && ingredientToExpand >= 0 ? jump : -1);
        // Candidate popups handle input before the inspector. Resolve the favorite
        // button ahead of paging and Unity's scroll view using the same clipped rect as its tooltip.
        if (e.type == EventType.ScrollWheel && e.delta.y != 0 && !HandleRecipeFavoriteWheel(panel.Content, method, page, e))
        {
            delta = panel.PageWheelDirection(e.mousePosition, e.delta.y, _recipePages.Count, height);
            if (delta != 0)
            {
                e.Use();
                page = NavigateRecipePage(page, delta);
                height = PrepareRecipePage(panel.Content, method, page, -1);
            }
        }
        NeiSkin.Label(pageLabel, L($"Page {page + 1} / {_recipePages.Count}", $"第 {page + 1} / {_recipePages.Count} 页"), TextAnchor.MiddleCenter);
        DrawRecipePage(panel.Content, method, page, height);
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
        var categories = _catalog.RecipeCategories;
        if (_methodRevision != revision)
        {
            _categoryMachines.Clear();
            foreach (var category in categories) _categoryMachines[category.Id] = category.MachineId;
            ClearSupportedItemsTip();
        }
        _methodRevision = revision;
        IEnumerable<Recipe> matches = _item == null ? _catalog.Recipes : _usage ? _catalog.GetRecipesUsing(_item) : _catalog.GetRecipesProducing(_item);
        if (_usage && _lookupContents.Length > 0)
            matches = matches.Concat(_lookupContents.SelectMany(_catalog.GetRecipesUsing)).Distinct();
        // The host's ingredient list is not a Minecraft-style ordered crafting grid.
        return _recipeMethods = RecipeMethods.Group(matches, categories, L("Crafting", "制作栏"));
    }

    private int DrawRecipeArrows(Rect row, float size, bool enabled, string description, bool methodSwitch = false)
    {
        var left = new Rect(row.x, row.y, size, row.height);
        var right = new Rect(row.xMax - size, row.y, size, row.height);
        bool previous = Arrow(left, "previous", L($"Previous {description}", $"上一{description}"));
        bool next = Arrow(right, "next", L($"Next {description}", $"下一{description}"));
        return previous ? -1 : next ? 1 : 0;

        bool Arrow(Rect rect, string icon, string tooltip) => methodSwitch
            ? NeiSkin.RecipeTab(rect, ModernityFrames.Caption(icon), enabled: enabled, tooltip: tooltip)
            : NeiSkin.IconButton(rect, icon, tooltip, enabled);
    }

    private int DrawMethodTabs(RecipePanelLayout panel, RecipeMethod[] methods, int selected)
    {
        int delta = DrawMethodArrows(panel, methods.Length > 1, L("crafting method", "种合成方式"));
        if (delta != 0) selected = Wrap(selected + delta, methods.Length);
        NeiSkin.RecipeMethodTitle(panel.MethodTitle, methods[selected].Name);
        if (panel.MethodTitle.Contains(Event.current.mousePosition))
            Tip(methods[selected].Name, L("Right-click to go back", "右键返回"));
        return selected;
    }

    private int DrawMethodArrows(RecipePanelLayout panel, bool enabled, string description)
    {
        bool previous = NeiSkin.IconButton(panel.PreviousMethod, "previous",
            L($"Previous {description}", $"上一{description}"), enabled);
        bool next = NeiSkin.IconButton(panel.NextMethod, "next",
            L($"Next {description}", $"下一{description}"), enabled);
        return previous ? -1 : next ? 1 : 0;
    }

    private void DrawRecipeCatalyst(Rect slot, RecipeMethod method)
    {
        string machine = RecipeMachine(method.Recipes[0]);
        if (machine.Length == 0) return;
        ItemSlot(slot, Catalog.ItemKey(machine, false), fallback: Display(Catalog.ItemKey(machine, false)), diagram: true);
    }

    private float RecipeRowHeight(Recipe recipe, float width) =>
        new RecipeGridLayout(new Rect(0, 0, width, 0), recipe.Inputs.Count, recipe.Outputs.Count, IconScale).Plate.height
        + RecipeDiagramBottom;

    private float PrepareRecipePage(Rect viewport, RecipeMethod method, int page, int revealRecipe)
    {
        var (first, end) = _recipePages.Range(page);
        float height = 0;
        _pagePlans.Clear();
        for (int i = first; i < end; i++)
        {
            if (i == revealRecipe) _recipeScroll.y = height;
            _pagePlans.Add(BuildRecipeDiagramPlan(method.Recipes[i], RecipePanelLayout.DiagramArea(viewport, height)));
            height += _recipePages.Height(i) + _pagePlans[^1].ExtraHeight;
        }
        _recipeScroll.y = Mathf.Clamp(_recipeScroll.y, 0, Mathf.Max(0, height - viewport.height));
        return height;
    }

    private void DrawRecipePage(Rect viewport, RecipeMethod method, int page, float height)
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
                DrawRecipeDiagram(area, plan, method.Recipes[i], viewport, offset);
                DrawRecipeActions(row, plan.Grid, method.Recipes[i], viewport, offset);
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

    private bool HandleRecipeFavoriteWheel(Rect viewport, RecipeMethod method, int page, Event e)
    {
        if (!viewport.Contains(e.mousePosition)) return false;
        Vector2 offset = viewport.position - _recipeScroll;
        var (first, end) = _recipePages.Range(page);
        for (int i = first; i < end; i++)
        {
            Recipe recipe = method.Recipes[i];
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

    private string RecipeMachine(Recipe recipe) => !string.IsNullOrWhiteSpace(recipe.MachineId) ? recipe.MachineId.Trim() :
        _categoryMachines.TryGetValue(recipe.CategoryId, out var machine) ? machine.Trim() : string.Empty;

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
