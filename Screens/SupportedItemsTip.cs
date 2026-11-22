using System;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private const float SupportedItemRow = 18f;
    private bool _showSupportedItems;
    private Rect _supportedItemsHit;
    private int _supportedItemsToken = -1, _supportedItemsPage, _supportedItemsPageCount;
    private (string Title, string Details, string[] Names) _supportedItems;

    private void SetSupportedItemsTip(Rect hit, int token, string title, string details, string[] names)
    {
        if (_supportedItemsToken != token || !ReferenceEquals(_supportedItems.Names, names)) _supportedItemsPage = 0;
        _supportedItemsToken = token;
        _supportedItems = (title, details, names);
        _supportedItemsHit = hit;
        _showSupportedItems = true;
    }

    private void ClearSupportedItemsTip()
    {
        _showSupportedItems = false;
        _supportedItemsHit = default;
        _supportedItemsToken = -1;
        _supportedItemsPage = _supportedItemsPageCount = 0;
        _supportedItems = default;
    }

    private void HandleSupportedItemsWheel(Rect viewport)
    {
        var e = Event.current;
        if (e.type != EventType.ScrollWheel || !e.shift || e.delta.y == 0 || _supportedItemsPageCount <= 1 ||
            !viewport.Contains(e.mousePosition) || !_supportedItemsHit.Contains(e.mousePosition)) return;
        _supportedItemsPage = Wrap(_supportedItemsPage + Math.Sign(e.delta.y), _supportedItemsPageCount);
        e.Use();
    }

    private void DrawSupportedItemsTip(Vector2 pointer)
    {
        const float margin = 4, pad = 7, footer = 20;
        var viewport = new Rect(margin, margin, _width - 2 * margin, _height - 2 * margin);
        float width = Mathf.Min(440, viewport.width);
        if (width <= 2 * pad) return;
        var (requirement, details, names) = _supportedItems;
        string title = L($"Supported items ({names.Length})", $"支持的物品（{names.Length}）") + " · " + requirement;
        float textWidth = width - 2 * pad;
        float titleHeight = NeiSkin.TextHeight(title, textWidth);
        float detailsHeight = string.IsNullOrWhiteSpace(details) ? 0 : NeiSkin.TextHeight(details, textWidth) + 4;
        float header = pad + titleHeight + detailsHeight + 3;
        var page = TooltipPlacement.ItemListPage(names.Length, _supportedItemsPage,
            viewport.height - header - footer - pad, SupportedItemRow);
        _supportedItemsPage = page.Index;
        _supportedItemsPageCount = page.Pages;
        float bodyHeight = (page.End - page.Start) * SupportedItemRow;
        float footerHeight = page.Pages > 1 ? footer : 0;
        var box = TooltipPlacement.AtPointer(pointer, new Vector2(width, header + bodyHeight + footerHeight + pad), viewport);
        NeiSkin.TooltipBox(box);
        float x = box.x + pad, y = box.y + pad;
        NeiSkin.Text(new Rect(x, y, textWidth, titleHeight), title);
        if (detailsHeight > 0) NeiSkin.Text(new Rect(x, y + titleHeight, textWidth, detailsHeight), details);
        y = box.y + header;
        for (int i = page.Start; i < page.End; i++, y += SupportedItemRow)
            NeiSkin.Label(new Rect(x, y, textWidth, SupportedItemRow), "• " + names[i]);
        if (page.Pages > 1)
            NeiSkin.Label(new Rect(x, y, textWidth, footer),
                L($"Shift+wheel: more items · {page.Index + 1}/{page.Pages}", $"Shift＋滚轮：查看更多 · {page.Index + 1}/{page.Pages}"), muted: true);
    }
}
