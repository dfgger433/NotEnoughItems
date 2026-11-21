using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace NotEnoughItems;

// Use only Together's registered admin dispatcher; never mutate a multiplayer body
// directly or construct our own network request.
internal readonly struct CheatSession(CheatAccess access, object? transport = null, Type? console = null, bool timeManipulation = false)
{
    internal CheatAccess Access { get; } = access;
    internal object? Transport { get; } = transport;
    internal bool TimeManipulation { get; } = timeManipulation;

    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const string TogetherGuid = "CasualtiesMP";
    private const string LegacyGuid = "KrokoshaCasualtiesMP";
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
        var plugins = Chainloader.PluginInfos;
        if (plugins == null) return default;
        // Unrecognized integrations and connecting legacy sessions cannot be treated as offline.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = assembly.GetName().Name;
            if ((name == TogetherGuid || name == LegacyGuid) && !plugins.ContainsKey(name)) return default;
        }
        if (plugins.TryGetValue(LegacyGuid, out var legacy))
        {
            var assembly = legacy.Instance?.GetType().Assembly;
            var mp = assembly?.GetType("KrokoshaCasualtiesMP.KrokoshaScavMultiplayer");
            var transport = assembly?.GetType("KrokoshaCasualtiesMP.Net")?.GetProperty("TRANSPORT", PublicStatic);
            if (ReadBool(mp, "network_system_is_running") != false || ReadBool(mp, "network_system_is_client_and_waiting_connecting") != false ||
                transport == null || transport.GetValue(null) != null) return default;
        }
        if (!plugins.TryGetValue(TogetherGuid, out var together)) return new(CheatAccess.Offline);
        var mod = together.Instance?.GetType().Assembly;
        var net = mod?.GetType("Together.Net");
        var transportProperty = net?.GetProperty("Transport", PublicStatic);
        if (transportProperty == null) return default;
        object? currentTransport = transportProperty.GetValue(null);
        bool? running = ReadBool(net, "IsRunning");
        if (running == false && currentTransport == null) return new(CheatAccess.Offline);
        if (running != true || currentTransport == null || ReadBool(net, "IsConnected") != true) return default;
        var playerType = mod?.GetType("Together.ScavPlayer");
        var localPlayer = playerType?.GetField("LocalPlayer", PublicStatic)?.GetValue(null);
        if (localPlayer is not UnityEngine.Object player || !player ||
            playerType?.GetField("body", BindingFlags.Public | BindingFlags.Instance)?.GetValue(localPlayer) is not Body localBody ||
            localBody != body) return default;

        var con = mod?.GetType("Together.Con");
        var dispatcher = con?.GetMethod("ExecuteCommandAdminNoLog", PublicStatic, null, [typeof(string).MakeByRefType()], null);
        if (dispatcher?.ReturnType != typeof(void)) return default;
        bool? server = ReadBool(net, "IsServer"), client = ReadBool(net, "IsClient");
        var rules = mod?.GetType("Together.Multiplayer")?.GetField("rules", PublicStatic)?.GetValue(null);
        // For forwarded requests, sv_cheats must also permit execution on the host.
        bool? canCheat = CallBool(con, "CanCheat");
        if (rules?.GetType().GetField("sv_cheats")?.GetValue(rules) is not true) canCheat = false;
        CheatAccess access = CheatPermission.ForTogether(running, true, server, client, canCheat, CallBool(con, "CanExecuteAdminCommands"));
        bool canChangeSpeed = rules?.GetType().GetProperty("DisableTimeManipulation")?.GetValue(rules) is false;
        return new CheatSession(access, currentTransport, con, canChangeSpeed);
    }

    private static bool? ReadBool(Type? type, string property) => type?.GetProperty(property, PublicStatic)?.GetValue(null) as bool?;
    private static bool? CallBool(Type? type, string method) => type?.GetMethod(method, PublicStatic, null, Type.EmptyTypes, null)?.Invoke(null, null) as bool?;
}
