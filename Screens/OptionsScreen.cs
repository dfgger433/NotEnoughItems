using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private void DrawOptions(Rect p)
    {
        NeiSkin.Background(p, "options");
        Header(p, L("NEI options", "NEI 选项"), "NEI");
        float x = p.x + 12, w = p.width - 24, y = p.y + 38;
        var catalog = CatalogSetting;
        string catalogLabel = _plugin.IsNativeInventory ? L("Inventory catalog", "背包右侧目录") : L("Item catalog", "物品目录");
        if (NeiSkin.Button(new Rect(x, y, w, 27), catalogLabel + (catalog.Value ? L(": On", "：开") : L(": Off", "：关"))))
            catalog.Value = !catalog.Value;
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27), _showHistory.Value ? L("History strip: On", "浏览记录：开") : L("History strip: Off", "浏览记录：关"))) _showHistory.Value = !_showHistory.Value;
        y += 31;
        string mode = NeiActions.ModeName(NeiActions.Mode);
        bool modeEnabled = NeiActions.Mode != NeiActions.LocalMode.Recipe;
        if (NeiSkin.Button(new Rect(x, y, w, 27),
            L($"Mode: {mode}", $"模式：{mode}"), NeiActions.CanEnter || modeEnabled, modeEnabled))
        {
            NeiActions.TryCycleMode(out var notice);
            Status(notice);
        }
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27),
            _plugin.AllowCheats ? L("AllowLocalCheats: On", "AllowLocalCheats：开") : L("AllowLocalCheats: Off", "AllowLocalCheats：关"),
            selected: _plugin.AllowCheats))
        {
            _plugin.SetAllowCheats(!_plugin.AllowCheats);
            Status(_plugin.AllowCheats
                ? L("Local cheats enabled; select Utility or Cheat mode.", "已启用单机作弊；请选择实用或作弊模式。")
                : L("Local cheats disabled.", "已禁用单机作弊。"));
        }
        y += 32;
        if (NeiActions.ShowsUtilities)
        {
            if (NeiSkin.Button(new Rect(x, y, w, 27), L("Heal local character", "治疗本地角色"), NeiActions.CanUse))
                Status(NeiActions.Heal() ?? L("Character healed", "已治疗角色"));
            y += 29;
            if (_plugin.IsNativeInventory)
            {
                int[] speeds = { 1, 5, 20 };
                for (int i = 0; i < speeds.Length; i++)
                    if (NeiSkin.Button(new Rect(x + i * w / 3, y, w / 3 - 3, 24), $"×{speeds[i]}", NeiActions.CanUse))
                        Status(NeiActions.SetSpeed(speeds[i]) ?? L("Game speed changed", "已更改游戏速度"));
                y += 27;
            }
        }
        NeiSkin.Label(new Rect(x, y, w, 24), L("Left click: recipes · Right click: uses · Middle: bookmark", "左键查配方 · 右键查用途 · 中键加书签"));
        NeiSkin.Label(new Rect(x, y + 26, w, 24), L("Cheat: catalog LMB = quantity · RMB = 1 · Shift = lookup", "作弊：目录左键=数量 · 右键=1 · Shift=查询"));
        if (NeiSkin.Button(new Rect(x, p.yMax - 68, w, 26), L("Hide overlay", "隐藏 NEI"))) { ResetView(); _plugin.HideOverlay(); }
        if (NeiSkin.Button(new Rect(x, p.yMax - 35, w, 26), L("Done", "完成"))) _options = false;
    }

    private static void Header(Rect p, string heading, string mark)
    {
        if (p.width >= 108) NeiSkin.Label(new Rect(p.x + 8, p.y + 5, p.width - 61, 19), heading);
        NeiSkin.Label(new Rect(p.xMax - Mathf.Min(55, p.width - 5), p.y + 5, Mathf.Min(48, p.width - 7), 19), mark, TextAnchor.MiddleRight, true);
    }
}
