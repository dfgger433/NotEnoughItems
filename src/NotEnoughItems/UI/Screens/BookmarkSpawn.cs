using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    /// <summary>
    /// Ctrl+Shift+M: spawn the shortages of the chain under the pointer, or of the open
    /// dependency tree. The debug opt-in and cheat permissions still apply.
    /// </summary>
    internal bool HandleBookmarkSpawnMissing(Vector2 pointer)
    {
        if (!_plugin.DebugSpawnMissingMaterials || !ShowLeft || _options || _layoutEditing || _producing ||
            _plugin.IsCrafting || _plugin.NativePointerActive || _bookmarkDrag != null) return false;
        string? group;
        if (_treeOpen && _group == _treePage) group = _treeGroup;
        else
        {
            pointer /= Mathf.Max(.01f, _scale);
            if (!_left.Contains(pointer)) return false;
            var cell = _bookmarkCells.Find(hit => hit.Rect.Contains(pointer)).Cell;
            group = cell?.Entry.Group ?? _bookmarkHeaders.Find(hit => hit.Rect.Contains(pointer)).Group;
            // A page-number tip covers several groups; do not guess which one to spawn for.
            if (group == null) return false;
        }
        SpawnBookmarkGroupMissing(group);
        return true;
    }

    private void SpawnBookmarkGroupMissing(string group)
    {
        if (!_book.IsChainEnabled(_group, group))
        {
            Status(L("chain.spawn.enable_chain_first"));
            return;
        }
        var entries = BookmarkGroupEntries(group);
        if (!entries.Any(entry => entry.IsRecipe))
        {
            Status(L("chain.spawn.nothing_missing"));
            return;
        }
        Status(NeiActions.SpawnMissingMaterials(_catalog, body => BookmarkChainBalance.PlanCraft(_catalog,
            entries.Select(BalanceEntry).ToArray(), targetIndex: null, topUp: false,
            NativeCraftStock.Quantities(body), PreferredMaterials, NativeCraftStock.Quantities(body, nearby: true))));
    }
}
