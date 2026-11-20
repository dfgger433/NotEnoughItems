using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Adapts the game's TMP tooltip content to NEI's non-interactive IMGUI overlay.</summary>
internal static class NativeTooltip
{
    internal readonly struct Run(string text, Color32? color = null, byte? alpha = null,
        FontStyle font = FontStyle.Normal, float Size = 1, int Sprite = -1, bool Tint = false)
    {
        internal readonly string Text = text;
        internal readonly Color32? Color = color;
        internal readonly byte? Alpha = alpha;
        internal readonly FontStyle Font = font;
        internal readonly float Scale = Size;
        internal readonly int SpriteIndex = Sprite;
        internal readonly bool TintSprite = Tint;
    }

    internal sealed class Parts(IReadOnlyList<Run> title, IReadOnlyList<Run> body, IReadOnlyList<Run> shortcuts)
    {
        internal readonly IReadOnlyList<Run> Title = title;
        internal readonly IReadOnlyList<Run> Body = body;
        internal readonly IReadOnlyList<Run> Shortcuts = shortcuts;
    }
    private static readonly ConditionalWeakTable<string, Parts> ItemTips = new();

    // The existing tooltip dispatch passes a pair of strings. Associate the parsed parts with
    // this exact body string, not a magic prefix that could collide with player/provider text.
    internal static bool TryGetParts(string? body, out Parts? parts)
    {
        parts = null;
        return body != null && ItemTips.TryGetValue(body, out parts);
    }

    internal static (string Title, string Body) ForSlot(string title, CatalogItem? item, string id,
        string? details, string shortcuts, bool recipeBookmark, Item? liveItem = null)
    {
        bool native = item != null && !item.IsVirtual && (item.NativeItemInfo != null || item.IsLiquid);
        IReadOnlyList<Run> heading = native && !recipeBookmark && title != id && title == item!.DisplayName ? Parse(title) : Literal(title);
        var body = new List<Run>(Literal(id));
        if (native && !recipeBookmark && item!.Kind == CatalogItemKind.Item && TryReadLive(liveItem, item.Id, ref heading, out var liveDescription))
        {
            AddLine(body, Parse(liveDescription));
        }
        else
        {
            string? description = item?.Description;
            if (string.IsNullOrWhiteSpace(description)) description = item?.NativeItemInfo?.description;
            if (!string.IsNullOrWhiteSpace(description)) AddLine(body, native ? Parse(description!) : Literal(description!));
            if (!recipeBookmark && item is { IsVirtual: false, NativeItemInfo: { } info }) AddStaticProperties(body, info);
        }
        if (!string.IsNullOrWhiteSpace(details)) AddLine(body, Literal(details!));
        // Concat always allocates here: a nonempty ID and suffix never alias their inputs.
        string key = string.Concat(id, "\n", shortcuts);
        ItemTips.Add(key, new Parts(heading, body, Literal(shortcuts)));
        return (title, key);
    }

    private static void AddLine(List<Run> body, IReadOnlyList<Run> content)
    {
        if (content.Count == 0) return;
        if (body.Count > 0) body.Add(new Run("\n"));
        body.AddRange(content);
    }

    private static void AddStaticProperties(List<Run> body, ItemInfo info)
    {
        AddLine(body, new[] { new Run("", Sprite: 0, Tint: true), new Run($"{Other("weight", "Weight")}: {info.weight:0.##}u") });
        if (info.onlyHoldInHands) AddLine(body, Property(6, "itemonlyinhands", "Only held in hands"));
        if (info.wearable)
        {
            AddLine(body, Property(7, "itemwearable", "Wearable", $" \"{info.wearSlotId}\""));
            if (info.wearableArmor > 0) AddLine(body, Property(13, "itemwearableprotection", "Protection", $" {info.wearableArmor:P0}"));
            if (info.wearableIsolation > 0) AddLine(body, Property(14, "itemwearableisolation", "Insulation", $" {info.wearableIsolation:P0}"));
        }
        if (info.usable || info.usableOnLimb)
        {
            var use = new List<Run>(Property(11, "itemusable", "Usable in: "));
            if (info.usable) use.AddRange(info.usableWithLMB ? Property(6, "itemusablehand", "hand") : Property(6, "itemusableinventory", "inventory"));
            if (info.usable && info.usableOnLimb) use.Add(new Run(" / "));
            if (info.usableOnLimb) use.AddRange(Property(12, "itemusablewound", "health panel"));
            AddLine(body, use);
        }
        if (info.qualities is { Count: > 0 })
            AddLine(body, Property(26, "itemqualities", "Qualities: ", string.Join(", ", info.qualities.Where(q => q != null)
                .Select(q => $"{Other("cq" + q.id, q.id)} ({q.amount:0.#})"))));
        if (info.value > 0 && !info.untradeable) AddLine(body, Property(9, "itemvalue", "Value: ", info.value.ToString(CultureInfo.CurrentCulture)));
    }

