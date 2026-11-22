using UnityEngine;

namespace NotEnoughItems;

internal enum BookmarkShortcut { None, Tree, OrderTree, OrderFavorite, Pull, PullMissing, Craft, CraftMissing, Share, ShareRecipe, Copy, Paste, OpenNative }

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
        (KeyCode.L, true, false) => BookmarkShortcut.Share,
        (KeyCode.L, true, true) => BookmarkShortcut.ShareRecipe,
        (KeyCode.C, true, false) => BookmarkShortcut.Copy,
        (KeyCode.V, true, false) => BookmarkShortcut.Paste,
        (KeyCode.S, false, true) => BookmarkShortcut.OpenNative,
        _ => BookmarkShortcut.None
    };
}
