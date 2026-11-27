namespace NotEnoughItems;

internal readonly struct CheatSession(CheatAccess access, object? transport = null)
{
    internal CheatAccess Access { get; } = access;
    internal object? Transport { get; } = transport;

    internal static CheatSession Detect(Body body)
    {
        var session = NativePlaySession.Detect(body);
        if (session.Offline) return new(CheatAccess.Offline);
        return session.Together != null ? new(TogetherCheats.Access(session.Transport), session.Transport) : default;
    }
}
