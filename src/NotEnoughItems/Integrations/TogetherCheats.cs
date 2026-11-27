using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NotEnoughItems;

internal static class TogetherCheats
{
    private const string AdminCommand = "neiadmin";
    private const float QueryInterval = 5f, PermissionLifetime = 12f;
    private static readonly NeiCheatGrants<object> Grants = new();
    private static TogetherCheatApi? _api;
    private static bool _attempted, _consoleHook, _loggedFailure;
    private static bool _active;
    private static ManualLogSource? _log;
    private static object? _transport, _localPlayer;
    private static Command? _command;
    private static ISet<string>? _ownedLocalOnly;
    private static string[]? _localSubmission;
    private static float _nextQuery, _permissionUntil;
    private static bool _server;
    private static Action<ConsoleScript, string[], bool>? _runCommand;
    private static readonly byte[] PermissionQuery = [0];

    internal static void Update(Harmony harmony, ManualLogSource log)
    {
        _active = true;
        _log = log;
        if (!_attempted && Chainloader.PluginInfos.TryGetValue(NativePlaySession.TogetherGuid, out var plugin) &&
            plugin.Instance is { } instance)
        {
            _attempted = true;
            try
            {
                var api = new TogetherCheatApi(instance.GetType().Assembly);
                api.Register(Method(nameof(ReceiveRequest)), Method(nameof(ReceivePermission)), Method(nameof(ForgetPlayer)));
                _api = api;
            }
            catch (Exception error) { Warn(error); }
        }

        if (_api is not { } activeApi) return;
        try
        {
            RefreshSession(activeApi);
            if (_transport == null) return;
            if (_server)
            {
                if (!_consoleHook)
                {
                    // Install only for a Together server. A missing or changed input
                    // call site must never fall back to the shared command dispatcher.
                    InstallConsoleHook(harmony);
                    _consoleHook = true;
                }
                if (ConsoleScript.instance && ConsoleScript.instance.logs != null &&
                    ConsoleScript.SearchExact(AdminCommand) == null && activeApi.LocalOnlyCommands is { } localOnly)
                {
                    _command = new Command(AdminCommand, "NEI - Grant or revoke NEI cheats for a connected player.", ManageGrant,
                        new Dictionary<int, List<string>> { [0] = ["add", "remove"] }, ("add/remove", "operation"), ("player", "exact name or id:<number>"));
                    if (localOnly.Add(AdminCommand)) _ownedLocalOnly = localOnly;
                    ConsoleScript.Commands.Add(_command);
                }
            }
            else if (_localPlayer is UnityEngine.Object player && player && Time.realtimeSinceStartup >= _nextQuery)
            {
                _nextQuery = Time.realtimeSinceStartup + QueryInterval;
                activeApi.SendRequest(PermissionQuery);
            }
        }
        catch (Exception error)
        {
            ResetSession();
            // A failed API or console binding never silently enables cheats.
            _api = null;
            Warn(error);
        }
    }

    internal static CheatAccess Access(object? transport)
    {
        if (!_active || _api is not { } api || transport == null) return CheatAccess.Unavailable;
        RefreshSession(api);
        if (!ReferenceEquals(transport, _transport) || !api.AllowsCheats(remote: !_server)) return CheatAccess.Unavailable;
        if (_server) return CheatAccess.Host;
        return _permissionUntil > Time.realtimeSinceStartup ? CheatAccess.GrantedClient : CheatAccess.Unavailable;
    }

    internal static bool Submit(object? transport, Body body, byte[] packet, out int dropped, out bool submitted)
    {
        dropped = 0;
        submitted = false;
        if (_api is not { } api || Access(transport) == CheatAccess.Unavailable || _localPlayer is not { } player || api.Body(player) != body)
            return false;
        if (_server)
            return CheatPermission.DecodeGive(packet) is { Length: > 0 } ids && Deliver(api, player, ids, remote: false, out dropped);
        api.SendRequest(packet);
        submitted = true;
        return true;
    }

    private static void RefreshSession(TogetherCheatApi api)
    {
        var transport = api.Transport;
        bool server = api.IsServer;
        if (transport != null && !server && !api.IsClient) transport = null;
        var localPlayer = transport != null ? api.LocalPlayer : null;
        if (ReferenceEquals(transport, _transport) && server == _server && ReferenceEquals(localPlayer, _localPlayer)) return;
        Grants.SetTransport(server ? transport : null);
        if (transport == null || !server || !ReferenceEquals(transport, _transport)) UnregisterCommand();
        _transport = transport;
        _server = server;
        _localPlayer = localPlayer;
        _permissionUntil = _nextQuery = 0;
    }

    private static void ReceiveRequest(object sender, object reader)
    {
        if (!_active || _api is not { } api) return;
        RefreshSession(api);
        if (_transport == null || !_server || ReferenceEquals(sender, _localPlayer) || !IsConnectedPlayer(sender) || !Grants.Allows(sender)) return;
        // No endpoint to manage grants. Together supplies the authenticated sender;
        // the packet contains only resource IDs, never a target player or a command.
        if (api.Read(reader, CheatPermission.MaxPacketBytes) is not { } packet) return;
        bool query = packet.Length == 1 && packet[0] == 0;
        if (!Grants.TrySpend(sender, Time.realtimeSinceStartup, query)) return;
        if (query) api.SendPermission(sender, true);
        else if (api.AllowsCheats(remote: true) && CheatPermission.DecodeGive(packet) is { Length: > 0 } ids)
            Deliver(api, sender, ids, remote: true, out _);
    }

    private static void ForgetPlayer(object player) => Grants.Change(player, add: false);

