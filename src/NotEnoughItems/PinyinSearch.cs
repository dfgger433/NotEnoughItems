using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace NotEnoughItems;

/// <summary>Adapts the optional Just Unknown Characters plug-in without loading or patching it ourselves.</summary>
internal sealed class PinyinSearch(Func<string, string, bool> contains, Func<string, string>? generate = null)
{
    internal const string PluginGuid = "JustUnknownCharacters";
    private const string ProviderNamespace = "JustUnknownCharacters.";
    private readonly ConditionalWeakTable<string, Spellings> _spellingCache = new();
    private bool _unavailable;

    internal static PinyinSearch? Connect(Assembly? assembly)
    {
        if (assembly == null) return null;
        try
        {
            var matcher = Bind<Func<string, string, bool>>(assembly, "PinyinMatcher", "Contains", typeof(string), typeof(string));
            if (matcher == null) return null;
            var variants = Bind<Func<string, string>>(assembly, "PinyinSearchVariants", "Generate", typeof(string));
            return new PinyinSearch(matcher, variants);
        }
        catch (Exception error) when (ProviderUnavailable(error) || error is ArgumentException or AmbiguousMatchException)
        {
            return null;
        }
    }

    internal bool Matches(string text, string term)
    {
        if (_unavailable || !HasPinyinLetters(term) || !HasHan(text)) return false;
        string spelling;
        try { spelling = Normalize(term); }
        catch (ArgumentException) { return false; } // An incomplete Unicode input must not interrupt filtering.
        if (spelling.Length == 0) return false;
        try
        {
            if (contains(text, term) || (spelling != term && contains(text, spelling))) return true;

            // In JUC 1.0.1 Contains handles mixed scripts and polyphonic characters,
            // but can miss initials of vowel-led syllables, e.g. aq for 安全.
            // Generate includes those initials. Keep non-Han boundaries intact rather
            // than letting Generate concatenate across unrelated labels or punctuation.
            return generate != null && _spellingCache.GetValue(text, CreateSpellings).Value
                .IndexOf(spelling, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception error) when (ProviderUnavailable(error))
        {
            _unavailable = true;
            return false;
        }
    }

    private static T? Bind<T>(Assembly assembly, string type, string name, params Type[] parameters) where T : Delegate
    {
        var method = assembly.GetType(ProviderNamespace + type)?.GetMethod(name, BindingFlags.Public | BindingFlags.Static,
            null, parameters, null);
        return method == null ? null : (T?)Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false);
    }

    private static bool ProviderUnavailable(Exception error) => error is TypeInitializationException or TypeLoadException
        or MissingMemberException or FileNotFoundException or FileLoadException or BadImageFormatException;

    private Spellings CreateSpellings(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length;)
        {
            if (!IsHan(text[i])) { i++; continue; }
            int start = i++;
            while (i < text.Length && IsHan(text[i])) i++;
            if (result.Length > 0) result.Append(' ');
            result.Append(generate!(text.Substring(start, i - start)));
        }
        return new Spellings(result.ToString());
    }

    private sealed class Spellings(string value)
    {
        internal string Value { get; } = value;
    }

    private static bool HasHan(string text)
    {
        foreach (char c in text)
            if (IsHan(c)) return true;
        return false;
    }

    private static bool IsHan(char c) => c is '\u3007' or >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff'
        or >= '\uf900' and <= '\ufaff';

    private static bool HasPinyinLetters(string term)
    {
        foreach (char c in term)
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '\u00c0' and <= '\u024f') return true;
        return false;
    }

    private static string Normalize(string term)
    {
        var result = new StringBuilder(term.Length);
        foreach (char c in term.Normalize(NormalizationForm.FormD))
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                // JUC's dictionary spells ü as v; other combining marks are tones.
                if (c == '\u0308' && result.Length > 0 && result[result.Length - 1] is 'u' or 'U')
                    result[result.Length - 1] = 'v';
            }
            else if (!char.IsWhiteSpace(c) && c is not ('\'' or '\u2019')) result.Append(c);
        }
        return result.ToString();
    }
}
