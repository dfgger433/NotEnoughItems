using System;
using System.Collections.Generic;
using System.Text;

namespace NotEnoughItems;

internal enum CheatAccess { Unavailable, Offline, Host, AdminClient }

internal static class CheatPermission
{
    internal static CheatAccess ForTogether(bool? running, bool hasTransport, bool? server, bool? client, bool? canCheat, bool? canAdmin)
    {
        if (running == false && !hasTransport) return CheatAccess.Offline;
        if (running != true || !hasTransport || canCheat != true) return CheatAccess.Unavailable;
        if (server == true && client == false) return CheatAccess.Host;
        return server == false && client == true && canAdmin == true ? CheatAccess.AdminClient : CheatAccess.Unavailable;
    }

    // Together's admin dispatcher tokenizes on spaces. Accept only a single, plain resource
    // path and always address the caller; do not pass catalog labels or player-entered commands.
    internal static string? GiveCommand(string id, int amount, int maximum)
    {
        return GiveBatchCommand(new[] { (id, amount) }, maximum);
    }

    internal static string? GiveBatchCommand(IEnumerable<(string Id, int Amount)> items, int maximum)
    {
        var command = new StringBuilder("give @m");
        int remaining = maximum;
        foreach (var (id, amount) in items)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128 || amount < 1 || amount > remaining) return null;
            foreach (char c in id)
                if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '/' or '.')) return null;
            foreach (string segment in id.Split('/'))
                if (segment is "" or "." or "..") return null;
            for (int i = 0; i < amount; ++i) command.Append(' ').Append(id);
            remaining -= amount;
        }
        return remaining == maximum ? null : command.ToString();
    }
}
