using System;
using System.Collections.Generic;
using System.Linq;

namespace NotEnoughItems;

internal static class CatalogListing
{
    internal static CatalogItem[] Filter(IEnumerable<CatalogItem> items, string query, PinyinSearch? pinyin,
        ISet<string> favorites, ISet<string> carried, Func<string, string>? sourceName = null) => items
        .Where(item => item.IsCatalogVisible &&
            NeiSearch.Matches(query, item.SearchText, item.Source, string.Join(" ", item.Tags), item.Description, item.Id, pinyin, sourceName))
        // OrderBy is stable: keep catalog order within each priority, including when a search is active.
        .OrderBy(item => Priority(item.BrowseId, favorites, carried))
        .ToArray();

    private static int Priority(string id, ISet<string> favorites, ISet<string> carried) =>
        favorites.Contains(id) ? 0 : carried.Contains(id) ? 1 : 2;
}
