using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

internal static class TooltipPlacement
{
    // All values, including the viewport, are already in top-left logical GUI units.
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

    // Keep the pinned tooltip reachable across the gap between its source and its box.
    internal static bool KeepItemTip(Vector2 pointer, Vector2 anchor, Rect source, Rect box)
    {
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

    // Use top-left screen pixels, including the space occupied by the vanilla/terminal UI.
    internal static Rect Place(Rect tooltip, Rect screen, IReadOnlyList<Rect> obstacles)
    {
        float maxX = Math.Max(screen.xMin, screen.xMax - tooltip.width);
        float maxY = Math.Max(screen.yMin, screen.yMax - tooltip.height);
        float x = Mathf.Clamp(tooltip.xMin, screen.xMin, maxX);
        float y = Mathf.Clamp(tooltip.yMin, screen.yMin, maxY);
        var xs = new List<float> { x, screen.xMin, maxX };
        var ys = new List<float> { y, screen.yMin, maxY };
        foreach (var rect in obstacles)
        {
            xs.Add(Mathf.Clamp(rect.xMin - tooltip.width - 4, screen.xMin, maxX));
            xs.Add(Mathf.Clamp(rect.xMax + 4, screen.xMin, maxX));
            ys.Add(Mathf.Clamp(rect.yMin - tooltip.height - 4, screen.yMin, maxY));
            ys.Add(Mathf.Clamp(rect.yMax + 4, screen.yMin, maxY));
        }
        var best = new Rect(x, y, tooltip.width, tooltip.height);
        float bestOverlap = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
        foreach (float left in xs)
        foreach (float top in ys)
        {
            var test = new Rect(left, top, tooltip.width, tooltip.height);
            float overlap = 0;
            foreach (var rect in obstacles)
                overlap += Math.Max(0, Math.Min(test.xMax, rect.xMax) - Math.Max(test.xMin, rect.xMin)) *
                    Math.Max(0, Math.Min(test.yMax, rect.yMax) - Math.Max(test.yMin, rect.yMin));
            float distance = (left - x) * (left - x) + (top - y) * (top - y);
            if (overlap < bestOverlap || overlap == bestOverlap && distance < bestDistance)
            {
                best = test;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }
        return best;
    }
}
