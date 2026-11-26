using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

internal static class CatalogListing
{
    private static readonly IComparer<string> ModNameOrder = Comparer<string>.Create(CompareNames);

    // Group before filtering so equally named mods keep the same order even when a search hides their first items.
    internal static CatalogItem[] Filter(IEnumerable<CatalogItem> items, string query, PinyinSearch? pinyin,
        Func<string, string>? sourceName = null) => items
        .Where(item => item.IsCatalogVisible)
        .GroupBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => !group.Key.Equals("native", StringComparison.OrdinalIgnoreCase))
        .ThenBy(group => SortName(group.Key, sourceName), ModNameOrder)
        .SelectMany(group => group)
        .Where(item => NeiSearch.Matches(query, item.SearchText, item.Source,
            string.Join(" ", item.Tags), item.Description, item.Id, pinyin, sourceName))
        .ToArray();

    private static string SortName(string source, Func<string, string>? sourceName)
    {
        string? name = sourceName?.Invoke(source);
        return NeiSearch.CompactSource(string.IsNullOrWhiteSpace(name) ? source : name!).ToLowerInvariant();
    }

    private static int CompareNames(string? left, string? right)
    {
        if (left == null || right == null) return StringComparer.Ordinal.Compare(left, right);
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int comparison = CharacterRank(left[i]).CompareTo(CharacterRank(right[i]));
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }

    private static int CharacterRank(char c) => c switch
    {
        >= 'a' and <= 'z' => c - 'a',
        >= '0' and <= '9' => 26 + c - '0',
        _ => 36 + c
    };
}
