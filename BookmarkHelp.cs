using System.Collections.Generic;

namespace NotEnoughItems;

// The data tooltip and the keyboard tutorial are separate views of the same bookmark target.
internal static class BookmarkHelp
{
    internal static string Rows(bool page, bool hasEntries, bool chain, bool crafting, bool chinese, string? bookmarkKey)
    {
        var lines = new List<string>();
        void Add(string en, string zh) => lines.Add(chinese ? zh : en);

        Add("Left click: switch grid / queue", "左键：切换网格／队列");
        if (hasEntries)
        {
            if (page)
            {
                Add("Alt+left click: fold / unfold ungrouped", "Alt＋左键：折叠／展开未分组书签");
                Add(chain ? "Right click: disable ungrouped chain" : "Right click: enable ungrouped chain",
                    chain ? "右键：关闭未分组合成链" : "右键：启用未分组合成链");
                Add("Ctrl+right click: toggle all chains", "Ctrl＋右键：切换整页合成链");
                Add("Ctrl+hover: show all enabled chain totals", "Ctrl＋悬停：汇总已启用的合成链");
            }
            else
            {
                Add("Ctrl+left click: fold / unfold group", "Ctrl＋左键：折叠／展开书签组");
                Add(chain ? "Right click: disable chain" : "Right click: enable chain", chain ? "右键：关闭合成链" : "右键：启用合成链");
                Add("Alt+left click: ungroup without deleting", "Alt＋左键：解散分组，保留书签");
                Add("Ctrl+right click: ungroup without deleting", "Ctrl＋右键：解散分组，保留书签");
                Add("Left drag: extend group range", "左键拖动：扩大分组范围");
                Add("Right drag: shrink group range", "右键拖动：缩小分组范围");
                Add("Right Alt+left drag: shrink group range", "右 Alt＋左键拖动：缩小分组范围");
                Add("Shift+left drag: reorder groups", "Shift＋左键拖动：调整组顺序");
            }
            Add(chain ? "Ctrl+wheel: adjust target batches" : "Ctrl+wheel: adjust quantity",
                chain ? "Ctrl＋滚轮：调整目标批数" : "Ctrl＋滚轮：调整数量");
            Add("Ctrl+Alt+wheel: adjust by the selected amount", "Ctrl＋Alt＋滚轮：按选定数量调整");
            if (chain)
            {
                Add("Wheel: scroll totals", "滚轮：滚动用量");
                if (!crafting)
                {
                    Add(page ? "T: inspect the ungrouped chain" : "T: inspect the crafting tree",
                        page ? "T：查看未分组依赖树" : "T：查看合成链依赖树");
                    Add("Hold Shift: pin totals and show steps", "按住 Shift：固定用量并显示步骤");
                }
                else
                {
                    Add("Shift+C: craft targets", "Shift＋C：制作目标");
                    Add("Ctrl+Shift+C: craft missing targets", "Ctrl＋Shift＋C：补齐目标数量");
                }
            }
            Add(page ? "Ctrl+C: copy this page" : "Ctrl+C: copy group", page ? "Ctrl＋C：复制整页" : "Ctrl＋C：复制书签组");
            if (bookmarkKey != null)
                Add(page ? $"Shift+{bookmarkKey}: remove ungrouped bookmarks" : $"Shift+{bookmarkKey}: remove group",
                    page ? $"Shift＋{bookmarkKey}：删除未分组书签" : $"Shift＋{bookmarkKey}：删除书签组");
        }
        Add("Ctrl+V: paste into this page", "Ctrl＋V：粘贴到当前页");
        return string.Join("\n", lines);
    }
}
