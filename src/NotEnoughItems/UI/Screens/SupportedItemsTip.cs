using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private enum CandidateSource { Inspector, Bookmark, Chain }

    private sealed class SupportedItems(Rect source, NativeTooltip.Content tip, string requirement,
        IReadOnlyList<CatalogItem> candidates, Action<CatalogItem>? selectCandidate, CandidateSource origin,
        RecipeIngredient? ingredient, bool expanded, bool showId, Func<bool, bool, NativeTooltip.Content> readTip)
    {
        internal readonly Rect Source = source;
        internal readonly string Requirement = requirement;
        internal readonly IReadOnlyList<CatalogItem> Candidates = candidates;
        internal readonly Action<CatalogItem>? SelectCandidate = selectCandidate;
        internal readonly CandidateSource Origin = origin;
        internal readonly RecipeIngredient? Ingredient = ingredient;
        private NativeTooltip.Content _tip = tip;
        private bool _expanded = expanded, _showId = showId;
        internal NativeTooltip.Content Tooltip(bool current, bool showId)
        {
            if (current != _expanded || showId != _showId)
            {
                _tip = readTip(current, showId);
                _expanded = current;
                _showId = showId;
            }
            return _tip;
        }
    }

    private SupportedItems? _supportedItemsHover;
    private SupportedItems? _pinnedItems;
    private Rect _supportedItemsBox, _supportedItemsGrid;
    private int _supportedItemsPage, _supportedItemsColumns, _supportedItemsStart, _supportedItemsEnd, _supportedItemsPages;
    private float _supportedItemsCell;

    internal static bool SelectionShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static bool ItemDetailsHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
        KeyBinds.GetBind("expanddesc") != KeyCode.None && Input.GetKey(KeyBinds.GetBind("expanddesc"));

    private void ItemHover(Rect source, string title, CatalogItem? item, string? details,
        IReadOnlyList<CatalogItem>? candidates = null, string? requirement = null, Action<CatalogItem>? selectCandidate = null,
        CandidateSource origin = CandidateSource.Inspector, RecipeIngredient? ingredient = null, float? liquidVolume = null)
    {
        if (PropertyQuery(ingredient) != null)
        {
            string key = _plugin.RecipeKey;
            var hint = L("recipe.candidates.property_hint", key);
            details = string.IsNullOrEmpty(details) ? hint : details + "\n" + hint;
        }
        bool expanded = ItemDetailsHeld || PlayerCamera.alwaysExpandDescriptions;
        var tooltip = NativeTooltip.ForItem(title, item, details, expanded, _plugin.Chinese, liquidVolume, _showTooltipId.Value);
        if (string.IsNullOrWhiteSpace(tooltip.Name)) return;
        _itemTip = tooltip;
        _itemTipSource = source;
        if (candidates is { Count: > 0 } && _pinnedItems == null && !_options && !_layoutEditing && !_plugin.NativePointerActive &&
            (_pinnedChainTip == null || origin == CandidateSource.Chain))
        {
            var hover = new SupportedItems(source, tooltip, NativeTooltip.PlainGameText(requirement ?? tooltip.Name),
                candidates, selectCandidate, origin, ingredient, expanded, _showTooltipId.Value,
                (held, showId) => NativeTooltip.ForItem(title, item, details, held, _plugin.Chinese, liquidVolume, showId));
            _supportedItemsHover = hover;
            if (TooltipCanInteract(SelectionShiftHeld, available: true, nested: origin == CandidateSource.Chain))
            {
                ClearItemTooltipInput();
                _pinnedItems = hover;
                _supportedItemsPage = 0;
            }
        }
    }

    private void ClearSupportedItemsTip()
    {
        _supportedItemsHover = null;
        _pinnedItems = null;
        _supportedItemsBox = _supportedItemsGrid = default;
        _supportedCopyId = null;
        _supportedItemsPage = _supportedItemsPages = 0;
    }

    private bool CanUseSupportedItems(SupportedItems items) => !_options && !_layoutEditing && !_plugin.NativePointerActive && SelectionShiftHeld &&
        (items.Origin switch { CandidateSource.Bookmark => ShowLeft, CandidateSource.Chain => false, _ => _open || _treeOpen });

    private bool SupportedItemsHit(Vector2 pointer) =>
        _pinnedItems is { } items && CanUseSupportedItems(items) && _supportedItemsBox.Contains(pointer);

    // Handle the overlay before any sidebar controls or scrollers process this event.
    private void HandleSupportedItemsInput(Event e)
    {
        if (_pinnedItems is not { } items) return;
        if (!CanUseSupportedItems(items))
        {
            ClearSupportedItemsTip();
            return;
        }
        // A bookmark slot already owns Shift+wheel; do not consume it even if a
        // narrow viewport forced the candidate popup to overlap its source.
        if (e.type == EventType.ScrollWheel && items.Origin == CandidateSource.Bookmark && items.Source.Contains(e.mousePosition))
        {
            ClearSupportedItemsTip();
            return;
        }
        if (e.type == EventType.ScrollWheel && (_supportedItemsBox.Contains(e.mousePosition) ||
            (items.Origin == CandidateSource.Inspector && items.Source.Contains(e.mousePosition))))
        {
            if (_supportedItemsPages > 1 && e.delta.y != 0)
            {
                _supportedItemsPage = Wrap(_supportedItemsPage + Math.Sign(e.delta.y), _supportedItemsPages);
                // Don't use the old page's hit map if another input arrives before repaint.
                _supportedItemsGrid = default;
            }
            e.Use();
            return;
        }
        if (!_supportedItemsBox.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown) ClearSupportedItemsTip();
            return;
        }
        if (e.type is not (EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag)) return;
        if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            int index = TooltipPlacement.CandidateAt(_supportedItemsGrid, _supportedItemsColumns, _supportedItemsCell,
                _supportedItemsStart, _supportedItemsEnd, e.mousePosition);
            if (index >= 0)
            {
                CatalogItem candidate = items.Candidates[index];
                var select = items.SelectCandidate;
                ClearSupportedItemsTip();
                if (e.button == 0 && select != null) select(candidate);
                else Lookup(candidate.BrowseId, e.button == 1);
            }
        }
        e.Use();
    }

    private bool DrawSupportedItemsTip(Vector2 pointer)
    {
        var items = _pinnedItems ?? _supportedItemsHover;
        if (items == null) return false;

        const float margin = 4f, pad = 7f, line = 19f;
        float cell = 32f * IconScale;
        var viewport = new Rect(margin, margin, _width - 2 * margin, _height - 2 * margin);
        if (viewport.width < 2 * pad + cell + 4 || viewport.height < 2 * pad + 4 * line + cell + 8) return false;
        int columns = Math.Min(10, (int)((viewport.width - 2 * pad - 4) / cell));
        float width = columns * cell + 2 * pad + 4;
        float textWidth = width - 2 * pad;
        bool expanded = ItemDetailsHeld || PlayerCamera.alwaysExpandDescriptions;
        var tip = items.Tooltip(expanded, _showTooltipId.Value);
        var title = NeiSkin.FormatItemText(tip.Name);
        var body = NeiSkin.FormatItemBody(tip);
        float nameHeight = Mathf.Min(2 * line, NeiSkin.ItemTextHeight(title.Rich, textWidth));
        var (idHeight, modHeight) = NeiSkin.ItemFooterHeights(tip, textWidth);
        float footerHeight = idHeight + modHeight;
        if (footerHeight > 0f) footerHeight += 4f;
        float available = viewport.height - 2 * pad - 2 * line - 8 - cell;
        if (nameHeight + footerHeight > available)
        {
            // Use the ordinary tooltip on very small viewports; do not leave an invisible hit map.
            if (_pinnedItems != null) ClearSupportedItemsTip();
            return false;
        }
        float descriptionHeight = body.Rich.Length == 0 ? 0f : Mathf.Min(available - nameHeight - footerHeight,
            NeiSkin.ItemTextHeight(body.Rich, textWidth, description: true) + 3);
        float header = pad + nameHeight + descriptionHeight + line + 4;
        float tail = line + footerHeight + pad + 4;
        int rows = (items.Candidates.Count + columns - 1) / columns;
        var page = TooltipPlacement.ItemListPage(rows, _supportedItemsPage, viewport.height - header - tail, cell);
        int first = page.Start * columns, end = Math.Min(items.Candidates.Count, page.End * columns);
        var size = new Vector2(width, header + (page.End - page.Start) * cell + tail);
        bool pinned = _pinnedItems != null;
        Rect box = pinned ? TooltipPlacement.AtSlot(items.Source, size, viewport) : TooltipPlacement.AtPointer(pointer, size, viewport);
        bool chainPreview = items.Origin == CandidateSource.Chain;
        // A chain candidate list is the second-level hover menu: keep it under the
        // pointer instead of pushing it outside the first-level chain frame. It is
        // paint-only, so the parent remains the input owner.
        box = TooltipPlacement.Avoid(box, viewport, RecipeNavigationBounds);
        if (chainPreview) _queuedItemTip = null; // Replaces the chain slot's plain fallback.
        var tray = new Rect(box.x + pad, box.y + header, box.width - 2 * pad, (page.End - page.Start) * cell + 4);
        var grid = new Rect(tray.x + 2, tray.y + 2, columns * cell, tray.height - 4);

        if (pinned)
        {
            _supportedItemsBox = box;
            _supportedItemsGrid = grid;
            _supportedItemsCell = cell;
            _supportedItemsColumns = columns;
            _supportedItemsPage = page.Index;
            _supportedItemsStart = first;
            _supportedItemsEnd = end;
            _supportedItemsPages = page.Pages;
        }
        int hovered = pinned ? TooltipPlacement.CandidateAt(grid, columns, cell, first, end, pointer) : -1;
        NeiSkin.TooltipBox(box);
        NeiSkin.ItemText(new Rect(box.x + pad, box.y + pad, textWidth, nameHeight), title.Rich, title.Shadow);
        if (descriptionHeight > 0f)
            NeiSkin.ItemText(new Rect(box.x + pad, box.y + pad + nameHeight, textWidth, descriptionHeight), body.Rich, body.Shadow, description: true);
        NeiSkin.Label(new Rect(box.x + pad, tray.y - line - 2, textWidth, line),
            L("recipe.candidates.accepts_count", items.Requirement, items.Candidates.Count), muted: true);
        NeiSkin.BookmarkTint(tray, new Color32(195, 191, 202, 255));
        for (int i = first; i < end; i++)
        {
            var candidate = items.Candidates[i];
            var rect = TooltipPlacement.CandidateCell(grid, columns, cell, i - first);
            NeiSkin.Icon(rect, "recipe_slot");
            if (i == hovered) NeiSkin.Slot(rect, true, true);
            DrawItemSprite(new Rect(rect.x + 4, rect.y + 4, cell - 8, cell - 8), candidate.BrowseId, candidate, candidate.DisplayName);
            if (items.Ingredient is { IsLiquid: true } ingredient && DiagramAmount(ingredient, candidate) is { } volume)
                NeiSkin.SlotAmount(rect, volume);
        }
        string hint = chainPreview ? L("recipe.candidates.chain_hint") :
            !pinned ? L("recipe.candidates.pin_hint") :
            items.SelectCandidate != null ? L("recipe.candidates.select_hint") :
            L("recipe.candidates.recipe_hint");
        if (!chainPreview && page.Pages > 1) hint += $"  ·  ↕ {page.Index + 1}/{page.Pages}";
        NeiSkin.Label(new Rect(box.x + pad, tray.yMax + 1, textWidth, line), hint, muted: true);
        if (footerHeight > 0f)
        {
            bool copyEnabled = pinned && CanCopyTooltip;
            var (idRow, modRow) = NeiSkin.DrawItemFooter(new Rect(box.x + pad, box.yMax - pad - footerHeight + 4f, textWidth, footerHeight - 4f),
                tip, idHeight, copyIdEnabled: copyEnabled && _showTooltipId.Value, copyModEnabled: copyEnabled);
            if (copyEnabled) _supportedCopyId = (idRow, tip.Id, modRow, tip.ModText);
        }
        if (hovered >= 0)
        {
            var candidate = items.Candidates[hovered];
            var volume = items.Ingredient is { IsLiquid: true } input ? DiagramAmount(input, candidate)?.LiquidVolume : null;
            var candidateTip = NativeTooltip.ForItem(candidate.DisplayName, candidate, null, ItemDetailsHeld, _plugin.Chinese, volume, _showTooltipId.Value);
            QueueItemTip(pointer, TooltipPlacement.CandidateCell(grid, columns, cell, hovered - first), candidateTip, box);
        }
        return true;
    }
}
