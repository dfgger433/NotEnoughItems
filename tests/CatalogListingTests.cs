using NotEnoughItems;

internal static class CatalogListingTests
{
    internal static void Run()
    {
        CatalogItem[] items =
        [
            new("stone"),
            new("rope", tags: ["craft"]),
            new("water"),
            new("water", kind: CatalogItemKind.Liquid),
            new("cloth", tags: ["craft"]),
            new("needle", tags: ["craft"]),
            new("tag:meat", "Meat-tagged item", kind: CatalogItemKind.Virtual, isVirtual: true, source: "native")
        ];
        var favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ITEM:CLOTH", "item:needle" };
        var carried = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "liquid:water", "item:rope", "item:cloth" };

        Expect("", "item:cloth", "item:needle", "item:rope", "liquid:water", "item:stone", "item:water");
        Expect("$craft -needle", "item:cloth", "item:rope");
        Expect("&water", "liquid:water", "item:water");

        favorites.Remove("ITEM:CLOTH");
        carried.Remove("item:rope");
        carried.Add("item:stone");
        Expect("", "item:needle", "item:stone", "liquid:water", "item:cloth", "item:rope", "item:water");

        favorites.Clear();
        carried.Clear();
        Expect("", "item:stone", "item:rope", "item:water", "liquid:water", "item:cloth", "item:needle");
        Expect("@CasualtiesUnknown &water", "item:water", "liquid:water");
        if (CatalogListing.Filter(items, "meat", null, favorites, carried).Any(item => item.BrowseId == "tag:meat"))
            throw new InvalidOperationException("Internal tag requirements must not appear in the browsable catalog.");

        var modded = new CatalogItem("press", source: "forge.tools");
        string SourceName(string source) => source == "forge.tools" ? "Forge Guild Tools" : source;
        foreach (string query in new[] { "@forgeguildtools", "@\"Forge Guild Tools\"", "@forge.tools" })
        {
            var matches = CatalogListing.Filter(items.Append(modded), query, null, favorites, carried, SourceName);
            if (!matches.SequenceEqual([modded]))
                throw new InvalidOperationException($"'{query}' must recognize only the mod's display name or registered source.");
        }

        void Expect(string query, params string[] expected)
        {
            var actual = CatalogListing.Filter(items, query, null, favorites, carried).Select(item => item.BrowseId);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Unexpected catalog order for '{query}': {string.Join(", ", actual)}");
        }
    }
}
