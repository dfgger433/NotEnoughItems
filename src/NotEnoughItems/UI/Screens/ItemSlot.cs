using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void ItemSlot(Rect rect, string id, bool bookmarked = false, Sprite? overrideIcon = null, SlotQuantity? amount = null,
        string? fallback = null, Rect? clipping = null, Vector2? globalOffset = null, string? details = null,
        int requirement = -1, bool belowRequirement = false, bool catalogSlot = false,
        bool interactive = true, Color? backing = null, string? corner = null, bool diagram = false,
        IReadOnlyList<CatalogItem>? candidates = null, string? candidateRequirement = null, bool iconOnly = false,
        RecipeIngredient? requirementIngredient = null, Action<CatalogItem>? selectCandidate = null,
        CandidateSource candidateSource = CandidateSource.Inspector)
    {
        _catalog.TryGetItem(id, out var item);
        var recipeBookmark = id.StartsWith("recipe/", StringComparison.Ordinal) ? _catalog.Recipes.FirstOrDefault(r => RecipeKey(r) == id) : null;
        var result = recipeBookmark?.Outputs.FirstOrDefault();
        if (result != null) _catalog.TryGetItem(result.BrowseId, out item);
        string title = !string.IsNullOrWhiteSpace(fallback) ? fallback! : recipeBookmark?.Name ??
            (requirementIngredient?.MatchesAny == true && SameId(id, requirementIngredient.BrowseId) ? RequirementTitle(requirementIngredient) : item?.DisplayName ?? id);
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
        bool expanded = requirement >= 0 && _expandedRequirement == requirement;
        if (diagram) NeiSkin.RecipeSlot(rect);
        if (backing is { } tint) NeiSkin.BookmarkTint(rect, tint);
        if (hover || bookmarked || expanded) NeiSkin.Slot(rect, hover, bookmarked || expanded);
        DrawItemSprite(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), result?.BrowseId ?? id,
            item, iconOnly ? null : title, requirementIngredient, overrideIcon, result?.IsLiquid == true);
        if (!iconOnly && !string.IsNullOrEmpty(corner)) NeiSkin.Label(new Rect(rect.x + 1, rect.y, rect.width - 2, 12), corner!);
        if (!iconOnly && requirement >= 0)
            NeiSkin.Label(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, 13), expanded ? "−" : "+", TextAnchor.MiddleRight);
        if (belowRequirement)
            NeiSkin.SlotWarning(rect);
        if (amount is { } quantity) NeiSkin.SlotAmount(rect, quantity);
        if (e.type == EventType.Repaint && absolute.width > 0 && absolute.height > 0)
        {
            _itemTargets.Add((absolute, id, PropertyQuery(requirementIngredient)));
            if (hover)
            {
                if (amount is { Liquid: true } volume)
                    details = (string.IsNullOrEmpty(details) ? string.Empty : details + "\n") + L("item.volume", volume.FullText);
                ItemHover(absolute, title, item, details, candidates, candidateRequirement, selectCandidate, candidateSource,
                    requirementIngredient, amount?.LiquidVolume);
            }
        }
        if (!hover || !interactive) return;
        if (e.type != EventType.MouseDown || (PlayerCamera.main && PlayerCamera.main.dragItem)) return;
        if (e.button == 2 || e.alt) ToggleBookmark(id);
        else if (e.button == 0 && requirement >= 0) _expandedRequirement = expanded ? -1 : requirement;
        else if (CanGiveSlot(catalogSlot, id, item, e.button, e.shift, e.alt)) GiveSlot(item!, e.button);
        else if (e.button == 0 || e.button == 1) Lookup(id, e.button == 1);
        e.Use();
    }

    private readonly Dictionary<RecipeIngredient, CatalogItem?> _requirementPreviews = new();
    private int _requirementPreviewRevision = -1;

    private CatalogItem? RequirementPreview(RecipeIngredient ingredient)
    {
        if (_requirementPreviewRevision != _catalog.Revision)
        {
            _requirementPreviewRevision = _catalog.Revision;
            _requirementPreviews.Clear();
        }
        if (!_requirementPreviews.TryGetValue(ingredient, out var preview))
        {
            // This is only artwork, not a material allocation. The chain plan can leave
            // a slot generic when neither inventory nor a producer supplies an item.
            var matches = _catalog.GetMatchingItems(ingredient, includeBelowRequirement: true);
            preview = matches.FirstOrDefault(candidate => candidate.Icon && ingredient.Matches(candidate)) ??
                matches.FirstOrDefault(candidate => candidate.Icon);
            _requirementPreviews.Add(ingredient, preview);
        }
        return preview;
    }

    private void DrawItemSprite(Rect rect, string id, CatalogItem? item, string? fallback,
        RecipeIngredient? ingredient = null, Sprite? overrideIcon = null, bool liquid = false)
    {
        if (Event.current.type != EventType.Repaint) return;
        var icon = overrideIcon ? overrideIcon : item?.Icon;
        var tint = item?.IconTint ?? Color.white;
        if (!icon && ingredient?.MatchesAny == true && SameId(id, ingredient.BrowseId) && RequirementPreview(ingredient) is { } preview)
        {
            icon = preview.Icon;
            tint = preview.IconTint;
        }
        if (!icon && (liquid || ingredient?.IsLiquid == true || item?.IsLiquid == true || id.StartsWith("liquid:", StringComparison.OrdinalIgnoreCase)))
            icon = Catalog.LoadDroplet();
        DrawSprite(rect, icon, tint, fallback);
    }

    private static bool CanGiveSlot(bool source, string id, CatalogItem? item, int button, bool shift, bool alt) =>
        CanGiveSlot(source, id, item, button, shift, alt, cheatMode: true) && NeiActions.CanGive;

    internal static bool CanGiveSlot(bool source, string id, CatalogItem? item, int button, bool shift, bool alt, bool cheatMode) =>
        source && cheatMode && (button == 0 || button == 1) && !shift && !alt &&
        item is { Kind: CatalogItemKind.Item or CatalogItemKind.Liquid, IsVirtual: false } &&
        !id.StartsWith("recipe/", StringComparison.Ordinal);

    private void GiveSlot(CatalogItem item, int button)
    {
        // Catalog, history and item bookmarks stay interactive beside the inspector.
        if (PlayerCamera.main && PlayerCamera.main.dragItem) return;
        if (!NeiActions.TryHandleGiveFromSlot(item, button, false, _spawnAmount.Value, out var error, out int dropped, out bool submitted)) return;
        if (error != null) Status(error);
        else if (submitted) Status(L("item.give_submitted"));
        else if (item.IsLiquid)
            Status(dropped > 0 ? L("item.liquid_dropped", dropped) : L("item.liquid_given"));
        else Status(dropped > 0 ? L("item.given_dropped", dropped) : L("item.given"));
    }

    private static void DrawSprite(Rect rect, Sprite? sprite, Color tint, string? fallback)
    {
        if (!TryDrawSprite(rect, sprite, tint) && fallback != null)
            NeiSkin.Label(rect, fallback.Length == 0 ? "?" : fallback.Substring(0, 1).ToUpperInvariant(), TextAnchor.MiddleCenter, true);
    }

    private static bool TryDrawSprite(Rect rect, Sprite? sprite, Color tint)
    {
        if (Event.current.type != EventType.Repaint || !sprite || !sprite!.texture) return false;
        var tex = sprite.texture;
        Rect src;
        try
        {
            // textureRect is the only safe UV source for a rectangular sprite. Unity
            // throws for tight/rotated atlas entries; sprite.rect is in source-image
            // coordinates and would sample an unrelated tile from the packed texture.
            src = sprite.textureRect;
        }
        catch (UnityException) { return false; }
        if (src.width <= 0 || src.height <= 0) return false;

        var ratio = Mathf.Min(rect.width / src.width, rect.height / src.height);
        float w = src.width * ratio, h = src.height * ratio;
        var dst = new Rect(rect.center.x - w / 2, rect.center.y - h / 2, w, h);
        var originalColor = GUI.color;
        try
        {
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(dst, tex, new Rect(src.x / tex.width, src.y / tex.height, src.width / tex.width, src.height / tex.height), true);
        }
        finally { GUI.color = originalColor; }
        return true;
    }
}
