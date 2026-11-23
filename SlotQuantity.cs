using System;
using System.Globalization;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>A slot count or a liquid volume, in mL. Keep the numeric value until the label is measured.</summary>
internal readonly struct SlotQuantity(double value, bool liquid)
{
    private const string Prefixes = " kmgtpezy";
    internal bool Liquid { get; } = liquid;
    internal string FullText => Format(value) + (Liquid ? "mL" : string.Empty);

    internal string Fit(Func<string, bool> fits)
    {
        string full = FullText;
        if (!Liquid || fits(full) || double.IsNaN(value) || double.IsInfinity(value)) return full;

        double scaled = value;
        int prefix = 0;
        while (Math.Abs(scaled) >= 1000 && prefix + 1 < Prefixes.Length)
        {
            scaled /= 1000;
            prefix++;
        }
        if (Math.Abs(scaled) >= 1000) return value.ToString("0.#e+0", CultureInfo.InvariantCulture);
        if (prefix == 0) return full; // Scale the font rather than lose the unit or round a small volume to zero.

        // Keep as much precision as fits, promoting the suffix if rounding reaches the next tier.
        string shortest = full;
        for (int decimals = 2; decimals >= 0; decimals--)
        {
            double rounded = Math.Round(scaled, decimals, MidpointRounding.AwayFromZero);
            int tier = prefix;
            if (Math.Abs(rounded) >= 1000 && tier + 1 < Prefixes.Length)
            {
                rounded /= 1000;
                tier++;
            }
            shortest = Format(rounded) + Prefixes[tier];
            if (fits(shortest)) return shortest;
        }
        return shortest; // Extremely narrow slots still get the shortest label, scaled to fit.
    }

    internal Rect LabelBounds(Rect slot, Vector2 size) => new(Liquid ? slot.xMin + 1f : slot.xMax - 1f - size.x,
        slot.yMax - 1f - size.y, size.x, size.y);

    private static string Format(double amount) => amount != 0 && Math.Abs(amount) < .005
        ? amount.ToString("G3", CultureInfo.InvariantCulture)
        : amount.ToString("0.##", CultureInfo.InvariantCulture);
}
