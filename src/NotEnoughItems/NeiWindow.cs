using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    internal const string SearchControl = "NEI.ItemSearch";
    private const string CatalogSearchControl = "NEI.CatalogSearch";
    internal const string GroupControl = "NEI.BookmarkGroup";
    private readonly Plugin _plugin;
    private readonly Catalog _catalog = Catalog.Shared;
    private readonly ConfigEntry<string> _saved;
    private readonly ConfigEntry<bool> _showHistory, _showTooltipId;
    private readonly ConfigEntry<bool> _showGrid;
    private readonly ConfigEntry<bool> _showInventoryGrid;
    private readonly BookmarkBook _book = new();
    private readonly List<string> _recent = new();
    private readonly Stack<(string? Item, bool Usage, string? Method, int Index, Vector2 Scroll, string[] Contents, Item? LiveItem,
        RecipeIngredient? Property, int PropertyPage)> _back = new();
    private string[] _lookupContents = Array.Empty<string>();
    private readonly List<(Rect Rect, string? Id, RecipeIngredient? Property)> _itemTargets = new();
    private Item? _lookupItem;
    private Rect _left, _right, _catalogControls, _center, _inspector, _optionsBounds, _searchRect, _groupRect;
    private NeiLayout.ToolButtons _leftTools;
    private NeiLayout.CatalogPanel _catalogLayout;
    private bool _bookmarksVisible = true;
    private float _scale = 1f, _optionsZoom = 1f;
    private float _width, _height;
    private string _query = string.Empty, _group = "0", _editGroup = string.Empty;
    private string? _selectedRecipeBookmark;
    private int _selectedRecipeIngredient = -1;
    private int _page, _bookmarkPage;
    private readonly ConfigEntry<int> _spawnAmount;
    private readonly ConfigEntry<int> _bookmarkWidth, _catalogWidth, _panelY, _iconSize;
    private readonly ConfigEntry<int> _bookmarkX, _bookmarkY, _catalogX, _catalogY;
    internal (int Bookmark, int Catalog, int Y, int Icon, bool History, int LeftX, int LeftY, int RightX, int RightY) LayoutOptions =>
        (_bookmarkWidth.Value, _catalogWidth.Value, _panelY.Value, _iconSize.Value, _showHistory.Value,
            _bookmarkX.Value, _bookmarkY.Value, _catalogX.Value, _catalogY.Value);
    internal bool LayoutEditing => _layoutEditing;
    private float IconScale => _iconSize.Value / 100f;
    // This is the default for new bookmarks and larger adjustment steps, not a rewrite of saved quantities.
    private int BookmarkQuantity => _spawnAmount.Value;
    private int _seenRevision = -1;
    private string? _item, _method;
    private bool _usage, _open, _options, _groups, _searchFocused;
    private string? _focusSearch;
    private bool _skipFocusFrame;
    private string? _tipTitle, _tipBody, _status;
    private NativeTooltip.Content? _itemTip;
    private Rect _itemTipSource;
    private bool _boxedTip;
    private float _statusUntil;
    private RecipePanelLayout _recipePanel;
    private Rect ActiveInspectorBounds => _options ? _optionsBounds : !_treeOpen && _open ? _recipePanel.Bounds : _inspector;
    private Rect RecipeNavigationBounds => _open && !_options && !_treeOpen && !_layoutEditing && !_plugin.IsCrafting
        ? _recipePanel.NavigationBounds : default;
    private int _expandedRequirement = -1;
    private PanelLayoutDrag? _layoutDrag;
    private Vector2 _layoutPreviewOffset, _leftOffset, _rightOffset;
    private int _layoutPreviewWidth;
    private bool _layoutEditing;
    private IReadOnlyList<CatalogItem> _filtered = Array.Empty<CatalogItem>();
    private readonly HashSet<string> _catalogCarried = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _catalogNativeFavorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _nextCatalogCarried = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _nextCatalogNativeFavorites = new(StringComparer.OrdinalIgnoreCase);
    private bool _catalogFilterDirty = true;
    private string? _lastError;
    internal bool SearchFocused { get => _searchFocused; set => _searchFocused = value; }
    private bool _returnToTree;
    internal bool ModalOpen => _open || _options || _treeOpen || _layoutEditing;

    internal NeiWindow(Plugin plugin, ConfigFile config)
    {
        _plugin = plugin;
        _saved = config.Bind("Bookmarks", "Entries", string.Empty, "Bookmarked item ids and their page names. Managed by NEI.");
        _showHistory = config.Bind("NotEnoughItems", "HistoryStrip", true, "Show recently inspected items in the catalog.");
        _showTooltipId = BindTooltipId(config);
        _showGrid = config.Bind("NotEnoughItems", "ItemList", true, "Show the right-hand item catalog beside the Inventory Terminal.");
        _showInventoryGrid = config.Bind("NotEnoughItems", "ItemListInInventory", true, "Show the right-hand item catalog beside the native radial inventory.");
        _spawnAmount = Plugin.BindSpawnAmount(config);
        _bookmarkWidth = LayoutOption("BookmarkWidth", 100, 65, 120, "Bookmark panel width percentage.");
        _catalogWidth = LayoutOption("CatalogWidth", 100, 65, 120, "Catalog panel width percentage.");
        _panelY = LayoutOption("VerticalOffset", 0, -6, 5, "Sidebar vertical offset as a percentage of window height.");
        _iconSize = LayoutOption("IconSize", 100, 70, 150, "Item icon grid size percentage, independent of UI zoom.");
        _bookmarkX = OffsetOption("BookmarkX"); _bookmarkY = OffsetOption("BookmarkY");
        _catalogX = OffsetOption("CatalogX"); _catalogY = OffsetOption("CatalogY");
        _book.Load(_saved.Value);

        ConfigEntry<int> OffsetOption(string key) => LayoutOption(key, 0, -1000, 1000, "Drag offset in thousandths of the viewport. Managed by NEI.");

        ConfigEntry<int> LayoutOption(string key, int defaults, int min, int max, string description) =>
            config.Bind("Layout", key, defaults, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
    }

    private string L(string en, string zh) => _plugin.Chinese ? zh : en;
    internal void Position(Rect center, float scale)
    {
        _scale = NeiLayout.GuiScale(Screen.width, Screen.height, scale, _bookmarkWidth.Value, _catalogWidth.Value);
        _width = Screen.width / _scale;
        _height = Screen.height / _scale;
        _center = new Rect(center.position / _scale, center.size / _scale);
        PositionPanels();
    }

    private void PositionPanels()
    {
        var drag = _layoutDrag;
        int leftWidth = drag is { Resize: true, Right: false } ? _layoutPreviewWidth : _bookmarkWidth.Value;
        int rightWidth = drag is { Resize: true, Right: true } ? _layoutPreviewWidth : _catalogWidth.Value;
        var viewport = new Vector2(_width, _height);
        var (bookmarks, catalog, tools, catalogControls) = NeiLayout.Overlay(_width, _height, leftWidth, rightWidth, _panelY.Value);
        var leftUnits = new Vector2(_bookmarkX.Value, _bookmarkY.Value);
        var rightUnits = new Vector2(_catalogX.Value, _catalogY.Value);
        float leftGroupWidth = Mathf.Max(bookmarks.xMax, tools.xMax) - Mathf.Min(bookmarks.xMin, tools.xMin);
        float rightGroupWidth = Mathf.Max(catalog.xMax, catalogControls.xMax) - Mathf.Min(catalog.xMin, catalogControls.xMin);
        _leftOffset = NeiLayout.PanelOffset(bookmarks, tools, viewport, leftUnits, right: false, rightGroupWidth);
        _rightOffset = NeiLayout.PanelOffset(catalog, catalogControls, viewport, rightUnits, right: true, leftGroupWidth);
        if (drag is { Resize: false, Right: false })
            _leftOffset = NeiLayout.ClampPanelOffset(new Vector2(_layoutPreviewOffset.x * viewport.x / 1000f,
                _layoutPreviewOffset.y * viewport.y / 1000f), bookmarks, tools, viewport, right: false, rightGroupWidth);
        if (drag is { Resize: false, Right: true })
            _rightOffset = NeiLayout.ClampPanelOffset(new Vector2(_layoutPreviewOffset.x * viewport.x / 1000f,
                _layoutPreviewOffset.y * viewport.y / 1000f), catalog, catalogControls, viewport, right: true, leftGroupWidth);
        bookmarks = NeiLayout.Translate(bookmarks, _leftOffset);
        _left = _plugin.IsCrafting ? NeiLayout.BesideCrafting(bookmarks, _center) : bookmarks;
        _right = NeiLayout.Translate(catalog, _rightOffset);
        _catalogControls = NeiLayout.Translate(catalogControls, _rightOffset);
        _leftTools = new NeiLayout.ToolButtons(NeiLayout.Translate(tools, _leftOffset));
        _catalogLayout = new NeiLayout.CatalogPanel(_right, _catalogControls, _showHistory.Value);
        _inspector = NeiLayout.InspectorBetween(_center, _left, _right, _width, _height);
        (_optionsBounds, _optionsZoom) = NeiLayout.OptionsBetween(_center, _left, _right, _width, _height);
        _recipePanel = new RecipePanelLayout(Rect.MinMaxRect(_left.xMax + 5, 8, _right.xMin - 5, _height - 8), _width * .5f);
        ClearSupportedItemsTip();
        ClearChainTip();
        ClearItemTooltipInput();
    }

    internal void Hide()
    {
        _left = _right = _catalogControls = _inspector = _optionsBounds = _searchRect = _groupRect = default;
        _leftTools = default;
        _catalogLayout = default;
        _recipePanel = default;
        _itemTargets.Clear();
        _lookupItem = null;
        NativeTooltipPreview.Release();
        ClearItemTooltipInput();
        _tooltipCopyPressed = false;
        ClearSupportedItemsTip();
        ClearChainTip();
        ClearBookmarkHits();
        _bookmarkDrag = null;
        StopLayoutEditing();
    }

    internal void ToggleBookmarks()
    {
        ClearItemTooltipInput();
        _productionContext++;
        _bookmarksVisible = !_bookmarksVisible;
        ClearBookmarkHits();
        _bookmarkDrag = null;
    }
    internal void ResetView()
    {
        _catalogQuantityDraft = null;
        ClearItemTooltipInput();
        ClearChainTip();
        _productionContext++;
        _open = _treeOpen = _returnToTree = _options = _groups = _searchFocused = false;
        _propertyQuery = null;
        _propertyPage = 0;
        StopLayoutEditing();
        _focusSearch = null;
        _pendingBinding = null;
        CloseLanguageMenu();
        _bookmarkDrag = null;
        ResetRecipeResults();
        ClearBookmarkHits();
        _back.Clear();
        _itemTargets.Clear();
        _lookupItem = null;
    }

    private bool HideBookmarksForDrag
    {
        get
        {
            var camera = PlayerCamera.main;
            return camera && camera.dragItem && camera.dragItem.TryGetComponent<WaterContainerItem>(out _);
        }
    }
    private bool ShowLeft => _bookmarksVisible && !HideBookmarksForDrag && _left.width > 0;
    private bool ShowRight => !_plugin.IsCrafting && CatalogSetting.Value;
    private ConfigEntry<bool> CatalogSetting => _plugin.IsNativeInventory ? _showInventoryGrid : _showGrid;

    internal bool Hit(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (ItemTooltipHit(pointer)) return true;
        if (_plugin.IsCrafting) return (ShowLeft && _left.Contains(pointer)) || SupportedItemsHit(pointer);
        return LayoutEditHit(pointer) || ChainTipHit(pointer) || SupportedItemsHit(pointer) || (ModalOpen && ActiveInspectorBounds.Contains(pointer)) || (ShowLeft && _left.Contains(pointer)) ||
            (ShowRight && (_right.Contains(pointer) || _catalogControls.Contains(pointer))) || _searchRect.Contains(pointer) ||
            _leftTools.Hit(pointer, !HideBookmarksForDrag);
    }
    internal bool OptionsHit(Vector2 pointer) => _options && !_plugin.IsCrafting &&
        _optionsBounds.Contains(pointer / Mathf.Max(.01f, _scale));

    internal bool CandidatePopupHit(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        return ChainTipHit(pointer) || SupportedItemsHit(pointer);
    }

    internal bool ItemTooltipPopupHit(Vector2 pointer) => ItemTooltipHit(pointer / Mathf.Max(.01f, _scale));

    internal bool TextInputHit(Vector2 pointer)
    {
        pointer /= _scale;
        return _searchRect.Contains(pointer) || _groupRect.Contains(pointer) ||
            (_recipeSearchOpen && RecipeNavigationBounds.Contains(pointer) && _recipePanel.SearchField.Contains(pointer)) ||
            (ShowRight && (_catalogLayout.Search.Contains(pointer) || _catalogLayout.Quantity.Contains(pointer)));
    }
    internal bool RecipeNavigationHit(Vector2 pointer) => RecipeNavigationBounds.Contains(pointer / Mathf.Max(.01f, _scale));
    internal static bool IsTextControl(string? name) => name is SearchControl or CatalogSearchControl or CatalogQuantityControl or GroupControl or RecipeSearchControl;
    internal string? ItemAt(Vector2 pointer) => ItemTargetAt(pointer).Id;

    private (string? Id, RecipeIngredient? Property) ItemTargetAt(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (ItemTooltipHit(pointer)) return default;
        if (SupportedItemsHit(pointer))
        {
            int index = TooltipPlacement.CandidateAt(_supportedItemsGrid, _supportedItemsColumns, _supportedItemsCell,
                _supportedItemsStart, _supportedItemsEnd, pointer);
            return index < 0 ? default : (_pinnedItems!.Candidates[index].BrowseId, null);
        }
        if (ChainTipHit(pointer)) return ChainTipItemAt(pointer);
        for (int i = _itemTargets.Count - 1; i >= 0; i--)
        {
            var target = _itemTargets[i];
            if (target.Rect.Contains(pointer)) return (target.Id, target.Property);
        }
        return default;
    }
    /// <summary>
    /// Hit-tests only the panels and controls that were actually drawn. The stock
    /// tooltip is untouched outside this union, including while a native drag is live.
    /// </summary>
    internal bool NativeTooltipHit(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (ItemTooltipHit(pointer)) return true;
        if (_plugin.IsCrafting) return (ShowLeft && _left.Contains(pointer)) || SupportedItemsHit(pointer);
        return LayoutEditHit(pointer) || ChainTipHit(pointer) || SupportedItemsHit(pointer) || _leftTools.Hit(pointer, !HideBookmarksForDrag) || NativeTooltip.Hit(pointer,
            (ModalOpen, ActiveInspectorBounds), (ShowLeft, _left), (ShowRight, _right), (ShowRight, _catalogControls),
            (true, _searchRect), (true, _groupRect));
    }

    internal bool RequestSearchFocus()
    {
        if (!_treeOpen && !ShowRight) return false;
        _focusSearch = !_options && _treeOpen ? SearchControl : CatalogSearchControl;
        return true;
    }

    internal void RefreshCatalog(ManualLogSource log)
    {
        try
        {
            RefreshBookmarkStock();
            int revision = _catalog.Revision;
            if (_seenRevision != revision)
            {
                bool first = _seenRevision < 0;
                _seenRevision = revision;
                _preferredMaterials = null;
                _catalogFilterDirty = true;
                InvalidateBookmarkView();
                if (first || _lastError != null)
                    log.LogInfo($"NEI catalog: {_catalog.Items.Count} entries, {_catalog.Recipes.Count} recipes, {_catalog.RecipeCategories.Count} categories.");
            }
            if (ShowRight) RefreshCatalogCarried();
            _lastError = null;
        }
        catch (Exception error)
        {
            if (_lastError != error.Message) log.LogError("NEI could not refresh the recipe catalog: " + error);
            _lastError = error.Message;
        }
    }

    private void RefreshCatalogCarried()
    {
        var camera = PlayerCamera.main;
        _nextCatalogCarried.Clear();
        _nextCatalogNativeFavorites.Clear();
        NativeCraftStock.CollectCarriedTypes(camera ? camera.body : null, _nextCatalogCarried, _nextCatalogNativeFavorites);
        if (_catalogCarried.SetEquals(_nextCatalogCarried) && _catalogNativeFavorites.SetEquals(_nextCatalogNativeFavorites)) return;
        _catalogCarried.Clear();
        _catalogCarried.UnionWith(_nextCatalogCarried);
        _catalogNativeFavorites.Clear();
        _catalogNativeFavorites.UnionWith(_nextCatalogNativeFavorites);
        _catalogFilterDirty = true;
    }

    private void ApplyFilter()
    {
        var favorites = new HashSet<string>(PreferredMaterials, StringComparer.OrdinalIgnoreCase);
        favorites.UnionWith(_catalogNativeFavorites);
        foreach (var entry in _book.Entries)
        {
            if (entry.IsRecipe)
            {
                if (BookmarkedRecipe(entry.Id) is { } recipe && BookmarkProduct(entry, recipe) is { } product)
                    favorites.Add(product.BrowseId);
            }
            else if (_catalog.TryGetItem(entry.Id, out var item)) favorites.Add(item.BrowseId);
        }
        _filtered = CatalogListing.Filter(_catalog.Items, _query, _plugin.Pinyin, favorites, _catalogCarried, NativeTooltip.SourceName);
        _catalogFilterDirty = false;
    }

    internal void ToggleBookmark(string id, int quantity = 1)
    {
        if (_book.Toggle(_group, id, quantity)) SaveBookmarks();
    }

    private void SaveBookmarks()
    {
        _productionContext++;
        _preferredMaterials = null;
        _catalogFilterDirty = true;
        _favoriteCandidates.Clear();
        _saved.Value = _book.Serialize();
        InvalidateBookmarkView();
    }

    internal bool HandleBookmarkShortcut(Vector2 pointer, string? id, bool control, bool shift)
    {
        if (_options) return false;
        pointer /= Mathf.Max(.01f, _scale);
        if (ShowLeft && _left.Contains(pointer))
        {
            if (_bookmarkDrag != null) return false;
            var group = _bookmarkNavigation.Contains(pointer) ? "" : _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
            if (shift && group != null)
            {
                if (_book.RemoveGroup(_group, group)) SaveBookmarks();
                return true;
            }
            var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
            if (cell != null)
            {
                if (_book.IsCollapsed(_group, cell.Entry.Group) && _book.IsChainEnabled(_group, cell.Entry.Group))
                {
                    if (shift && _book.RemoveGroup(_group, cell.Entry.Group)) SaveBookmarks();
                }
                else if (cell.Ingredient >= 0 && !shift)
                    Status(L("Shift+A removes the saved recipe. Its ingredients stay attached to it.", $"Shift＋{_plugin.BookmarkKey} 移除整条配方；原料与配方一起保留。"));
                else if (_book.Remove(cell.Entry)) SaveBookmarks();
                return true;
            }
        }
        var hit = _recipeTargets.Find(target => target.Row.Contains(pointer));
        if (_open && hit.Recipe is { } recipe)
        {
            if (shift && hit.Heart.Contains(pointer))
            {
                BookmarkOrder(recipe, control ? BookmarkQuantity : 1, FavoriteProduct(recipe));
                return true;
            }
            string? product = recipe.Outputs.FirstOrDefault(output => SameId(output.BrowseId, id))?.BrowseId;
            if (shift || product != null)
            {
                product ??= recipe.Outputs.FirstOrDefault()?.BrowseId;
                if (_book.ToggleRecipe(_group, RecipeKey(recipe), control ? BookmarkQuantity : 1, shift, product,
                    showQuantity: control)) SaveBookmarks();
                return true;
            }
        }
        if (id == null) return false;
        if (shift && FavoriteForProduct(id) is { } preferred)
        {
            if (_book.ToggleRecipe(_group, RecipeKey(preferred), control ? BookmarkQuantity : 1, true, id, showQuantity: control)) SaveBookmarks();
        }
        else if (_book.Toggle(_group, id, control ? BookmarkQuantity : 1, string.Empty, control)) SaveBookmarks();
        return true;
    }

    // The host has no NEI chat-link receiver. Share the same scoped, pasteable payload
    // locally rather than sending an unusable Minecraft NBT link to multiplayer chat.
    internal bool ShareBookmark(Vector2 pointer, bool withRecipe)
    {
        pointer /= _scale;
        if (!ShowLeft || !_left.Contains(pointer) || _options) return false;
        string? group = _bookmarkNavigation.Contains(pointer) ? "" : _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
        if (group != null)
        {
            if (_book.Page(_group).Any(entry => entry.Group == group)) CopyBookmark(_book.CopyGroup(_group, group));
            return true;
        }
        var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
        if (cell == null) return false;
        if (withRecipe && cell.Entry.IsRecipe) CopyBookmark(_book.CopyEntry(cell.Entry));
        else
        {
            var single = new BookmarkBook();
            int count = (int)Math.Clamp(Math.Ceiling(cell.Amount), 1, BookmarkBook.MaximumQuantity);
            if (single.Add(_group, cell.Id, count, showQuantity: cell.Entry.ShowQuantity) is { } item) CopyBookmark(single.CopyEntry(item));
        }
        return true;
    }

    internal bool HandleBookmarkClipboard(Vector2 pointer, bool copy)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (!ShowLeft || !_left.Contains(pointer) || _options) return false;
        if (!copy) { PasteBookmarks(); return true; }
        if (_bookmarkNavigation.Contains(pointer)) { CopyBookmark(_book.CopyPage(_group)); return true; }
        var group = _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
        if (!string.IsNullOrEmpty(group)) { CopyBookmark(_book.CopyGroup(_group, group)); return true; }
        var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
        if (cell == null) return false;
        CopyBookmark(_book.CopyEntry(cell.Entry));
        return true;
    }

    internal void Lookup(string id, bool usage, string[]? contents = null, Item? liveItem = null)
    {
        contents ??= Array.Empty<string>();
        if (_open && _item == id && _usage == usage && _lookupContents.SequenceEqual(contents) && _lookupItem == liveItem) return;
        OpenView(id, usage);
        _lookupContents = contents;
        _lookupItem = liveItem;
        if (RecentBrowseId(id) is { } recent)
        {
            _recent.Remove(recent);
            _recent.Insert(0, recent);
            if (_recent.Count > 32) _recent.RemoveAt(_recent.Count - 1);
        }
    }

    private string? RecentBrowseId(string id)
    {
        if (id.StartsWith("recipe/", StringComparison.OrdinalIgnoreCase))
            id = _catalog.Recipes.FirstOrDefault(recipe => RecipeKey(recipe) == id)?.Outputs.FirstOrDefault()?.BrowseId ?? string.Empty;
        return _catalog.TryGetItem(id, out var item) && item.IsCatalogVisible ? item.BrowseId : null;
    }

    private void LookupFromTree(string id) => Lookup(id, false);

    private void OpenView(string? id, bool usage)
    {
        ClearItemTooltipInput();
        ClearChainTip();
        if (id?.StartsWith("recipe/", StringComparison.Ordinal) == true)
        {
            var recipe = _catalog.Recipes.FirstOrDefault(r => RecipeKey(r) == id);
            if (recipe != null)
            {
                OpenView(recipe.Outputs.FirstOrDefault()?.BrowseId, false);
                _selectedRecipeBookmark = id;
                return;
            }
        }
        if (_treeOpen) { _treeOpen = false; _returnToTree = true; _open = false; _back.Clear(); }
        else if (!_open) _returnToTree = false;
        if (_open && _back.Count < 64)
            _back.Push((_item, _usage, _method, UnfilteredRecipeIndex(), _recipeScroll, _lookupContents, _lookupItem, _propertyQuery, _propertyPage));
        _item = id;
        _propertyQuery = null;
        _propertyPage = 0;
        _lookupContents = Array.Empty<string>();
        _lookupItem = null;
        _selectedRecipeBookmark = null;
        _selectedRecipeIngredient = -1;
        _usage = usage;
        _method = null;
        ResetRecipeResults();
        _options = false;
        CloseLanguageMenu();
        _pendingBinding = null;
        _open = true;
        _searchFocused = false;
        _skipFocusFrame = true;
    }

    internal void NavigateBack()
    {
        if (_treeOpen && _treeView != TreeView.Steps && _treePlan != null)
        {
            ClearSupportedItemsTip();
            ClearChainTip();
            if (_treeView == TreeView.Step) TreePrevious();
            else SelectTreeView(TreeView.Steps);
            return;
        }
        Back();
    }

    internal void Back()
    {
        ClearItemTooltipInput();
        ClearSupportedItemsTip();
        ClearChainTip();
        if (_options) { _options = false; CloseLanguageMenu(); _pendingBinding = null; _skipFocusFrame = true; return; }
        if (_treeOpen) { _treeOpen = _returnToTree = false; _skipFocusFrame = true; return; }
        if (_returnToTree && _open && _back.Count == 0 && _expandedRequirement < 0)
        { _open = _returnToTree = false; _treeOpen = true; _skipFocusFrame = true; return; }
        if (_expandedRequirement >= 0) { CollapseRecipeRequirement(); return; }
        if (_back.Count == 0) { CloseRecipe(); return; }
        ResetRecipeResults();
        _selectedRecipeBookmark = null;
        _skipFocusFrame = true;
        (_item, _usage, _method, _recipeIndex, _recipeScroll, _lookupContents, _lookupItem, _propertyQuery, _propertyPage) = _back.Pop();
    }

    private void CloseRecipe()
    {
        _open = _returnToTree = false;
        _propertyQuery = null;
        _propertyPage = 0;
        _back.Clear();
        _itemTargets.Clear();
        ResetRecipeResults();
        if (_focusSearch == SearchControl) _focusSearch = null;
        _skipFocusFrame = true;
    }

    internal void Draw()
    {
        var e = Event.current;
        Vector2 screenMouse = e.mousePosition;
        if (_skipFocusFrame)
        {
            if (IsTextControl(GUI.GetNameOfFocusedControl())) GUI.FocusControl(null);
            _skipFocusFrame = false;
        }
        _tipTitle = _tipBody = null;
        _itemTip = null;
        _itemTipSource = default;
        _boxedTip = false;
        _supportedItemsHover = null;
        _bookmarkTip = null;
        _bookmarkHelp = null;
        _searchRect = _groupRect = default;
        if (e.type == EventType.Repaint)
        {
            _itemTargets.Clear(); _recipeTargets.Clear(); ClearBookmarkHits();
            if ((_bookmarkDrag is { } drag && !Input.GetMouseButton(drag.Button)) || !ShowLeft || _plugin.NativePointerActive) _bookmarkDrag = null;
            EnsureDiagramRevision();
            AdvanceRecipeCandidates();
        }
        Matrix4x4 savedMatrix = GUI.matrix;
        try
        {
            // IMGUI's clip stack maps mouse events into the scaled GUI coordinate system.
            if (_scale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * _scale, Vector2.zero);
            bool layoutPointer = _layoutEditing && (_layoutDrag.HasValue || LayoutEditHit(e.mousePosition));
            HandleLayoutInput(e);
            if (layoutPointer && e.type is (EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel)) e.Use();
            HandleItemTooltipInput(e);
            DismissLanguageMenuOnOutsideClick(e);
            HandleSupportedItemsInput(e);
            HandleChainTipInput(e);
            bool modalAtStart = ModalOpen;
            if (ShowLeft) DrawBookmarkPanel(_left);
            if (ShowRight)
            {
                if (_catalogFilterDirty && e.type == EventType.Repaint) ApplyFilter();
                DrawCatalogControls(_catalogControls, _catalogLayout);
                DrawCatalog(_right, _catalogLayout);
            }
            else if (GUI.GetNameOfFocusedControl() is CatalogSearchControl or CatalogQuantityControl)
                GUI.FocusControl(null);
            if (!_plugin.IsCrafting) DrawLeftTools(_leftTools);
            if (modalAtStart && ModalOpen && !_plugin.IsCrafting && !_layoutEditing)
            {
                if (_options) DrawOptions(_optionsBounds);
                else if (_treeOpen) DrawBookmarkTree(_inspector);
                else DrawInspector(_recipePanel);
            }
            if (e.type == EventType.Repaint)
            {
                DrawLayoutHandles();
                _supportedCopyId = null;
                _queuedItemTip = null;
                var guiSize = new Vector2(_width, _height);
                if (_status != null && Time.unscaledTime < _statusUntil)
                    NeiSkin.Tooltip(new Vector2(Mathf.Max(15, _center.xMin), Mathf.Max(8, _height - 72)), guiSize, _status);
                if (_bookmarkHelp is { } help && !ChainTipHit(e.mousePosition) && !SupportedItemsHit(e.mousePosition))
                {
                    // Hidden popups must not intercept clicks while the shortcut help is displayed.
                    _chainTipBox = _supportedItemsBox = default;
                    _chainTipItems.Clear();
                    NeiSkin.Tooltip(e.mousePosition, guiSize, help.Title, help.Rows, boxed: true, protectedControls: RecipeNavigationBounds);
                }
                else
                {
                    bool chainTipDrawn = DrawBookmarkChainTip(e.mousePosition);
                    if (!DrawSupportedItemsTip(e.mousePosition) && !chainTipDrawn)
                    {
                        if (_itemTip is { } itemTip) QueueItemTip(e.mousePosition, _itemTipSource, itemTip);
                        else if (_drawnItemTip == null && _tipTitle != null)
                            NeiSkin.Tooltip(e.mousePosition, guiSize, _tipTitle, _tipBody, boxed: _boxedTip, protectedControls: RecipeNavigationBounds);
                        else if (_drawnItemTip == null && Hit(screenMouse) && !string.IsNullOrEmpty(GUI.tooltip))
                            NeiSkin.Tooltip(e.mousePosition, guiSize, GUI.tooltip, boxed: true, protectedControls: RecipeNavigationBounds);
                    }
                }
                DrawQueuedItemTip();
            }
            _searchFocused = IsTextControl(GUI.GetNameOfFocusedControl());
            if (Hit(screenMouse) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
        }
        finally { GUI.matrix = savedMatrix; }
    }

    private void Tip(string title, string? text, bool boxed = false)
    {
        _itemTip = null;
        _tipTitle = title;
        _tipBody = text;
        _boxedTip = boxed;
    }
    private void Status(string text) { _status = text; _statusUntil = Time.unscaledTime + 3.5f; }
    private static int Wrap(int value, int count) => (value % count + count) % count;
}
