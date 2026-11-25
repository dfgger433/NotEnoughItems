using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NotEnoughItems;

/// <summary>Adapts the game's TMP color markup to the balanced tags required by NEI's IMGUI labels.</summary>
internal static class TooltipText
{
    private static readonly Regex Tags = new(@"<(?<tag>/?[a-z]+|#[0-9a-f]+)(?=[\s=/>])(?<args>[^<>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Hex = new(@"\A[0-9a-f]+\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> GameOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "sprite", "size", "u", "s", "align", "font", "voffset", "link"
    };

    internal static (string Rich, string Shadow, string Plain) Format(string text, string baseRgb)
    {
        var rich = new StringBuilder(text.Length);
        var shadow = new StringBuilder(text.Length);
        var plain = new StringBuilder(text.Length);
        var colors = new Stack<string>();
        string defaultColor = baseRgb + "FF";
        string current = defaultColor;
        string? emitted = null;
        int position = 0;
        foreach (Match match in Tags.Matches(text))
        {
            Append(text.AsSpan(position, match.Index - position));
            string tag = match.Groups["tag"].Value.ToLowerInvariant();
            string args = match.Groups["args"].Value.Trim();
            string value = args.StartsWith("=", StringComparison.Ordinal) ? args.Substring(1).Trim().Trim('"', '\'') : string.Empty;
            if (tag is "/color" && args.Length == 0)
            {
                if (colors.Count > 0) colors.Pop();
                current = colors.Count == 0 ? defaultColor : colors.Peek();
            }
            else if (tag == "alpha" && value.Length == 3 && value[0] == '#' && Hex.IsMatch(value.Substring(1)))
                current = current.Substring(0, 6) + value.Substring(1).ToUpperInvariant();
            else if ((tag == "color" || tag[0] == '#') && ParseColor(tag == "color" ? value : tag) is { } rgba)
            {
                colors.Push(rgba);
                current = rgba;
            }
            else if (tag == "br" && args is "" or "/") Append("\n".AsSpan());
            else if ((tag is "b" or "/b" or "i" or "/i") && args.Length == 0)
            {
                CloseColor();
                rich.Append(match.Value);
                shadow.Append(match.Value);
            }
            else if (!GameOnly.Contains(tag.TrimStart('/')))
                Append(text.AsSpan(match.Index, match.Length));
            position = match.Index + match.Length;
        }
        Append(text.AsSpan(position));
        CloseColor();
        return (rich.ToString(), shadow.ToString(), plain.ToString());

        void Append(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return;
            if (current != emitted)
            {
                CloseColor();
                if (current != defaultColor)
                {
                    rich.Append("<color=#").Append(current).Append('>');
                    // Use the same glyphs, layout, and opacity for the drop shadow, not the foreground RGB.
                    shadow.Append("<color=#191827").Append(current, 6, 2).Append('>');
                    emitted = current;
                }
            }
            rich.Append(value);
            shadow.Append(value);
            plain.Append(value);
        }

        void CloseColor()
        {
            if (emitted == null) return;
            rich.Append("</color>");
            shadow.Append("</color>");
            emitted = null;
        }
    }

    private static string? ParseColor(string value)
    {
        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            string hex = value.Substring(1).ToUpperInvariant();
            if (hex.Length is not (3 or 4 or 6 or 8) || !Hex.IsMatch(hex)) return null;
            if (hex.Length <= 4)
            {
                var expanded = new StringBuilder(hex.Length * 2);
                foreach (char c in hex) expanded.Append(c, 2);
                hex = expanded.ToString();
            }
            return hex.Length == 6 ? hex + "FF" : hex;
        }
        // TextMeshPro's named colors (not IMGUI's differently valued CSS colors).
        return value.ToLowerInvariant() switch
        {
            "red" => "FF0000FF", "lightblue" => "ADD8E6FF", "blue" => "0000FFFF", "grey" => "808080FF",
            "black" => "000000FF", "green" => "00FF00FF", "white" => "FFFFFFFF", "orange" => "FF8000FF",
            "purple" => "A020F0FF", "yellow" => "FFFF00FF", _ => null
        };
    }
}
