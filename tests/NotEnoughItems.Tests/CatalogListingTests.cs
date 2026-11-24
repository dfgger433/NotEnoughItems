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
        string[] fullOrder = ["item:stone", "item:rope", "item:water", "liquid:water", "item:cloth", "item:needle"];
        Expect("", fullOrder);
        Expect("$craft -needle", "item:rope", "item:cloth");
        Expect("@CasualtiesUnknown &water", "item:water", "liquid:water");
        Expect("$craft | &water", "item:rope", "item:water", "liquid:water", "item:cloth", "item:needle");
        Expect("", fullOrder); // Clearing a search restores the same order.
        if (CatalogListing.Filter(items, "meat", null).Any(item => item.BrowseId == "tag:meat"))
            throw new InvalidOperationException("Internal tag requirements must not appear in the browsable catalog.");

        var modded = new CatalogItem("press", source: "forge.tools");
        string SourceName(string source) => source == "forge.tools" ? "Forge Guild Tools" : source;
        foreach (string query in new[] { "@forgeguildtools", "@\"Forge Guild Tools\"", "@forge.tools" })
        {
            var matches = CatalogListing.Filter(items.Append(modded), query, null, SourceName);
            if (!matches.SequenceEqual([modded]))
                throw new InvalidOperationException($"'{query}' must recognize only the mod's display name or registered source.");
        }

        void Expect(string query, params string[] expected)
        {
            var actual = CatalogListing.Filter(items, query, null).Select(item => item.BrowseId);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Unexpected catalog order for '{query}': {string.Join(", ", actual)}");
        }
    }
}
