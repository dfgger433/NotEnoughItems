using UnityEngine;

namespace NotEnoughItems;

/// <summary>Temporarily raises the stock tooltip Canvas without replacing the game's frame.</summary>
internal static class NativeTooltipLayer
{
    private static Canvas? _leasedCanvas;
    private static bool _previousOverride;
    private static int _previousOrder;

    internal static void Raise(GlobalDark ui)
    {
        var root = ui.tooltipRect;
        if (!root || !root.gameObject.activeInHierarchy)
        {
            Restore();
            return;
        }

        var canvas = root.GetComponent<Canvas>();
        if (!canvas)
            canvas = root.GetComponentInParent<Canvas>();
        if (!canvas)
            return;
        if (_leasedCanvas == canvas) return;
        Restore();
        _leasedCanvas = canvas;
        _previousOverride = canvas.overrideSorting;
        _previousOrder = canvas.sortingOrder;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
    }

    // ScreenSpaceOverlay UGUI must not be switched to a camera or RenderTexture during OnGUI.
    // The stock Canvas remains responsible for all tooltip pixels and frame composition.
    internal static void RenderAfterGui(GlobalDark? ui) { }

    internal static void Restore()
    {
        if (_leasedCanvas && _leasedCanvas!.overrideSorting && _leasedCanvas.sortingOrder == short.MaxValue)
        {
            _leasedCanvas.sortingOrder = _previousOrder;
            _leasedCanvas.overrideSorting = _previousOverride;
        }
        _leasedCanvas = null;
    }

    internal static void Release() => Restore();
}
