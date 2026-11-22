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

    // Keep both grids available even when a large host leaves no complete side gutter.
    internal static (Rect Left, Rect Right) VisibleSides(Rect center, float width, float height)
    {
        var (left, right) = Sides(center, width, height, 1f);
        float margin = Mathf.Min(8f, width * .04f);
        float maxSide = Mathf.Max(0, (width - 2 * margin - 255f) / 2);
        float minSide = Mathf.Min(108f, maxSide);
        float top = Mathf.Clamp(center.yMin, 8, Mathf.Max(8, height - 190));
        float bottom = Mathf.Min(height - 8, Mathf.Max(center.yMax, top + 310));
        float panelHeight = Mathf.Max(0, bottom - top);
        if (left.width < minSide) left = new Rect(margin, top, minSide, panelHeight);
        else left = new Rect(left.xMax - Mathf.Min(left.width, maxSide), top, Mathf.Min(left.width, maxSide), panelHeight);
        if (right.width < minSide) right = new Rect(width - margin - minSide, top, minSide, panelHeight);
        else right = new Rect(right.xMin, top, Mathf.Min(right.width, maxSide), panelHeight);
        // If the host is off-center, move both grids to the edges to leave room for recipe controls.
        if (right.xMin - left.xMax < 255f)
        {
            left = new Rect(margin, top, Mathf.Clamp(left.width, minSide, maxSide), panelHeight);
            right = new Rect(width - margin - Mathf.Clamp(right.width, minSide, maxSide), top,
                Mathf.Clamp(right.width, minSide, maxSide), panelHeight);
        }
        return (left, right);
    }

    // The wrench and bookmark toggle clear the native HUD; the catalog footer belongs to its panel.
    internal static Rect LeftTools(float height) => new(54, Mathf.Max(8, height - 31), 49, 23);

    internal readonly struct CatalogPanel
    {
        internal readonly Rect Previous, Page, Group, Next, Grid, Search, Name, Add, Remove;

        internal CatalogPanel(Rect panel)
        {
            const float gap = 2, h = 23;
            float x = panel.x + 3, end = panel.xMax - 3, top = panel.y + 2;
            float w = end - x;
            float cap = Mathf.Min(h, w / 6);
            Previous = new Rect(x, top, cap, h);
            Next = new Rect(end - cap, top, cap, h);
            Group = new Rect(Next.x - cap - gap, top, cap, h);
            Page = Rect.MinMaxRect(Previous.xMax, top, Group.x - gap, top + h);

            // On a narrow gutter let the search have a full line rather than clipping both editors.
            bool stacked = w < 180;
            float bottom = panel.yMax - 3, y = bottom - h;
            float nameWidth = stacked ? w - 2 * (cap + gap) : Mathf.Clamp(w * .24f, 48, 85);
            Remove = new Rect(end - cap, y, cap, h);
            Add = new Rect(Remove.x - gap - cap, y, cap, h);
            Name = new Rect(Add.x - gap - nameWidth, y, nameWidth, h);
            Search = new Rect(x, stacked ? y - gap - h : y, stacked ? w : Name.x - gap - x, h);
            Grid = Rect.MinMaxRect(x, Previous.yMax + 3, end, Search.y - 3);
        }
    }

    internal static Rect InspectorBetween(Rect center, Rect left, Rect right, float width, float height)
    {
        var inspector = Inspector(center, width, height, 1f);
        float start = left.xMax + 5;
        float end = right.xMin - 5;
        if (end - start >= 245)
        {
            inspector.width = Mathf.Min(inspector.width, end - start);
            inspector.x = Mathf.Clamp(center.center.x - inspector.width / 2, start, end - inspector.width);
        }
        return inspector;
    }

    internal static Rect Inspector(Rect center, float width, float height, float scale)
    {
        float w = Mathf.Min(width - 16, 560 * scale);
        float h = Mathf.Min(height - 16, 530 * scale);
        return new Rect(Mathf.Clamp(center.center.x - w / 2, 8, width - w - 8),
            Mathf.Clamp(center.center.y - h / 2, 8, height - h - 8), w, h);
    }
}
