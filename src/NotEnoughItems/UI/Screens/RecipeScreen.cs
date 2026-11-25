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
    private bool _previewMethodTabs, _previewMethodChinese;
    private RecipeMethod[]? _previewMethods;
    private string? _previewMethod;
    private readonly RecipePages _recipePages = new();
    private RecipeMethod? _pagedMethod;
    private float _pageWidth, _pageHeight, _pageIconScale;
    private int _recipeIndex;
    private Vector2 _recipeScroll;
    private readonly RecipeMethodTabPager _methodTabs = new();
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
        if (NeiSkin.IconButton(panel.Back, "back", L("recipe.back"), maxIconSize: 12f))
        {
            NavigateBack();
            return;
        }
        if (_propertyQuery is { } property)
        {
            DrawMethodNavigation(panel, Array.Empty<RecipeMethod>(), 0);
            DrawPropertyResults(panel, property);
            return;
        }
        if (_previewMethodTabs)
        {
            DrawMethodPreview(panel);
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

        if (methods.Length == 0)
        {
            DrawMethodNavigation(panel, methods, 0);
            DrawPageArrows(panel, false);
            NeiSkin.RecipeMethodTitle(panel.MethodTitle, _item is { } subject ? Display(subject) : L("recipe.recipes"));
            DrawPageTitle(panel, 0, 0);
            DrawNoRecipe(panel.Content);
            return;
        }

        int selected = DrawMethodNavigation(panel, methods, current, reveal: jump >= 0);
        NeiSkin.RecipeMethodTitle(panel.MethodTitle, methods[selected].Name);
        if (selected != current) jump = -1;
        RecipeMethod method = methods[selected];
        if (!SameId(method.Id, _method))
        {
            _method = method.Id;
            ResetRecipePosition();
        }
        if (panel.MethodTitle.Contains(e.mousePosition))
            Tip(method.Name, L("recipe.back_hint"));

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
            NeiSkin.Label(panel.Content, L("recipe.no_recipes_available"), TextAnchor.MiddleCenter, muted: true);
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
                if (DrawFavorite(favorite, item, selected, hover, L("recipe.favorite_material")) &&
                    _book.ToggleFavoriteMaterial(item)) SaveBookmarks();
                labelY = slot.yMax + gap;
            }
        }
        NeiSkin.Label(new Rect(viewport.x, labelY, viewport.width, labelHeight), L("recipe.no_matching_recipes"), TextAnchor.MiddleCenter, true);
    }

    private bool DrawFavorite(Rect button, string product, bool selected, bool hovered, string hint, Rect? inputClip = null)
    {
        bool clicked = NeiSkin.IconButton(button, "favorite", string.Empty, selected: selected, backed: true, maxIconSize: 10f, inputClip: inputClip);
        if (hovered) Tip(Display(product), hint);
        return clicked;
    }

    private void ResetRecipeResults()
    {
        _favoriteProductRecipe = _favoriteProductChoice = null;
        _recipeMethods = null;
        _pagedMethod = null;
        _pageRows = Array.Empty<Recipe>();
        _methodTabs.Reset();
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
        property ? L("recipe.previous_property_page") : L("recipe.previous_page"),
        property ? L("recipe.next_property_page") : L("recipe.next_page"));

    private int DrawArrowPair(Rect left, Rect right, bool enabled, string previousTip, string nextTip)
    {
        bool previous = NeiSkin.IconButton(left, "previous", previousTip, enabled);
        bool next = NeiSkin.IconButton(right, "next", nextTip, enabled);
        ShowArrowTip(left, right, previousTip, nextTip);
        return previous ? -1 : next ? 1 : 0;
    }

    private void ShowArrowTip(Rect left, Rect right, string previousTip, string nextTip)
    {
        var pointer = Event.current.mousePosition;
        if (left.Contains(pointer)) Tip(previousTip);
        else if (right.Contains(pointer)) Tip(nextTip);
    }

    private void DrawMethodPreview(RecipePanelLayout panel)
    {
        bool chinese = _plugin.Chinese;
        if (_previewMethods == null || _previewMethods.Length != panel.TabsPerPage * 2 || _previewMethodChinese != chinese)
        {
            _previewMethods = RecipeMethods.PreviewTabs(panel.TabsPerPage, chinese);
            _previewMethodChinese = chinese;
        }
        var methods = _previewMethods;
        int selected = DrawMethodNavigation(panel, methods, RecipeMethods.SelectedIndex(methods, _previewMethod));
        _previewMethod = methods[selected].Id;
        NeiSkin.RecipeMethodTitle(panel.MethodTitle, methods[selected].Name);
        if (panel.MethodTitle.Contains(Event.current.mousePosition))
            Tip(methods[selected].Name, L("recipe.back_hint"));
        DrawPageArrows(panel, enabled: false);
        DrawPageTitle(panel, 0, 0);
        NeiSkin.Label(panel.Content, L("recipe.preview_empty"), TextAnchor.MiddleCenter, muted: true);
    }

    private int DrawMethodArrows(RecipePanelLayout panel, bool enabled) => DrawArrowPair(
        panel.PreviousMethod, panel.NextMethod, enabled,
        L("recipe.previous_method"), L("recipe.next_method"));

    private int DrawMethodNavigation(RecipePanelLayout panel, RecipeMethod[] methods, int selected, bool reveal = false)
    {
        int methodDelta = DrawMethodArrows(panel, RecipeMethods.CanSwitch(methods.Length));
        selected = RecipeMethods.Cycle(selected, methodDelta, methods.Length);
        _methodTabs.Show(panel, methods, selected, reveal || methodDelta != 0);
        if (methods.Length == 0) return 0;

        int pageCount = panel.TabPageCount(methods.Length);
        var e = Event.current;
        // Page the rail on the press, before drawing the new tab range. No MouseUp or control ID is needed.
        if (e.type == EventType.MouseDown && GUI.enabled && GUIUtility.hotControl == 0 &&
            _methodTabs.TurnOnPress(panel, methods.Length, e.type, e.button, e.mousePosition)) e.Use();

        int start = panel.TabPageStart(_methodTabs.Page);
        int end = panel.TabPageEnd(_methodTabs.Page, methods.Length);
        if (pageCount > 1)
        {
            var (previous, next) = panel.TabArrows(_methodTabs.Page, methods.Length);
            NeiSkin.RecipeTabArrow(previous, "previous");
            NeiSkin.RecipeTabArrow(next, "next");
            ShowArrowTip(previous, next, L("recipe.previous_method_tabs"),
                L("recipe.next_method_tabs"));
        }
        for (int i = start; i < end; i++)
        {
            Rect rect = panel.Tab(i - start);
            RecipeMethod candidate = methods[i];
            if (NeiSkin.RecipeTab(panel, rect, selected: i == selected, tooltip: candidate.Name)) selected = i;
            if (Event.current.type != EventType.Repaint) continue;
            var inner = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
            CatalogItem? machine = null;
            if (candidate.MachineId.Length > 0) _catalog.TryGetItem(Catalog.ItemKey(candidate.MachineId, false), out machine);
            Sprite? icon = machine?.Icon;
            if (!icon) icon = candidate.Icon;
            if (!icon && candidate.Id == RecipeMethods.CraftingId) icon = NativeCraftingButtonSprite();
            if (!TryDrawSprite(inner, icon, machine?.IconTint ?? Color.white))
                NeiSkin.RecipeTabLabel(rect, candidate.TabLabel);
        }
        return selected;
    }

    private void DrawPageTitle(RecipePanelLayout panel, int page, int count) =>
        NeiSkin.Label(panel.PageTitle, L("recipe.page", page, count), TextAnchor.MiddleCenter);

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
        _recipeScroll = NeiSkin.BeginScrollView(viewport, _recipeScroll, new Rect(0, 0, scrollWidth, height),
            horizontal: false, vertical: height > viewport.height, recipe: true);
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
        var clip = new Rect(viewport.position - offset, viewport.size);
        if (Event.current.type == EventType.Repaint)
        {
            _recipeTargets.Add((RecipeListLayout.VisibleHit(row, viewport, offset), heartHit, recipe));
            var (first, end) = grid.VisibleOutputs(clip, recipe.Outputs.Count);
            for (int i = first; i < Math.Min(end, recipe.Outputs.Count); i++)
            {
                string output = recipe.Outputs[i].BrowseId;
                bool preview = hover && SameId(output, product);
                bool selected = ReferenceEquals(FavoriteForProduct(output), recipe);
                if (preview || selected) NeiSkin.FavoriteMark(grid.Output(i), selected, preview);
            }
        }
        // Custom IMGUI buttons retain their full visual bounds, but only the visible scroll clip can activate them.
        if (product is not null)
        {
            bool favorite = ReferenceEquals(FavoriteForProduct(product), recipe);
            if (DrawFavorite(heart, product, favorite, hover,
                L("recipe.favorite_hint", _plugin.BookmarkKey), clip)) ToggleFavorite(product, RecipeKey(recipe));
        }
        // The host has no ingredient-fill API on this surface. Its native crafting opener is the transfer equivalent.
        Rect add = grid.Transfer;
        var native = recipe.NativeRecipe;
        bool enabled = native is not null && _plugin.CanOpenCrafting(native);
        if (NeiSkin.IconButton(add, "add", L("recipe.open_crafting_menu"), enabled, inputClip: clip) &&
            native is not null && _plugin.OpenCrafting(native))
            CloseRecipe();
        if (!enabled && RecipeListLayout.VisibleHit(add, viewport, offset).Contains(pointer))
            Tip(L("recipe.crafting_unavailable"), null);
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
        return L("recipe.any_matching_item");
    }

    private static int Rows(int count, int columns) => Math.Max(1, (count + columns - 1) / columns);
    private static SlotQuantity? Amount(double amount, bool liquid) => amount == 1 && !liquid ? null : new SlotQuantity(amount, liquid);
}
