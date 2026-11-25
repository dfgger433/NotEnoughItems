using NotEnoughItems;
using UnityEngine;

internal static class RecipeListLayoutTests
{
    internal static void Run()
    {
        var pages = new RecipePages();
        pages.Build(Array.Empty<float>(), h => h, 200);
        Check(pages.Count == 0 && pages.PageOf(0) == 0);
        pages.Build(new[] { 80f, 120f, 300f, 90f, 90f, 40f }, h => h, 200);
        Check(pages.Count == 4 && pages.Range(0) == (0, 2) && pages.Range(3) == (5, 6));
        Check(pages.PageOf(2) == 1 && pages.Range(1) == (2, 3) && pages.Height(2) == 300);
        Check(pages.PageOf(4) == 2 && pages.Range(2) == (3, 5));
        Check(pages.PageOf(99) == 3);
        pages.Build(new[] { 55f }, h => h, 150);
        Check(pages.Count == 1 && pages.Range(0) == (0, 1));

        var panel = new RecipePanelLayout(new Rect(13, 5, 250, 440));
        Check(panel.Bounds.Contains(panel.Back.center) && panel.Bounds.Contains(panel.Method.center) &&
            panel.Bounds.Contains(panel.Pages.center) && panel.Bounds.Contains(panel.Content.center));
        var slot = panel.Tab(0);
        Check(slot.size == new Vector2(24f, 24f) && slot.yMin == panel.Bounds.yMin &&
            panel.Frame.yMin == panel.Bounds.yMin + 18f &&
            panel.Method.yMin >= panel.Frame.yMin && panel.Method.yMax < panel.Pages.yMin && panel.Pages.yMax < panel.Content.yMin,
            "Changing only the tab backgrounds must keep the panel and hit targets in place.");
        foreach (var layout in new[] { panel, new RecipePanelLayout(new Rect(13.3f, 5.25f, 165.75f, 400)) })
        {
            Rect tabHit = layout.Tab(0);
            float panelRim = layout.Frame.yMin + NeiAssets.RecipeBackgroundTransparentInset;
            var first = layout.TabFace(tabHit);
            var next = layout.TabFace(layout.Tab(1));
            Check(Near(first.Body.xMin, tabHit.xMin) && Near(first.Body.yMin, tabHit.yMin + 1f) &&
                Near(first.Body.yMax, first.Highlight.yMin) && Near(first.Highlight.yMin, panelRim + 1f) &&
                Near(first.Highlight.yMax, first.Fill.yMin) && Near(first.Fill.yMin, panelRim + 2f) &&
                Near(first.Fill.yMax, panelRim + 3f),
                "The tab's highlight and fill must cover the equivalent panel rows without gaps.");
            Check(Near(first.Body.xMax, next.Body.xMin) && Near(first.Highlight.xMax, next.Highlight.xMin) &&
                Near(first.Fill.xMax, next.Fill.xMin), "Adjacent tab bands must share a horizontal edge.");
        }
        Check(panel.PreviousMethod.xMin == panel.Method.xMin && panel.NextMethod.xMax == panel.Method.xMax &&
            panel.PreviousMethod.xMax == panel.MethodTitle.xMin && panel.MethodTitle.xMax == panel.NextMethod.xMin &&
            panel.PreviousMethod.xMin == panel.PreviousPage.xMin && panel.NextMethod.xMax == panel.NextPage.xMax,
            "The method and page controls remain aligned in separate rows.");
        Check(panel.PageTitle.xMin == panel.PreviousPage.xMax && panel.PageTitle.xMax == panel.NextPage.xMin &&
            panel.PageTitle.center == panel.Pages.center && panel.NavigationBounds.Contains(panel.PageTitle.center),
            "Removing the recipe-search lane centers the page count between the arrows.");
        Check(panel.MethodTitle.height >= 19 && panel.PreviousMethod.size == panel.PreviousPage.size &&
            panel.NextMethod.size == panel.NextPage.size && panel.PreviousMethod.width >= 18 &&
            panel.NavigationBounds.Contains(panel.PreviousMethod.center) && panel.NavigationBounds.Contains(panel.NextMethod.center),
            "Reserve a full-height title and button-sized hit targets for both method arrows inside the protected header.");
        Check(panel.Method.xMin == panel.Content.xMin && panel.Method.xMax == panel.Content.xMax &&
            panel.Pages.xMin == panel.Content.xMin && panel.Pages.xMax == panel.Content.xMax &&
            panel.MethodTitle.center == panel.Method.center && panel.PreviousMethod.width == 18f,
            "The original inset method and page rows keep both labels centered between the 18px arrows.");
        foreach (float width in new[] { 104f, 120.5f, 140f, 180f, 228f })
        {
            var layout = new RecipePanelLayout(new Rect(13.3f, 0, width, 400));
            for (int visible = 1; visible <= layout.TabsPerPage; visible++)
            {
                var (left, right) = layout.TabArrows(1, layout.TabsPerPage + visible);
                Check(layout.Back.size == new Vector2(16f, 20f) && layout.Back.xMax < left.xMin &&
                    left.size == new Vector2(16f, 20f) && right.size == left.size && Near(Mathf.Min(right.xMax, layout.Bounds.xMax), right.xMax) &&
                    left.yMin == layout.Bounds.yMin + 2f && right.yMin == left.yMin &&
                    layout.NavigationBounds.Contains(left.center) && layout.NavigationBounds.Contains(right.center),
                    "Both paging controls retain the earlier narrow 16×20 positions beside the visible tab range and leave Back clear.");

                float edge = left.xMax;
                for (int i = 0; i < visible; i++)
                {
                    var tab = layout.Tab(i);
                    Check(Near(tab.xMin, edge) && tab.size == new Vector2(24f, 24f) && tab.yMin == layout.Bounds.yMin,
                        "Tabs retain their earlier 24×24 hit targets, independent of their tall rectangular artwork.");
                    edge = tab.xMax;
                }
                Check(Near(edge, right.xMin), "The next-page control follows the last visible tab, including partial pages.");
            }
        }
        Check(panel.TabsPerPage == 7 && panel.TabPageCount(0) == 0 && panel.TabPageCount(7) == 1 &&
            panel.TabPageCount(8) == 2 && panel.TabPageStart(1) == 7 && panel.TabPageEnd(1, 8) == 8,
            "Overflow methods are grouped into clickable pages of seven tabs at full width.");

        float fits = panel.Content.height;
        float clipped = fits + 1;
        Check(panel.PageWheelDirection(panel.Content.center, -3, 4, fits) == -1);
        Check(panel.PageWheelDirection(panel.Content.center, 5, 4, fits) == 1);
        Check(panel.PageWheelDirection(panel.Content.center, 0, 4, fits) == 0);
        Check(panel.PageWheelDirection(panel.Content.center, 3, 1, fits) == 0);
        Check(panel.PageWheelDirection(panel.Content.center, 3, 4, clipped) == 0);
        Check(panel.PageWheelDirection(panel.Pages.center, 3, 4, clipped) == 1);
        Check(panel.PageWheelDirection(panel.Method.center, 3, 4, fits) == 0);

        var viewport = new Rect(15, 100, 220, 80);
        var offset = viewport.position - new Vector2(0, 130);
        var hit = RecipeListLayout.VisibleHit(new Rect(140, 119, 23, 22), viewport, offset);
        Check(hit.Contains(new Vector2(160, 105)) && !hit.Contains(new Vector2(160, 99)) && hit.height == 11);
        Check(RecipeListLayout.VisibleHit(new Rect(30, 0, 23, 22), viewport, offset).height == 0);
        Check(Enumerable.Range(0, 6).Select(step => RecipeGridLayout.CandidateIndex(step, 5))
            .SequenceEqual(new[] { 0, 1, 2, 3, 4, 0 }));
        Check(RecipeGridLayout.CandidateIndex(int.MaxValue, 1) == 0);
        Check(RecipeGridLayout.CandidateIndex(7, 3) == 1 && RecipeGridLayout.CandidateIndex(7, 5) == 2);

        var sizing = new Rect(0, 0, 210, 0);
        var small = new RecipeGridLayout(sizing, 9, 1, .7f);
        var large = new RecipeGridLayout(sizing, 9, 1, 1.5f);
        Check(small.Cell < large.Cell && large.Plate.xMax <= sizing.xMax);

        foreach (float width in new[] { 100f, 150f, 208f, 525f })
        {
            foreach (var (inputs, outputs) in new[] { (0, 0), (1, 1), (9, 1), (10, 1), (13, 10), (52, 2) })
            {
                var area = new Rect(4, 130, width, 0);
                var grid = new RecipeGridLayout(area, inputs, outputs);
                int expectedInputRows = Math.Max(3, (inputs + 2) / 3);
                Check(grid.InputRows == expectedInputRows && grid.Plate.x >= area.x && grid.Plate.xMax <= area.xMax);
                Check(grid.VisibleInputs(grid.Plate) == (0, 3 * expectedInputRows));
                int lastInputIndex = Math.Max(0, inputs - 1);
                var lastInput = grid.Input(lastInputIndex);
                var lastOutput = grid.Output(Math.Max(0, outputs - 1));
                Check(grid.Input(2).xMax <= grid.Arrow.x && grid.Arrow.xMax <= grid.Output(0).x);
                Check(lastInput.yMax <= grid.Plate.yMax && lastOutput.yMax <= grid.Plate.yMax && lastOutput.xMax <= grid.Plate.xMax);
                var heart = grid.Favorite;
                var crafting = grid.Transfer;
                Check(heart.size == crafting.size && heart.width == 16f && heart.height == 16f && heart.x == crafting.x);
                Check(Mathf.Abs(heart.center.y - grid.Output(0).center.y) < .01f && heart.xMin >= lastOutput.xMax &&
                    heart.yMin >= grid.Plate.yMin && heart.yMax < crafting.yMin && crafting.yMax <= grid.Plate.yMax &&
                    crafting.xMax <= area.xMax);
                Check(Mathf.Abs((grid.Plate.xMin + grid.Plate.xMax + RecipeGridLayout.ActionColumn) * .5f - area.center.x) < .01f &&
                    Mathf.Abs(grid.Arrow.center.y - grid.Plate.center.y) < .01f);
                var clip = new Rect(area.x, lastInput.y, width, 1);
                var visible = grid.VisibleInputs(clip);
                Check(visible.Start <= lastInputIndex && visible.End > lastInputIndex);
                Check(grid.VisibleOutputs(grid.Plate, outputs) == (0, Math.Max(1, outputs)));
            }
        }
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .01f;

    private static void Check(bool condition, string message = "Recipe pagination, grid bounds, or hit clipping is incorrect.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
