using System;
using System.Reflection;

namespace NotEnoughItems;

// Use only Together's registered admin dispatcher; never mutate a multiplayer body
// directly or construct our own network request.
internal readonly struct CheatSession(CheatAccess access, object? transport = null, Type? console = null)
{
    internal CheatAccess Access { get; } = access;
    internal object? Transport { get; } = transport;
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private delegate void AdminCommand(in string command);

    internal bool Supports(string name)
    {
        if (Access == CheatAccess.Offline) return true;
        if (Access == CheatAccess.Unavailable || console == null || !ConsoleScript.instance || ConsoleScript.SearchExact(name) == null) return false;
        if (Access != CheatAccess.AdminClient) return true;
        return NotListed("localonly_commands", name) && NotListed("banned_admin_commands", name);
    }

    private bool NotListed(string field, string command) => console?.GetField(field, PublicStatic)?.GetValue(null)
        is System.Collections.Generic.ISet<string> names && !names.Contains(command);

    internal void Dispatch(string command)
    {
        var execute = console!.GetMethod("ExecuteCommandAdminNoLog", PublicStatic, null, [typeof(string).MakeByRefType()], null)!;
        ((AdminCommand)execute.CreateDelegate(typeof(AdminCommand)))(in command);
    }

    internal static CheatSession Detect(Body body)
    {
        var session = NativePlaySession.Detect(body);
        if (session.Offline) return new(CheatAccess.Offline);
        if (session.Together is not { } mod) return default;
        var net = mod.GetType("Together.Net");
        var con = mod.GetType("Together.Con");
        var dispatcher = con?.GetMethod("ExecuteCommandAdminNoLog", PublicStatic, null, [typeof(string).MakeByRefType()], null);
        if (dispatcher?.ReturnType != typeof(void)) return default;
        bool? server = NativePlaySession.ReadBool(net, "IsServer"), client = NativePlaySession.ReadBool(net, "IsClient");
        var rules = mod.GetType("Together.Multiplayer")?.GetField("rules", PublicStatic)?.GetValue(null);
        // For forwarded requests, sv_cheats must also permit execution on the host.
        bool? canCheat = CallBool(con, "CanCheat");
        if (rules?.GetType().GetField("sv_cheats")?.GetValue(rules) is not true) canCheat = false;
        CheatAccess access = CheatPermission.ForTogether(true, true, server, client, canCheat, CallBool(con, "CanExecuteAdminCommands"));
        return new CheatSession(access, session.Transport, con);
    }

    private static bool? CallBool(Type? type, string method) => type?.GetMethod(method, PublicStatic, null, Type.EmptyTypes, null)?.Invoke(null, null) as bool?;
}
