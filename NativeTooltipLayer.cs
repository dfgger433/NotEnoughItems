using UnityEngine;

namespace NotEnoughItems;

internal static class NativeTooltipLayer
{
    private static readonly Vector3[] Corners = new Vector3[4];

    internal static void MoveClearOfOverlay(GlobalDark ui, Plugin plugin)
    {
        var root = ui.tooltipRect;
        if (!root || !root.gameObject.activeInHierarchy || !ui.hoverNameBackground) return;
        var canvas = ui.canvas;
        var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Rect actual = ScreenBounds(ui.hoverNameBackground, camera);
        if (ui.hoverDescriptionBackground && ui.hoverDescriptionBackground.gameObject.activeInHierarchy)
        {
            Rect body = ScreenBounds(ui.hoverDescriptionBackground, camera);
            actual = Rect.MinMaxRect(Mathf.Min(actual.xMin, body.xMin), Mathf.Min(actual.yMin, body.yMin),
                Mathf.Max(actual.xMax, body.xMax), Mathf.Max(actual.yMax, body.yMax));
        }
        if (actual.width <= 0 || actual.height <= 0) return;
        Rect placed = TooltipPlacement.Place(actual, new Rect(6, 6, Mathf.Max(1, Screen.width - 12), Mathf.Max(1, Screen.height - 12)),
            plugin.TooltipObstacles());
        var delta = placed.position - actual.position;
        if (delta == Vector2.zero) return;

        // Preserve the stock renderer, text, scale and layout; only move its finished root.
        var origin = RectTransformUtility.WorldToScreenPoint(camera, root.position);
        if (root.parent is RectTransform parent &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, origin + new Vector2(delta.x, -delta.y), camera, out var world))
            root.position = world;
    }

    private static Rect ScreenBounds(RectTransform rect, Camera? camera)
    {
        rect.GetWorldCorners(Corners);
        var first = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
        var last = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);
        return Rect.MinMaxRect(Mathf.Min(first.x, last.x), Screen.height - Mathf.Max(first.y, last.y),
            Mathf.Max(first.x, last.x), Screen.height - Mathf.Min(first.y, last.y));
    }
}
