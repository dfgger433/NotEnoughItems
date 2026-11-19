using UnityEngine;

namespace NotEnoughItems;

internal static class NeiLayout
{
    internal static Rect FromScreen(Rect rect, float screenHeight) =>
        new(rect.x, screenHeight - rect.yMax, rect.width, rect.height);

    // Work in top-left screen pixels, in the same coordinate system as OnGUI.
    internal static (Rect Left, Rect Right) Sides(Rect center, float width, float height, float scale)
    {
        const float margin = 8f;
        const float gap = 7f;
        float maxWidth = 330f * scale;
        float y = Mathf.Max(margin, center.yMin);
        float bottom = Mathf.Min(height - margin, Mathf.Max(center.yMax, y + 310f));
        float leftEdge = Mathf.Clamp(center.xMin - gap, margin, width - margin);
        float rightEdge = Mathf.Clamp(center.xMax + gap, margin, width - margin);
        float leftWidth = Mathf.Min(maxWidth, leftEdge - margin);
        float rightWidth = Mathf.Min(maxWidth, width - margin - rightEdge);
        return (new Rect(leftEdge - leftWidth, y, leftWidth, bottom - y),
            new Rect(rightEdge, y, rightWidth, bottom - y));
    }

    internal static Rect Inspector(Rect center, float width, float height, float scale)
    {
        float w = Mathf.Min(width - 16, 560 * scale);
        float h = Mathf.Min(height - 16, 530 * scale);
        return new Rect(Mathf.Clamp(center.center.x - w / 2, 8, width - w - 8),
            Mathf.Clamp(center.center.y - h / 2, 8, height - h - 8), w, h);
    }
}
