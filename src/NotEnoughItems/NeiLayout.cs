using UnityEngine;

namespace NotEnoughItems;

internal static class NeiLayout
{
    internal static Rect FromScreen(Rect rect, float screenHeight) =>
        new(rect.x, screenHeight - rect.yMax, rect.width, rect.height);

    private const float ReferenceWidth = 2560f, ReferenceHeight = 1440f;
    private static readonly Rect BookmarkBounds = new(10, 135, 741, 1025);
    private static readonly Rect CatalogBounds = new(1831, 135, 719, 1042);
    private static readonly Rect ToolBounds = new(104, 1198, 640, 96);
    private static readonly Rect CatalogControlsBounds = new(1831, 1178, 719, 184);

    internal static float GuiScale(float width, float height, float zoom, int bookmarkWidth = 100, int catalogWidth = 100)
    {
        float preferred = Mathf.Clamp(height / 720f, .9f, 2f) * zoom;
        // Keep a 245-unit inspector plus padding between the fixed sidebars, and
        // enough height for the catalog controls on compact displays.
        float sideWidth = BookmarkBounds.width * WidthFraction(bookmarkWidth) + CatalogBounds.width * WidthFraction(catalogWidth);
        float centerScale = width * ((ReferenceWidth - 20f - sideWidth) / ReferenceWidth) / 255f;
        return Mathf.Max(.01f, Mathf.Min(preferred, Mathf.Min(centerScale, height / 440f)));
    }

    // Both game surfaces share these top-left screenshot regions. Project X and Y
    // independently into the logical IMGUI viewport; the GUI matrix scales back to pixels.
    internal static (Rect Bookmarks, Rect Catalog, Rect Tools, Rect CatalogControls) Overlay(float width, float height,
        int bookmarkWidth = 100, int catalogWidth = 100, int verticalOffset = 0)
    {
        float x = width / ReferenceWidth, y = height / ReferenceHeight;
        float dy = Mathf.Clamp(verticalOffset, -6, 5) * ReferenceHeight / 100f;
        Rect Project(Rect rect, float factor, bool right = false)
        {
            float w = rect.width * factor;
            return new Rect((right ? rect.xMax - w : rect.x) * x, (rect.y + dy) * y, w * x, rect.height * y);
        }
        return (Project(BookmarkBounds, WidthFraction(bookmarkWidth)), Project(CatalogBounds, WidthFraction(catalogWidth), right: true),
            Project(ToolBounds, WidthFraction(bookmarkWidth)), Project(CatalogControlsBounds, WidthFraction(catalogWidth), right: true));
    }

    private static float WidthFraction(int percent) => Mathf.Clamp(percent, 65, 120) / 100f;

    // The complete panel (including its footer/tools) follows the same drag. Leave room
    // for the central inspector, and leave a header-sized margin above the preview.
    internal static Vector2 PanelOffset(Rect panel, Rect footer, Vector2 viewport, Vector2 perMille, bool right,
        float siblingWidth = 0f)
    {
        return ClampPanelOffset(new Vector2(perMille.x * viewport.x / 1000f, perMille.y * viewport.y / 1000f),
            panel, footer, viewport, right, siblingWidth);
    }

    internal static Vector2 ClampPanelOffset(Vector2 offset, Rect panel, Rect footer, Vector2 viewport, bool right,
        float siblingWidth = 0f)
    {
        const float margin = 4f;
        float left = Mathf.Min(panel.xMin, footer.xMin), end = Mathf.Max(panel.xMax, footer.xMax);
        float groupWidth = end - left;
        float gap = Mathf.Min(255f, Mathf.Max(0f, viewport.x - groupWidth - siblingWidth - 2 * margin));
        float centerEdge = (viewport.x + (right ? gap : -gap)) * .5f;
        float minX = right ? centerEdge - left : margin - left;
        float maxX = right ? viewport.x - margin - end : centerEdge - end;
        float top = Mathf.Min(panel.yMin, footer.yMin), bottom = Mathf.Max(panel.yMax, footer.yMax);
        return new Vector2(Mathf.Clamp(offset.x, minX, maxX),
            Mathf.Clamp(offset.y, Mathf.Min(23f, top) - top, viewport.y - margin - bottom));
    }

