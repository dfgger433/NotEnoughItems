using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private (NativeTooltip.Content Content, Vector2 Anchor, Rect Source, Rect Box, Rect IdRow, Rect ModRow)? _drawnItemTip;
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
        _drawnItemTip = null;
        _supportedCopyId = null;
        _tooltipCopyPressed = false;
    }

    private bool ItemTooltipHit(Vector2 pointer) => _drawnItemTip is { } tip && tip.Box.Contains(pointer) && CanCopyTooltip;

    // Consume clicks over tooltip rows before slots and popups underneath receive them.
    private void HandleItemTooltipInput(Event e)
    {
        if (!CanUseTooltip)
        {
            ClearItemTooltipInput();
            return;
        }
        bool interactive = CanCopyTooltip;
        if (_lastTooltipIdVisibility != _showTooltipId.Value)
        {
            _lastTooltipIdVisibility = _showTooltipId.Value;
            _drawnItemTip = null;
        }
        Vector2 pointer = e.mousePosition;
        if (_drawnItemTip is { } tip && !TooltipPlacement.KeepItemTip(pointer, tip.Anchor, tip.Source, tip.Box, interactive))
            _drawnItemTip = null;

        if (!interactive)
        {
            _tooltipCopyPressed = false;
            return;
        }
        if (e.type == EventType.MouseUp && e.button == 0 && _tooltipCopyPressed)
        {
            _tooltipCopyPressed = false;
            e.Use();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0) _tooltipCopyPressed = false;

        bool overItemTip = ItemTooltipHit(pointer);
        (Rect IdRow, string? Id, Rect ModRow, string? Mod) rows = default;
        if (overItemTip && _drawnItemTip is { } item)
            rows = (item.IdRow, item.Content.Id, item.ModRow, item.Content.ModText);
        else if (SupportedItemsHit(pointer) && _supportedCopyId is { } supported)
            rows = supported;

        var idRow = _showTooltipId.Value ? rows.IdRow : default;
        if (CopyTooltipValueAt(rows.Id, idRow, rows.Mod, rows.ModRow, pointer, e.type, e.button, interactive) is { } copied)
        {
            GUIUtility.systemCopyBuffer = copied;
            _tooltipCopyPressed = true;
            Status(L("Copied: ", "已复制：") + copied);
            e.Use();
        }
        else if (overItemTip && e.type is (EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel))
            e.Use();
        else if (e.type is (EventType.MouseDown or EventType.ScrollWheel))
            _drawnItemTip = null;
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
        // Paint passive tips from the current hover only. Shift can hold the last
        // drawn tip in place so the pointer can leave the source to reach its rows.
        bool copyEnabled = CanCopyTooltip;
        var next = copyEnabled && _drawnItemTip is { } last ? (last.Anchor, last.Source, last.Content) : _queuedItemTip;
        _drawnItemTip = null;
        if (next is not { } tip) return;
        var (anchor, source, content) = tip;
        var (box, idRow, modRow) = NeiSkin.ItemTooltip(anchor, new Vector2(_width, _height), content, RecipeNavigationBounds,
            copyIdEnabled: copyEnabled && _showTooltipId.Value, copyModEnabled: copyEnabled);
        if (box.width > 0f) _drawnItemTip = (content, anchor, source, box, idRow, modRow);
    }
}
