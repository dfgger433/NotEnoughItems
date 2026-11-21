using UnityEngine;

namespace NotEnoughItems;

internal static class NativeTooltipLayer
{
    private static Canvas? _leasedCanvas;
    private static Canvas? _ownedCanvas;
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
        if (_leasedCanvas && _leasedCanvas == canvas) return;
        Restore();
        // The inventory terminal already raises this same native tooltip while open.
        if (canvas && canvas.overrideSorting && canvas.sortingOrder == short.MaxValue) return;
        if (!canvas)
        {
            canvas = root.gameObject.AddComponent<Canvas>();
            _ownedCanvas = canvas;
        }
        _leasedCanvas = canvas;
        _previousOverride = canvas.overrideSorting;
        _previousOrder = canvas.sortingOrder;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
    }

    internal static void Restore()
    {
        if (_leasedCanvas && _leasedCanvas!.overrideSorting && _leasedCanvas.sortingOrder == short.MaxValue)
        {
            _leasedCanvas.sortingOrder = _previousOrder;
            _leasedCanvas.overrideSorting = _previousOverride;
        }
        _leasedCanvas = null;
    }

    internal static void Release()
    {
        Restore();
        if (_ownedCanvas) UnityEngine.Object.Destroy(_ownedCanvas);
        _ownedCanvas = null;
    }
}
