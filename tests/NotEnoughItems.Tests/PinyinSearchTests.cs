using System;
using System.Reflection;
using NotEnoughItems;

internal static class PinyinSearchTests
{
    internal static void OptionalProvider()
    {
        Check(PinyinSearch.Connect(null) == null, "A missing plug-in is not required for catalog search.");
        Check(PinyinSearch.Connect(typeof(PinyinSearchTests).Assembly) == null, "An incompatible plug-in does not bind.");
        Check(Search("纤维绳 rope @Native"), "Literal Chinese and English searches work without a plug-in.");
        Check(!Search("xws"), "Without the optional provider there is no transliteration.");

        int calls = 0;
        var unavailable = new PinyinSearch((_, _) =>
        {
            calls++;
            throw new TypeInitializationException("PinyinDict", null);
        });
        Check(!Search("xws", unavailable), "A provider initialization failure is not a search failure.");
        Check(Search("rope", unavailable), "Literal search remains available after a provider failure.");
        Check(!Search("xws", unavailable) && calls == 1, "A broken optional provider is not retried on every item.");
    }

    internal static void SearchSyntax()
    {
        var pinyin = new PinyinSearch((text, query) => (text, query.ToLowerInvariant()) switch
        {
            ("rope 纤维绳 柔韧的绳子 植物", "xws" or "xianweisheng") => true,
            ("植物", "zw") => true,
            ("柔韧的绳子", "rr") => true,
            _ => false
        });
        Check(Search("XWS @native $zw #rr &rope", pinyin), "Pinyin and literal search terms compose across fields.");
        Check(Search("\"xian wei sheng\"", pinyin), "A quoted romanization may delimit syllables with spaces.");
        Check(Search("missing | rope xws", pinyin), "Pinyin terms work in a later OR branch.");
        Check(!Search("rope -xws", pinyin) && !Search("rope -$zw", pinyin), "A matching pinyin exclusion rejects the entry.");
        Check(!Search("#xws", pinyin) && !Search("$rr", pinyin), "A pinyin variant never leaks into another field.");
        var alwaysMatches = new PinyinSearch((_, _) => true);
        Check(!NeiSearch.Matches("@zw | &zw", "", "植物", "", "", "植物", alwaysMatches),
            "Source and stable IDs remain literal even when they contain Han characters.");
    }

    internal static void InstalledProvider(string pluginPath)
    {
        PinyinSearch pinyin = PinyinSearch.Connect(Assembly.LoadFrom(pluginPath))
            ?? throw new InvalidOperationException("Could not bind the installed Just Unknown Characters matcher.");
        Check(Search("xianweisheng", pinyin), "Find the localized rope by full pinyin.");
        Check(Search("xws", pinyin), "Find the localized rope by initials.");
        Check(Search("sheng", pinyin) && Search("xianwei sh", pinyin), "Match subsequences and a partially typed syllable.");
        Check(Search("纤wei", pinyin) && Search("xian维", pinyin), "Mix pinyin and literal Han characters.");
        Check(Search("qiānwéi", pinyin), "Normalize tone marks and match the alternate pronunciation.");
        Check(Search("\"xian wei sheng\"", pinyin) && Search("xian'wei'sheng", pinyin), "Accept pinyin syllable separators.");
        Check(pinyin.Matches("安全", "aq") && pinyin.Matches("棉袄", "mian'ao"), "Match a vowel-led initial and a separated vowel syllable.");
        Check(pinyin.Matches("绿色", "lǜse") && pinyin.Matches("绿色", "lvse"), "Accept ü with marks and its v input spelling.");
        Check(!pinyin.Matches("纤维 用绳", "xws") && !Search("xianweijunk", pinyin), "Reject noncontiguous aliases and unrelated spellings.");
        Check(Search("@native #rr &rope", pinyin) && !Search("-xws", pinyin), "Keep field and exclusion semantics with the real provider.");
    }

    private static bool Search(string query, PinyinSearch? pinyin = null) =>
        NeiSearch.Matches(query, "rope 纤维绳 柔韧的绳子 植物", "native", "植物", "柔韧的绳子", "rope", pinyin);

    private static void Check(bool condition, string explanation)
    {
        if (!condition) throw new InvalidOperationException(explanation);
    }
}
