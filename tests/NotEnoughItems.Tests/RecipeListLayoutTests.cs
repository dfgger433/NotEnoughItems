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
        float frameEdge = panel.Frame.yMin + NeiAssets.RecipeBackgroundTransparentInset;
        Check(panel.Tab(0).yMin < frameEdge && panel.Tab(0).yMax > frameEdge &&
            panel.Method.yMin >= panel.Frame.yMin && panel.Method.yMax < panel.Pages.yMin && panel.Pages.yMax < panel.Content.yMin,
            "The open-bottom tab overlaps the visible frame edge, not just the transparent inset.");
        Check(panel.PreviousMethod.xMin == panel.Method.xMin && panel.NextMethod.xMax == panel.Method.xMax &&
            panel.PreviousMethod.xMax <= panel.MethodTitle.xMin && panel.MethodTitle.xMax <= panel.NextMethod.xMin &&
            panel.Search.xMin >= panel.PageTitle.xMin && panel.Search.xMax <= panel.PageTitle.xMax &&
            panel.PageTitle.xMax <= panel.NextPage.xMin && panel.Search.yMin >= panel.Pages.yMin &&
            panel.Search.yMax <= panel.Pages.yMax,
            "Method/page navigation keeps the page-row magnifier inside the page title lane.");
        Check(panel.PreviousMethod.xMin == panel.PreviousPage.xMin && panel.NextMethod.xMax == panel.NextPage.xMax &&
            panel.PageTitle.center.x == panel.MethodTitle.center.x &&
            panel.NavigationBounds.Contains(panel.PageTitle.center) && panel.NavigationBounds.Contains(panel.Tab(0).center),
            "Method and page arrows share columns while the page title remains a single non-search bar.");
        Check(panel.MethodTitle.height >= 19 && panel.PreviousMethod.size == panel.PreviousPage.size &&
            panel.NextMethod.size == panel.NextPage.size && panel.PreviousMethod.width >= 18 &&
            panel.NavigationBounds.Contains(panel.PreviousMethod.center) && panel.NavigationBounds.Contains(panel.NextMethod.center),
            "Reserve a full-height title and button-sized hit targets for both method arrows inside the protected header.");
        Check(panel.TabsPerPage == 7 && panel.TabPageCount(0) == 0 && panel.TabPageCount(7) == 1 && panel.TabPageCount(8) == 2 &&
            panel.Tab(0).xMax == panel.Tab(1).xMin && panel.Tab(6).xMax == panel.NextTabs.xMin,
            "The top row fits seven adjacent tabs and exposes overflow when needed.");
        var narrow = new RecipePanelLayout(new Rect(0, 0, 104, 400));
        Check(narrow.TabsPerPage < panel.TabsPerPage && narrow.Bounds.Contains(narrow.NextTabs.center) &&
            narrow.NextTabs.xMax <= narrow.Bounds.xMax && !narrow.Back.Overlaps(narrow.PreviousTabs) &&
            narrow.PageTitle.xMax <= narrow.NextPage.xMin);

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

    private static void Check(bool condition, string message = "Recipe pagination, grid bounds, or hit clipping is incorrect.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
