using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NotEnoughItems;

/// <summary>Named item-catalog searches shown by the right-hand catalog group controls.</summary>
/// <remarks>
/// A group owns its query; selecting a group is expected to re-run the catalog filter with that query.
/// These groups are independent from bookmark pages and bookmark groups.
/// </remarks>
internal sealed class CatalogGroups
{
    internal const int MaxNameLength = 32;
    internal const int MaxQueryLength = 1024;
    private const string Format = "cg1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly List<Group> _groups = new();
    private int _selected;

    /// <summary>One named saved search.</summary>
    internal sealed class Group
    {
        internal Group(string name, string query)
        {
            Name = name;
            Query = query;
        }

        internal string Name { get; set; }
        internal string Query { get; set; }
    }

    internal int Count => _groups.Count;
    internal int SelectedIndex => _selected;
    internal Group Current => _groups[_selected];
    internal Group this[int index] => _groups[index];

    /// <summary>The index a single next-button press would select.</summary>
    internal int NextIndex => _groups.Count == 0 ? -1 : (_selected + 1) % _groups.Count;

    /// <summary>Replaces the current groups with one empty default group.</summary>
    internal void Reset(string defaultName)
    {
        string? name = NormalizeName(defaultName);
        _groups.Clear();
        _groups.Add(new Group(name ?? "Group 1", string.Empty));
        _selected = 0;
    }

    /// <summary>Appends and selects a group. Names and queries must be within the documented limits.</summary>
    internal bool Add(string name, string query = "")
    {
        string? normalized = NormalizeName(name);
        if (normalized == null || !IsNameAvailable(normalized) || !ValidChars(query) || query.Length > MaxQueryLength)
            return false;
        _groups.Add(new Group(normalized, query));
        _selected = _groups.Count - 1;
        return true;
    }

    /// <summary>Removes the selected group; the last remaining group is never removed.</summary>
    internal bool RemoveSelected()
    {
        if (_groups.Count <= 1) return false;
        _groups.RemoveAt(_selected);
        if (_selected >= _groups.Count) _selected = _groups.Count - 1;
        return true;
    }

    /// <summary>Selects an existing group. Returns whether the selection changed.</summary>
    internal bool Select(int index)
    {
        if (index < 0 || index >= _groups.Count || index == _selected) return false;
        _selected = index;
        return true;
    }

    /// <summary>Trims and validates a group name. Empty, oversized, malformed, or duplicate names are rejected.</summary>
    internal CatalogGroupRenameResult Rename(int index, string? name, out string normalized)
    {
        normalized = string.Empty;
        if (index < 0 || index >= _groups.Count) return CatalogGroupRenameResult.Invalid;
        string? candidate = name?.Trim();
        if (string.IsNullOrEmpty(candidate)) return CatalogGroupRenameResult.Empty;
        if (!IsValidName(candidate, out string valid)) return CatalogGroupRenameResult.Invalid;
        if (!IsNameAvailable(valid, index)) return CatalogGroupRenameResult.Duplicate;
        _groups[index].Name = valid;
        normalized = valid;
        return CatalogGroupRenameResult.Renamed;
    }

    /// <summary>Stores a query verbatim. Returns whether it changed.</summary>
    internal bool SetQuery(int index, string? query)
    {
        if (index < 0 || index >= _groups.Count) return false;
        query ??= string.Empty;
        if (query.Length > MaxQueryLength || !ValidChars(query)) return false;
        if (string.Equals(_groups[index].Query, query, StringComparison.Ordinal)) return false;
        _groups[index].Query = query;
        return true;
    }

    internal bool IsNameAvailable(string? name, int exceptIndex = -1)
    {
        string? normalized = NormalizeName(name, out string valid);
        if (normalized == null) return false;
        for (int i = 0; i < _groups.Count; i++)
        {
            if (i == exceptIndex) continue;
            if (string.Equals(_groups[i].Name, valid, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    /// <summary>Serializes groups, their queries, and the current selection into one stable config value.</summary>
    internal string Serialize()
    {
        var text = new StringBuilder(Format).Append('|').Append(_selected.ToString(CultureInfo.InvariantCulture));
        foreach (Group group in _groups)
            text.Append('|').Append(Escape(group.Name)).Append('|').Append(Escape(group.Query));
        return text.ToString();
    }

    /// <summary>Loads a complete, valid payload atomically. Malformed data leaves the current groups untouched.</summary>
    internal bool Load(string? data)
    {
        if (string.IsNullOrEmpty(data)) return false;
        string[] parts = data.Split('|');
        if (parts.Length < 4 || parts[0] != Format || (parts.Length - 2) % 2 != 0) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int selected)) return false;
        var loaded = new List<Group>();
        for (int i = 2; i < parts.Length; i += 2)
        {
            if (!Decode(parts[i], MaxNameLength, out string name) || string.IsNullOrWhiteSpace(name)) return false;
            if (!IsValidName(name.Trim(), out name)) return false;
            if (loaded.Exists(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))) return false;
            if (!Decode(parts[i + 1], MaxQueryLength, out string query)) return false;
            loaded.Add(new Group(name, query));
        }
        if (loaded.Count == 0) return false;
        _groups.Clear();
        _groups.AddRange(loaded);
        _selected = Math.Clamp(selected, 0, _groups.Count - 1);
        return true;
    }

    private static string? NormalizeName(string? name) => NormalizeName(name, out _);

    private static string? NormalizeName(string? name, out string valid)
    {
        valid = string.Empty;
        string? trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return IsValidName(trimmed, out valid) ? valid : null;
    }

    private static bool IsValidName(string name, out string valid)
    {
        valid = name;
        return name.Length <= MaxNameLength && ValidChars(name);
    }

    private static bool ValidChars(string? text)
    {
        if (text == null) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsControl(text[i]) || char.IsLowSurrogate(text[i])) return false;
            if (char.IsHighSurrogate(text[i]) && (++i >= text.Length || !char.IsLowSurrogate(text[i]))) return false;
        }
        return true;
    }

    private static string Escape(string text) => Uri.EscapeDataString(text);

    private static bool Decode(string field, int limit, out string value)
    {
        value = string.Empty;
        if (field.Length > limit * 9) return false;
        // Uri.UnescapeDataString is permissive; reject bad percent triplets and invalid UTF-8 first.
        int start = field.IndexOf('%');
        if (start >= 0)
        {
            var bytes = new byte[field.Length / 3];
            try
            {
                for (int i = start; i < field.Length; i++)
                {
                    if (field[i] != '%') continue;
                    int count = 0;
                    do
                    {
                        if (i + 2 >= field.Length || !byte.TryParse(field.Substring(i + 1, 2), NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture, out bytes[count])) return false;
                        count++;
                        i += 3;
                    } while (i < field.Length && field[i] == '%');
                    StrictUtf8.GetCharCount(bytes, 0, count);
                    i--;
                }
            }
            catch (DecoderFallbackException) { return false; }
        }
        value = Uri.UnescapeDataString(field);
        return value.Length <= limit && ValidChars(value);
    }
}

/// <summary>Why a catalog-group rename was accepted or rejected.</summary>
internal enum CatalogGroupRenameResult
{
    Renamed,
    Empty,
    Duplicate,
    Invalid
}
