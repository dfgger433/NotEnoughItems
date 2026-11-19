using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace NotEnoughItems;

internal sealed class NeiWindow
{
    internal const string SearchControl = "NEI.ItemSearch";
    internal const string GroupControl = "NEI.BookmarkGroup";
    private readonly Plugin _plugin;
    private readonly Catalog _catalog = Catalog.Shared;
    private readonly ConfigEntry<string> _saved;
    private readonly ConfigEntry<bool> _showHistory;
    private readonly ConfigEntry<bool> _showGrid;
    private readonly List<(string Group, string Id)> _bookmarks = new();
    private readonly List<string> _recent = new();
    private readonly Stack<(string? Item, bool Usage, string? Category, int Index)> _back = new();
    private readonly List<(Rect Rect, string Id)> _itemTargets = new();
    private Rect _left, _right, _center, _inspector, _searchRect, _groupRect, _compactBar, _compactGrid;
    private bool _compactBookmarks, _bookmarksVisible = true;
    private float _scale = 1f;
    private float _width, _height;
    private string _query = string.Empty, _group = "0", _editGroup = string.Empty, _recipeQuery = string.Empty;
    private bool _showPlan, _craftableOnly;
    private string? _selectedRecipeBookmark;
    private float _craftableUntil, _planUntil;
    private readonly Dictionary<global::Recipe, bool> _craftable = new();
    private Recipe? _plannedRecipe;
    private List<(int Depth, string Label, string Amount, string? Item, bool? Available)> _plan = new();
    private int _page, _bookmarkPage, _recipeIndex, _subset;
    private string[] _subsets = Array.Empty<string>();
    private int _seenRevision = -1;
    private string? _item, _category;
    private bool _usage, _open, _options, _groups, _focusSearch, _searchFocused;
    private bool _skipFocusFrame;
    private string? _tipTitle, _tipBody, _status;
    private float _statusUntil;
    private Vector2 _recipeScroll;
    private IReadOnlyList<CatalogItem> _filtered = Array.Empty<CatalogItem>();
    private string? _lastError;
    internal bool SearchFocused { get => _searchFocused; set => _searchFocused = value; }
    internal bool ModalOpen => _open || _options;

