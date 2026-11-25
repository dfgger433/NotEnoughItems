using System;
using System.Collections.Generic;
using System.Text;

namespace NotEnoughItems;

internal static class NeiSearch
{
    internal const string NativeSourceName = "Casualties: Unknown";
    private static readonly string CompactNativeSourceName = CompactSource(NativeSourceName);

    internal static bool Matches(string query, string text, string source, string tags, string tooltip, string id,
        PinyinSearch? pinyin = null, Func<string, string>? sourceName = null) =>
        MatchesFields(query, text, key => key switch { '@' => source, '$' => tags, '#' => tooltip, '&' => id, _ => null }, pinyin, sourceName);

    private static bool MatchesFields(string query, string text, Func<char, string?> field, PinyinSearch? pinyin,
        Func<string, string>? sourceName)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        bool hadTerm = false, passes = true;
        foreach (string raw in Terms(query))
        {
            if (raw == "|")
            {
                if (hadTerm && passes) return true;
                hadTerm = false;
                passes = true;
                continue;
            }
            hadTerm = true;
            if (!passes) continue;
            bool exclude = raw.Length > 1 && raw[0] == '-';
            string term = exclude ? raw.Substring(1) : raw;
            string? selected = term.Length > 1 ? field(term[0]) : null;
            char prefix = selected == null ? '\0' : term[0];
            if (selected != null) term = term.Substring(1);
            string candidate = selected ?? text;
            // A pinyin alias belongs only to the field it came from, never IDs or owners.
            bool matches = prefix == '@' ? MatchesSource(candidate, term, sourceName) :
                candidate.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (prefix is '\0' or '$' or '#' && pinyin?.Matches(candidate, term) == true);
            if (matches == exclude) passes = false;
        }
        return hadTerm && passes;
    }

    private static bool MatchesSource(string source, string term, Func<string, string>? sourceName)
    {
        string key = CompactSource(term);
        if (key.Length == 0) return false;
        if (CompactSource(source).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (source.Equals("native", StringComparison.OrdinalIgnoreCase))
            return "CasualtiesUnknown".IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                CompactNativeSourceName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        string? displayName = sourceName?.Invoke(source);
        return displayName != null && !string.Equals(displayName, source, StringComparison.OrdinalIgnoreCase) &&
            CompactSource(displayName).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string CompactSource(string value)
    {
        int first = 0;
        while (first < value.Length && !char.IsWhiteSpace(value[first])) first++;
        if (first == value.Length) return value;
        var result = new StringBuilder(value.Length).Append(value, 0, first);
        for (int i = first + 1; i < value.Length; i++)
            if (!char.IsWhiteSpace(value[i])) result.Append(value[i]);
        return result.ToString();
    }

    private static IEnumerable<string> Terms(string query)
    {
        bool quoted = false;
        var term = new StringBuilder();
        foreach (char c in query)
        {
            if (c == '"') { quoted = !quoted; continue; }
            if (!quoted && (char.IsWhiteSpace(c) || c == '|'))
            {
                if (term.Length > 0) { yield return term.ToString(); term.Clear(); }
                if (c == '|') yield return "|";
            }
            else term.Append(c);
        }
        if (term.Length > 0) yield return term.ToString();
    }
}
