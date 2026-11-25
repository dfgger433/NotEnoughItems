using System.Globalization;
using System.Text;
using System.Text.Json;
using NotEnoughItems;

internal static class LocalizationTests
{
    internal static void ShippedFiles()
    {
        var english = ReadPack("en_us");
        var chinese = ReadPack("zh_cn");
        Check(english.Count > 0 && english.Keys.ToHashSet().SetEquals(chinese.Keys), "Shipped language keys must match.");
        var catalog = NeiText.Load(AppContext.BaseDirectory);
        foreach (var (key, text) in english)
        {
            var en = CompositeFormat.Parse(text);
            var zh = CompositeFormat.Parse(chinese[key]);
            var arguments = Enumerable.Range(0, en.MinimumArgumentCount).Select(index => (object)$"<arg{index}>").ToArray();
            string enResult = string.Format(CultureInfo.InvariantCulture, en, arguments);
            string zhResult = string.Format(CultureInfo.InvariantCulture, zh, arguments);
            Check(arguments.All(arg => enResult.Contains((string)arg) && zhResult.Contains((string)arg)),
                $"Both languages must use the same placeholders for '{key}'.");
            Check(catalog.Resolve(key, false, arguments) == enResult && catalog.Resolve(key, true, arguments) == zhResult,
                $"The loader must resolve both shipped translations for '{key}'.");
        }
#if RECIPE_TESTS
        using var fallback = typeof(Catalog).Assembly.GetManifestResourceStream("Lang/en_us.json");
        Check(fallback != null, "The built plugin must contain its English fallback.");
        using var reader = new StreamReader(fallback!);
        Check(reader.ReadToEnd() == File.ReadAllText(PackPath("en_us")), "The English fallback must come from the shipped JSON.");
#endif
    }

    internal static void LooseFiles()
    {
        const string label = "options.title";
        const string formatted = "method.preview_test";
        string bundledEnglish = ReadPack("en_us")[label];
        string root = Path.Combine(AppContext.BaseDirectory, $"nei-lang-{Guid.NewGuid():N}");
        string lang = Path.Combine(root, "Lang");
        string english = Path.Combine(lang, "en_us.json");
        string chinese = Path.Combine(lang, "zh_cn.json");
        try
        {
            Check(NeiText.Load(root).Resolve(label, chinese: true) == bundledEnglish,
                "Missing language directory must use built-in English.");
            Directory.CreateDirectory(lang);
            File.WriteAllText(english, """
                {"options.title":"Edited English","test.order":"First {0}, second {1}","method.preview_test":"Edited {0:D2}","test.only_english":"Only in English"}
                """);
            File.WriteAllText(chinese, """
                {"options.title":"  ","test.order":"第二个 {1}，第一个 {0}","method.preview_test":"无效 {2}","test.invalid":2,"test.missing":null}
                """);
            var warnings = new List<string>();
            var catalog = NeiText.Load(root, warnings.Add);
            Check(catalog.Resolve(label, chinese: true) == "Edited English" && catalog.Resolve(label, chinese: false) == "Edited English",
                "Blank Chinese values should fall back to editable English.");
            Check(catalog.Resolve("test.order", true, "A", "B") == "第二个 B，第一个 A" &&
                catalog.Resolve("test.order", false, "A", "B") == "First A, second B",
                "Switching language should select the right format immediately and support reordered arguments.");
            Check(catalog.Resolve(formatted, true, 3) == "Edited 03" && catalog.Resolve("test.only_english", true) == "Only in English" &&
                catalog.Resolve("test.unknown", true) == "test.unknown",
                "Missing and malformed translations fall back to English; unrecognized keys remain identifiable.");
            Check(warnings.Any(warning => warning.Contains("test.invalid")) && warnings.Any(warning => warning.Contains("test.missing")),
                "Non-string values should be ignored without discarding valid entries.");

            File.WriteAllText(chinese, "{invalid");
            Check(NeiText.Load(root, warnings.Add).Resolve(label, chinese: true) == "Edited English" &&
                warnings.Any(warning => warning.Contains(chinese)), "A malformed JSON file must be logged and skipped.");
            File.Delete(chinese);
            Check(NeiText.Load(root).Resolve(label, chinese: true) == "Edited English", "A missing Chinese file should still use editable English.");
            File.WriteAllText(english, """{"options.title":"Unclosed {0"}""");
            Check(NeiText.Load(root).Resolve(label, chinese: false) == bundledEnglish, "A malformed English template must use built-in English.");
            File.WriteAllText(english, "{}");
            Check(NeiText.Load(root).Resolve(label, chinese: false) == bundledEnglish, "An incomplete English file must use built-in English.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string PackPath(string locale) => Path.Combine(AppContext.BaseDirectory, "Lang", locale + ".json");

    private static Dictionary<string, string> ReadPack(string locale)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PackPath(locale)));
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property =>
        {
            string value = property.Value.GetString() ?? string.Empty;
            Check(!string.IsNullOrWhiteSpace(value), $"Empty translation for '{property.Name}' in {locale}.");
            return value;
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