    internal NeiWindow(Plugin plugin, ConfigFile config)
    {
        _plugin = plugin;
        _saved = config.Bind("Bookmarks", "Entries", string.Empty, "Bookmarked item ids and their page names. Managed by NEI.");
        _showHistory = config.Bind("NotEnoughItems", "HistoryStrip", true, "Show recently inspected items in the catalog.");
        _showGrid = config.Bind("NotEnoughItems", "ItemList", true, "Show the item catalog.");
        foreach (var entry in _saved.Value.Split('|'))
        {
            var parts = entry.Split(new[] { ':' }, 2);
            if (parts.Length != 2) continue;
            try { _bookmarks.Add((Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]))); }
            catch (UriFormatException) { /* Ignore malformed persisted entries. */ }
        }
    }

    private string L(string en, string zh) => _plugin.Chinese ? zh : en;
    internal void Position(Rect center, float scale)
    {
        _scale = Mathf.Clamp(Screen.height / 720f, 0.9f, 2f) * scale;
        _width = Screen.width / _scale;
        _height = Screen.height / _scale;
        _center = new Rect(center.position / _scale, center.size / _scale);
        (_left, _right) = NeiLayout.Sides(_center, _width, _height, 1f);
        _inspector = NeiLayout.Inspector(_center, _width, _height, 1f);
    }

    internal void Hide() { _left = _right = _inspector = _searchRect = _groupRect = _compactBar = _compactGrid = default; _itemTargets.Clear(); }
    internal void ToggleBookmarks() { _bookmarksVisible = !_bookmarksVisible; _compactBookmarks = false; }
    internal void ResetView()
    {
        _open = _options = _groups = _searchFocused = _focusSearch = false;
        NeiActions.Enabled = false;
        _back.Clear();
        _itemTargets.Clear();
    }

    private bool ShowLeft => _bookmarksVisible && _left.width >= 35 && _left.height >= 100;
    private bool ShowRight => _right.width >= 35 && _right.height >= 100 && _showGrid.Value;
    internal bool Hit(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        return ModalOpen || (ShowLeft && _left.Contains(pointer)) ||
            (ShowRight && _right.Contains(pointer)) || _compactBar.Contains(pointer) || _compactGrid.Contains(pointer);
    }
    internal bool SearchHit(Vector2 pointer) => _searchRect.Contains(pointer / Mathf.Max(.01f, _scale));
    internal bool GroupHit(Vector2 pointer) => _groupRect.Contains(pointer / Mathf.Max(.01f, _scale));
    internal string? ItemAt(Vector2 pointer)
    {
        pointer /= Mathf.Max(.01f, _scale);
        for (int i = _itemTargets.Count - 1; i >= 0; i--)
            if (_itemTargets[i].Rect.Contains(pointer)) return _itemTargets[i].Id;
        return null;
    }
    internal void RequestSearchFocus() { _showGrid.Value = true; _focusSearch = true; }

    internal void RefreshCatalog(ManualLogSource log)
    {
        try
        {
            int revision = _catalog.Revision;
            if (_seenRevision != revision)
            {
                bool first = _seenRevision < 0;
                _seenRevision = revision;
                var selected = _subset > 0 && _subset <= _subsets.Length ? _subsets[_subset - 1] : null;
                _subsets = _catalog.Items.Select(item => item.CategoryId).Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
                _subset = selected == null ? 0 : Array.IndexOf(_subsets, selected) + 1;
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
        var subset = _subset > 0 && _subset <= _subsets.Length ? _subsets[_subset - 1] : null;
        _filtered = _catalog.Items.Where(item =>
            (subset == null || item.CategoryId == subset) &&
            NeiSearch.Matches(_query, item.SearchText, item.Source, string.Join(" ", item.Tags), item.Description, item.Id)).ToArray();
    }

    internal void ToggleBookmark(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        int index = _bookmarks.FindIndex(entry => entry.Group == _group && entry.Id == id);
        if (index >= 0) _bookmarks.RemoveAt(index);
        else _bookmarks.Add((_group, id));
        SaveBookmarks();
    }

    private void SaveBookmarks() => _saved.Value = string.Join("|", _bookmarks.Select(entry =>
        Uri.EscapeDataString(entry.Group) + ":" + Uri.EscapeDataString(entry.Id)));

    internal void Lookup(string id, bool usage)
    {
        if (_open && _item == id && _usage == usage) return;
        OpenView(id, usage, null);
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
        if (_open && _back.Count < 64) _back.Push((_item, _usage, _category, _recipeIndex));
        _item = id;
        _selectedRecipeBookmark = null;
        _showPlan = _craftableOnly = false;
        _recipeQuery = string.Empty;
        _usage = usage;
        _category = category;
        _recipeIndex = 0;
        _recipeScroll = Vector2.zero;
        _options = false;
        _open = true;
        _searchFocused = false;
        _skipFocusFrame = true;
    }

    internal void Back()
    {
        if (_options) { _options = false; return; }
        if (_back.Count == 0) { _open = false; _itemTargets.Clear(); return; }
        _recipeQuery = string.Empty; _showPlan = _craftableOnly = false;
        (_item, _usage, _category, _recipeIndex) = _back.Pop();
        _recipeScroll = Vector2.zero;
    }

    internal void Draw()
    {
        var e = Event.current;
        Vector2 screenMouse = e.mousePosition;
        if (_skipFocusFrame)
        {
            if (GUI.GetNameOfFocusedControl() is SearchControl or GroupControl) GUI.FocusControl(null);
            _skipFocusFrame = false;
        }
        _tipTitle = _tipBody = null;
        _searchRect = _groupRect = _compactBar = _compactGrid = default;
        if (e.type == EventType.Repaint) _itemTargets.Clear();
        Matrix4x4 savedMatrix = GUI.matrix;
        try
        {
            // IMGUI's clip stack maps mouse events into the scaled GUI coordinate system.
            if (_scale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * _scale, Vector2.zero);
            if (ModalOpen)
            {
                if (e.type == EventType.Repaint) Dim();
                if (_options) DrawOptions(_inspector);
                else DrawInspector(_inspector);
            }
            else
            {
                if (ShowLeft) DrawBookmarkPanel(_left);
                if (ShowRight) DrawCatalog(_right);
                if (!ShowLeft || !ShowRight) DrawCompactBar();
            }
            if (e.type == EventType.Repaint && _tipTitle != null) NeiSkin.Tooltip(e.mousePosition, _tipTitle, _tipBody);
            if (e.type == EventType.Repaint && _status != null && Time.unscaledTime < _statusUntil)
                NeiSkin.Tooltip(new Vector2(Mathf.Max(15, _center.xMin), Mathf.Max(8, _height - 72)), _status);
            _searchFocused = GUI.GetNameOfFocusedControl() is SearchControl or GroupControl;
            if (Hit(screenMouse) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
        }
        finally { GUI.matrix = savedMatrix; }
    }

    private void DrawBookmarkPanel(Rect panel)
    {
        NeiSkin.Panel(panel);
        Header(panel, L("Bookmarks", "书签"), "B");
        float x = panel.x + 5, y = panel.y + 30, w = panel.width - 10;
        if (w < 90)
        {
            if (NeiSkin.Button(new Rect(x, y, w, 23), "B", selected: _groups)) _groups = !_groups;
            y += 26;
        }
        else
        {
            if (NeiSkin.Button(new Rect(x, y, w - 29, 23), _group == "0" ? L("Default page", "默认页") : _group, selected: _groups)) _groups = !_groups;
            if (NeiSkin.Button(new Rect(panel.xMax - 31, y, 26, 23), "+")) _groups = !_groups;
            y += 27;
        }
        if (_groups)
        {
            var groups = new[] { "0" }.Concat(_bookmarks.Select(bookmark => bookmark.Group)).Append(_group).Distinct().ToArray();
            int index = Array.IndexOf(groups, _group);
            if (NeiSkin.Button(new Rect(x, y, w * .49f, 22), "‹")) { _group = groups[Wrap(index - 1, groups.Length)]; _bookmarkPage = 0; }
            if (NeiSkin.Button(new Rect(x + w * .51f, y, w * .49f, 22), "›")) { _group = groups[Wrap(index + 1, groups.Length)]; _bookmarkPage = 0; }
            y += 25;
            _groupRect = new Rect(x, y, w, 23);
            _editGroup = NeiSkin.TextField(_groupRect, _editGroup, GroupControl, 32);
            y += 26;
            if (NeiSkin.Button(new Rect(x, y, w, 23), L("Use named page", "使用命名页"), !string.IsNullOrWhiteSpace(_editGroup)))
            {
                _group = _editGroup.Trim(); _editGroup = string.Empty; _groups = false; _bookmarkPage = 0;
            }
            y += 26;
        }
        float bottom = panel.yMax - 61;
        var entries = _bookmarks.Where(entry => entry.Group == _group).Select(entry => entry.Id).ToArray();
        var grid = new Rect(x, y, w, bottom - y);
        DrawGrid(grid, entries.Length, i => entries[i], ref _bookmarkPage, true, bottom + 2);
        if (entries.Length == 0 && grid.height > 70 && w > 115)
            NeiSkin.Label(new Rect(x + 5, y + 4, w - 10, 24), L($"{_plugin.BookmarkKey} adds a bookmark", $"按 {_plugin.BookmarkKey} 收藏物品"), muted: true);
        DrawActions(new Rect(x, panel.yMax - 29, w, 24));
    }

    private void DrawActions(Rect row)
    {
        if (row.width < 95)
        {
            if (NeiSkin.Button(row, "⚙")) { _options = true; _skipFocusFrame = true; }
            return;
        }
        if (NeiSkin.Button(new Rect(row.x, row.y, 31, row.height), "⚙")) { _options = true; _skipFocusFrame = true; }
        if (NeiSkin.Button(new Rect(row.x + 34, row.y, row.width - 34, row.height), L("Machines", "机器配方"))) OpenView(null, false, null);
    }

    private void DrawCatalog(Rect panel)
    {
        NeiSkin.Panel(panel);
        Header(panel, L("Item catalog", "物品目录"), "NEI");
        float x = panel.x + 5, y = panel.y + 30, w = panel.width - 10;
        if (w >= 100)
        {
            string caption = _subset == 0 ? L("Item subsets · All", "物品子集 · 全部") : _subsets[_subset - 1];
            if (NeiSkin.Button(new Rect(x, y, w - 28, 22), caption)) { _subset = Wrap(_subset + 1, _subsets.Length + 1); _page = 0; ApplyFilter(); }
            if (NeiSkin.Button(new Rect(x + w - 26, y, 26, 22), "‹")) { _subset = Wrap(_subset - 1, _subsets.Length + 1); _page = 0; ApplyFilter(); }
            y += 25;
        }
        float bottom = panel.yMax - 56;
        if (_showHistory.Value && _recent.Count > 0 && bottom - y > 140) bottom -= 32;
        DrawGrid(new Rect(x, y, w, bottom - y), _filtered.Count, i => _filtered[i].BrowseId, ref _page, false, bottom + 1);
        if (_filtered.Count == 0 && w > 78) NeiSkin.Label(new Rect(x, y + 4, w, 24), L("No matching items", "没有匹配物品"), TextAnchor.MiddleCenter, true);
        if (bottom < panel.yMax - 60)
        {
            float slot = 28;
            for (int i = 0; i < Math.Min(_recent.Count, (int)(w / slot)); i++)
                ItemSlot(new Rect(x + i * slot, bottom + 27, slot, slot), _recent[i], false);
        }
        _searchRect = new Rect(x, panel.yMax - 26, w, 21);
        DrawSearch(_searchRect);
    }

    private void DrawCompactBar()
    {
        float w = Mathf.Min(_width - 16, 278);
        var bar = _compactBar = new Rect(Mathf.Clamp(_center.center.x - w / 2, 8, _width - w - 8), _height - 37, w, 29);
        NeiSkin.Panel(bar);
        var machines = new Rect(bar.x + 5, bar.y + 4, 47, 21);
        if (NeiSkin.Button(machines, "NEI")) OpenView(null, false, null);
        if (NeiSkin.Button(new Rect(bar.xMax - 28, bar.y + 4, 23, 21), "⚙")) _options = true;
        float left = machines.xMax + 3;
        if (_bookmarksVisible && !ShowLeft)
        {
            if (NeiSkin.Button(new Rect(left, machines.y, 22, 21), "B", selected: _compactBookmarks)) _compactBookmarks = !_compactBookmarks;
            left += 25;
        }
        if (!ShowRight && _showGrid.Value)
        {
            _searchRect = new Rect(left, machines.y, bar.xMax - left - 30, 21);
            DrawSearch(_searchRect);
        }
        if (!ModalOpen && (_compactBookmarks && !ShowLeft || (!ShowRight && _showGrid.Value)))
        {
            bool bookmarks = _compactBookmarks && !ShowLeft;
            var strip = _compactGrid = new Rect(bar.x, bar.y - 55, bar.width, 51);
            NeiSkin.Panel(strip);
            var grid = new Rect(strip.x + 4, strip.y + 2, strip.width - 8, 28);
            if (bookmarks)
            {
                var entries = _bookmarks.Where(entry => entry.Group == _group).Select(entry => entry.Id).ToArray();
                DrawGrid(grid, entries.Length, i => entries[i], ref _bookmarkPage, true, strip.y + 29);
            }
            else DrawGrid(grid, _filtered.Count, i => _filtered[i].BrowseId, ref _page, false, strip.y + 29);
        }
    }

    private void DrawSearch(Rect rect)
    {
        var value = NeiSkin.TextField(rect, _query, SearchControl, 160);
        if (_focusSearch && Event.current.type == EventType.Repaint) { GUI.FocusControl(SearchControl); _focusSearch = false; }
        if (value != _query) { _query = value; _page = 0; ApplyFilter(); }
        if (rect.width > 82 && _query.Length == 0 && GUI.GetNameOfFocusedControl() != SearchControl)
            NeiSkin.Label(new Rect(rect.x + 5, rect.y, rect.width - 10, rect.height), L("Search items…", "搜索物品…"), muted: true);
        if (rect.Contains(Event.current.mousePosition) && GUI.GetNameOfFocusedControl() != SearchControl)
            Tip(L("Search · Ctrl+F", "搜索 · Ctrl+F"), L("@source  $tag  #description  &id  -exclude\nUse quotes for a phrase.", "@来源  $标签  #描述  &ID  -排除\n短语用双引号括住。"));
    }

    private void DrawGrid(Rect rect, int count, Func<int, string> idAt, ref int page, bool bookmarks, float navY)
    {
        if (rect.width < 20 || rect.height < 20) return;
        float cell = Mathf.Min(31, Mathf.Min(rect.height, rect.width));
        int columns = Mathf.Max(1, (int)(rect.width / cell));
        int rows = Mathf.Max(1, (int)(rect.height / cell));
        int size = columns * rows;
        int pages = Mathf.Max(1, (count + size - 1) / size);
        page = Mathf.Clamp(page, 0, pages - 1);
        var e = Event.current;
        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        {
            page = Wrap(page + (e.delta.y > 0 ? 1 : -1), pages);
            e.Use();
        }
        int start = page * size;
        for (int i = 0; i < size; i++)
        {
            var cellRect = new Rect(rect.x + (i % columns) * cell, rect.y + (i / columns) * cell, cell - 1, cell - 1);
            if (start + i < count) ItemSlot(cellRect, idAt(start + i), bookmarks);
            else NeiSkin.Slot(cellRect);
        }
        float button = Mathf.Min(23, rect.width * .31f);
        if (NeiSkin.Button(new Rect(rect.x, navY, button, 20), "‹", pages > 1)) page = Wrap(page - 1, pages);
        if (NeiSkin.Button(new Rect(rect.xMax - button, navY, button, 20), "›", pages > 1)) page = Wrap(page + 1, pages);
        if (rect.width > 64) NeiSkin.Label(new Rect(rect.x + button, navY, rect.width - 2 * button, 20), $"{page + 1}/{pages}", TextAnchor.MiddleCenter);
    }

    private void ItemSlot(Rect rect, string id, bool bookmarked, Sprite? overrideIcon = null, string? amount = null,
        string? fallback = null, Rect? clipping = null, Vector2? globalOffset = null, string? details = null)
    {
        _catalog.TryGetItem(id, out var item);
        var recipeBookmark = id.StartsWith("recipe/", StringComparison.Ordinal) ? _catalog.Recipes.FirstOrDefault(r => RecipeKey(r) == id) : null;
        if (recipeBookmark?.Outputs.FirstOrDefault() is { } result) _catalog.TryGetItem(result.BrowseId, out item);
        string title = !string.IsNullOrWhiteSpace(fallback) ? fallback! : recipeBookmark?.Name ?? item?.DisplayName ?? id;
        var e = Event.current;
        bool hover = rect.Contains(e.mousePosition);
        Vector2 position = globalOffset.GetValueOrDefault();
        var absolute = rect; absolute.position += position;
        if (clipping.HasValue)
        {
            var area = clipping.Value;
            absolute = Rect.MinMaxRect(Mathf.Max(area.xMin, absolute.xMin), Mathf.Max(area.yMin, absolute.yMin),
                Mathf.Min(area.xMax, absolute.xMax), Mathf.Min(area.yMax, absolute.yMax));
            hover &= absolute.width > 0 && absolute.height > 0 && area.Contains(e.mousePosition + position);
        }
        NeiSkin.Slot(rect, hover, bookmarked);
        DrawSprite(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), overrideIcon ?? item?.Icon, item?.IconTint ?? Color.white, title);
        if (!string.IsNullOrEmpty(amount)) NeiSkin.Label(new Rect(rect.x + 2, rect.yMax - 14, rect.width - 4, 13), amount!, TextAnchor.MiddleRight);
        if (e.type == EventType.Repaint && absolute.width > 0 && absolute.height > 0) _itemTargets.Add((absolute, id));
        if (!hover) return;
        var detail = string.IsNullOrEmpty(details) ? item?.Description : details;
        Tip(title, $"{id}{(string.IsNullOrWhiteSpace(detail) ? "" : "\n" + detail)}\n" +
            L($"{_plugin.RecipeKey} / LMB · Recipes   {_plugin.UsageKey} / RMB · Uses   {_plugin.BookmarkKey} · Bookmark",
              $"{_plugin.RecipeKey}／左键 · 配方   {_plugin.UsageKey}／右键 · 用途   {_plugin.BookmarkKey} · 书签"));
        if (e.type != EventType.MouseDown || (PlayerCamera.main && PlayerCamera.main.dragItem)) return;
        if (e.button == 2 || e.alt) ToggleBookmark(id);
        else if (!ModalOpen && recipeBookmark == null && item is { Kind: CatalogItemKind.Item } && NeiActions.CanUse && e.control && (e.button == 0 || e.button == 1))
        {
            var error = NeiActions.Give(item.Id, e.shift ? 16 : (e.button == 1 ? 4 : 1), out int dropped);
            Status(error ?? (dropped > 0 ? L($"Given; {dropped} left nearby", $"已给予；{dropped} 件放在地上") : L("Item given", "已给予物品")));
        }
        else if (e.button == 0 || e.button == 1) Lookup(id, e.button == 1);
        e.Use();
    }

    private void DrawInspector(Rect p)
    {
        NeiSkin.Panel(p);
        var all = _item == null ? _catalog.Recipes : _usage ? _catalog.GetRecipesUsing(_item) : _catalog.GetRecipesProducing(_item);
        var categories = (_item == null ? _catalog.RecipeCategories : _catalog.RecipeCategories.Where(c => all.Any(r => SameId(r.CategoryId, c.Id)))).ToArray();
        _catalog.TryGetItem(_item ?? string.Empty, out var subject);
        string name = subject?.DisplayName ?? _item ?? L("All machines", "全部机器");
        Header(p, name, _item == null ? "NEI" : _usage ? L("Uses", "用途") : L("Recipes", "配方"));
        float x = p.x + 8, w = p.width - 16, y = p.y + 31;
        if (NeiSkin.Button(new Rect(x, y, 64, 23), L("Back", "返回"))) { Back(); return; }
        if (NeiSkin.Button(new Rect(p.xMax - 71, y, 63, 23), L("Close", "关闭"))) { _open = false; _back.Clear(); return; }
        float mid = x + 68;
        if (_item != null && w >= 245)
        {
            float half = (w - 138) / 2;
            if (NeiSkin.Tab(new Rect(mid, y, half, 23), L("Recipes", "配方"), !_usage)) { Lookup(_item, false); return; }
            if (NeiSkin.Tab(new Rect(mid + half + 4, y, half, 23), L("Uses", "用途"), _usage)) { Lookup(_item, true); return; }
        }
        y += 28;
        int categoryIndex = Array.FindIndex(categories, c => SameId(c.Id, _category));
        if (_category != null && categoryIndex < 0) _category = null;
        int choice = categoryIndex + 1;
        if (NeiSkin.Button(new Rect(x, y, 24, 23), "‹", categories.Length != 0)) SetCategory(categories, Wrap(choice - 1, categories.Length + 1));
        if (NeiSkin.Button(new Rect(p.xMax - 32, y, 24, 23), "›", categories.Length != 0)) SetCategory(categories, Wrap(choice + 1, categories.Length + 1));
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
        if (NeiSkin.Button(new Rect(x, y, 24, 23), "‹", recipes.Count > 1)) StepRecipe(-1, recipes.Count);
        if (NeiSkin.Button(new Rect(p.xMax - 32, y, 24, 23), "›", recipes.Count > 1)) StepRecipe(1, recipes.Count);
        Recipe recipe = recipes[_recipeIndex];
        string recipeCategory = categories.FirstOrDefault(c => c.Id == recipe.CategoryId)?.Name ?? recipe.CategoryId;
        NeiSkin.Label(new Rect(x + 25, y, w - 50, 23), $"{_recipeIndex + 1}/{recipes.Count}  ·  {recipeCategory}", TextAnchor.MiddleCenter);
        y += 27;
        float quarter = w / 4;
        if (NeiSkin.Tab(new Rect(x, y, quarter - 2, 22), L("Details", "详情"), !_showPlan)) _showPlan = false;
        if (NeiSkin.Tab(new Rect(x + quarter, y, quarter - 2, 22), L("Recipe tree", "配方树"), _showPlan)) _showPlan = true;
        if (NeiSkin.Button(new Rect(x + 2 * quarter, y, quarter - 2, 22), L("Bookmark", "收藏配方"), selected: _bookmarks.Contains((_group, RecipeKey(recipe))))) ToggleBookmark(RecipeKey(recipe));
        if (NeiSkin.Button(new Rect(x + 3 * quarter, y, quarter, 22), L("Craftable", "可制作"), selected: _craftableOnly)) { _craftableOnly = !_craftableOnly; _recipeIndex = 0; }
        y += 27;
        bool native = recipe.IsNative && recipe.NativeIndex >= 0;
        float footer = native ? 33 : 9;
        var viewport = new Rect(x, y, w, p.yMax - y - footer);
        if (viewport.height >= 35)
        {
            if (_showPlan) DrawPlan(viewport, recipe);
            else DrawRecipeBody(viewport, recipe);
        }
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
            '<' => string.Join(" ", recipe.Inputs.Select(i => i.ItemId + " " + Display(i.ItemId) + " " + i.DisplayName + " " + i.QualityId)),
            '>' => string.Join(" ", recipe.Outputs.Select(o => o.ItemId + " " + Display(o.ItemId) + " " + o.DisplayName)),
            '^' => recipe.MachineId + " " + Display(recipe.MachineId) + " " + string.Join(" ", recipe.Inputs.Where(i => i.IsCatalyst).Select(i => Display(i.ItemId) + " " + i.DisplayName)),
            '~' => names,
            '@' => recipe.Source,
            _ => null
        });
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

    private void DrawPlan(Rect viewport, Recipe recipe)
    {
        if (!ReferenceEquals(recipe, _plannedRecipe) || Time.unscaledTime >= _planUntil)
        {
            _plannedRecipe = recipe;
            _planUntil = Time.unscaledTime + .8f;
            _plan.Clear();
            BuildPlan(recipe, 0, 1f, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
        var content = new Rect(0, 0, viewport.width - 18, Mathf.Max(viewport.height, 42 + _plan.Count * 24));
        _recipeScroll = GUI.BeginScrollView(viewport, _recipeScroll, content, false, content.height > viewport.height);
        try
        {
            NeiSkin.Label(new Rect(4, 0, content.width - 8, 22), L("One route per ingredient · planning only", "每种原料展示一条路径 · 不自动制作"));
            for (int i = 0; i < _plan.Count; i++)
            {
                var node = _plan[i];
                var row = new Rect(5 + node.Depth * 10, 24 + i * 24, content.width - 10 - node.Depth * 10, 21);
                string caption = (node.Available == true ? "+ " : node.Available == false ? "– " : "· ") + node.Amount + " " + node.Label;
                if (node.Item != null)
                {
                    if (NeiSkin.Button(row, caption)) { Lookup(node.Item, false); break; }
                }
                else NeiSkin.Label(row, caption, muted: true);
            }
        }
        finally { GUI.EndScrollView(); }
    }

    private void BuildPlan(Recipe recipe, int depth, float batches, HashSet<string> active)
    {
        if (depth >= 6 || _plan.Count >= 80 || !active.Add(RecipeKey(recipe))) return;
        List<Item>? matching = null;
        if (depth == 0 && recipe.NativeRecipe is { } native)
        {
            try { matching = native.GetItemsForRecipeThorough(); }
            catch (Exception) { /* The plan is available even without a world. */ }
        }
        for (int i = 0; i < recipe.Inputs.Count && _plan.Count < 80; i++)
        {
            var ingredient = recipe.Inputs[i];
            float amount = ingredient.Amount * (ingredient.IsCatalyst || ingredient.MatchesAny ? 1 : batches);
            string name = string.IsNullOrWhiteSpace(ingredient.DisplayName) ? Display(ingredient.ItemId) : ingredient.DisplayName;
            string? id = ingredient.MatchesAny || string.IsNullOrEmpty(ingredient.ItemId) ? null : ingredient.BrowseId;
            bool? available = matching != null && i < matching.Count ? matching[i] != null : null;
            _plan.Add((depth, name, Amount(amount, ingredient.IsLiquid) ?? "×1", id, available));
            if (id == null || ingredient.IsCatalyst || available == true) continue;
            var next = _catalog.GetRecipesProducing(id).FirstOrDefault(r => !active.Contains(RecipeKey(r)));
            if (next == null) continue;
            int yield = next.Outputs.FirstOrDefault(output => SameId(output.BrowseId, id))?.Amount ?? 1;
            BuildPlan(next, depth + 1, Mathf.Ceil(amount / Mathf.Max(1, yield)), active);
        }
        active.Remove(RecipeKey(recipe));
    }

    private void DrawRecipeBody(Rect viewport, Recipe recipe)
    {
        const float cell = 37;
        var inputs = recipe.Inputs;
        var outputs = recipe.Outputs;
        int cols = Mathf.Max(1, (int)((viewport.width - 26) / cell));
        float extra = string.IsNullOrWhiteSpace(recipe.Description) ? 0 : 69;
        float h = 58 + extra + (Mathf.Max(1, (inputs.Count + cols - 1) / cols) + Mathf.Max(1, (outputs.Count + cols - 1) / cols)) * cell;
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
            y = DrawIngredients(inputs, L("Inputs + tools", "输入与工具"), y, cell, cols, viewport, offset);
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
            y += Mathf.Max(1, (outputs.Count + cols - 1) / cols) * cell + 7;
            if (extra > 0)
            {
                var description = recipe.Description;
                var style = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
                GUI.Label(new Rect(4, y, content.width - 8, extra), description, style);
            }
        }
        finally { GUI.EndScrollView(); }
    }

    private float DrawIngredients(IReadOnlyList<RecipeIngredient> items, string heading, float y, float cell, int cols, Rect viewport, Vector2 offset)
    {
        NeiSkin.Label(new Rect(4, y, viewport.width - 26, 19), heading);
        y += 20;
        if (items.Count == 0) NeiSkin.Label(new Rect(4, y, viewport.width - 26, cell), "—", muted: true);
        for (int i = 0; i < items.Count; i++)
        {
            var ingredient = items[i];
            string? detail = ingredient.IsCatalyst ? L("Tool / not consumed", "工具／不消耗") : null;
            if (ingredient.MinimumCondition > 0 && !ingredient.IsLiquid) detail += "\n" + L("Minimum condition: ", "最低耐久：") + ingredient.MinimumCondition.ToString("P0", CultureInfo.InvariantCulture);
            var rect = IngredientRect(i, y, cell, cols);
            if ((ingredient.MatchesAny || string.IsNullOrEmpty(ingredient.ItemId)) && !_catalog.TryGetItem(ingredient.BrowseId, out _))
            {
                NeiSkin.Slot(rect, rect.Contains(Event.current.mousePosition), ingredient.IsCatalyst);
                NeiSkin.Label(rect, "*", TextAnchor.MiddleCenter);
                if (rect.Contains(Event.current.mousePosition) && viewport.Contains(Event.current.mousePosition + offset))
                    Tip(string.IsNullOrEmpty(ingredient.DisplayName) ? ingredient.QualityId : ingredient.DisplayName,
                        L("Any matching item · ", "任一匹配物品 · ") + ingredient.Amount.ToString("0.##", CultureInfo.InvariantCulture) + (detail == null ? "" : "\n" + detail));
                continue;
            }
            ItemSlot(rect, ingredient.BrowseId, ingredient.IsCatalyst, amount: Amount(ingredient.Amount, ingredient.IsLiquid),
                fallback: ingredient.DisplayName, clipping: viewport, globalOffset: offset, details: detail);
        }
        return y + Mathf.Max(1, (items.Count + cols - 1) / cols) * cell + 7;
    }

    private static Rect IngredientRect(int i, float y, float cell, int cols) =>
        new(4 + i % cols * cell, y + i / cols * cell, cell - 2, cell - 2);
    private static string? Amount(float amount, bool liquid) => amount == 1 && !liquid ? null :
        amount.ToString("0.##", CultureInfo.InvariantCulture) + (liquid ? "mL" : "");

    private void DrawOptions(Rect p)
    {
        NeiSkin.Panel(p);
        Header(p, L("NEI options", "NEI 选项"), "NEI");
        float x = p.x + 12, w = p.width - 24, y = p.y + 38;
        if (NeiSkin.Button(new Rect(x, y, w, 27), _showGrid.Value ? L("Item catalog: On", "物品目录：开") : L("Item catalog: Off", "物品目录：关"))) _showGrid.Value = !_showGrid.Value;
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27), _showHistory.Value ? L("History strip: On", "浏览记录：开") : L("History strip: Off", "浏览记录：关"))) _showHistory.Value = !_showHistory.Value;
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27), NeiActions.Enabled ? L("Mode: Local cheat", "模式：单机作弊") : L("Mode: Recipe", "模式：配方"), NeiActions.CanEnter || NeiActions.Enabled, NeiActions.Enabled))
            NeiActions.Enabled = !NeiActions.Enabled;
        y += 32;
        if (NeiActions.Enabled)
        {
            if (NeiSkin.Button(new Rect(x, y, w, 27), L("Heal local character", "治疗本地角色"), NeiActions.CanUse))
                Status(NeiActions.Heal() ?? L("Character healed", "已治疗角色"));
            y += 29;
            if (_plugin.IsNativeInventory)
            {
                int[] speeds = { 1, 5, 20 };
                for (int i = 0; i < speeds.Length; i++)
                    if (NeiSkin.Button(new Rect(x + i * w / 3, y, w / 3 - 3, 24), $"×{speeds[i]}", NeiActions.CanUse))
                        Status(NeiActions.SetSpeed(speeds[i]) ?? L("Game speed changed", "已更改游戏速度"));
                y += 27;
            }
        }
        NeiSkin.Label(new Rect(x, y, w, 24), L("Left click: recipes · Right click: uses · Middle: bookmark", "左键查配方 · 右键查用途 · 中键加书签"));
        NeiSkin.Label(new Rect(x, y + 26, w, 24), L("Cheat mode: Ctrl+click to give; add Shift for 16", "作弊模式：Ctrl+点击给物品；加 Shift 一次给 16 件"));
        if (NeiSkin.Button(new Rect(x, p.yMax - 68, w, 26), L("Hide overlay", "隐藏 NEI"))) { ResetView(); _plugin.HideOverlay(); }
        if (NeiSkin.Button(new Rect(x, p.yMax - 35, w, 26), L("Done", "完成"))) _options = false;
    }

    private static void Header(Rect p, string heading, string mark)
    {
        if (p.width >= 108) NeiSkin.Label(new Rect(p.x + 8, p.y + 5, p.width - 61, 19), heading);
        NeiSkin.Label(new Rect(p.xMax - Mathf.Min(55, p.width - 5), p.y + 5, Mathf.Min(48, p.width - 7), 19), mark, TextAnchor.MiddleRight, true);
    }

    private static void DrawSprite(Rect rect, Sprite? sprite, Color tint, string fallback)
    {
        if (Event.current.type != EventType.Repaint) return;
        var originalColor = GUI.color;
        GUI.color = tint;
        if (!sprite || !sprite!.texture)
        {
            NeiSkin.Label(rect, string.IsNullOrEmpty(fallback) ? "?" : fallback.Substring(0, 1).ToUpperInvariant(), TextAnchor.MiddleCenter, true);
            GUI.color = originalColor;
            return;
        }
        var tex = sprite.texture;
        Rect src;
        try { src = sprite.textureRect; }
        catch (UnityException) { src = sprite.rect; }
        if (src.width <= 0 || src.height <= 0) { GUI.color = originalColor; return; }
        var ratio = Mathf.Min(rect.width / src.width, rect.height / src.height);
        float w = src.width * ratio, h = src.height * ratio;
        var dst = new Rect(rect.center.x - w / 2, rect.center.y - h / 2, w, h);
        GUI.DrawTextureWithTexCoords(dst, tex, new Rect(src.x / tex.width, src.y / tex.height, src.width / tex.width, src.height / tex.height), true);
        GUI.color = originalColor;
    }

    private void SetCategory(RecipeCategory[] categories, int choice)
    {
        _category = choice == 0 ? null : categories[choice - 1].Id;
        _recipeIndex = 0; _recipeScroll = Vector2.zero;
    }
    private void StepRecipe(int delta, int count) { _recipeIndex = Wrap(_recipeIndex + delta, count); _recipeScroll = Vector2.zero; }
    private void Tip(string title, string? text) { _tipTitle = title; _tipBody = text; }
    private void Status(string text) { _status = text; _statusUntil = Time.unscaledTime + 3.5f; }
    private static int Wrap(int value, int count) => (value % count + count) % count;
    private void Dim()
    {
        var color = GUI.color;
        GUI.color = new Color(0.07f, 0.09f, 0.14f, 0.61f);
        GUI.DrawTexture(new Rect(0, 0, _width, _height), Texture2D.whiteTexture);
        GUI.color = color;
    }
}
