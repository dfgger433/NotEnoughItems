using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NotEnoughItems;

internal sealed class NeiText
{
    private const string English = "en_us.json";
    private const string Chinese = "zh_cn.json";
    private const string FallbackResource = "Lang/" + English;
    private static readonly char[] FormatBraces = { '{', '}' };
    private static readonly Lazy<NeiText> Default = new(() => Load(Path.GetDirectoryName(typeof(NeiText).Assembly.Location)));
    private static NeiText? _current;

    private readonly IReadOnlyDictionary<string, string> _english, _chinese, _fallback;

    private NeiText(IReadOnlyDictionary<string, string> english, IReadOnlyDictionary<string, string> chinese,
        IReadOnlyDictionary<string, string> fallback)
    {
        _english = english;
        _chinese = chinese;
        _fallback = fallback;
    }

    internal static void Initialize(string? pluginDirectory, Action<string> warn) => _current = Load(pluginDirectory, warn);

    internal static string Get(string key, bool chinese, params object[] args) =>
        (_current ?? Default.Value).Resolve(key, chinese, args);

    internal static NeiText Load(string? pluginDirectory, Action<string>? warn = null)
    {
        // Keep English available when files are missing or edited incorrectly. Loose files always take priority.
        using var stream = typeof(NeiText).Assembly.GetManifestResourceStream(FallbackResource)
            ?? throw new InvalidOperationException($"Missing built-in language resource: {FallbackResource}");
        using var reader = new StreamReader(stream);
        var fallback = Read(reader, FallbackResource, warn);
        string? directory = pluginDirectory == null ? null : Path.Combine(pluginDirectory, "Lang");
        return new NeiText(ReadFile(directory, English, warn), ReadFile(directory, Chinese, warn), fallback);
    }

    internal string Resolve(string key, bool chinese, params object[] args)
    {
        if (chinese && TryFormat(_chinese, key, args, out var translation)) return translation;
        if (TryFormat(_english, key, args, out var english)) return english;
        return TryFormat(_fallback, key, args, out var fallback) ? fallback : key;
    }

    private static Dictionary<string, string> ReadFile(string? directory, string file, Action<string>? warn)
    {
        if (directory != null)
        {
            string path = Path.Combine(directory, file);
            try
            {
                using var reader = File.OpenText(path);
                return Read(reader, path, warn);
            }
            catch (FileNotFoundException) { } // Optional; an absent file follows the same fallback as absent keys.
            catch (DirectoryNotFoundException) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                warn?.Invoke($"Could not read NEI language file '{path}': {error.Message}");
            }
        }
        return new Dictionary<string, string>();
    }

    private static Dictionary<string, string> Read(TextReader input, string source, Action<string>? warn)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        using var json = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None };
        var data = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (json.Read()) throw new JsonReaderException("Expected a single language object.");
        foreach (var property in data.Properties())
        {
            if (property.Value.Type == JTokenType.String && property.Value.Value<string>() is { } text && !string.IsNullOrWhiteSpace(text))
                entries.Add(property.Name, text);
            else
                warn?.Invoke($"Ignoring NEI language key '{property.Name}' in '{source}': expected a non-empty string.");
        }
        return entries;
    }

    private static bool TryFormat(IReadOnlyDictionary<string, string> entries, string key, object[] args, out string result)
    {
        if (entries.TryGetValue(key, out var text))
        {
            try
            {
                result = args.Length == 0 && text.IndexOfAny(FormatBraces) < 0
                    ? text : string.Format(CultureInfo.CurrentCulture, text, args);
                return true;
            }
            catch (FormatException) { } // A malformed user template should not interrupt rendering.
        }
        result = string.Empty;
        return false;
    }
}