    internal static int DragWidth(int start, float delta, float panelWidth, bool right) =>
        Mathf.Clamp(Mathf.RoundToInt(start + (right ? -delta : delta) * start / Mathf.Max(1f, panelWidth)), 65, 120);

    internal static Vector2 ToPanelUnits(Vector2 offset, Vector2 viewport) =>
        new(Mathf.Round(offset.x * 1000f / viewport.x), Mathf.Round(offset.y * 1000f / viewport.y));

    internal static Rect Translate(Rect rect, Vector2 offset) => new(rect.position + offset, rect.size);

    internal static Rect MoveHandle(Rect panel) => new(panel.x, panel.y - 19f, panel.width, 17f);

    internal static Rect ResizeHandle(Rect panel, bool right) =>
        new(right ? panel.x : panel.xMax - 12f, panel.y, Mathf.Min(panel.width, 12f), panel.height);

    internal static Rect LayoutDoneHandle(Rect left, Rect right) =>
        new(Mathf.Clamp((left.center.x + right.center.x) * .5f - 46f, 4f, Mathf.Max(4f, right.xMin - 96f)), 4f, 92f, 24f);

    internal const float BookmarkControlSize = 28f, BookmarkCellSize = 34f;

    internal readonly struct ToolButtons
    {
        internal readonly Rect Settings, Bookmarks;

        internal ToolButtons(Rect area)
        {
            const float gap = 3;
            float side = Mathf.Clamp((area.width - gap) / 2, 0, Mathf.Min(30f, area.height));
            float y = area.center.y - side / 2;
            Settings = new Rect(area.x, y, side, side);
            Bookmarks = new Rect(Settings.xMax + gap, y, side, side);
        }

        internal bool Hit(Vector2 pointer, bool showBookmarks) =>
            Settings.Contains(pointer) || (showBookmarks && Bookmarks.Contains(pointer));
    }

    internal readonly struct CatalogPanel
    {
        internal readonly Rect Previous, Page, Next, Grid, Search, Quantity, Increase, Decrease, History, HistoryDivider;

        internal CatalogPanel(Rect panel, Rect controls, bool showHistory = true)
        {
            const float gap = 2f, padding = 3f;
            float panelLeft = panel.x + padding, panelRight = panel.xMax - padding, panelTop = panel.y + 2f;
            float navSize = Mathf.Min(23f, (panelRight - panelLeft) / 6f);
            Previous = new Rect(panelLeft, panelTop, navSize, navSize);
            Next = new Rect(panelRight - navSize, panelTop, navSize, navSize);
            Page = Rect.MinMaxRect(Previous.xMax + gap, panelTop, Next.x - gap, Previous.yMax);
            Grid = Rect.MinMaxRect(panelLeft, Previous.yMax + padding, panelRight, panel.yMax - padding);

            float left = controls.x + padding, right = controls.xMax - padding;
            float width = right - left, top = controls.y + 2f;
            float rowHeight = Mathf.Min(23f, (controls.height - 2f - gap - padding) / 2f);
            // Reserve a readable two-digit value (13-unit font + 12 units of field padding)
            // and keep the search editor active even at the smallest catalog width.
            const float minimumAmountWidth = 30f, minimumSearchWidth = 22f;
            float buttonBudget = Mathf.Max(0f, (width - 3f * gap - minimumAmountWidth - minimumSearchWidth) / 2f);
            float buttonWidth = Mathf.Min(rowHeight, Mathf.Min(width / 6f, buttonBudget));
            float editorWidth = width - 2f * buttonWidth - 3f * gap;
            float searchReserve = Mathf.Clamp(width / 3f, minimumSearchWidth, Mathf.Max(minimumSearchWidth, editorWidth - minimumAmountWidth));
            float amountWidth = Mathf.Clamp(editorWidth - searchReserve, 0f, Mathf.Clamp(width * .24f, 44f, 85f));
            float historyHeight = showHistory ? Mathf.Max(0f, Mathf.Min(28f, controls.yMax - padding - top - rowHeight - 2 * gap)) : 0f;
            HistoryDivider = historyHeight > 0f ? new Rect(left, top - 1f, width, 1f) : default;
            History = new Rect(left, historyHeight > 0f ? top + gap : top, width, historyHeight);
            float editorTop = historyHeight > 0f ? History.yMax + gap : top;
            Decrease = new Rect(right - buttonWidth, editorTop, buttonWidth, rowHeight);
            Increase = new Rect(Decrease.x - gap - buttonWidth, editorTop, buttonWidth, rowHeight);
            Quantity = new Rect(Increase.x - gap - amountWidth, editorTop, amountWidth, rowHeight);
            Search = Rect.MinMaxRect(left, editorTop, Quantity.x - gap, editorTop + rowHeight);
        }
    }

