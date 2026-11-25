using System;
using UnityEngine;

namespace NotEnoughItems;

internal static class TooltipPlacement
{
    // Only the viewport can displace a hover preview from its normal pointer offset.
    // All values are in top-left logical GUI units.
    internal static Rect AtPointer(Vector2 pointer, Vector2 size, Rect viewport)
    {
        float width = Math.Min(size.x, viewport.width);
        float height = Math.Min(size.y, viewport.height);
        float x = pointer.x + 12f;
        if (x + width > viewport.xMax) x = pointer.x - width - 8f;
        float y = pointer.y + 15f;
        if (y + height > viewport.yMax) y = pointer.y - height - 8f;
        return new Rect(Mathf.Clamp(x, viewport.xMin, viewport.xMax - width),
            Mathf.Clamp(y, viewport.yMin, viewport.yMax - height), width, height);
    }

    // Only Shift can hold the tooltip across the gap between its source and its box.
    internal static bool KeepItemTip(Vector2 pointer, Vector2 anchor, Rect source, Rect box, bool interactive)
    {
        if (!interactive) return false;
        if (source.Contains(pointer) || box.Contains(pointer)) return true;
        const float slop = 4f;
        return Rect.MinMaxRect(Math.Min(anchor.x, box.xMin) - slop, Math.Min(anchor.y, box.yMin) - slop,
            Math.Max(anchor.x, box.xMax) + slop, Math.Max(anchor.y, box.yMax) + slop).Contains(pointer);
    }

    // Page boundaries are contiguous so large lists can be read without losing the last entries.
    internal static (int Index, int Pages, int Start, int End) ItemListPage(int count, int page, float height, float rowHeight)
    {
        int perPage = Math.Max(1, (int)Math.Floor(height / rowHeight));
        int pages = count == 0 ? 1 : 1 + (count - 1) / perPage;
        int index = Math.Clamp(page, 0, pages - 1);
        int start = index * perPage;
        return (index, pages, start, start + Math.Min(perPage, count - start));
    }

    // A pinned grid is anchored to its source slot rather than to the moving pointer.
    internal static Rect AtSlot(Rect anchor, Vector2 size, Rect viewport)
    {
        const float gap = 6f;
        float width = Math.Min(size.x, viewport.width);
        float height = Math.Min(size.y, viewport.height);
        float right = viewport.xMax - anchor.xMax;
        float left = anchor.xMin - viewport.xMin;
        float x = right >= width + gap || right >= left ? anchor.xMax + gap : anchor.xMin - gap - width;
        return new Rect(Mathf.Clamp(x, viewport.xMin, viewport.xMax - width),
            Mathf.Clamp(anchor.yMin, viewport.yMin, viewport.yMax - height), width, height);
    }

    internal static Rect CenteredGridCell(Rect area, float cell, int columns, int count, int index)
    {
        if (columns < 1 || cell <= 0 || count <= 0 || index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        int row = index / columns;
        int first = row * columns;
        int rowCount = Math.Min(columns, count - first);
        int rows = 1 + (count - 1) / columns;
        float top = area.center.y - Math.Min(area.height, rows * cell) * .5f;
        return new Rect(area.center.x - rowCount * cell * .5f + (index - first) * cell,
            top + row * cell, cell, cell);
    }

    internal static Rect CandidateCell(Rect grid, int columns, float cell, int index) =>
        new(grid.x + index % columns * cell, grid.y + index / columns * cell, cell, cell);

    internal static int CandidateAt(Rect grid, int columns, float cell, int start, int end, Vector2 pointer)
    {
        if (!grid.Contains(pointer)) return -1;
        int column = (int)((pointer.x - grid.xMin) / cell);
        if (column >= columns) return -1;
        int row = (int)((pointer.y - grid.yMin) / cell);
        int index = start + row * columns + column;
        return index < end ? index : -1;
    }
}
