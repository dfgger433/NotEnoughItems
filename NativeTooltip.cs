using UnityEngine;

namespace NotEnoughItems;

/// <summary>Selects when NEI hides the game's stock GlobalDark tooltip.</summary>
internal static class NativeTooltip
{
    /// <summary>
    /// Only a visible NEI surface under the pointer hides the stock tooltip. Captured
    /// pointer state and native dragging are intentionally not inputs: the overlay can
    /// be active while the pointer is outside every NEI panel.
    /// </summary>
    internal static bool Suppress(bool overlayActive, bool pointerOverVisibleRegion) =>
        overlayActive && pointerOverVisibleRegion;

    /// <summary>Hit test over the rectangles NEI actually drew this frame.</summary>
    internal static bool Hit(Vector2 pointer, params (bool Visible, Rect Rect)[] regions)
    {
        foreach (var (visible, rect) in regions)
            if (visible && rect.width > 0f && rect.height > 0f && rect.Contains(pointer)) return true;
        return false;
    }
}