    internal static Rect BesideCrafting(Rect bookmarks, Rect crafting)
    {
        bookmarks.width = Mathf.Max(0, Mathf.Min(bookmarks.xMax, crafting.xMin - 5f) - bookmarks.x);
        if (bookmarks.width < 48f) bookmarks.width = 0;
        return bookmarks;
    }

    internal static Rect InspectorBetween(Rect center, Rect left, Rect right, float width, float height)
    {
        var inspector = Inspector(center, width, height, 1f);
        float start = left.xMax + 5;
        float end = right.xMin - 5;
        if (end > start)
        {
            inspector.width = Mathf.Min(inspector.width, end - start);
            inspector.x = Mathf.Clamp(center.center.x - inspector.width / 2, start, end - inspector.width);
        }
        return inspector;
    }

    internal const float OptionsZoom = 1.2f;
    private const float OptionsMinWidth = 245f, OptionsMinHeight = 420f;

    internal static (Rect Bounds, float Zoom) OptionsBetween(Rect host, Rect left, Rect right, float width, float height)
    {
        const float margin = 8f, gap = 5f;
        float start = Mathf.Max(margin, left.xMax + gap), end = Mathf.Min(width - margin, right.xMin - gap);
        float availableWidth = end - start, availableHeight = height - 2f * margin;
        // Fit the larger controls, including the expanded language list, without covering either sidebar.
        float zoom = Mathf.Min(OptionsZoom, Mathf.Min(availableWidth / OptionsMinWidth, availableHeight / OptionsMinHeight));
        if (zoom <= 0) return (default, 1f);
        float w = Mathf.Min(availableWidth, 560f * zoom), h = Mathf.Min(availableHeight, 530f * zoom);
        float x = Mathf.Clamp(host.center.x - w * .5f, start, end - w);
        return (new Rect(x, (height - h) * .5f, w, h), zoom);
    }

    internal static Rect OptionsContentBounds(Rect bounds, float zoom) => new(bounds.position, bounds.size / zoom);

    internal static (Rect Nei, Rect ToPlayer) SpawnModeChoices(Rect row)
    {
        const float gap = 8f;
        float width = (row.width - gap) * .5f;
        return (new Rect(row.x, row.y, width, row.height), new Rect(row.x + width + gap, row.y, width, row.height));
    }

    internal static Rect Inspector(Rect center, float width, float height, float scale)
    {
        float w = Mathf.Min(width - 16, 560 * scale);
        float h = Mathf.Min(height - 16, 530 * scale);
        return new Rect(Mathf.Clamp(center.center.x - w / 2, 8, width - w - 8),
            Mathf.Clamp(center.center.y - h / 2, 8, height - h - 8), w, h);
    }
}
