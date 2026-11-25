using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace NotEnoughItems;

// Distinguish verified offline play from a connection in progress or an unsupported backend.
// Ordinary crafting and admin commands apply their own rules after resolving the session.
internal readonly struct NativePlaySession(bool offline, Assembly? together = null, object? transport = null, UnityEngine.Object? player = null)
{
    internal bool Offline { get; } = offline;
    internal Assembly? Together { get; } = together;
    internal object? Transport { get; } = transport;
    private UnityEngine.Object? Player { get; } = player;
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    internal const string TogetherGuid = "CasualtiesMP";
    private const string LegacyGuid = "KrokoshaCasualtiesMP";

    internal bool Matches(NativePlaySession other) => Offline ? other.Offline :
        Together != null && ReferenceEquals(Together, other.Together) && ReferenceEquals(Transport, other.Transport) && ReferenceEquals(Player, other.Player);

    internal static NativePlaySession Detect(Body body)
    {
        var plugins = Chainloader.PluginInfos;
        if (plugins == null) return default;
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
        if (!plugins.TryGetValue(TogetherGuid, out var together)) return new(offline: true);
        var mod = together.Instance?.GetType().Assembly;
        var net = mod?.GetType("Together.Net");
        var transportProperty = net?.GetProperty("Transport", PublicStatic);
        if (transportProperty == null) return default;
        object? currentTransport = transportProperty.GetValue(null);
        bool? running = ReadBool(net, "IsRunning");
        if (running == false && currentTransport == null) return new(offline: true);
        if (running != true || currentTransport == null || ReadBool(net, "IsConnected") != true) return default;
        var playerType = mod?.GetType("Together.ScavPlayer");
        var localPlayer = playerType?.GetField("LocalPlayer", PublicStatic)?.GetValue(null);
        if (localPlayer is not UnityEngine.Object player || !player ||
            playerType?.GetField("body", BindingFlags.Public | BindingFlags.Instance)?.GetValue(localPlayer) is not Body localBody ||
            localBody != body) return default;
        return new(offline: false, mod, currentTransport, player);
    }

    internal static bool? ReadBool(Type? type, string property) => type?.GetProperty(property, PublicStatic)?.GetValue(null) as bool?;
}