    private static void ReceivePermission(object reader)
    {
        if (!_active || _api is not { } api) return;
        // Together dispatches this directional receiver only from the connected server.
        RefreshSession(api);
        if (!_server && _transport != null && _localPlayer is UnityEngine.Object player && player && api.Read(reader, 1) is { } packet && packet[0] <= 1)
        {
            _permissionUntil = packet[0] == 1 ? Time.realtimeSinceStartup + PermissionLifetime : 0;
            if (packet[0] == 0) NeiActions.DisableMultiplayerCheats();
        }
    }

    private static bool Deliver(TogetherCheatApi api, object player, string[] ids, bool remote, out int dropped)
    {
        dropped = 0;
        var world = WorldGeneration.world;
        var body = api.Body(player);
        if (!world || world.generatingWorld || world.cutsceneActive || !body || !body!.alive || !body.conscious ||
            body.gameObject.scene != world.gameObject.scene || (remote && !api.Loaded(player))) return false;
        // Validate every prefab on the server before making any changes.
        if (!ids.Distinct(StringComparer.Ordinal).All(IsResourceItem)) return false;
        if (world.biomeOverride != WorldGeneration.OverrideSceneType.Tutorial) SteamManager.disableAchievements = true;
        foreach (string id in ids)
        {
            var spawned = api.Spawn(id, body);
            if (!spawned) return false;
            if (!spawned!.transform.IsChildOf(body.transform)) dropped++;
        }
        return true;
    }

    internal static bool IsResourceItem(string id)
    {
        var prefab = Resources.Load<GameObject>(id);
        return prefab && prefab.TryGetComponent<Item>(out _);
    }

    private static bool IsConnectedPlayer(object player)
    {
        if (_api is not { } api || player is not UnityEngine.Object obj || !obj) return false;
        return api.Players.Cast<object>().Any(candidate => ReferenceEquals(candidate, player));
    }

    private static void ManageGrant(string[] args)
    {
        if (!_active || !ReferenceEquals(args, _localSubmission) || _api is not { } api) return;
        _localSubmission = null; // No nested/forwarded/replayed invocation can consume this input.
        RefreshSession(api);
        if (_transport == null || !_server || _command == null || !ReferenceEquals(api.CommandCaller, _localPlayer)) return;
        bool add = args.Length >= 3 && args[1].Equals("add", StringComparison.OrdinalIgnoreCase);
        if (args.Length < 3 || (!add && !args[1].Equals("remove", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Usage: neiadmin add/remove <exact player name or id:number>");
        var players = api.Players.Cast<object>().Where(p => p is UnityEngine.Object obj && obj && !ReferenceEquals(p, _localPlayer));
        var target = CheatPermission.FindUniquePlayer(players, string.Join(" ", args, 2, args.Length - 2), api.Name, api.Id);
        if (target == null) throw new ArgumentException("Choose one connected client by exact name or id:number.");
        Grants.Change(target, add);
        api.SendPermission(target, add);
        WriteConsole(add ? "NEI cheat permission granted." : "NEI cheat permission revoked.");
    }

    private static void InstallConsoleHook(Harmony harmony)
    {
        _runCommand = (Action<ConsoleScript, string[], bool>)ConsoleMethod("TryExecuteCommand", [typeof(string[]), typeof(bool)])
            .CreateDelegate(typeof(Action<ConsoleScript, string[], bool>));
        harmony.Patch(ConsoleMethod(nameof(ConsoleScript.Update), Type.EmptyTypes), transpiler: new HarmonyMethod(Method(nameof(WrapConsoleInput))));
    }

    private static IEnumerable<CodeInstruction> WrapConsoleInput(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var dispatch = ConsoleMethod("TryExecuteCommand", [typeof(string[]), typeof(bool)]);
        var calls = code.Where(i => Equals(i.operand, dispatch) && (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)).ToArray();
        if (calls.Length != 1) throw new InvalidOperationException("Could not identify the Together host's native console input call.");
        calls[0].opcode = OpCodes.Call;
        calls[0].operand = Method(nameof(RunLocalConsoleInput));
        return code;
    }

    private static void RunLocalConsoleInput(ConsoleScript console, string[] args, bool addToLog)
    {
        if (_active && _command != null && addToLog && console == ConsoleScript.instance && console.active && Input.GetKeyDown(KeyCode.Return) &&
            args.Length > 0 && args[0].Equals(AdminCommand, StringComparison.OrdinalIgnoreCase)) _localSubmission = args;
        try { _runCommand!(console, args, addToLog); }
        finally { _localSubmission = null; }
    }

    private static void WriteConsole(string message) => ConsoleMethod("LogToConsole", [typeof(string)]).Invoke(ConsoleScript.instance, [message]);

    private static MethodInfo ConsoleMethod(string name, Type[] parameters) => typeof(ConsoleScript).GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null)
        ?? throw new MissingMethodException(typeof(ConsoleScript).FullName, name);

    private static MethodInfo Method(string name) => typeof(TogetherCheats).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    private static void Warn(Exception error)
    {
        if (_loggedFailure) return;
        _loggedFailure = true;
        _log?.LogWarning($"NEI Together cheats unavailable: {error.GetBaseException().Message}");
    }

    private static void UnregisterCommand()
    {
        if (_command != null) ConsoleScript.Commands.Remove(_command);
        _command = null;
        _ownedLocalOnly?.Remove(AdminCommand);
        _ownedLocalOnly = null;
    }

    private static void ResetSession()
    {
        UnregisterCommand();
        Grants.SetTransport(null);
        _transport = _localPlayer = null;
        _permissionUntil = _nextQuery = 0;
        _server = false;
    }

    internal static void Suspend()
    {
        _active = false;
        _log = null;
        ResetSession();
    }

    internal static void Release()
    {
        Suspend();
        _consoleHook = false;
    }
}
