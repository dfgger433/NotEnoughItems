using System;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void ItemSlot(Rect rect, string id, bool bookmarked = false, Sprite? overrideIcon = null, string? amount = null,
        string? fallback = null, Rect? clipping = null, Vector2? globalOffset = null, string? details = null,
        int requirement = -1, bool belowRequirement = false, Item? liveItem = null, bool catalogSlot = false,
        bool interactive = true, Color? backing = null, string? corner = null)
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
        bool expanded = requirement >= 0 && _expandedRequirement == requirement;
        if (backing is { } tint) NeiSkin.BookmarkTint(rect, tint);
        if (hover || bookmarked || expanded) NeiSkin.Slot(rect, hover, bookmarked || expanded);
        DrawSprite(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), overrideIcon ?? item?.Icon, item?.IconTint ?? Color.white, title);
        if (!string.IsNullOrEmpty(corner)) NeiSkin.Label(new Rect(rect.x + 1, rect.y, rect.width - 2, 12), corner!);
        if (requirement >= 0)
            NeiSkin.Label(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, 13), expanded ? "−" : "+", TextAnchor.MiddleRight);
        if (belowRequirement)
            NeiSkin.Label(new Rect(rect.x + 3, rect.y + 1, rect.width - 5, 13), "!");
        if (!string.IsNullOrEmpty(amount)) NeiSkin.Label(new Rect(rect.x + 2, rect.yMax - 14, rect.width - 4, 13), amount!, TextAnchor.MiddleRight);
        if (e.type == EventType.Repaint && absolute.width > 0 && absolute.height > 0)
        {
            _itemTargets.Add((absolute, id));
            _tooltipSlots.Add((absolute, title, item, details, liveItem ?? (SameId(id, _item) ? _lookupItem : null)));
            _hoveringItemSlot |= hover;
        }
        if (!hover || !interactive) return;
        if (e.type != EventType.MouseDown || (PlayerCamera.main && PlayerCamera.main.dragItem)) return;
        if (e.button == 2 || e.alt) ToggleBookmark(id);
        else if (e.button == 0 && requirement >= 0) _expandedRequirement = expanded ? -1 : requirement;
        else if (CanGiveSlot(catalogSlot, id, item, e.button, e.shift, e.alt)) GiveSlot(item!, e.button);
        else if (e.button == 0 || e.button == 1) Lookup(id, e.button == 1);
        e.Use();
    }

    private static bool CanGiveSlot(bool source, string id, CatalogItem? item, int button, bool shift, bool alt) =>
        CanGiveSlot(source, id, item, button, shift, alt, cheatMode: true) && NeiActions.CanGive;

    internal static bool CanGiveSlot(bool source, string id, CatalogItem? item, int button, bool shift, bool alt, bool cheatMode) =>
        source && cheatMode && (button == 0 || button == 1) && !shift && !alt &&
        item is { Kind: CatalogItemKind.Item, IsVirtual: false } && !id.StartsWith("recipe/", StringComparison.Ordinal);

    private void GiveSlot(CatalogItem item, int button)
    {
        if (ModalOpen || (PlayerCamera.main && PlayerCamera.main.dragItem)) return;
        if (NeiActions.TryHandleGiveFromSlot(item.Id, button, false, GiveQuantity, out var error, out int dropped, out bool submitted))
            Status(error ?? (submitted ? L("Give command submitted to Together", "已向 Together 提交发放命令") :
                dropped > 0 ? L($"Given; {dropped} left nearby", $"已给予；{dropped} 件放在地上") : L("Item given", "已给予物品")));
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
}
