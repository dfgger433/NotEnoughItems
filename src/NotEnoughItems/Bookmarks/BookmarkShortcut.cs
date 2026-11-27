using UnityEngine;

namespace NotEnoughItems;

internal enum BookmarkShortcut { None, Tree, OrderTree, OrderFavorite, Pull, PullMissing, Craft, CraftMissing, SpawnMissing, Share, ShareRecipe, Copy, Paste, OpenNative }
internal enum BookmarkBracketClick { None, ToggleLayout, ToggleChain, ToggleCollapse, Ungroup }
internal enum BookmarkSlotClick { None, Default, Split, ExpandLane, ToggleRecipeCollapse }

internal static class BookmarkClicks
{
    // Resolve clicks only after release; a drag must never fall through to a click action.
    internal const float DragDistance = 4;

    internal static BookmarkBracketClick Bracket(int button, bool alt, bool control, bool shift) => (button, alt, control, shift) switch
    {
        (0, _, _, true) => BookmarkBracketClick.None,
        (0, true, _, false) or (1, _, true, _) => BookmarkBracketClick.Ungroup,
        (0, false, true, false) => BookmarkBracketClick.ToggleCollapse,
        (0, false, false, false) => BookmarkBracketClick.ToggleLayout,
        (1, _, false, _) => BookmarkBracketClick.ToggleChain,
        _ => BookmarkBracketClick.None
    };

    internal static BookmarkSlotClick Slot(int button, bool alt, bool shift, bool grouped, bool laneCollapsed, bool canFoldRecipe)
    {
        if (!alt) return BookmarkSlotClick.Default;
        if (button != 0) return BookmarkSlotClick.None;
        if (grouped && !shift) return BookmarkSlotClick.Split;
        if (!grouped && laneCollapsed) return BookmarkSlotClick.ExpandLane;
        return canFoldRecipe ? BookmarkSlotClick.ToggleRecipeCollapse : BookmarkSlotClick.None;
    }
}

internal static class BookmarkKeys
{
    // Keep destructive actions distinct from similarly modified clipboard and navigation shortcuts.
    internal static BookmarkShortcut Resolve(KeyCode key, bool control, bool shift) => (key, control, shift) switch
    {
        (KeyCode.T, false, false) => BookmarkShortcut.Tree,
        (KeyCode.T, false, true) => BookmarkShortcut.OrderTree,
        (KeyCode.F, false, true) => BookmarkShortcut.OrderFavorite,
        (KeyCode.V, false, false) => BookmarkShortcut.Pull,
        (KeyCode.V, false, true) => BookmarkShortcut.PullMissing,
        (KeyCode.C, false, true) => BookmarkShortcut.Craft,
        (KeyCode.C, true, true) => BookmarkShortcut.CraftMissing,
        (KeyCode.M, true, true) => BookmarkShortcut.SpawnMissing,
        (KeyCode.L, true, false) => BookmarkShortcut.Share,
        (KeyCode.L, true, true) => BookmarkShortcut.ShareRecipe,
        (KeyCode.C, true, false) => BookmarkShortcut.Copy,
        (KeyCode.V, true, false) => BookmarkShortcut.Paste,
        (KeyCode.S, false, true) => BookmarkShortcut.OpenNative,
        _ => BookmarkShortcut.None
    };
}
