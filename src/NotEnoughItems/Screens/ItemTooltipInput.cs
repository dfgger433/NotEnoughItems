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
        "Show item, liquid and entry IDs in NEI tooltips. Hold Shift and left-click an ID or mod name to copy it.");

    private bool CanUseTooltip => !_layoutEditing && !_plugin.NativePointerActive;
    private bool CanCopyTooltip => TooltipCanInteract(SelectionShiftHeld, CanUseTooltip);

    internal static bool TooltipCanInteract(bool shiftHeld, bool available) => shiftHeld && available;

    private void ClearItemTooltipInput()
    {
        _pinnedIdTip = null;
        _supportedCopyId = null;
    }

    private bool ItemTooltipVisibleHit(Vector2 pointer) => _pinnedIdTip is { } tip && tip.Box.Contains(pointer) && CanUseTooltip;

    private bool ItemTooltipHit(Vector2 pointer) => ItemTooltipVisibleHit(pointer) && TooltipCanInteract(SelectionShiftHeld, CanUseTooltip);

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
        (Rect IdRow, string? Id, Rect ModRow, string? Mod) rows = default;
        if (overPinned && _pinnedIdTip is { } item)
            rows = (item.IdRow, item.Content.Id, item.ModRow, item.Content.ModText);
        else if (overSupported && _supportedCopyId is { } supported)
            rows = supported;

        var idRow = _showTooltipId.Value ? rows.IdRow : default;
        if (CopyTooltipValueAt(rows.Id, idRow, rows.Mod, rows.ModRow, pointer, e.type, e.button, SelectionShiftHeld) is { } copied)
        {
            GUIUtility.systemCopyBuffer = copied;
            _tooltipCopyPressed = true;
            Status(L("Copied: ", "已复制：") + copied);
            e.Use();
        }
        else if ((overPinned || overSupported) && SelectionShiftHeld &&
            e.type is (EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel))
            e.Use();
        else if (!overPinned && e.type is (EventType.MouseDown or EventType.ScrollWheel))
            _pinnedIdTip = null;
    }

    internal static string? CopyTooltipValueAt(string? id, Rect idRow, string? mod, Rect modRow,
        Vector2 pointer, EventType type, int button, bool shift)
    {
        if (!shift || type != EventType.MouseDown || button != 0) return null;
        return idRow.Contains(pointer) ? id : modRow.Contains(pointer) ? mod : null;
    }

    private void QueueItemTip(Vector2 pointer, Rect source, NativeTooltip.Content content) =>
        _queuedItemTip = (pointer, source, content);

    private void DrawQueuedItemTip()
    {
        // Draw after the chain and supported-items popups so the copy row is never behind them.
        var next = _pinnedIdTip is { } pinned ? (pinned.Anchor, pinned.Source, pinned.Content) : _queuedItemTip;
        if (next is not { } tip) return;
        var (anchor, source, content) = tip;
        bool copyEnabled = CanCopyTooltip;
        var (box, idRow, modRow) = NeiSkin.ItemTooltip(anchor, new Vector2(_width, _height), content, RecipeNavigationBounds,
            copyIdEnabled: copyEnabled && _showTooltipId.Value, copyModEnabled: copyEnabled);
        _pinnedIdTip = box.width > 0f ? (content, anchor, source, box, idRow, modRow) : null;
    }
}
