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
        int leftIndex = 0, rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (IsDigit(left[leftIndex]) && IsDigit(right[rightIndex]))
            {
                int leftEnd = leftIndex;
                while (leftEnd < left.Length && IsDigit(left[leftEnd])) leftEnd++;
                int rightEnd = rightIndex;
                while (rightEnd < right.Length && IsDigit(right[rightEnd])) rightEnd++;

                int leftSignificant = leftIndex;
                while (leftSignificant < leftEnd && left[leftSignificant] == '0') leftSignificant++;
                int rightSignificant = rightIndex;
                while (rightSignificant < rightEnd && right[rightSignificant] == '0') rightSignificant++;
                int comparison = (leftEnd - leftSignificant).CompareTo(rightEnd - rightSignificant);
                if (comparison != 0) return comparison;
                for (int offset = 0; offset < leftEnd - leftSignificant; offset++)
                {
                    comparison = left[leftSignificant + offset].CompareTo(right[rightSignificant + offset]);
                    if (comparison != 0) return comparison;
                }
                leftIndex = leftEnd;
                rightIndex = rightEnd;
                continue;
            }

            int characterComparison = CharacterRank(left[leftIndex]).CompareTo(CharacterRank(right[rightIndex]));
            if (characterComparison != 0) return characterComparison;
            leftIndex++;
            rightIndex++;
        }
        return (leftIndex == left.Length, rightIndex == right.Length) switch
        {
            (true, true) => 0,
            (true, false) => -1,
            _ => 1
        };
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static int CharacterRank(char c) => c switch
    {
        >= 'a' and <= 'z' => c - 'a',
        >= '0' and <= '9' => 26 + c - '0',
        _ => 36 + c
    };
}
