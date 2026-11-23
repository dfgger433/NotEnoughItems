using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private (NativeTooltip.Content Content, Vector2 Anchor, Rect Source, Rect Box, Rect IdRow, Rect ModRow)? _pinnedIdTip;
    private (Rect IdRow, string? Id, Rect ModRow, string? Mod)? _supportedCopyId;
    private bool _tooltipCopyPressed;
    private bool _lastTooltipIdVisibility;
    private (Vector2 Pointer, Rect Source, NativeTooltip.Content Content)? _queuedItemTip;

    internal static ConfigEntry<bool> BindTooltipId(ConfigFile config) => config.Bind("NotEnoughItems", "ShowTooltipId", true,
        "Show item, liquid and entry IDs in NEI tooltips. Hold Shift and click an ID to copy it; click a mod name to copy it.");

    private bool CanUseTooltip => !_layoutEditing && !_plugin.NativePointerActive;
    private bool CanCopyTooltipId => _showTooltipId.Value && SelectionShiftHeld && CanUseTooltip;

    private void ClearItemTooltipInput()
    {
        _pinnedIdTip = null;
        _supportedCopyId = null;
    }

    private bool ItemTooltipHit(Vector2 pointer) => _pinnedIdTip is { } tip && tip.Box.Contains(pointer) && CanUseTooltip;

    // Consume clicks over tooltip rows before slots and popups underneath receive them.
    private void HandleItemTooltipInput(Event e)
    {
        if (e.type == EventType.MouseUp && e.button == 0 && _tooltipCopyPressed)
        {
            _tooltipCopyPressed = false;
            e.Use();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0) _tooltipCopyPressed = false;
        if (!CanUseTooltip)
        {
            ClearItemTooltipInput();
            return;
        }
        if (_lastTooltipIdVisibility != _showTooltipId.Value)
        {
            _lastTooltipIdVisibility = _showTooltipId.Value;
            _pinnedIdTip = null;
        }
        Vector2 pointer = e.mousePosition;
        if (_pinnedIdTip is { } pinned && !TooltipPlacement.KeepItemTip(pointer, pinned.Anchor, pinned.Source, pinned.Box))
            _pinnedIdTip = null;

        bool overSupported = SupportedItemsHit(pointer);
        bool overPinned = _pinnedIdTip is { } pinnedTip && pinnedTip.Box.Contains(pointer);
        Rect row = default;
        string? value = null;
        bool copy = false, requireShift = true;
        if (overPinned && _pinnedIdTip is { } item)
            SelectRow(item.IdRow, item.Content.Id, item.ModRow, item.Content.ModText);
        else if (overSupported && _supportedCopyId is { } supported)
            SelectRow(supported.IdRow, supported.Id, supported.ModRow, supported.Mod);

        if (CopyTooltipValueAt(value, row, pointer, e.type, e.button, SelectionShiftHeld, requireShift) is { } copied)
        {
            GUIUtility.systemCopyBuffer = copied;
            _tooltipCopyPressed = true;
            Status(L("Copied: ", "已复制：") + copied);
            e.Use();
        }
        else if ((overPinned || overSupported) && e.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel)
            e.Use();
        else if (e.type is EventType.MouseDown or EventType.ScrollWheel)
            _pinnedIdTip = null;

        void SelectRow(Rect idRow, string? id, Rect modRow, string? mod)
        {
            if (_showTooltipId.Value && idRow.Contains(pointer))
                (row, value, copy) = (idRow, id, CanCopyTooltipId);
            else if (modRow.Contains(pointer))
                (row, value, copy, requireShift) = (modRow, mod, true, false);
        }
    }

    internal static string? CopyTooltipValueAt(string? value, Rect row, Vector2 pointer, EventType type, int button, bool shift,
        bool requireShift = true) =>
        (!requireShift || shift) && type == EventType.MouseDown && button == 0 && row.Contains(pointer) ? value : null;

    private void QueueItemTip(Vector2 pointer, Rect source, NativeTooltip.Content content) =>
        _queuedItemTip = (pointer, source, content);

    private void DrawQueuedItemTip()
    {
        // Draw after the chain and supported-items popups so the copy row is never behind them.
        var next = _pinnedIdTip is { } pinned ? (pinned.Anchor, pinned.Source, pinned.Content) : _queuedItemTip;
        if (next is not { } tip) return;
        var (anchor, source, content) = tip;
        bool copyId = CanCopyTooltipId && content.Id != null;
        bool copyMod = CanUseTooltip && content.ModText != null;
        var (box, idRow, modRow) = NeiSkin.ItemTooltip(anchor, new Vector2(_width, _height), content, copyId, copyMod);
        _pinnedIdTip = box.width > 0f ? (content, anchor, source, box, idRow, modRow) : null;
    }
}
