using System;

namespace NotEnoughItems;

internal enum TogetherCraftStatus { Pending, Accepted, Rejected, Busy, Interrupted }

internal readonly struct TogetherCraftSlot(bool open, bool done, uint session, uint attempt)
{
    internal bool Open { get; } = open;
    internal bool Done { get; } = done;
    internal uint Session { get; } = session;
    internal uint Attempt { get; } = attempt;
}

internal readonly struct TogetherCraftReply(TogetherCraftStatus status, string? reason = null)
{
    internal TogetherCraftStatus Status { get; } = status;
    internal string? Reason { get; } = reason;
}

// Together's Request API has a single shared receipt slot. Polling an empty slot
// with a recipe would submit it again; only collect a completed receipt we own.
internal sealed class TogetherCraftRequest(Func<TogetherCraftSlot> read, Func<TogetherCraftReply> collect)
{
    private TogetherCraftSlot _owned;

    internal TogetherCraftReply Start(Func<TogetherCraftReply> submit)
    {
        var slot = read();
        if (slot.Open)
        {
            if (!Owns(slot) || !slot.Done) return new(TogetherCraftStatus.Busy);
            // The prior order stopped waiting. Drain only its known, completed receipt.
            if (Collect().Status is not (TogetherCraftStatus.Accepted or TogetherCraftStatus.Rejected) || read().Open)
                return new(TogetherCraftStatus.Interrupted);
        }
        _owned = default;
        var reply = submit();
        if (reply.Status != TogetherCraftStatus.Pending) return reply;
        slot = read();
        if (!slot.Open || slot.Session == 0 || slot.Attempt == 0) return new(TogetherCraftStatus.Interrupted);
        _owned = slot;
        return reply;
    }

    internal TogetherCraftReply Poll()
    {
        var slot = read();
        if (!Owns(slot))
        {
            _owned = default;
            return new(TogetherCraftStatus.Interrupted);
        }
        return slot.Done ? Collect() : new(TogetherCraftStatus.Pending);
    }

    private bool Owns(TogetherCraftSlot slot) => _owned.Open && slot.Open && _owned.Session == slot.Session && _owned.Attempt == slot.Attempt;

    private TogetherCraftReply Collect()
    {
        var reply = collect();
        if (reply.Status != TogetherCraftStatus.Pending) _owned = default;
        return reply;
    }
}
