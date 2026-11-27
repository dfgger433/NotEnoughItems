using System.Collections.Generic;

namespace NotEnoughItems;

// The data tooltip and the keyboard tutorial are separate views of the same bookmark target.
internal static class BookmarkHelp
{
    internal static string Rows(bool page, bool hasEntries, bool chain, bool crafting, bool chinese, string? bookmarkKey,
        bool spawnMissingEnabled = false)
    {
        var lines = new List<string>();
        void Add(string key, params object[] args) => lines.Add(NeiText.Get(key, chinese, args));

        Add("bookmark.help.switch_layout");
        if (hasEntries)
        {
            if (page)
            {
                Add("bookmark.help.page.toggle_ungrouped_fold");
                Add(chain ? "bookmark.help.page.disable_ungrouped_chain" : "bookmark.help.page.enable_ungrouped_chain");
                Add("bookmark.help.page.toggle_all_chains");
                Add("bookmark.help.page.show_enabled_totals");
            }
            else
            {
                Add("bookmark.help.group.toggle_fold");
                Add(chain ? "bookmark.help.group.disable_chain" : "bookmark.help.group.enable_chain");
                Add("bookmark.help.group.ungroup_alt");
                Add("bookmark.help.group.ungroup_ctrl");
                Add("bookmark.help.group.extend_range");
                Add("bookmark.help.group.shrink_range");
                Add("bookmark.help.group.shrink_range_right_alt");
                Add("bookmark.help.group.reorder");
            }
            Add(chain ? "bookmark.help.adjust_batches" : "bookmark.help.adjust_quantity");
            Add("bookmark.help.adjust_selected");
            if (chain)
            {
                Add("bookmark.help.scroll_totals");
                if (!crafting)
                {
                    Add(page ? "bookmark.help.page.inspect_ungrouped" : "bookmark.help.group.inspect_tree");
                    Add("bookmark.help.pin_totals");
                    if (!page && spawnMissingEnabled) Add("bookmark.help.spawn_missing");
                }
                else
                {
                    Add("bookmark.help.craft_targets");
                    Add("bookmark.help.craft_missing_targets");
                }
            }
            Add(page ? "bookmark.help.copy_page" : "bookmark.help.copy_group");
            if (bookmarkKey != null)
                Add(page ? "bookmark.help.remove_ungrouped" : "bookmark.help.remove_group", bookmarkKey);
        }
        Add("bookmark.help.paste_page");
        return string.Join("\n", lines);
    }
}
