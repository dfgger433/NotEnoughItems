using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    internal const string SearchControl = "NEI.ItemSearch";
    internal const string GroupControl = "NEI.BookmarkGroup";
    internal const string QuantityControl = "NEI.Quantity";
    private readonly Plugin _plugin;
    private readonly Catalog _catalog = Catalog.Shared;
    private readonly ConfigEntry<string> _saved;
    private readonly ConfigEntry<bool> _showHistory;
    private readonly ConfigEntry<bool> _showGrid;
    private readonly ConfigEntry<bool> _showInventoryGrid;
    private readonly BookmarkBook _book = new();
    private string? _activeRecipeKey, _chainGroup;
    private bool _chainDetails;
    private Vector2 _chainScroll;
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private readonly List<string> _recent = new();
    private readonly Stack<(string? Item, bool Usage, string? Category, int Index, string[] Contents, Item? LiveItem)> _back = new();
    private string[] _lookupContents = Array.Empty<string>();
    private readonly List<(Rect Rect, string Id)> _itemTargets = new();
    private readonly List<(Rect Rect, string Title, CatalogItem? Item, string? Details, Item? Instance)> _tooltipSlots = new();
    private int _tooltipFrame = -1;
    private bool _hoveringItemSlot;
    private Item? _lookupItem;
    private Rect _left, _right, _center, _inspector, _searchRect, _groupRect, _quantityRect, _leftTools;
    private bool _bookmarksVisible = true;
    private float _scale = 1f;
    private float _width, _height;
    private string _query = string.Empty, _group = "0", _editGroup = string.Empty, _recipeQuery = string.Empty;
    private bool _craftableOnly;
    private string? _selectedRecipeBookmark;
    private float _craftableUntil;
    private readonly Dictionary<global::Recipe, bool> _craftable = new();
    private int _page, _bookmarkPage, _recipeIndex;
    private string _quantityText = "1";
    private int GiveQuantity => int.TryParse(_quantityText, out int value) ? Mathf.Clamp(value, 1, 64) : 1;
    private int _seenRevision = -1;
    private string? _item, _category;
    private bool _usage, _open, _options, _groups, _focusSearch, _searchFocused;
    private bool _skipFocusFrame;
    private string? _tipTitle, _tipBody, _status;
    private float _statusUntil;
    private Vector2 _recipeScroll;
    private Recipe? _requirementRecipe;
    private int _requirementRevision = -1, _expandedRequirement = -1;
    private float _previewUntil;
    private List<Item>? _nativePreview;
    private readonly Dictionary<int, (IReadOnlyList<CatalogItem> Related, IReadOnlyList<CatalogItem> Eligible)> _alternatives = new();
    private readonly Dictionary<RecipeIngredient, string> _ingredientSearch = new();
    private IReadOnlyList<CatalogItem> _filtered = Array.Empty<CatalogItem>();
    private string? _lastError;
    internal bool SearchFocused { get => _searchFocused; set => _searchFocused = value; }
    internal bool ModalOpen => _open || _options || _chainGroup != null;

    internal NeiWindow(Plugin plugin, ConfigFile config)
    {
        _plugin = plugin;
        _saved = config.Bind("Bookmarks", "Entries", string.Empty, "Bookmarked item ids and their page names. Managed by NEI.");
        _showHistory = config.Bind("NotEnoughItems", "HistoryStrip", true, "Show recently inspected items in the catalog.");
        _showGrid = config.Bind("NotEnoughItems", "ItemList", true, "Show the item catalog outside the native inventory.");
        _showInventoryGrid = config.Bind("NotEnoughItems", "ItemListInInventory", false, "Show the right-hand item catalog beside the native radial inventory.");
        _book.Load(_saved.Value);
    }

    private string L(string en, string zh) => _plugin.Chinese ? zh : en;
    internal void Position(Rect center, float scale)
    {
        float preferredScale = Mathf.Clamp(Screen.height / 720f, .9f, 2f) * scale;
        // Preserve enough logical width and height for two grids and the recipe controls.
        _scale = Mathf.Max(.01f, Mathf.Min(preferredScale, Mathf.Min(Screen.width / 480f, Screen.height / 410f)));
        _width = Screen.width / _scale;
        _height = Screen.height / _scale;
        _center = new Rect(center.position / _scale, center.size / _scale);
        (_left, _right) = NeiLayout.VisibleSides(_center, _width, _height);
        _inspector = NeiLayout.InspectorBetween(_center, _left, _right, _width, _height);
    }

    internal void Hide()
    {
        _left = _right = _inspector = _searchRect = _groupRect = _quantityRect = _leftTools = default;
        _itemTargets.Clear();
        _tooltipSlots.Clear();
        _tooltipFrame = -1;
        _lookupItem = null;
    }

    internal void ToggleBookmarks() => _bookmarksVisible = !_bookmarksVisible;
    internal void ResetView()
    {
        _open = _options = _groups = _searchFocused = _focusSearch = false;
        _chainGroup = null;
        _bookmarkDrag = null;
        _dragGroup = null;
        _activeRecipeKey = null;
        _back.Clear();
        _itemTargets.Clear();
        _tooltipSlots.Clear(); _tooltipFrame = -1;
        _lookupItem = null;
        _expandedRequirement = -1;
        _requirementRecipe = null;
        _nativePreview = null;
        _alternatives.Clear();
    }

    private bool HideBookmarksForDrag
    {
        get
        {
            var camera = PlayerCamera.main;
            return camera && camera.dragItem && camera.dragItem.TryGetComponent<WaterContainerItem>(out _);
        }
    }
    private bool ShowLeft => _bookmarksVisible && !HideBookmarksForDrag;
    private bool ShowRight => CatalogSetting.Value;
    private ConfigEntry<bool> CatalogSetting => _plugin.IsNativeInventory ? _showInventoryGrid : _showGrid;

    internal bool Hit(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        return (ModalOpen && _inspector.Contains(pointer)) || (ShowLeft && _left.Contains(pointer)) ||
            (ShowRight && _right.Contains(pointer)) || _searchRect.Contains(pointer) || _leftTools.Contains(pointer);
    }
    internal bool SearchHit(Vector2 pointer) => _searchRect.Contains(pointer / Mathf.Max(.01f, _scale));
    internal bool GroupHit(Vector2 pointer) => _groupRect.Contains(pointer / Mathf.Max(.01f, _scale)) || _quantityRect.Contains(pointer / Mathf.Max(.01f, _scale));
    internal static bool IsTextControl(string? name) => name is SearchControl or GroupControl or QuantityControl;
    internal string? ItemAt(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        for (int i = _itemTargets.Count - 1; i >= 0; i--)
            if (_itemTargets[i].Rect.Contains(pointer)) return _itemTargets[i].Id;
        return null;
    }
    internal bool TryGetItemTooltip(Vector2 pointer, out (string, string) result)
    {
        result = default;
        if (_tooltipFrame < 0 || Time.frameCount - _tooltipFrame > 2 || !Hit(pointer)) return false;
        var logical = pointer / _scale;
        for (int i = _tooltipSlots.Count - 1; i >= 0; i--)
        {
            var slot = _tooltipSlots[i];
            if (!slot.Rect.Contains(logical)) continue;
            result = NativeTooltip.ForItem(slot.Title, slot.Item, slot.Details, slot.Instance);
            return !string.IsNullOrEmpty(result.Item1);
        }
        return false;
    }

    internal bool RequestSearchFocus()
    {
        if (_options || _chainGroup != null || (!_open && !ShowRight)) return false;
        _focusSearch = true;
        return true;
    }

    internal void RefreshCatalog(ManualLogSource log)
    {
        try
        {
            int revision = _catalog.Revision;
            if (_seenRevision != revision)
            {
                bool first = _seenRevision < 0;
                _seenRevision = revision;
                _ingredientSearch.Clear();
                ApplyFilter();
                if (first || _lastError != null)
                    log.LogInfo($"NEI catalog: {_catalog.Items.Count} entries, {_catalog.Recipes.Count} recipes, {_catalog.RecipeCategories.Count} categories.");
            }
            _lastError = null;
        }
        catch (Exception error)
        {
            if (_lastError != error.Message) log.LogError("NEI could not refresh the recipe catalog: " + error);
            _lastError = error.Message;
        }
    }

    private void ApplyFilter()
    {
        _filtered = _catalog.Items.Where(item =>
            NeiSearch.Matches(_query, item.SearchText, item.Source, string.Join(" ", item.Tags), item.Description, item.Id)).ToArray();
    }

    internal void ToggleBookmark(string id, int quantity = 1)
    {
        if (_book.Toggle(_group, id, quantity)) SaveBookmarks();
    }

    private void SaveBookmarks() => _saved.Value = _book.Serialize();

    internal bool HandleBookmarkShortcut(Vector2 pointer, string? id, bool control, bool shift)
    {
        pointer /= Mathf.Max(.01f, _scale);
        if (shift && !_open)
        {
            var header = _bookmarkHeaders.Find(entry => entry.Rect.Contains(pointer));
            if (header.Group != null && header.Group.Length > 0)
            {
                _book.Entries.RemoveAll(entry => entry.Page == _group && entry.Group == header.Group);
                _collapsedGroups.Remove(header.Group);
                SaveBookmarks();
                return true;
            }
        }
        if (shift && _open && _activeRecipeKey != null)
        {
            ToggleBookmark(_activeRecipeKey, control ? GiveQuantity : 1);
            return true;
        }
        if (id == null) return false;
        ToggleBookmark(id, control ? GiveQuantity : 1);
        return true;
    }

    internal void Lookup(string id, bool usage, string[]? contents = null, Item? liveItem = null)
    {
        contents ??= Array.Empty<string>();
        if (_open && _item == id && _usage == usage && _lookupContents.SequenceEqual(contents) && _lookupItem == liveItem) return;
        OpenView(id, usage, null);
        _lookupContents = contents;
        _lookupItem = liveItem;
        _recent.Remove(id);
        _recent.Insert(0, id);
        if (_recent.Count > 32) _recent.RemoveAt(_recent.Count - 1);
    }

    private void OpenView(string? id, bool usage, string? category)
    {
        if (id?.StartsWith("recipe/", StringComparison.Ordinal) == true)
        {
            var recipe = _catalog.Recipes.FirstOrDefault(r => RecipeKey(r) == id);
            if (recipe != null)
            {
                OpenView(null, false, recipe.CategoryId);
                _selectedRecipeBookmark = id;
                return;
            }
        }
        if (_open && _back.Count < 64) _back.Push((_item, _usage, _category, _recipeIndex, _lookupContents, _lookupItem));
        _item = id;
        _lookupContents = Array.Empty<string>();
        _lookupItem = null;
        _tooltipSlots.Clear(); _tooltipFrame = -1;
        _selectedRecipeBookmark = null;
        _craftableOnly = false;
        _activeRecipeKey = null;
        _chainGroup = null;
        _recipeQuery = string.Empty;
        _usage = usage;
        _category = category;
        _recipeIndex = 0;
        _recipeScroll = Vector2.zero;
        _expandedRequirement = -1;
        _options = false;
        _open = true;
        _searchFocused = false;
        _skipFocusFrame = true;
    }

    internal void Back()
    {
        if (_options) { _options = false; return; }
        if (_chainGroup != null) { _chainGroup = null; return; }
        if (_expandedRequirement >= 0) { _expandedRequirement = -1; return; }
        if (_back.Count == 0) { _open = false; _itemTargets.Clear(); return; }
        _recipeQuery = string.Empty; _craftableOnly = false;
        _activeRecipeKey = null;
        (_item, _usage, _category, _recipeIndex, _lookupContents, _lookupItem) = _back.Pop();
        _recipeScroll = Vector2.zero;
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
        _searchRect = _groupRect = _quantityRect = _leftTools = default;
        if (e.type == EventType.Repaint)
        {
            _itemTargets.Clear(); _bookmarkHeaders.Clear(); _tooltipSlots.Clear();
            _tooltipFrame = Time.frameCount;
            _hoveringItemSlot = false;
        }
        if (e.type == EventType.Repaint && !Input.GetMouseButton(0)) { _bookmarkDrag = null; _dragGroup = null; }
        Matrix4x4 savedMatrix = GUI.matrix;
        try
        {
            // IMGUI's clip stack maps mouse events into the scaled GUI coordinate system.
            if (_scale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * _scale, Vector2.zero);
            bool modalAtStart = ModalOpen;
            if (ShowLeft) DrawBookmarkPanel(_left);
            if (ShowRight) DrawCatalog(_right);
            DrawBottomRail(showSearch: !modalAtStart && ShowRight);
            if (modalAtStart && ModalOpen)
            {
                if (_options) DrawOptions(_inspector);
                else if (_chainGroup != null) DrawChain(_inspector);
                else DrawInspector(_inspector);
            }
            if (e.type == EventType.Repaint && !_hoveringItemSlot)
            {
                if (_tipTitle != null) NeiSkin.Tooltip(e.mousePosition, _tipTitle, _tipBody);
                else if (Hit(screenMouse) && !string.IsNullOrEmpty(GUI.tooltip)) NeiSkin.Tooltip(e.mousePosition, GUI.tooltip);
            }
            if (e.type == EventType.Repaint && _status != null && Time.unscaledTime < _statusUntil)
                NeiSkin.Tooltip(new Vector2(Mathf.Max(15, _center.xMin), Mathf.Max(8, _height - 72)), _status);
            _searchFocused = IsTextControl(GUI.GetNameOfFocusedControl());
            if (Hit(screenMouse) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
        }
        finally { GUI.matrix = savedMatrix; }
    }

    private void Tip(string title, string? text) { _tipTitle = title; _tipBody = text; }
    private void Status(string text) { _status = text; _statusUntil = Time.unscaledTime + 3.5f; }
    private static int Wrap(int value, int count) => (value % count + count) % count;
}
