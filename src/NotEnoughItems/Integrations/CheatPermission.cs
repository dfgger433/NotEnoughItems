using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NotEnoughItems;

internal enum CheatAccess { Unavailable, Offline, Host, GrantedClient }

// Only the server owns this table. Use the connection's player object rather
// than its name or numeric ID, both of which can be reused after disconnect.
internal sealed class NeiCheatGrants<T> where T : class
{
    private readonly Dictionary<T, (float Give, float Query)> _grants = new(ReferenceEqualityComparer.Instance);
    private object? _transport;

    internal void SetTransport(object? transport)
    {
        if (ReferenceEquals(_transport, transport)) return;
        _grants.Clear();
        _transport = transport;
    }

    internal bool Change(T player, bool add) => _transport != null &&
        (add ? _grants.TryAdd(player, default) : _grants.Remove(player));

    internal bool Allows(T player) => _transport != null && _grants.ContainsKey(player);

    internal bool TrySpend(T player, float now, bool query)
    {
        if (!_grants.TryGetValue(player, out var next) || now < (query ? next.Query : next.Give)) return false;
        _grants[player] = query ? (next.Give, now + 1f) : (now + .25f, next.Query);
        return true;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<T>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

internal static class CheatPermission
{
    internal const int MaxItems = 64;
    private const int MaxIdLength = 128;
    internal const int MaxPacketBytes = 1 + MaxItems * (1 + MaxIdLength);

    // Give packets consist only of a total item count, then length-prefixed
    // resource IDs. The empty count is reserved for a permission query.
    internal static byte[]? EncodeGive(IEnumerable<(string Id, int Amount)> items)
    {
        using var packet = new MemoryStream();
        packet.WriteByte(0);
        int count = 0;
        foreach (var (id, amount) in items)
        {
            if (amount < 1 || amount > MaxItems - count || !IsResourceId(id)) return null;
            byte[] bytes = Encoding.ASCII.GetBytes(id);
            for (int i = 0; i < amount; i++)
            {
                packet.WriteByte((byte)bytes.Length);
                packet.Write(bytes, 0, bytes.Length);
            }
            count += amount;
        }
        if (count == 0) return null;
        packet.Position = 0;
        packet.WriteByte((byte)count);
        return packet.ToArray();
    }

    internal static string[]? DecodeGive(byte[] packet)
    {
        if (packet.Length is < 1 or > MaxPacketBytes || packet[0] > MaxItems) return null;
        var ids = new string[packet[0]];
        int offset = 1;
        for (int i = 0; i < ids.Length; i++)
        {
            if (offset >= packet.Length) return null;
            int length = packet[offset++];
            if (length == 0 || length > MaxIdLength || length > packet.Length - offset) return null;
            string id = Encoding.ASCII.GetString(packet, offset, length);
            if (!IsResourceId(id)) return null;
            ids[i] = id;
            offset += length;
        }
        return offset == packet.Length ? ids : null;
    }

    private static bool IsResourceId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength) return false;
        foreach (char c in id)
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '/' or '.')) return false;
        foreach (string segment in id.Split('/'))
            if (segment is "" or "." or "..") return false;
        return true;
    }

    internal static T? FindUniquePlayer<T>(IEnumerable<T> players, string selector, Func<T, string?> name, Func<T, ushort> id) where T : class
    {
        bool byId = selector.StartsWith("id:", StringComparison.OrdinalIgnoreCase);
        ushort target = 0;
        if (byId && !ushort.TryParse(selector.AsSpan(3), System.Globalization.NumberStyles.None, null, out target)) return null;
        T? match = null;
        foreach (var player in players)
        {
            if (!(byId ? id(player) == target : string.Equals(name(player), selector, StringComparison.OrdinalIgnoreCase))) continue;
            if (match != null) return null;
            match = player;
        }
        return match;
    }
}
