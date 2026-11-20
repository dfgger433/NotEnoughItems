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
        if (NeiSkin.Button(new Rect(x, y, w, 27), _showGrid.Value ? L("Item catalog: On", "物品目录：开") : L("Item catalog: Off", "物品目录：关"))) _showGrid.Value = !_showGrid.Value;
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27), _showHistory.Value ? L("History strip: On", "浏览记录：开") : L("History strip: Off", "浏览记录：关"))) _showHistory.Value = !_showHistory.Value;
        y += 31;
        if (NeiSkin.Button(new Rect(x, y, w, 27), NeiActions.Enabled ? L("Mode: Local cheat", "模式：单机作弊") : L("Mode: Recipe", "模式：配方"), NeiActions.CanEnter || NeiActions.Enabled, NeiActions.Enabled))
            NeiActions.Enabled = !NeiActions.Enabled;
        y += 32;
        if (NeiActions.Enabled)
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
        NeiSkin.Label(new Rect(x, y + 26, w, 24), L("Cheat mode: Ctrl+click to give; add Shift for 16", "作弊模式：Ctrl+点击给物品；加 Shift 一次给 16 件"));
        if (NeiSkin.Button(new Rect(x, p.yMax - 68, w, 26), L("Hide overlay", "隐藏 NEI"))) { ResetView(); _plugin.HideOverlay(); }
        if (NeiSkin.Button(new Rect(x, p.yMax - 35, w, 26), L("Done", "完成"))) _options = false;
    }

    private static void Header(Rect p, string heading, string mark)
    {
        if (p.width >= 108) NeiSkin.Label(new Rect(p.x + 8, p.y + 5, p.width - 61, 19), heading);
        NeiSkin.Label(new Rect(p.xMax - Mathf.Min(55, p.width - 5), p.y + 5, Mathf.Min(48, p.width - 7), 19), mark, TextAnchor.MiddleRight, true);
    }
}
