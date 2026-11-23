namespace NotEnoughItems;

internal static class DragInputPolicy
{
    internal enum Release { Native, OverlayClick, CancelDrag }

    // A real drop keeps the original path. A NEI click only suppresses the game
    // handler; unlike cancelling a live drag it must leave IMGUI input active.
    internal static Release ReleaseOverOverlay(bool dragging, bool overlayOwnsPointer, bool nativeDropTarget) =>
        dragging ? (nativeDropTarget ? Release.Native : Release.CancelDrag) :
        overlayOwnsPointer ? Release.OverlayClick : Release.Native;

    internal static bool CancelOnClose(bool closingWithDrag, bool stillOpen) => closingWithDrag && !stillOpen;

    // Other integrations may consume the release edge later in the same frame.
    internal static bool IsStale(bool held, bool releasedThisFrame) => !held && !releasedThisFrame;
}
