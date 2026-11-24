using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

internal static class CatalogListing
{
    // Filtering retains the definition order, regardless of what the player carries or bookmarks.
    internal static CatalogItem[] Filter(IEnumerable<CatalogItem> items, string query, PinyinSearch? pinyin,
        Func<string, string>? sourceName = null) => items
        .Where(item => item.IsCatalogVisible &&
            NeiSearch.Matches(query, item.SearchText, item.Source, string.Join(" ", item.Tags), item.Description, item.Id, pinyin, sourceName))
        .ToArray();
}