    private static Run[] Property(int icon, string key, string fallback, string extra = "") =>
        new[] { new Run("", Sprite: icon, Tint: true), new Run(Other(key, fallback) + extra) };

    private static string Other(string key, string fallback) =>
        Locale.currentLang?.other is { } text && text.TryGetValue(key, out var value) ? value : fallback;

    private static bool TryReadLive(Item? item, string id, ref IReadOnlyList<Run> heading, out string description)
    {
        description = string.Empty;
        if (!item || !string.Equals(item!.id, id, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var (name, text) = PlayerCamera.ItemHoverDescription(item);
            if (string.IsNullOrWhiteSpace(name)) return false;
            // The game interpolates item.label (user text) into rich text without escaping.
            // Preserve it as a literal run only when the stock title interpolates that label.
            if (item.Stats.rec.recognizable && !string.IsNullOrEmpty(item.label))
            {
                var runs = new List<Run>(Literal(item.label));
                runs.Add(new Run(item.condition is > 0 and < .01f ? " (<1%)" : $" ({Mathf.FloorToInt(item.condition * 100f)}%)"));
                heading = runs;
            }
            else heading = Parse(name);
            description = text ?? string.Empty;
            return true;
        }
        catch (Exception) // An unavailable mod/instance must not break the static tooltip.
        {
            return false;
        }
    }

    private static IReadOnlyList<Run> Literal(string text) => new[] { new Run(text) };

    /// <summary>Reads the TMP formatting used by CU; the resulting text is always drawn as plain IMGUI text.</summary>
    internal static List<Run> Parse(string text)
    {
        var runs = new List<Run>();
        var pending = new StringBuilder();
        var colors = new Stack<Color32?>();
        var sizes = new Stack<float>();
        Color32? color = null;
        byte? alpha = null;
        float size = 1;
        int bold = 0, italic = 0;
        bool noParse = false;
        void Flush()
        {
            if (pending.Length == 0) return;
            runs.Add(new Run(pending.ToString(), color, alpha, (FontStyle)((bold > 0 ? 1 : 0) | (italic > 0 ? 2 : 0)), size));
            pending.Clear();
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '<' || !TryTag(text, i, out int end, out string name, out string arg, out bool closing))
            {
                pending.Append(text[i]);
                continue;
            }
            if (noParse)
            {
                if (name == "noparse" && closing) { noParse = false; i = end; }
                else pending.Append(text[i]);
                continue;
            }
            // Only consume the delimiter itself, never the content between unknown tags.
            // Malformed tags cannot reach across a subsequent tag or a line break.
            i = end;
            switch (name)
            {
                case "color":
                    if (closing)
                    {
                        Flush();
                        color = colors.Count > 0 ? colors.Pop() : null;
                        alpha = null;
                    }
                    else if (TryColor(ReadValue(arg), out var nextColor))
                    {
                        Flush();
                        colors.Push(color);
                        color = nextColor;
                        alpha = null;
                    }
                    break;
                case "alpha":
                    string value = ReadValue(arg);
                    if (closing || (value.Length == 3 && value[0] == '#' && byte.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)))
                    {
                        Flush();
                        alpha = closing ? null : byte.Parse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    }
                    break;
                case "b": Flush(); bold = Math.Max(0, bold + (closing ? -1 : 1)); break;
                case "i": Flush(); italic = Math.Max(0, italic + (closing ? -1 : 1)); break;
                case "size":
                    if (closing) { Flush(); size = sizes.Count > 0 ? sizes.Pop() : 1; }
                    else if (TrySize(ReadValue(arg), out float nextSize)) { Flush(); sizes.Push(size); size = nextSize; }
                    break;
                case "br": if (!closing) pending.Append('\n'); break;
                case "space": if (!closing) pending.Append(' '); break;
                case "noparse":
                    // An unmatched opener must not turn the remainder into raw markup.
                    noParse = !closing && text.IndexOf("</noparse>", i + 1, StringComparison.OrdinalIgnoreCase) >= 0;
                    break;
                case "sprite":
                    if (!closing && TrySprite(arg, out int sprite, out bool tint, out var spriteColor))
                    {
                        Flush();
                        runs.Add(new Run("", spriteColor ?? color, alpha, Size: size, Sprite: sprite, Tint: tint || spriteColor.HasValue));
                    }
                    break;
            }
        }
        Flush();
        return runs;
    }

    private static bool TryTag(string text, int start, out int end, out string name, out string arg, out bool closing)
    {
        end = start;
        name = arg = string.Empty;
        closing = false;
        int i = start + 1;
        if (i == text.Length) return false;
        if (text[i] == '/') { closing = true; i++; }
        int first = i;
        bool hex = !closing && i < text.Length && text[i] == '#';
        if (!hex && (i == text.Length || !IsAsciiLetter(text[i]))) return false;
        while (i < text.Length && (IsAsciiLetter(text[i]) || char.IsDigit(text[i]) || text[i] is '-' or '_' or ':' or '#' or '.')) i++;
        int nameEnd = i;
        if (i == text.Length || !(char.IsWhiteSpace(text[i]) || text[i] is '>' or '=' or '/')) return false;
        char quote = '\0';
        for (; i < text.Length && i - start <= 256; i++)
        {
            char ch = text[i];
            if (ch is '<' or '\n' or '\r') return false;
            if (quote != '\0') { if (ch == quote) quote = '\0'; continue; }
            if (ch is '"' or '\'') quote = ch;
            else if (ch == '>')
            {
                name = hex ? "color" : text.Substring(first, nameEnd - first).ToLowerInvariant();
                arg = hex ? "=" + text.Substring(first, nameEnd - first) : text.Substring(nameEnd, i - nameEnd).Trim();
                end = i;
                return true;
            }
        }
        return false;
    }

    private static bool IsAsciiLetter(char ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static string ReadValue(string attributes) => ReadValue(attributes, string.Empty);

    private static string ReadValue(string attributes, string key)
    {
        int i = 0;
        while (i < attributes.Length)
        {
            while (i < attributes.Length && char.IsWhiteSpace(attributes[i])) i++;
            int start = i;
            while (i < attributes.Length && !char.IsWhiteSpace(attributes[i]) && attributes[i] != '=') i++;
            string name = attributes.Substring(start, i - start);
            while (i < attributes.Length && char.IsWhiteSpace(attributes[i])) i++;
            if (i == attributes.Length || attributes[i] != '=') continue;
            i++;
            while (i < attributes.Length && char.IsWhiteSpace(attributes[i])) i++;
            if (i == attributes.Length) return string.Empty;
            char quote = attributes[i] is '\'' or '"' ? attributes[i++] : '\0';
            start = i;
            while (i < attributes.Length && (quote != '\0' ? attributes[i] != quote : !char.IsWhiteSpace(attributes[i]))) i++;
            string value = attributes.Substring(start, i - start);
            if (quote != '\0' && i < attributes.Length) i++;
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase)) return value.TrimEnd('/');
        }
        return string.Empty;
    }

    private static bool TrySprite(string args, out int index, out bool tint, out Color32? color)
    {
        string value = ReadValue(args, "index");
        if (value.Length == 0) value = ReadValue(args);
        tint = ReadValue(args, "tint") == "1";
        color = TryColor(ReadValue(args, "color"), out var c) ? c : null;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;
    }

    private static bool TrySize(string value, out float scale)
    {
        scale = 1;
        bool percent = value.EndsWith("%", StringComparison.Ordinal), em = value.EndsWith("em", StringComparison.OrdinalIgnoreCase);
        string number = percent ? value.Substring(0, value.Length - 1) : em || value.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - 2) : value;
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) || x <= 0) return false;
        scale = Math.Clamp(percent ? x / 100 : em ? x : x / 12, .6f, 2f);
        return true;
    }

    private static bool TryColor(string value, out Color32 color)
    {
        if (value.Length > 1 && value[0] == '#' && value.Length is 4 or 5 or 7 or 9 &&
            uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
        {
            if (value.Length is 4 or 5)
            {
                if (value.Length == 4) hex = (hex << 4) | 0xF;
                color = new Color32((byte)((hex >> 12) * 17), (byte)(((hex >> 8) & 15) * 17), (byte)(((hex >> 4) & 15) * 17), (byte)((hex & 15) * 17));
                return true;
            }
            if (value.Length == 7) hex = (hex << 8) | 0xFF;
            color = new Color32((byte)(hex >> 24), (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
            return true;
        }
        uint rgb = value.ToLowerInvariant() switch
        {
            "white" => 0xFFFFFF, "black" => 0, "red" => 0xFF0000, "green" or "lime" => 0x00FF00,
            "blue" => 0x0000FF, "yellow" => 0xFFFF00, "orange" => 0xFFA500, "purple" => 0xA020F0,
            "cyan" or "aqua" => 0x00FFFF, "magenta" or "fuchsia" => 0xFF00FF, "grey" or "gray" => 0x808080,
            "lightblue" => 0xADD8E6, "brown" => 0xA52A2A, "darkblue" => 0x00008B, _ => uint.MaxValue
        };
        color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        return rgb != uint.MaxValue;
    }

    // TMP exposes the atlas from its own assembly, but Glyph/GlyphRect are in a
    // separate Unity font-engine assembly that the plug-in does not reference.
    private static readonly Lazy<GlyphAccess?> SpriteGlyph = new(CreateGlyphAccess);

    private sealed class GlyphAccess(System.Reflection.PropertyInfo glyph, System.Reflection.PropertyInfo rect,
        System.Reflection.PropertyInfo x, System.Reflection.PropertyInfo y, System.Reflection.PropertyInfo width, System.Reflection.PropertyInfo height)
    {
        internal Rect Read(TMP_SpriteCharacter? character)
        {
            var value = character == null ? null : glyph.GetValue(character);
            var bounds = value == null ? null : rect.GetValue(value);
            return bounds == null ? default : new Rect((int)x.GetValue(bounds)!, (int)y.GetValue(bounds)!,
                (int)width.GetValue(bounds)!, (int)height.GetValue(bounds)!);
        }
    }

    private static GlyphAccess? CreateGlyphAccess()
    {
        var glyph = typeof(TMP_SpriteCharacter).GetProperty("glyph");
        var rect = glyph?.PropertyType.GetProperty("glyphRect");
        var x = rect?.PropertyType.GetProperty("x");
        var y = rect?.PropertyType.GetProperty("y");
        var width = rect?.PropertyType.GetProperty("width");
        var height = rect?.PropertyType.GetProperty("height");
        return glyph != null && rect != null && x != null && y != null && width != null && height != null
            ? new GlyphAccess(glyph, rect, x, y, width, height) : null;
    }

    internal static bool TryResolveSprite(int index, bool title, out Texture? atlas, out Rect uv, out float aspect)
    {
        atlas = null;
        uv = default;
        aspect = 1;
        var game = GlobalDark.main;
        var label = game ? (title ? game.tooltipNameObj : game.tooltipDescObj) : null;
        TMP_SpriteAsset? asset = (label ? label!.spriteAsset : null) ?? TMP_Settings.defaultSpriteAsset;
        if (!asset || asset!.spriteCharacterTable is not { } characters || index >= characters.Count || index < 0) return false;
        atlas = asset.spriteSheet;
        if (!atlas || SpriteGlyph.Value is not { } access) return false;
        var rect = access.Read(characters[index]);
        if (rect.width <= 0 || rect.height <= 0) return false;
        uv = new Rect((float)rect.x / atlas!.width, (float)rect.y / atlas.height, (float)rect.width / atlas.width, (float)rect.height / atlas.height);
        aspect = (float)rect.width / rect.height;
        return true;
    }
}
