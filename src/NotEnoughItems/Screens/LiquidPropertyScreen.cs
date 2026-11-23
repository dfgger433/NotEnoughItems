using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private RecipeIngredient? _propertyQuery;
    private int _propertyPage;
    private RecipeIngredient? _cachedPropertyQuery;
    private int _propertyRevision = -1;
    private IReadOnlyList<CatalogItem> _propertyMatches = Array.Empty<CatalogItem>();

    private static RecipeIngredient? PropertyQuery(RecipeIngredient? ingredient) =>
        ingredient is { MatchesAny: true } && (ingredient.QualityId.Length > 0 || ingredient.RequiredTag.Length > 0) ? ingredient : null;

    private static bool SameProperty(RecipeIngredient? first, RecipeIngredient? second) =>
        first == null ? second == null : second != null && first.IsLiquid == second.IsLiquid &&
        SameId(first.QualityId, second.QualityId) && SameId(first.RequiredTag, second.RequiredTag);

    internal bool TryLookupPropertyAt(Vector2 pointer)
    {
        if (_options || _layoutEditing || PropertyQuery(ItemTargetAt(pointer).Property) is not { } property) return false;
        if (!_open || !SameProperty(_propertyQuery, property))
        {
            OpenView(null, usage: false);
            _propertyQuery = property;
        }
        return true;
    }

    private void DrawPropertyResults(RecipePanelLayout panel, RecipeIngredient property)
    {
        int revision = _catalog.Revision;
        if (_propertyRevision != revision || !SameProperty(_cachedPropertyQuery, property))
        {
            _propertyMatches = _catalog.GetItemsWithProperty(property);
            _cachedPropertyQuery = property;
            _propertyRevision = revision;
        }
        int count = _propertyMatches.Count;
        string key = property.QualityId.Length > 0 ? "cq" + property.QualityId : property.RequiredTag;
        string name = Locale.currentLang?.other is { } terms && terms.TryGetValue(key, out var localized)
            ? localized : property.DisplayName.Length > 0 ? property.DisplayName : property.QualityId.Length > 0 ? property.QualityId : property.RequiredTag;
        string kind = property.IsLiquid ? L("Liquid", "流体") : L("Item", "物品");
        NeiSkin.RecipeBar(panel.MethodTitle);
        NeiSkin.Label(panel.MethodTitle, L($"{kind} property: {name}", $"{kind}性质：{name}"), TextAnchor.MiddleCenter);
        NeiSkin.RecipeBar(panel.PageTitle);

        var area = panel.Content;
        float cell = Mathf.Min(29f * IconScale, area.width, area.height);
        if (cell < 9f) return;
        int columns = Math.Max(1, (int)(area.width / cell));
        int size = columns * Math.Max(1, (int)(area.height / cell));
        int pages = Math.Max(1, (count + size - 1) / size);
        var e = Event.current;
        int delta = DrawPageArrows(panel, pages > 1, property: true);
        if (pages > 1 && e.type == EventType.ScrollWheel && e.delta.y != 0 &&
            (area.Contains(e.mousePosition) || panel.Pages.Contains(e.mousePosition)))
        {
            delta = Math.Sign(e.delta.y);
            e.Use();
        }
        _propertyPage = Wrap(Math.Clamp(_propertyPage, 0, pages - 1) + delta, pages);
        NeiSkin.Label(panel.PageTitle,
            L($"{count} matching types · {_propertyPage + 1} / {pages}", $"{count} 种匹配物品 · {_propertyPage + 1} / {pages}"), TextAnchor.MiddleCenter);
        if (count == 0)
            NeiSkin.Label(area, L("No items have this property", "没有物品含此性质"), TextAnchor.MiddleCenter, muted: true);
        int first = _propertyPage * size;
        int visible = Math.Min(size, count - first);
        for (int i = first; i < Math.Min(first + size, count); i++)
        {
            int index = i - first;
            var rect = TooltipPlacement.CenteredGridCell(area, cell, columns, visible, index);
            // These are concrete items, not requirement placeholders: normal R, U and clicks apply.
            ItemSlot(rect, _propertyMatches[i].BrowseId, diagram: true);
            if (_propertyQuery == null) break; // A result click opens another inspector view immediately.
        }
    }
}
