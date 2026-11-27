using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NotEnoughItems;

// Late-bind Together and its LiteNetLib version so single-player has no new dependency.
internal sealed class TogetherCheatApi
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private const string Request = "nei.cheat.request.v1", Permission = "nei.cheat.permission.v1";
    private readonly PropertyInfo _transport, _running, _connected, _server, _client, _players, _playerId;
    private readonly FieldInfo _id, _name, _body, _localPlayer, _rules, _serverCheats, _clientCheats, _serverState, _loaded, _localOnly;
    private readonly PropertyInfo _available;
    private readonly EventInfo _onPlayerLeft;
    private readonly MethodInfo _registerServer, _registerClient, _writer, _put, _bytes, _sendToServer, _sendToPlayer, _spawn, _caller;
    private readonly object _reliableOrdered;

    internal TogetherCheatApi(Assembly assembly)
    {
        var mp = assembly.GetType("Together.Multiplayer", true)!;
        var net = assembly.GetType("Together.Net", true)!;
        var con = assembly.GetType("Together.Con", true)!;
        var player = assembly.GetType("Together.ScavPlayer", true)!;
        _registerServer = Static(mp, "RegisterCustomServerReceiver");
        _registerClient = Static(mp, "RegisterCustomClientReceiver");
        _onPlayerLeft = player.GetEvent("OnPlayerLeft", PublicStatic) ?? throw new MissingMemberException(player.FullName, "OnPlayerLeft");
        var reader = _registerClient.GetParameters()[1].ParameterType.GetGenericArguments()[0];
        _writer = Static(mp, "CreateNamedWriter", [typeof(string)]);
        var writer = _writer.ReturnType;
        var delivery = writer.Assembly.GetType("LiteNetLib.DeliveryMethod", true)!;
        _reliableOrdered = Enum.Parse(delivery, "ReliableOrdered");
        _available = Property(reader, "AvailableBytes", PublicInstance);
        _bytes = reader.GetMethod("GetRemainingBytes", Type.EmptyTypes) ?? throw new MissingMethodException(reader.FullName, "GetRemainingBytes");
        _put = writer.GetMethod("Put", [typeof(byte[])]) ?? throw new MissingMethodException(writer.FullName, "Put(byte[])");
        _sendToServer = Static(net, "Client_Send", [delivery.MakeByRefType(), writer.MakeByRefType()]);
        _sendToPlayer = Static(net, "Server_SendTo", [delivery.MakeByRefType(), writer.MakeByRefType(), player.MakeByRefType()]);
        _spawn = Static(con, "SpawnThingOnPlayer", [typeof(string), typeof(Body), typeof(bool), typeof(GameObject)]);
        _caller = Static(con, "GetCommandCallerPlayer", Type.EmptyTypes);
        _localOnly = Field(con, "localonly_commands", PublicStatic);
        _transport = Property(net, "Transport", PublicStatic);
        _running = Property(net, "IsRunning", PublicStatic);
        _connected = Property(net, "IsConnected", PublicStatic);
        _server = Property(net, "IsServer", PublicStatic);
        _client = Property(net, "IsClient", PublicStatic);
        _players = Property(player, "AllPlayers", PublicStatic);
        _playerId = Property(player, "playerId", PublicInstance);
        _id = Field(_playerId.PropertyType, "id", PublicInstance);
        _localPlayer = Field(player, "LocalPlayer", PublicStatic);
        _name = Field(player, "playerName", PublicInstance);
        _body = Field(player, "body", PublicInstance);
        _serverState = Field(player, "ServerPlayerState", PublicInstance);
        _loaded = Field(_serverState.FieldType, "is_loaded_in", PublicInstance);
        _rules = Field(mp, "rules", PublicStatic);
        _serverCheats = Field(_rules.FieldType, "sv_cheats", PublicInstance);
        _clientCheats = Field(_rules.FieldType, "AllowClientCheatCommands", PublicInstance);
    }

    internal object? Transport => _running.GetValue(null) is true && _connected.GetValue(null) is true ? _transport.GetValue(null) : null;
    internal bool IsServer => _server.GetValue(null) is true && _client.GetValue(null) is false;
    internal bool IsClient => _client.GetValue(null) is true && _server.GetValue(null) is false;
    internal IEnumerable Players => (IEnumerable)_players.GetValue(null)!;
    internal object? CommandCaller => _caller.Invoke(null, null);
    internal object? LocalPlayer => _localPlayer.GetValue(null);
    internal ISet<string>? LocalOnlyCommands => _localOnly.GetValue(null) as ISet<string>;
    internal string? Name(object player) => _name.GetValue(player) as string;
    internal ushort Id(object player) => (ushort)_id.GetValue(_playerId.GetValue(player))!;
    internal Body? Body(object player) => _body.GetValue(player) as Body;
    internal bool Loaded(object player) => _serverState.GetValue(player) is { } state && _loaded.GetValue(state) is true;

    internal bool AllowsCheats(bool remote) => _rules.GetValue(null) is { } rules &&
        _serverCheats.GetValue(rules) is true && (!remote || _clientCheats.GetValue(rules) is true);

    internal Delegate[] BindCallbacks(MethodInfo server, MethodInfo client, MethodInfo playerLeft) =>
    [
        server.CreateDelegate(_registerServer.GetParameters()[1].ParameterType),
        client.CreateDelegate(_registerClient.GetParameters()[1].ParameterType),
        playerLeft.CreateDelegate(_onPlayerLeft.EventHandlerType!)
    ];

    internal void Register(MethodInfo server, MethodInfo client, MethodInfo playerLeft)
    {
        // Action is contravariant: these static callbacks accept object instead
        // of referencing optional Together types. The API retains them for the process.
        var callbacks = BindCallbacks(server, client, playerLeft);
        _registerServer.Invoke(null, [Request, callbacks[0]]);
        _registerClient.Invoke(null, [Permission, callbacks[1]]);
        _onPlayerLeft.AddEventHandler(null, callbacks[2]);
    }

    internal byte[]? Read(object reader, int maximum) => _available.GetValue(reader) is int count && count > 0 && count <= maximum
        ? (byte[])_bytes.Invoke(reader, null)! : null;

    internal void SendRequest(byte[] packet) => _sendToServer.Invoke(null, [_reliableOrdered, Write(Request, packet)]);
    internal void SendPermission(object player, bool granted) =>
        _sendToPlayer.Invoke(null, [_reliableOrdered, Write(Permission, [granted ? (byte)1 : (byte)0]), player]);

    internal GameObject? Spawn(string id, Body body) => _spawn.Invoke(null, [id, body, true, null]) as GameObject;

    private object Write(string name, byte[] payload)
    {
        var writer = _writer.Invoke(null, [name])!;
        _put.Invoke(writer, [payload]);
        return writer;
    }

    private static MethodInfo Static(Type type, string name, Type[]? parameters = null) =>
        (parameters == null ? type.GetMethod(name, PublicStatic) : type.GetMethod(name, PublicStatic, null, parameters, null))
        ?? throw new MissingMethodException(type.FullName, name);

    private static PropertyInfo Property(Type type, string name, BindingFlags flags) => type.GetProperty(name, flags)
        ?? throw new MissingMemberException(type.FullName, name);

    private static FieldInfo Field(Type type, string name, BindingFlags flags) => type.GetField(name, flags)
        ?? throw new MissingFieldException(type.FullName, name);
}
