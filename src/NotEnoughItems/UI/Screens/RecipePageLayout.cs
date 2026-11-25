using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>Positions the recipe controls and content inside one NEI inspector.</summary>
internal readonly struct RecipePanelLayout
{
    internal const float DiagramInset = 23, ScrollbarAllowance = 17f;
    internal readonly Rect Bounds, Frame, Back, Method, MethodTitle, PreviousMethod, NextMethod, Pages,
        PageTitle, PreviousPage, NextPage, Content, Catalyst;
    internal readonly int TabsPerPage;
    private const float TabSize = 24f, TabArrowWidth = 16f, TabArrowHeight = 20f;
    private readonly float _tabX;

    internal RecipePanelLayout(Rect available, float? centerX = null)
    {
        this = default;
        float width = Math.Min(228, available.width);
        float height = Math.Min(426, available.height);
        if (width < 104 || height < 100) return;
        float x = Math.Clamp((centerX ?? available.center.x) - width / 2, available.xMin, available.xMax - width);
        float y = available.center.y - height / 2;
        Bounds = new Rect(x, y, width, height);
        float inset = NeiAssets.RecipeBackgroundSlice;
        float left = x + inset, right = Bounds.xMax - inset;

        const float rowHeight = 19, arrowWidth = 18, gap = 2;
        _tabX = x + 36;
        Back = new Rect(x + 2, y + 2, 16, 20);
        TabsPerPage = Math.Clamp((int)((Bounds.xMax - TabArrowWidth - _tabX) / TabSize), 1, 7);
        Frame = Rect.MinMaxRect(x, y + TabSize - NeiAssets.RecipeBackgroundTransparentInset - 2, Bounds.xMax, Bounds.yMax);

        Method = new Rect(left, Frame.y + inset, right - left, rowHeight);
        Pages = new Rect(left, Method.yMax + gap, right - left, rowHeight);
        PreviousMethod = new Rect(left, Method.y, arrowWidth, rowHeight);
        NextMethod = new Rect(right - arrowWidth, Method.y, arrowWidth, rowHeight);
        PreviousPage = new Rect(left, Pages.y, arrowWidth, rowHeight);
        NextPage = new Rect(right - arrowWidth, Pages.y, arrowWidth, rowHeight);
        MethodTitle = Rect.MinMaxRect(PreviousMethod.xMax, Method.y, NextMethod.xMin, Method.yMax);
        PageTitle = Rect.MinMaxRect(PreviousPage.xMax, Pages.y, NextPage.xMin, Pages.yMax);
        Content = Rect.MinMaxRect(left, Pages.yMax + 4, right, Bounds.yMax - inset);
        Catalyst = new Rect(left, Content.y + 3f, 20, 20);
    }

    internal Rect Tab(int slot) => new(_tabX + slot * TabSize, Bounds.yMin, TabSize, TabSize);

    // The panel's first two visible rows are its outline and highlight. Keep the
    // tab's one-texel highlight on that same row, with panel-colored fill below it.
    internal (Rect Body, Rect Highlight, Rect Fill) TabFace(Rect slot)
    {
        float top = slot.yMin + NeiAssets.RecipeTabOffset;
        float highlight = Frame.yMin + NeiAssets.RecipeBackgroundTransparentInset + 1f;
        var body = Rect.MinMaxRect(slot.xMin, top, slot.xMax, highlight);
        return (body, new Rect(slot.xMin, highlight, slot.width, 1f),
            new Rect(slot.xMin, highlight + 1f, slot.width, 1f));
    }

    internal (Rect Previous, Rect Next) TabArrows(int page, int methodCount)
    {
        int visible = TabPageEnd(page, methodCount) - TabPageStart(page);
        float y = Bounds.yMin + 2f;
        return (new Rect(_tabX - TabArrowWidth, y, TabArrowWidth, TabArrowHeight),
            new Rect(Tab(visible).xMin, y, TabArrowWidth, TabArrowHeight));
    }

    internal int TabPageCount(int methodCount) => TabsPerPage > 0 ? (methodCount + TabsPerPage - 1) / TabsPerPage : 0;
    internal int TabPageStart(int page) => page * TabsPerPage;
    internal int TabPageEnd(int page, int methodCount) => Math.Min(TabPageStart(page) + TabsPerPage, methodCount);

    // Include the icon tabs and both navigation rows in tooltip avoidance and input ownership.
    internal Rect NavigationBounds => Rect.MinMaxRect(Bounds.xMin, Bounds.yMin, Bounds.xMax, Pages.yMax + 1);

    // Both edge lanes have the same width: the machine and the possible scrollbar
    // do not shift the recipe itself when they appear or disappear.
    internal static Rect DiagramArea(Rect viewport, float y) => new(DiagramInset, y, viewport.width - 2f * DiagramInset, 0);

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
