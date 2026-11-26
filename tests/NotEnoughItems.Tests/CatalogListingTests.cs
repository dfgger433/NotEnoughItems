using System.Reflection;
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

    internal static void RegistrationOrder()
    {
        const string source = "test.provider-order";
        var catalog = new Catalog();
        catalog.RegisterItem(new CatalogItem("z-tool", source: source));
        catalog.RegisterItem(new CatalogItem("other-tool", source: "test.other-provider"));
        catalog.RegisterItem(new CatalogItem("a-fluid", kind: CatalogItemKind.Liquid, source: source));
        catalog.RegisterItem(new CatalogItem("z-tool", "Updated tool", source: source));
        catalog.Refresh();

        var browseItems = (IReadOnlyList<CatalogItem>)typeof(Catalog)
            .GetProperty("BrowseItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(catalog)!;
        var actual = CatalogListing.Filter(browseItems, "@" + source, null);
        if (!actual.Select(item => item.BrowseId).SequenceEqual(["item:z-tool", "liquid:a-fluid"]) ||
            actual[0].DisplayName != "Updated tool")
            throw new InvalidOperationException("Browsing must keep registration order, even when a provider item is updated.");
    }

    internal static void SourceOrder()
    {
        CatalogItem[] items =
        [
            new("tenth", source: "zz.ten"),
            new("z-last-by-id", source: "zz.first", tags: ["drop"]),
            new("native-water", kind: CatalogItemKind.Liquid, source: "Native"),
            new("other", source: "aa.second"),
            new("digit-first", source: "aa.digit"),
            new("native-stone"),
            new("a-first-by-id", source: "ZZ.FIRST"),
            new("zebra", source: "aa.zebra"),
            new("fallback", source: "Middle"),
            new("second-digit", source: "second-digit"),
            new("a-only", source: "prefix"),
            new("zero", source: "zero")
        ];
        string SourceName(string source) => source switch
        {
            "zz.ten" => "A10",
            "zz.first" or "ZZ.FIRST" => " a B ",
            "aa.second" => "Ab",
            "aa.digit" => "0Start",
            "aa.zebra" => "Zebra",
            "second-digit" => "A2",
            "prefix" => "a",
            "zero" => "A0",
            _ => source
        };

        string[] fullOrder =
        [
            "native-water", "native-stone", "a-only", "z-last-by-id", "a-first-by-id", "other",
            "zero", "second-digit", "tenth", "fallback", "zebra", "digit-first"
        ];
        Expect("", fullOrder);
        Expect("-$drop", fullOrder.Where(id => id != "z-last-by-id").ToArray());
        Expect("@ab", "z-last-by-id", "a-first-by-id", "other");
        Expect("@CasualtiesUnknown", "native-water", "native-stone");
        Expect("", fullOrder);

        CatalogItem[] numericItems =
        [
            new("ten", source: "numeric-ten"),
            new("two", source: "numeric-two"),
            new("zero-padded", source: "numeric-zero-padded"),
            new("zero-equal", source: "numeric-zero-equal"),
            new("suffix-short", source: "numeric-suffix-short"),
            new("suffix-long", source: "numeric-suffix-long")
        ];
        string NumericSourceName(string source) => source switch
        {
            "numeric-ten" => "A10B1",
            "numeric-two" => "A2B9",
            "numeric-zero-padded" => "A02B10",
            "numeric-zero-equal" => "A2B9",
            "numeric-suffix-short" => "A02",
            "numeric-suffix-long" => "A2b",
            _ => source
        };
        string[] numericOrder = ["suffix-short", "suffix-long", "two", "zero-equal", "zero-padded", "ten"];
        var numericActual = CatalogListing.Filter(numericItems, string.Empty, null, NumericSourceName).Select(item => item.Id);
        if (!numericActual.SequenceEqual(numericOrder))
            throw new InvalidOperationException($"Unexpected numeric source order: {string.Join(", ", numericActual)}");

        void Expect(string query, params string[] expected)
        {
            var actual = CatalogListing.Filter(items, query, null, SourceName).Select(item => item.Id).ToArray();
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Unexpected source order for '{query}': {string.Join(", ", actual)}");
        }
    }
}
