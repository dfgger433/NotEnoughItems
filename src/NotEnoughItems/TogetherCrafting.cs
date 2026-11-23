using System;
using System.Reflection;

namespace NotEnoughItems;

// Optional Together integration. Invoke only its public normal-crafting API;
// Together owns ingredient selection, transport, server validation and all item changes.
internal sealed class TogetherCrafting
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private static TogetherCrafting? _current;

    private readonly Assembly _assembly;
    private readonly object _transport;
    private readonly MethodInfo _request;
    private readonly FieldInfo _pending, _accepted, _reason, _open, _done, _session, _attempt;
    private readonly TogetherCraftRequest _batch;

    private TogetherCrafting(Assembly assembly, object transport, MethodInfo request, FieldInfo pending, FieldInfo accepted,
        FieldInfo reason, FieldInfo open, FieldInfo done, FieldInfo session, FieldInfo attempt)
    {
        _assembly = assembly;
        _transport = transport;
        _request = request;
        _pending = pending;
        _accepted = accepted;
        _reason = reason;
        _open = open;
        _done = done;
        _session = session;
        _attempt = attempt;
        // No recipe is passed when reading an already completed receipt. If the
        // slot disappeared, Together cannot interpret this as a new submission.
        _batch = new(ReadSlot, () => Invoke(null, null));
    }

    internal static TogetherCrafting? Resolve(NativePlaySession session)
    {
        if (session.Together is not { } assembly || session.Transport is not { } transport || !IsPlayable(session)) return null;
        if (_current is { } current && ReferenceEquals(current._assembly, assembly) && ReferenceEquals(current._transport, transport)) return current;
        var craft = assembly.GetType("Together.CraftCommit");
        var request = craft?.GetMethod("Request", PublicStatic, null, [typeof(PlayerCamera), typeof(global::Recipe)], null);
        var reply = request?.ReturnType;
        if (request == null || reply?.FullName != "Together.CraftCommit+CraftStep" || !reply.IsValueType) return null;
        if (Field(reply, "Pending", PublicInstance, typeof(bool)) is not { } pending ||
            Field(reply, "Accepted", PublicInstance, typeof(bool)) is not { } accepted ||
            reply.GetField("Reason", PublicInstance) is not { FieldType.IsEnum: true } reason ||
            Field(craft, "_stepOpen", PrivateStatic, typeof(bool)) is not { } open ||
            Field(craft, "_stepDone", PrivateStatic, typeof(bool)) is not { } done ||
            Field(craft, "_session", PrivateStatic, typeof(uint)) is not { } sessionId ||
            Field(craft, "_stepAttempt", PrivateStatic, typeof(uint)) is not { } attempt) return null;
        return _current = new(assembly, transport, request, pending, accepted, reason, open, done, sessionId, attempt);
    }

    internal static bool IsPlayable(NativePlaySession session)
    {
        var net = session.Together?.GetType("Together.Net");
        var ui = session.Together?.GetType("Together.UIInGame");
        // Together blocks opening the crafting panel while spectating. Check even
        // if the panel remained open when spectator mode was activated.
        return NativePlaySession.ReadBool(ui, "SpectatorModeActive") == false &&
            (NativePlaySession.ReadBool(net, "IsHost"), NativePlaySession.ReadBool(net, "IsClient")) is (true, false) or (false, true);
    }

    private static FieldInfo? Field(Type? type, string name, BindingFlags flags, Type fieldType) =>
        type?.GetField(name, flags) is { } field && field.FieldType == fieldType ? field : null;

    internal TogetherCraftReply Submit(PlayerCamera camera, global::Recipe recipe) => _batch.Start(() =>
    {
        camera.SelectRecipe(recipe.index);
        return Invoke(camera, recipe);
    });
    internal TogetherCraftReply Poll() => _batch.Poll();
    internal static void Release() => _current = null;

    private TogetherCraftSlot ReadSlot() => new((bool)_open.GetValue(null)!, (bool)_done.GetValue(null)!,
        (uint)_session.GetValue(null)!, (uint)_attempt.GetValue(null)!);

    private TogetherCraftReply Invoke(PlayerCamera? camera, global::Recipe? recipe)
    {
        object? result = _request.Invoke(null, [camera, recipe]);
        if (result == null) return new(TogetherCraftStatus.Interrupted);
        if (_pending.GetValue(result) is true) return new(TogetherCraftStatus.Pending);
        if (_accepted.GetValue(result) is true) return new(TogetherCraftStatus.Accepted);
        return new(TogetherCraftStatus.Rejected, _reason.GetValue(result)?.ToString());
    }
}
