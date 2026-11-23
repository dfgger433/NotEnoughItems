using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Positions the recipe controls and content inside one NEI inspector.</summary>
internal readonly struct RecipePanelLayout
{
    private const float RowHeight = 19, Gap = 2;
    internal const float RowNavWidth = 18, DiagramInset = 23;
    internal readonly Rect Bounds, Frame, Header, Back, Method, MethodTitle, PreviousMethod, NextMethod, Pages, Content, Catalyst;

    internal RecipePanelLayout(Rect available, float? centerX = null)
    {
        const float inset = NeiAssets.RecipeBackgroundSlice;
        float width = Math.Min(228, available.width);
        float height = Math.Min(426, available.height);
        float x = Math.Clamp((centerX ?? available.center.x) - width / 2, available.xMin, available.xMax - width);
        float y = available.center.y - height / 2;
        Bounds = new Rect(x, y, width, height);
        float left = x + inset, right = Bounds.xMax - inset;
        Header = Rect.MinMaxRect(left, y, right, y + RowHeight);
        Back = new Rect(left, Header.y, RowNavWidth, RowHeight);

        // Match the GTNH NEI two-row recipe header: the method selector and page
        // selector share their left/right arrow columns, both inside the frame.
        Frame = Rect.MinMaxRect(x, y + RowHeight + Gap, Bounds.xMax, Bounds.yMax);
        Method = new Rect(left, Frame.y + inset, right - left, RowHeight);
        PreviousMethod = new Rect(Method.x, Method.y, RowNavWidth, Method.height);
        NextMethod = new Rect(Method.xMax - RowNavWidth, Method.y, RowNavWidth, Method.height);
        MethodTitle = Rect.MinMaxRect(PreviousMethod.xMax + Gap, Method.y, NextMethod.x - Gap, Method.yMax);
        Pages = new Rect(left, Method.yMax + Gap, right - left, RowHeight);
        Content = Rect.MinMaxRect(left, Pages.yMax + 2 * Gap, right, Bounds.yMax - inset);
        Catalyst = new Rect(left, Content.y + 3f, 20, 20);
    }

    internal const float ScrollbarAllowance = 17f;

    // Both edge lanes have the same width: the machine and the possible scrollbar
    // do not shift the recipe itself when they appear or disappear.
    internal static Rect DiagramArea(Rect viewport, float y) => new(DiagramInset, y, viewport.width - 2f * DiagramInset, 0);

    internal static Rect RowTitle(Rect row) => Rect.MinMaxRect(row.x + RowNavWidth + Gap, row.y, row.xMax - RowNavWidth - Gap, row.yMax);

    // The page bar always offers paging. A clipped diagram needs the wheel over
    // its viewport even at the scroll limits, so all of that recipe stays reachable.
    internal int PageWheelDirection(Vector2 pointer, float deltaY, int pageCount, float contentHeight) =>
        pageCount > 1 && (Pages.Contains(pointer) || contentHeight <= Content.height && Content.Contains(pointer))
            ? Math.Sign(deltaY) : 0;
}

/// <summary>Greedily fit complete recipes on a page. An oversized recipe gets a page of its own.</summary>
internal sealed class RecipePages
{
    private readonly List<int> _starts = new();
    private readonly List<float> _heights = new();
    internal int Count => _starts.Count - 1;

    internal void Build<T>(IReadOnlyList<T> recipes, Func<T, float> measure, float capacity)
    {
        _starts.Clear(); _starts.Add(0);
        _heights.Clear();
        float used = 0;
        for (int i = 0; i < recipes.Count; i++)
        {
            float height = measure(recipes[i]);
            if (used > 0 && used + height > capacity) { _starts.Add(i); used = 0; }
            used += height;
            _heights.Add(height);
        }
        if (recipes.Count > 0) _starts.Add(recipes.Count);
    }

    internal int PageOf(int index)
    {
        if (Count == 0) return 0;
        index = Math.Clamp(index, 0, _heights.Count - 1);
        int page = _starts.BinarySearch(index);
        return page < 0 ? ~page - 1 : page;
    }

    internal (int First, int End) Range(int page) => (_starts[page], _starts[page + 1]);
    internal float Height(int index) => _heights[index];
}
