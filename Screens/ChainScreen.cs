using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private static readonly Color BookmarkProductTint = new Color32(83, 145, 218, 112);
    private static readonly Color BookmarkMaterialTint = new Color32(83, 175, 112, 102);
    private readonly Dictionary<string, (BookmarkChainBalanceResult Plan, Dictionary<BookmarkBook.Entry, BookmarkChainBalanceStep> Steps)> _bookmarkBalance = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Recipe> _bookmarkRecipes = new(StringComparer.Ordinal);
    private int _bookmarkRecipeRevision = -1;
    private List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint)>? _bookmarkTip;

    private int _chainTipOffset, _chainTipMaxOffset;
    private (string Key, bool Details, string Title, string Hint, List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint)> Blocks)? _cachedChainTip;
    private Dictionary<string, int> _bookmarkStock = new(StringComparer.OrdinalIgnoreCase);

    private void RefreshBookmarkStock()
    {
        if (!ShowLeft || !_book.Page(_group).Any(entry => _book.IsChainEnabled(_group, entry.Group))) return;
        var camera = PlayerCamera.main;
        var stock = CarriedQuantities(camera ? camera.body : null);
        if (stock.Count == _bookmarkStock.Count && stock.All(pair => _bookmarkStock.TryGetValue(pair.Key, out var count) && count == pair.Value)) return;
        _bookmarkStock = stock;
        InvalidateBookmarkView();
    }

    private Recipe? BookmarkedRecipe(string key)
    {
        if (_bookmarkRecipeRevision != _catalog.Revision)
        {
            _bookmarkRecipeRevision = _catalog.Revision;
            _bookmarkRecipes.Clear();
            foreach (var recipe in _catalog.Recipes) _bookmarkRecipes.TryAdd(RecipeKey(recipe), recipe);
        }
        return _bookmarkRecipes.TryGetValue(key, out var found) ? found : null;
    }

    private static RecipeOutput? BookmarkProduct(BookmarkBook.Entry entry, Recipe recipe) =>
        recipe.Outputs.FirstOrDefault(output => SameId(output.BrowseId, entry.ProductId)) ?? recipe.Outputs.FirstOrDefault();

    private static int BookmarkBatches(BookmarkBook.Entry entry, Recipe recipe)
    {
        int output = Math.Max(1, BookmarkProduct(entry, recipe)?.Amount ?? 1);
        return entry.LegacyRecipeQuantity ? (entry.Quantity + output - 1) / output : entry.Quantity;
    }

    private bool AdjustBookmark(BookmarkBook.Entry entry, int delta)
    {
        if (entry.LegacyRecipeQuantity && BookmarkedRecipe(entry.Id) is { } recipe)
        {
            return _book.StepQuantity(entry, delta, BookmarkBatches(entry, recipe));
        }
        return _book.StepQuantity(entry, delta);
    }

    private BookmarkBook.Entry[] BookmarkGroupEntries(string group) => _book.Page(_group).Where(entry => entry.Group == group).ToArray();

    private BookmarkChainBalanceEntry BalanceEntry(BookmarkBook.Entry entry)
    {
        var recipe = entry.IsRecipe ? BookmarkedRecipe(entry.Id) : null;
        return new BookmarkChainBalanceEntry(recipe == null ? entry.Id : BookmarkProduct(entry, recipe)?.BrowseId ?? entry.Id,
            recipe == null ? entry.Quantity : BookmarkBatches(entry, recipe), recipe, entry.IsRecipe, entry.Variants);
    }

    private (BookmarkChainBalanceResult Plan, Dictionary<BookmarkBook.Entry, BookmarkChainBalanceStep> Steps) BookmarkBalance(string group)
    {
        if (_bookmarkBalance.TryGetValue(group, out var cached)) return cached;
        var entries = BookmarkGroupEntries(group);
        var plan = BookmarkChainBalance.Calculate(_catalog, entries.Select(BalanceEntry), _bookmarkStock);
        cached = (plan, plan.Steps.ToDictionary(step => entries[step.EntryIndex]));
        return _bookmarkBalance[group] = cached;
    }

    private IEnumerable<BookmarkCell> CellsForBookmark(BookmarkBook.Entry entry, bool compact)
    {
        bool chain = _book.IsChainEnabled(_group, entry.Group);
        if (!entry.IsRecipe || BookmarkedRecipe(entry.Id) is not { } recipe)
        {
            yield return new BookmarkCell(entry, entry.Id, -1, entry.Quantity, null, details: entry.IsRecipe ?
                L("The saved recipe is not currently available", "已保存的配方目前不可用") : null);
            yield break;
        }
        BookmarkChainBalanceStep? step = null;
        if (chain) BookmarkBalance(entry.Group).Steps.TryGetValue(entry, out step);
        int batches = step?.Batches ?? (chain ? 0 : BookmarkBatches(entry, recipe));
        var primary = BookmarkProduct(entry, recipe);
        bool expanded = !compact && entry.ShowRecipe;
        var outputs = expanded ? recipe.Outputs.OrderByDescending(output => ReferenceEquals(output, primary)) :
            (primary == null ? Enumerable.Empty<RecipeOutput>() : new[] { primary });
        if (primary == null) yield return new BookmarkCell(entry, entry.Id, -1, batches, $"×{batches}", details: recipe.Name);
        foreach (var output in outputs)
        {
            var planned = step?.Outputs.FirstOrDefault(amount => SameId(amount.ItemId, output.BrowseId));
            yield return new BookmarkCell(entry, output.BrowseId, -1, (double)output.Amount * batches,
                ReferenceEquals(output, primary) && (chain || entry.ShowQuantity) ? $"×{batches}" : null,
                planned?.NetQuantity > 0 ? BookmarkProductTint : null, recipe.Name + " · " + L("Left: saved recipe", "左键：查看收藏的配方"));
        }
        if (!expanded) yield break;
        foreach (int index in OrderedIngredients(entry, recipe))
        {
            var ingredient = recipe.Inputs[index];
            var planned = step?.Inputs.FirstOrDefault(amount => amount.IngredientIndex == index);
            string id = planned?.ItemId ?? (ingredient.MatchesAny ? SelectedBookmarkVariant(entry, index, ingredient) ?? ingredient.BrowseId : ingredient.BrowseId);
            double amount = planned?.Quantity ?? (ingredient.MatchesAny && !ingredient.IsLiquid ? 1 : ingredient.Amount) * (ingredient.IsCatalyst ? 1 : batches);
            string? details = ingredient.MatchesAny ? RequirementTitle(ingredient) + " · " + L("Shift+wheel: choose material", "Shift＋滚轮：切换材料") : null;
            if (ingredient.IsCatalyst) details = (details == null ? "" : details + "\n") + L("Tool · retained", "工具 · 不消耗");
            yield return new BookmarkCell(entry, id, index, amount, null, planned?.NetQuantity > 0 ? BookmarkMaterialTint : null, details);
        }
    }

    private static IEnumerable<int> OrderedIngredients(BookmarkBook.Entry entry, Recipe recipe) =>
        entry.IngredientOrder.Where(slot => slot < recipe.Inputs.Count).Concat(Enumerable.Range(0, recipe.Inputs.Count)).Distinct();

    private string? SelectedBookmarkVariant(BookmarkBook.Entry entry, int index, RecipeIngredient ingredient)
    {
        var matches = _catalog.GetMatchingItems(ingredient);
        return matches.FirstOrDefault(item => SameId(item.BrowseId, entry.Variant(index)))?.BrowseId ?? matches.FirstOrDefault()?.BrowseId;
    }

    private bool CycleBookmarkVariant(BookmarkCell cell, int direction)
    {
        if (BookmarkedRecipe(cell.Entry.Id) is not { } recipe) return false;
        if (cell.Ingredient >= 0 && cell.Ingredient < recipe.Inputs.Count && recipe.Inputs[cell.Ingredient].MatchesAny)
        {
            var matches = _catalog.GetMatchingItems(recipe.Inputs[cell.Ingredient]).ToArray();
            if (matches.Length == 0) return true;
            // Include the unpinned slot in the cycle so an explicit choice is reversible.
            string? pinned = cell.Entry.Variant(cell.Ingredient);
            int current = Array.FindIndex(matches, item => SameId(item.BrowseId, pinned));
            int next = Wrap(current + 1 + direction, matches.Length + 1) - 1;
            if (_book.SetVariant(cell.Entry, cell.Ingredient, next < 0 ? null : matches[next].BrowseId)) SaveBookmarks();
            return true;
        }
        return false;
    }

    private bool ReorderBookmarkIngredients(BookmarkCell source, BookmarkCell target, bool before)
    {
        if (source.Ingredient < 0 || target.Ingredient < 0 || source.Ingredient == target.Ingredient || BookmarkedRecipe(source.Entry.Id) is not { } recipe) return false;
        var slots = OrderedIngredients(source.Entry, recipe).ToList();
        slots.Remove(source.Ingredient);
        slots.Insert(slots.IndexOf(target.Ingredient) + (before ? 0 : 1), source.Ingredient);
        return _book.SetIngredientOrder(source.Entry, slots);
    }

    private void ClickBookmark(BookmarkCell cell, int button, bool shift, bool alt)
    {
        if (PlayerCamera.main && PlayerCamera.main.dragItem) return;
        var entry = cell.Entry;
        if (alt)
        {
            if (_book.Split(entry)) SaveBookmarks();
            return;
        }
        if (entry.IsRecipe && cell.Ingredient < 0 && button == 0)
        {
            Lookup(entry.Id, false);
            return;
        }
        _catalog.TryGetItem(cell.Id, out var item);
        if (CanGiveSlot(!entry.IsRecipe, cell.Id, item, button, shift, alt)) GiveSlot(item!, button);
        else Lookup(cell.Id, button == 1);
    }

    private bool PageChainEnabled() => _book.IsChainEnabled(_group) || _book.Page(_group).Any(entry => _book.IsChainEnabled(_group, entry.Group));

    private void BookmarkBracketTip(string group, bool details)
    {
        if (group.Length == 0)
        {
            Tip(L("Create bookmark group", "创建书签组"), L("Drag along this gutter to select entries", "沿左侧拖动，框选要加入分组的书签"));
            return;
        }
        string title = L("Bookmark group", "书签组");
        if (_book.IsChainEnabled(_group, group)) BookmarkChainTip(title, new[] { group }, details);
        else Tip(title, L("Left: grid/queue · Right: enable chain\nAlt+drag: resize · Shift+drag: reorder\nShift+C: craft · Ctrl+C: copy · Shift+A: remove group", "左键：网格／队列 · 右键：启用合成链\nAlt＋拖动：调整范围 · Shift＋拖动：排序\nShift＋C：制作 · Ctrl＋C：复制 · Shift＋A：删除整组"));
    }

    private void BookmarkPageChainTip(bool details) => BookmarkChainTip(L("Bookmark page chain", "整页书签合成链"),
        _book.Page(_group).Select(entry => entry.Group).Distinct().Where(group => _book.IsChainEnabled(_group, group)), details);

    private void BookmarkChainTip(string title, IEnumerable<string> groups, bool details)
    {
        var e = Event.current;
        if (e.type == EventType.ScrollWheel && !e.control && !e.alt && _chainTipMaxOffset > 0)
        {
            _chainTipOffset = Math.Clamp(_chainTipOffset + Math.Sign(e.delta.y), 0, _chainTipMaxOffset);
            e.Use();
            return;
        }
        if (e.type != EventType.Repaint) return;
        string[] selected = groups.ToArray();
        string key = title + "\n" + string.Join("\n", selected);
        if (_cachedChainTip is { } cache && cache.Key == key && cache.Details == details)
        {
            Tip(cache.Title, cache.Hint);
            _bookmarkTip = cache.Blocks.Count == 0 ? null : cache.Blocks;
            return;
        }
        _chainTipOffset = _chainTipMaxOffset = 0;
        var plans = selected.Select(group => BookmarkBalance(group).Plan).ToArray();
        var blocks = new List<(string Title, IReadOnlyList<BookmarkChainBalanceAmount> Items, Color? Tint)>();
        void Section(string label, IEnumerable<BookmarkChainBalanceAmount> amounts, Color? tint, bool sum = true)
        {
            var items = (sum ? amounts.GroupBy(amount => (amount.ItemId, amount.IsCatalyst)).Select(row =>
            {
                var a = row.First();
                return new BookmarkChainBalanceAmount(a.ItemId, row.Sum(m => m.Quantity), 0, 0, a.IsLiquid, a.IsRequirement, a.Ingredient);
            }) : amounts).ToArray();
            if (items.Length > 0) blocks.Add((label, items, tint));
        }
        Section(L("Products", "产物"), plans.SelectMany(plan => plan.Results), BookmarkProductTint);
        Section(L("Materials", "原料"), plans.SelectMany(plan => plan.Inputs).Where(amount => !amount.IsCatalyst), BookmarkMaterialTint);
        Section(L("Tools · not consumed", "工具 · 不消耗"), plans.SelectMany(plan => plan.Inputs).Where(amount => amount.IsCatalyst), BookmarkMaterialTint);
        Section(L("Surplus", "余料"), plans.SelectMany(plan => plan.Surplus), null);
        // A page summary is the sum of independent lane bills. Do not sum per-lane
        // shortages: that would credit the same carried stack once per group.
        if (plans.Length == 1) Section(L("Short of carried / bookmarked stock", "随身和书签库存仍缺"), plans[0].Missing, new Color32(182, 93, 87, 105));
        if (details)
            foreach (var step in plans.SelectMany(plan => plan.Steps))
                Section($"×{step.Batches}  {step.Recipe.Name}", step.Outputs.Concat(step.Inputs), null, sum: false);
        var notes = plans.SelectMany(plan => plan.Diagnostics).Where(issue => issue.Code != BookmarkChainDiagnosticCode.MissingMaterial)
            .Select(issue => issue.Message).Distinct().Take(3).ToArray();
        string hint = details ? L("Recipe steps · wheel to scroll", "制作步骤 · 滚轮查看更多") : L("Hold Shift for steps · wheel to scroll", "按 Shift 查看步骤 · 滚轮查看更多");
        if (blocks.Count == 0) hint = L("No recipe totals to display", "没有可显示的配方用量") + (notes.Length == 0 ? "" : "\n" + string.Join("\n", notes));
        else if (notes.Length != 0) blocks.Add((string.Join(" · ", notes), Array.Empty<BookmarkChainBalanceAmount>(), null));
        Tip(title, hint);
        _cachedChainTip = (key, details, title, hint, blocks);
        _bookmarkTip = blocks.Count > 0 ? blocks : null;
    }

    private void DrawBookmarkChainTip(Vector2 mouse)
    {
        const int slot = 31, heading = 19;
        var blocks = _bookmarkTip!;
        int columns = Math.Max(3, Math.Min(11, (int)((_width - 24) / slot)));
        float width = columns * slot + 12;
        float maxHeight = _height - 12;
        var counts = blocks.Select(block => block.Items.Count).ToArray();
        int rowCount = counts.Sum(count => Math.Max(1, (count + columns - 1) / columns));
        float naturalHeight = 49 + blocks.Count * heading + counts.Sum(count => (count + columns - 1) / columns * slot);
        float height = Math.Min(maxHeight, naturalHeight);
        _chainTipMaxOffset = naturalHeight <= maxHeight ? 0 : rowCount - 1;
        _chainTipOffset = Math.Clamp(_chainTipOffset, 0, _chainTipMaxOffset);
        float x = Mathf.Clamp(mouse.x + 13, 6, _width - width - 6);
        float y = mouse.y + 14 + height <= _height - 6 ? mouse.y + 14 : Mathf.Clamp(mouse.y - height - 10, 6, _height - height - 6);
        var box = new Rect(x, y, width, height);
        NeiSkin.BookmarkTint(box, new Color32(28, 21, 36, 246));
        NeiSkin.BookmarkTint(new Rect(x, y, width, 2), new Color32(91, 164, 111, 255));
        GUI.BeginGroup(box);
        try
        {
            NeiSkin.Label(new Rect(6, 4, width - 12, 20), _tipTitle ?? "NEI");
            NeiSkin.Label(new Rect(6, 22, width - 12, 18), _tipBody ?? "", muted: true);
            float top = 43;
            int previous = -1;
            foreach (var (section, start, count) in BookmarkBoardPaging.Rows(counts, columns).Skip(_chainTipOffset))
            {
                var block = blocks[section];
                if (section != previous)
                {
                    if (top + heading > height - 6) break;
                    NeiSkin.Label(new Rect(6, top, width - 12, heading), block.Title);
                    top += heading;
                    previous = section;
                }
                if (count != 0 && top + slot > height - 6) break;
                for (int i = 0; i < count; i++)
                {
                    var a = block.Items[start + i];
                    var r = new Rect(6 + i * slot, top, slot - 1, slot - 1);
                    _catalog.TryGetItem(a.ItemId, out var item);
                    var caption = a.IsRequirement && a.Ingredient != null ? RequirementTitle(a.Ingredient) : item?.DisplayName ?? a.ItemId;
                    if (block.Tint is { } tint) NeiSkin.BookmarkTint(r, tint);
                    DrawSprite(new Rect(r.x + 3, r.y + 2, r.width - 6, r.height - 6), item?.Icon, item?.IconTint ?? Color.white, caption);
                    NeiSkin.Label(new Rect(r.x + 1, r.yMax - 13, r.width - 2, 13), BookmarkAmount(a.ItemId, a.Quantity), TextAnchor.MiddleRight);
                }
                if (count != 0) top += slot;
            }
            if (_chainTipMaxOffset > 0) NeiSkin.Label(new Rect(width - 88, 4, 81, heading), $"↕ {_chainTipOffset + 1}/{rowCount}", TextAnchor.MiddleRight);
        }
        finally { GUI.EndGroup(); }
    }

    private Recipe? FavoriteForProduct(string id) => _book.Favorites.Select(BookmarkedRecipe).FirstOrDefault(recipe =>
        recipe?.Outputs.Any(output => SameId(output.BrowseId, id)) == true);

    private void ToggleFavorite(Recipe recipe, string key)
    {
        if (!_book.IsFavorite(key))
        {
            // There is one preferred route per product. Otherwise selecting the heart on a
            // second recipe would leave new orders following an arbitrary earlier favorite.
            var products = recipe.Outputs.Select(output => output.BrowseId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string old in _book.Favorites.Where(k => BookmarkedRecipe(k)?.Outputs.Any(output => products.Contains(output.BrowseId)) == true).ToArray())
                _book.ToggleFavorite(old);
        }
        if (_book.ToggleFavorite(key)) SaveBookmarks();
    }

    internal bool OrderFavoriteAt(Vector2 pointer)
    {
        pointer /= _scale;
        if (!ShowRight || !_right.Contains(pointer) || _options) return false;
        var hit = _itemTargets.FindLast(t => t.Rect.Contains(pointer));
        if (hit.Id == null) return false;
        var recipe = FavoriteForProduct(hit.Id);
        if (recipe == null) Status(L("Mark a recipe with the heart before placing an order.", "请先点配方旁的爱心，指定要下单的配方。"));
        else BookmarkOrder(recipe, BookmarkQuantity, hit.Id);
        return true;
    }

    internal bool OrderAndInspectAt(Vector2 pointer)
    {
        if (!_open || _options || _activeRecipe == null || !_favoriteRect.Contains(pointer / _scale)) return false;
        BookmarkOrder(_activeRecipe, 1, inspect: true);
        return true;
    }

    private void BookmarkOrder(Recipe root, int quantity, string? productId = null, bool inspect = false)
    {
        var favorites = _book.Favorites.OrderBy(key => key, StringComparer.Ordinal).Select(BookmarkedRecipe).OfType<Recipe>().ToArray();
        var picked = new List<(Recipe Recipe, string? Product)>();
        var seen = new HashSet<Recipe> { root };
        picked.Add((root, root.Outputs.FirstOrDefault(output => SameId(output.BrowseId, productId ?? _item))?.BrowseId ?? root.Outputs.FirstOrDefault()?.BrowseId));
        for (int i = 0; i < picked.Count; i++)
            foreach (var input in picked[i].Recipe.Inputs)
            {
                var producer = favorites.FirstOrDefault(candidate => candidate != picked[i].Recipe && candidate.Outputs.Any(output => OutputMatches(input, output)));
                if (producer != null && seen.Add(producer))
                    picked.Add((producer, producer.Outputs.First(output => OutputMatches(input, output)).BrowseId));
            }

        string group = Guid.NewGuid().ToString("N");
        foreach (var (recipe, product) in picked)
        {
            if (_book.Add(_group, RecipeKey(recipe), recipe == root ? quantity : 1, group, productId: product, showQuantity: true) != null) continue;
            // Do not silently save a truncated order when bookmarks reach their limit.
            _book.RemoveGroup(_group, group);
            Status(L("Order could not fit on this page; no recipes were added.", "无法完整加入订单：已达书签上限或配方标识无效。"));
            return;
        }
        if (!_book.IsChainEnabled(_group, group)) _book.ToggleChain(_group, group);
        SaveBookmarks();
        _open = false;
        _bookmarkPage = int.MaxValue;
        _skipFocusFrame = true;
        if (inspect) OpenTreeGroup(group);
        else Status(L("Crafting order added to this bookmark page", "已在当前书签页添加合成订单"));
    }
}
