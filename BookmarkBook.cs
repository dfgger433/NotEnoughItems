using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NotEnoughItems;

// The legacy URI escaped page:id records remain readable; v2 adds an explicit group and amount.
internal sealed class BookmarkBook
{
    internal const string Format = "nei2";
    internal const int MaximumQuantity = 9999;
    private const int Limit = 2048;
    internal readonly List<Entry> Entries = new();

    internal sealed class Entry(string page, string group, string id, int quantity)
    {
        internal string Page = page;
        internal string Group = group;
        internal string Id { get; } = id;
        internal int Quantity = quantity;
        internal bool IsRecipe => Id.StartsWith("recipe/", StringComparison.Ordinal);
    }

    internal IEnumerable<Entry> Page(string page) => Entries.Where(e => e.Page == page);
    internal Entry? Find(string page, string id) => Entries.Find(e => e.Page == page && e.Id == id);

    internal bool Toggle(string page, string id, int quantity)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var entry = Find(page, id);
        if (entry != null) { Entries.Remove(entry); return true; }
        if (Entries.Count >= Limit) return false;
        Entries.Add(new Entry(page, string.Empty, id, Math.Clamp(quantity, 1, MaximumQuantity)));
        return true;
    }

    internal bool Adjust(Entry entry, int delta)
    {
        int value = Math.Clamp(entry.Quantity + delta, 1, MaximumQuantity);
        if (value == entry.Quantity) return false;
        entry.Quantity = value;
        return true;
    }

    // Dropping on another entry groups them; shift-drag moves/reorders without creating a new group.
    internal bool Move(Entry source, Entry target, bool reorderOnly)
    {
        if (source == target || source.Page != target.Page || !Entries.Contains(source) || !Entries.Contains(target)) return false;
        if (!reorderOnly && target.Group.Length == 0) target.Group = Guid.NewGuid().ToString("N");
        source.Group = target.Group;
        Entries.Remove(source);
        Entries.Insert(Entries.IndexOf(target) + 1, source);
        return true;
    }

    internal bool Split(Entry entry)
    {
        if (entry.Group.Length == 0) return false;
        entry.Group = string.Empty;
        return true;
    }

    internal bool MoveGroup(string page, string group, Entry target)
    {
        if (group.Length == 0 || target.Page != page || target.Group == group || !Entries.Contains(target)) return false;
        var moved = Page(page).Where(entry => entry.Group == group).ToArray();
        if (moved.Length == 0) return false;
        foreach (var entry in moved) Entries.Remove(entry);
        int index = target.Group.Length == 0 ? Entries.IndexOf(target) :
            Entries.FindLastIndex(entry => entry.Page == page && entry.Group == target.Group);
        Entries.InsertRange(index + 1, moved);
        return true;
    }

    internal bool Ungroup(string page, string group)
    {
        if (group.Length == 0) return false;
        bool changed = false;
        foreach (var entry in Page(page).Where(entry => entry.Group == group)) { entry.Group = string.Empty; changed = true; }
        return changed;
    }

    internal string Serialize(IEnumerable<Entry>? entries = null) => Format + "|" + string.Join("|", (entries ?? Entries).Select(e =>
        string.Join(":", Uri.EscapeDataString(e.Page), Uri.EscapeDataString(e.Group), Uri.EscapeDataString(e.Id), e.Quantity.ToString(CultureInfo.InvariantCulture))));

    internal bool Load(string? data, string? importToPage = null)
    {
        if (string.IsNullOrWhiteSpace(data) || data!.Length > 512_000) return false;
        string[] records = data.Split('|');
        bool v2 = records[0] == Format;
        if (importToPage != null && !v2) return false;
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        bool changed = false;
        foreach (string record in records.Skip(v2 ? 1 : 0))
        {
            if (Entries.Count >= Limit) break;
            var fields = record.Split(':');
            if (fields.Length != (v2 ? 4 : 2)) continue;
            try
            {
                string page = importToPage ?? Uri.UnescapeDataString(fields[0]);
                string id = Uri.UnescapeDataString(fields[v2 ? 2 : 1]);
                if (page.Length == 0 || page.Length > 128 || id.Length == 0 || id.Length > 256 || Find(page, id) != null) continue;
                string group = v2 ? Uri.UnescapeDataString(fields[1]) : string.Empty;
                if (group.Length > 128) continue;
                if (importToPage != null && group.Length > 0)
                {
                    if (!groups.TryGetValue(group, out var replacement)) groups.Add(group, replacement = Guid.NewGuid().ToString("N"));
                    group = replacement;
                }
                int amount = v2 && int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 1;
                Entries.Add(new Entry(page, group, id, Math.Clamp(amount, 1, MaximumQuantity)));
                changed = true;
            }
            catch (UriFormatException) { /* Ignore malformed individual records. */ }
        }
        return changed;
    }
}
