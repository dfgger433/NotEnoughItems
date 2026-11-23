using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private (NativeTooltip.Content Content, Vector2 Anchor, Rect Source, Rect Box, Rect IdRow)? _pinnedIdTip;
    private (Rect Row, string Id)? _supportedCopyId;
    private bool _tooltipCopyPressed;
    private (Vector2 Pointer, Rect Source, NativeTooltip.Content Content)? _queuedItemTip;

    internal static ConfigEntry<bool> BindTooltipId(ConfigFile config) => config.Bind("NotEnoughItems", "ShowTooltipId", true,
        "Show item, liquid and entry IDs in NEI tooltips. Hold Shift and click the ID to copy it.");

    private bool CanCopyTooltipId => _showTooltipId.Value && SelectionShiftHeld && !_layoutEditing && !_plugin.NativePointerActive;

    private void ClearItemTooltipInput()
    {
        _pinnedIdTip = null;
        _supportedCopyId = null;
    }

    private bool ItemTooltipHit(Vector2 pointer) => _pinnedIdTip is { } tip && tip.Box.Contains(pointer) && CanCopyTooltipId;

    // Consume the click before any IMGUI slot or popup underneath can use it.
    private void HandleItemTooltipInput(Event e)
    {
        if (e.type == EventType.MouseUp && e.button == 0 && _tooltipCopyPressed)
        {
            _tooltipCopyPressed = false;
            e.Use();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0) _tooltipCopyPressed = false;
        if (!CanCopyTooltipId)
        {
            ClearItemTooltipInput();
            return;
        }
        Vector2 pointer = e.mousePosition;
        if (_pinnedIdTip is { } pinned && !TooltipPlacement.KeepItemTip(pointer, pinned.Anchor, pinned.Source, pinned.Box))
            _pinnedIdTip = null;

        var (row, id) = default((Rect Row, string? Id));
        bool overPinned = ItemTooltipHit(pointer);
        if (overPinned && _pinnedIdTip is { } item) (row, id) = (item.IdRow, item.Content.Id);
        else if (SupportedItemsHit(pointer) && _supportedCopyId is { } supported) (row, id) = supported;
        if (CopyIdAt(id, row, pointer, e.type, e.button, SelectionShiftHeld) is { } rawId)
        {
            GUIUtility.systemCopyBuffer = rawId;
            _tooltipCopyPressed = true;
            Status(L("Copied ID: ", "已复制 ID：") + rawId);
            e.Use();
        }
        else if (overPinned && e.type is EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag or EventType.ScrollWheel)
            e.Use();
        else if (e.type is EventType.MouseDown or EventType.ScrollWheel)
            _pinnedIdTip = null;
    }

    internal static string? CopyIdAt(string? id, Rect row, Vector2 pointer, EventType type, int button, bool shift) =>
        shift && type == EventType.MouseDown && button == 0 && row.Contains(pointer) ? id : null;

    private void QueueItemTip(Vector2 pointer, Rect source, NativeTooltip.Content content) =>
        _queuedItemTip = (pointer, source, content);

    private void DrawQueuedItemTip()
    {
        // Draw after the chain and supported-items popups so the copy row is never behind them.
        var next = _pinnedIdTip is { } pinned ? (pinned.Anchor, pinned.Source, pinned.Content) : _queuedItemTip;
        if (next is not { } tip) return;
        var (anchor, source, content) = tip;
        bool canCopy = CanCopyTooltipId && content.Id != null;
        var (box, row) = NeiSkin.ItemTooltip(anchor, new Vector2(_width, _height), content, canCopy);
        _pinnedIdTip = row.width > 0f && row.height > 0f ? (content, anchor, source, box, row) : null;
    }
}
