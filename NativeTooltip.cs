using System;

namespace NotEnoughItems;

/// <summary>Supplies item content directly to the game's GlobalDark/TextMeshPro tooltip.</summary>
internal static class NativeTooltip
{
    internal static (string Name, string Description) ForItem(string title, CatalogItem? entry, string? details, Item? instance = null)
    {
        if (instance && entry is { Kind: CatalogItemKind.Item, IsVirtual: false } &&
            string.Equals(instance!.id, entry.Id, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Pass the game's text through unchanged, including its expand-description key and format tags.
                var tooltip = PlayerCamera.ItemHoverDescription(instance);
                if (!string.IsNullOrEmpty(tooltip.Item1)) return tooltip;
            }
            catch (Exception)
            {
                // A destroyed or incompletely initialized item must not break GlobalDark.Update.
            }
        }

        // Like the game's merchant catalogue, entries without a live instance show definition text.
        // Do not instantiate a prefab: its battery, liquid and container components need Awake state.
        string description = entry?.Description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description)) description = entry?.NativeItemInfo?.description ?? string.Empty;
        bool nativeText = entry is { IsVirtual: false } && (entry.NativeItemInfo != null || entry.IsLiquid);
        string name = nativeText && title == entry!.DisplayName ? title : Literal(title);
        if (!nativeText) description = Literal(description);
        if (!string.IsNullOrWhiteSpace(details) && !string.Equals(details, description, StringComparison.Ordinal))
            description += (description.Length == 0 ? string.Empty : "\n\n") + Literal(details!);
        return (name, description);
    }

    private static string Literal(string value) => value.Replace('<', '＜').Replace('>', '＞');
}
