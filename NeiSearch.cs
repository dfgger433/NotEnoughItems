using System;
using System.Collections.Generic;
using System.Text;

namespace NotEnoughItems;

internal static class NeiSearch
{
    internal static bool Matches(string query, string text, string source, string tags, string tooltip, string id) =>
        MatchesFields(query, text, key => key switch { '@' => source, '$' => tags, '#' => tooltip, '&' => id, _ => null });

    internal static bool MatchesFields(string query, string text, Func<char, string?> field)
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
            if (selected != null) term = term.Substring(1);
            if (((selected ?? text).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) == exclude) passes = false;
        }
        return hadTerm && passes;
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
