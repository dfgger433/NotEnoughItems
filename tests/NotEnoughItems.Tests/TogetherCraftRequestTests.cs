using NotEnoughItems;

internal static class TogetherCraftRequestTests
{
    internal static void Run()
    {
        ImmediateAcceptanceIsReturnedDirectly();
        PendingReceiptsAreCollectedOnce();
        ForeignAndUnfinishedSlotsAreNotPolled();
        ChangedIdentityAndInvalidPendingAreInterrupted();
        CompletedAbandonedReceiptIsDrainedBeforeRetry();
    }

    private static void ImmediateAcceptanceIsReturnedDirectly()
    {
        var fake = new Fake();
        var reply = fake.Submit(new(TogetherCraftStatus.Accepted));
        Check(reply.Status == TogetherCraftStatus.Accepted && fake.Submitted == 1 && fake.Collected == 0,
            "An immediate acceptance is returned without collecting a receipt.");
    }

    private static void PendingReceiptsAreCollectedOnce()
    {
        foreach (var (status, reason) in new[]
        {
            (TogetherCraftStatus.Accepted, (string?)null),
            (TogetherCraftStatus.Rejected, "host rejected")
        })
        {
            var fake = new Fake { CollectedReply = new(status, reason) };
            Check(fake.Submit(new(TogetherCraftStatus.Pending), Slot(true, false, 7, 11)).Status == TogetherCraftStatus.Pending,
                "A valid request waits for its receipt.");
            Check(fake.Request.Poll().Status == TogetherCraftStatus.Pending && fake.Collected == 0,
                "An unfinished owned slot is not collected.");
            fake.Slot = Slot(true, true, 7, 11);
            var completed = fake.Request.Poll();
            Check(completed.Status == status && completed.Reason == reason && fake.Collected == 1,
                "A completed matching slot returns exactly its receipt.");
            Check(fake.Request.Poll().Status == TogetherCraftStatus.Interrupted && fake.Submitted == 1 && fake.Collected == 1,
                "A consumed receipt cannot be collected or submitted again.");
        }
    }

    private static void ForeignAndUnfinishedSlotsAreNotPolled()
    {
        var foreign = new Fake { Slot = Slot(true, true, 90, 120) };
        Check(foreign.Submit(new(TogetherCraftStatus.Accepted)).Status == TogetherCraftStatus.Busy &&
            foreign.Request.Poll().Status == TogetherCraftStatus.Interrupted && foreign.Submitted == 0 && foreign.Collected == 0,
            "A foreign completed request is busy and never polled or collected.");

        var owned = new Fake();
        owned.Submit(new(TogetherCraftStatus.Pending), Slot(true, false, 3, 5));
        Check(owned.Submit(new(TogetherCraftStatus.Accepted)).Status == TogetherCraftStatus.Busy &&
            owned.Submitted == 1 && owned.Collected == 0,
            "A still-pending owned request blocks a replacement submission.");
    }

    private static void ChangedIdentityAndInvalidPendingAreInterrupted()
    {
        foreach (var replacement in new[] { Slot(true, true, 14, 17), Slot(true, true, 13, 18) })
        {
            var fake = new Fake();
            fake.Submit(new(TogetherCraftStatus.Pending), Slot(true, false, 13, 17));
            fake.Slot = replacement;
            Check(fake.Request.Poll().Status == TogetherCraftStatus.Interrupted && fake.Collected == 0,
                "A replacement session or attempt cannot complete the original request.");
        }

        foreach (var invalid in new[] { Slot(false, false, 1, 1), Slot(true, false, 0, 1), Slot(true, false, 1, 0) })
        {
            var fake = new Fake();
            Check(fake.Submit(new(TogetherCraftStatus.Pending), invalid).Status == TogetherCraftStatus.Interrupted,
                "Pending without an open slot and nonzero identity is interrupted.");
        }
    }

    private static void CompletedAbandonedReceiptIsDrainedBeforeRetry()
    {
        var fake = new Fake();
        fake.Submit(new(TogetherCraftStatus.Pending), Slot(true, false, 21, 34));

        // The caller was cancelled; its completed receipt must be drained before another submit.
        fake.Slot = Slot(true, true, 21, 34);
        Check(fake.Submit(new(TogetherCraftStatus.Pending), Slot(true, false, 21, 35)).Status == TogetherCraftStatus.Pending &&
            fake.Submitted == 2 && fake.Collected == 1,
            "A completed abandoned receipt is safely collected before retrying.");
        Check(fake.Request.Poll().Status == TogetherCraftStatus.Pending && fake.Submitted == 2 && fake.Collected == 1,
            "Draining the old receipt does not resend it or disturb the new pending attempt.");
    }

    // Optional one-off API drift check without loading Together or Unity:
    // run `ilspycmd -t Together.CraftCommit <path-to-Together.dll>` and
    // `ilspycmd -t Together.Net <path-to-Together.dll>`, then verify the
    // Request/CraftStep members and slot fields resolved by TogetherCrafting.
    private sealed class Fake
    {
        internal TogetherCraftSlot Slot;
        internal int Submitted, Collected;
        internal TogetherCraftReply CollectedReply = new(TogetherCraftStatus.Accepted);
        internal TogetherCraftRequest Request { get; }

        internal Fake()
        {
            Request = new(() => Slot, () =>
            {
                Collected++;
                Slot = default;
                return CollectedReply;
            });
        }

        internal TogetherCraftReply Submit(TogetherCraftReply reply, TogetherCraftSlot slot = default) => Request.Start(() =>
        {
            Submitted++;
            Slot = slot;
            return reply;
        });
    }

    private static TogetherCraftSlot Slot(bool open, bool done, uint session, uint attempt) => new(open, done, session, attempt);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
