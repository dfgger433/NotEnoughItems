using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Builds NEI item hover content and bounds stock tooltip suppression to NEI.</summary>
internal static class NativeTooltip
{
    internal readonly struct Content(string name, string description, string? note, string? id, string? idText, string? modText)
    {
        internal string Name { get; } = name;
        internal string Description { get; } = description;
        internal string? Note { get; } = note;
        internal string? Id { get; } = id;
        internal string? IdText { get; } = idText;
        internal string? ModText { get; } = modText;
    }

    /// <summary>Reads native tooltip content; NEI annotations and provenance stay separate from its markup.</summary>
    internal static Content ForItem(string title, CatalogItem? entry, string? details,
        bool expanded = false, bool chinese = false, float? liquidVolume = null, bool showId = true)
    {
        string name = title;
        string description = entry?.Description ?? string.Empty;
        if (entry is { IsVirtual: false, Kind: CatalogItemKind.Item })
        {
            if (string.IsNullOrWhiteSpace(description)) description = entry.NativeItemInfo?.description ?? string.Empty;
            if (ReadItem(entry, expanded, chinese) is { } native)
            {
                // A recipe or generic requirement can have its own title; keep that context.
                if (title == entry.DisplayName || PlainGameText(title).Length == 0) name = native.Name;
                description = native.Description;
            }
        }
        else if (entry is { IsVirtual: false, Kind: CatalogItemKind.Liquid } && Liquids.Registry?.TryGetValue(entry.Id, out var liquid) == true && liquid != null)
        {
            (string nativeName, string text) = ReadLiquid(entry, liquid, expanded || PlayerCamera.alwaysExpandDescriptions, liquidVolume);
            if (title == entry.DisplayName) name = nativeName;
            description = text;
        }
        if (PlainGameText(name).Length == 0) name = entry?.Id ?? title;
        string? note = string.IsNullOrWhiteSpace(details) || string.Equals(details.Trim(), PlainGameText(description), StringComparison.Ordinal)
            ? null : details;
        string? id = showId ? entry?.Id : null;
        string? idText = id == null ? null : IdLabel(entry!, chinese) + id;
        string? modText = entry == null ? null : SourceName(entry.Source);
        return new Content(name, description.Trim(), note, id, idText, modText);
    }

    private static (string Name, string Description)? ReadItem(CatalogItem entry, bool expanded, bool chinese)
    {
        if (entry.NativeItemInfo == null) return null;
        try
        {
            return NativeTooltipPreview.Get(entry) is { } preview ? TryDescribe(preview, expanded, chinese) : null;
        }
        catch (Exception)
        {
            return null; // A missing or incomplete prefab must not break OnGUI.
        }
    }

    private static (string Name, string Description)? TryDescribe(Item instance, bool expanded, bool chinese)
    {
        bool prior = PlayerCamera.alwaysExpandDescriptions;
        try
        {
            PlayerCamera.alwaysExpandDescriptions = prior || expanded;
            var (name, description) = PlayerCamera.ItemHoverDescription(instance);
            if (string.IsNullOrWhiteSpace(name)) return null;
            description ??= string.Empty;
            if (!prior && !expanded && Locale.currentLang?.other?.TryGetValue("shifttoexpand", out string hint) == true && !string.IsNullOrEmpty(hint))
                description = description.Replace("<i>" + hint + "</i>", chinese ? "<i>按 Ctrl 展开信息</i>" : "<i>Hold Ctrl for more details</i>");
            return (name, description);
        }
        catch (Exception) { return null; }
        finally { PlayerCamera.alwaysExpandDescriptions = prior; }
    }

    private static (string Name, string Description) ReadLiquid(CatalogItem entry, LiquidType liquid, bool expanded, float? volume)
    {
        // The game has no tooltip generator taking a liquid ID. Its inventory tooltip
        // looks up the contained liquid's locale keys and native scaled qualities.
        string key = liquid.localeFromItem || string.IsNullOrWhiteSpace(liquid.localeName) ? entry.Id : liquid.localeName;
        bool localized = Locale.currentLang != null;
        string? name = localized ? (liquid.localeFromItem ? Locale.GetItem(key) : Locale.GetOther(key)) : null;
        if (string.IsNullOrWhiteSpace(name) || name == key) name = entry.DisplayName;
        string? text = localized ? Locale.GetOther(key + "dsc") : null;
        if (string.IsNullOrWhiteSpace(text) || text == key + "dsc") text = entry.Description;
        if (!string.IsNullOrWhiteSpace(text)) text = "<alpha=#88>" + text.TrimEnd();
        if (expanded && localized && liquid.qualities is { Count: > 0 })
        {
            var qualities = volume is > 0f ? liquid.GetScaledQualities(volume.Value) : liquid.qualities;
            string line = Locale.GetOther("itemqualities") + string.Join(", ", qualities.Select(q => $"{q.LocaleName} ({q.amount:0.#})"));
            text = string.IsNullOrWhiteSpace(text) ? line : text + "\n\n<alpha=#FF>" + line;
        }
        return ($"<color=#{ColorUtility.ToHtmlStringRGB(liquid.color)}>{name}</color>",
            string.IsNullOrWhiteSpace(text) ? string.Empty : $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(liquid.color, Color.white, .5f))}>{text}");
    }

    internal static string SourceName(string owner)
    {
        if (owner.Equals("native", StringComparison.OrdinalIgnoreCase)) return NeiSearch.NativeSourceName;
        return Plugin.Instance is not null && Chainloader.PluginInfos.TryGetValue(owner, out var plugin) &&
            !string.IsNullOrWhiteSpace(plugin.Metadata.Name) ? plugin.Metadata.Name : owner;
    }

    private static string IdLabel(CatalogItem entry, bool chinese) => chinese
        ? (entry.IsLiquid ? "流体 ID：" : entry.IsVirtual ? "条目 ID：" : "物品 ID：")
        : (entry.IsLiquid ? "Fluid ID: " : entry.IsVirtual ? "Entry ID: " : "Item ID: ");

    /// <summary>Builds one readable name per eligible item type; does not merge items sharing a displayed name.</summary>
    internal static string[] SupportedNames(RecipeIngredient ingredient, IEnumerable<CatalogItem> related)
    {
        return related.Where(ingredient.Matches).Select(item =>
        {
            string name = PlainGameText(item.DisplayName).Replace('\r', ' ').Replace('\n', ' ');
            return string.IsNullOrWhiteSpace(name) ? item.Id : name;
        }).ToArray();
    }

    /// <summary>
    /// Only a visible NEI surface under the pointer hides the stock GlobalDark tooltip.
    /// Captured pointer state and native dragging are intentionally not inputs: the
    /// overlay may be active while the pointer is outside every NEI panel.
    /// </summary>
    internal static bool Suppress(bool overlayActive, bool pointerOverVisibleRegion, bool nativeUiTooltipVisible = false) =>
        overlayActive && pointerOverVisibleRegion && !nativeUiTooltipVisible;

    /// <summary>Hit test over the rectangles NEI actually drew this frame.</summary>
    internal static bool Hit(Vector2 pointer, params (bool Visible, Rect Rect)[] regions)
    {
        foreach (var (visible, rect) in regions)
            if (visible && rect.width > 0f && rect.height > 0f && rect.Contains(pointer)) return true;
        return false;
    }

    internal static string PlainGameText(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : TooltipText.Format(value!, "FFFFFF").Plain.Trim();
}
