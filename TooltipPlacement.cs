using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

internal static class TooltipPlacement
{
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
