using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NotEnoughItems;

/// <summary>Temporarily raises the stock tooltip root, matching the terminal's reversible VisualLease.</summary>
internal static class NativeTooltipLayer
{
    private static Canvas? _canvas;
    private static GraphicRaycaster? _raycaster;
    private static Graphic? _visual;
    private static bool _addedCanvas;
    private static bool _active;
    private static bool _previousEnabled;
    private static bool _previousOverrideSorting;
    private static bool _previousRaycasterEnabled;
    private static bool _previousRaycastTarget;
    private static int _previousOrder;
    private static int _previousLayer;
    private static GameObject? _root;

    /// <summary>
    /// Uses the same reversible lease as UnknownInventoryTerminal: add an override
    /// Canvas to MouseOver itself, rather than changing GlobalDark's whole canvas.
    /// </summary>
    internal static void Raise(GlobalDark ui)
    {
        var root = ui.tooltipRect;
        if (!root || !root.gameObject.activeInHierarchy)
        {
            Restore();
            return;
        }

        var gameObject = root.gameObject;
        if (_active && _root == gameObject && _canvas) return;
        Restore();

        _root = gameObject;
        _canvas = gameObject.GetComponent<Canvas>();
        _addedCanvas = !_canvas;
        if (_addedCanvas) _canvas = gameObject.AddComponent<Canvas>();

        var canvas = _canvas!;
        _previousEnabled = canvas.enabled;
        _previousOverrideSorting = canvas.overrideSorting;
        _previousOrder = canvas.sortingOrder;
        _previousLayer = canvas.sortingLayerID;
        _active = true;

        _raycaster = gameObject.GetComponent<GraphicRaycaster>();
        if (_raycaster)
        {
            _previousRaycasterEnabled = _raycaster.enabled;
            _raycaster.enabled = false;
        }
        _visual = gameObject.GetComponent<Graphic>();
        if (_visual)
        {
            _previousRaycastTarget = _visual.raycastTarget;
            _visual.raycastTarget = false;
        }

        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = 0;
        canvas.sortingOrder = short.MaxValue;
    }

    // ScreenSpaceOverlay UGUI must not be switched to a camera or RenderTexture during OnGUI.
    // The stock Canvas remains responsible for all tooltip pixels and frame composition.
    internal static void RenderAfterGui(GlobalDark? ui) { }

    internal static void Restore()
    {
        if (!_active)
        {
            _root = null;
            return;
        }

        _active = false;
        if (_canvas)
        {
            _canvas!.sortingLayerID = _previousLayer;
            _canvas.sortingOrder = _previousOrder;
            _canvas.overrideSorting = _previousOverrideSorting;
            _canvas.enabled = _previousEnabled;
        }
        if (_raycaster) _raycaster!.enabled = _previousRaycasterEnabled;
        if (_visual) _visual!.raycastTarget = _previousRaycastTarget;
        if (_addedCanvas && _canvas) Object.Destroy(_canvas);
        _canvas = null;
        _raycaster = null;
        _visual = null;
        _root = null;
        _addedCanvas = false;
    }

    internal static void Release() => Restore();
}
