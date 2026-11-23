using System;
using UnityEngine;

namespace NotEnoughItems;

// Translate a scroll-content hit target to the visible GUI coordinates.
internal static class RecipeListLayout
{
    internal static Rect VisibleHit(Rect local, Rect viewport, Vector2 offset)
    {
        local.position += offset;
        float left = Math.Max(local.xMin, viewport.xMin), top = Math.Max(local.yMin, viewport.yMin);
        float right = Math.Min(local.xMax, viewport.xMax), bottom = Math.Min(local.yMax, viewport.yMax);
        return left < right && top < bottom ? Rect.MinMaxRect(left, top, right, bottom) : default;
    }
}
