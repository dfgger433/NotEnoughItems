namespace NotEnoughItems;

internal static class DragInputPolicy
{
    // Called only on an interaction release over NEI. A real drop target keeps
    // the original game path; NEI must never synthesize a world drop.
    internal static bool CancelRelease(bool dragging, bool overlayOwnsPointer, bool nativeDropTarget) =>
        dragging ? !nativeDropTarget : overlayOwnsPointer;

    internal static bool CancelOnClose(bool closingWithDrag, bool stillOpen) => closingWithDrag && !stillOpen;

    // Other integrations may consume the release edge later in the same frame.
    internal static bool IsStale(bool held, bool releasedThisFrame) => !held && !releasedThisFrame;
}
