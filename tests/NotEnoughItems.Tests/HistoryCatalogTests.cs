using NotEnoughItems;
using UnityEngine;

internal static class HistoryCatalogTests
{
    internal static void Run()
    {
        var (_, catalog, _, footer) = NeiLayout.Overlay(2560, 1440);
        Check(footer == new Rect(1831, 1178, 719, 184),
            "The catalog search, history and quantity footer uses the requested 2560×1440 bounds.");
        var panel = new NeiLayout.CatalogPanel(catalog, footer);
        Check(panel.Search.y == panel.Quantity.y && panel.Search.xMax < panel.Quantity.xMin &&
            panel.Quantity.xMax < panel.Increase.xMin && panel.Increase.xMax < panel.Decrease.xMin &&
            panel.Grid.yMax < panel.HistoryDivider.yMin && panel.HistoryDivider.yMax < panel.History.yMin &&
            panel.History.yMax < panel.Search.yMin,
            "The history strip sits below the grid divider and above the search and quantity row.");
        var (bookmarks, _, _, _) = NeiLayout.Overlay(2560, 1440);
        Check(NeiLayout.BesideCrafting(bookmarks, new Rect(500, 100, 1000, 1000)).xMax == 495 &&
            NeiLayout.BesideCrafting(bookmarks, new Rect(40, 0, 1000, 1000)).width == 0,
            "The crafting bookmark surface does not cover native controls or draw an unusably narrow sidebar.");
        var (thin, wide, tools, movedFooter) = NeiLayout.Overlay(2560, 1440, 65, 120, -6);
        Check(thin.width < bookmarks.width && wide.width > catalog.width && wide.xMax == catalog.xMax &&
            thin.yMin > 0 && movedFooter.xMin == wide.xMin && wide.yMax < movedFooter.yMin && tools.yMin > thin.yMax,
            "Layout settings keep the edge anchors and move the footer with the catalog without touching the center.");
        var noHistory = new NeiLayout.CatalogPanel(catalog, footer, showHistory: false);
        Check(noHistory.History.height == 0 && noHistory.HistoryDivider.height == 0 &&
            noHistory.Search.yMin == noHistory.History.yMin && noHistory.Search.yMin < panel.Search.yMin && noHistory.Grid == panel.Grid,
            "Hiding history reclaims the top of the footer without moving the item grid.");

        foreach (float width in new[] { 640f, 1280f, 2560f })
        foreach (float height in new[] { 360f, 720f, 1440f })
        {
            var (scaledLeft, scaledRight, scaledTools, scaledControls) = NeiLayout.Overlay(width, height);
            var viewport = new Vector2(width, height);
            foreach (bool isRight in new[] { false, true })
            {
                Rect body = isRight ? scaledRight : scaledLeft;
                Rect footerArea = isRight ? scaledControls : scaledTools;
                Vector2 fitted = NeiLayout.PanelOffset(body, footerArea, viewport, new Vector2(1000, -1000), isRight);
                var movedBody = NeiLayout.Translate(body, fitted);
                var movedPanelFooter = NeiLayout.Translate(footerArea, fitted);
                Check(movedBody.xMin >= -1 && movedBody.xMax <= width + 1 && movedPanelFooter.xMin >= -1 && movedPanelFooter.xMax <= width + 1 &&
                    Mathf.Min(movedBody.yMin, movedPanelFooter.yMin) >= -1 && Mathf.Max(movedBody.yMax, movedPanelFooter.yMax) <= height + 1,
                    "Extreme dragged offsets keep the panel group inside every supported viewport.");
            }
        }

        Check(NeiLayout.DragWidth(100, 10, 200, right: false) == 105 &&
            NeiLayout.DragWidth(100, -10, 200, right: false) == 95 &&
            NeiLayout.DragWidth(100, 2000, 200, right: true) == 65,
            "Resize handles adjust each sidebar width within its configured limits.");

        Check(!BookmarkBoardPaging.CanPackBookmarkRow(false, "group") &&
            BookmarkBoardPaging.CanPackBookmarkRow(false, "") && BookmarkBoardPaging.CanPackBookmarkRow(true, "group"),
            "Ungrouped and dense grouped bookmarks use their intended row-packing behavior.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
