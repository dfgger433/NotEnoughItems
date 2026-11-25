using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private Rect LayoutEditHitBox(bool right) => NeiLayout.MoveHandle(right ? _right : _left);
    private Rect LayoutEditResize(bool right) => NeiLayout.ResizeHandle(right ? _right : _left, right);

    private Rect LayoutEditDone => NeiLayout.LayoutDoneHandle(_left, _right);

    private bool LayoutEditHit(Vector2 point) => _layoutEditing &&
        (_layoutDrag.HasValue || LayoutEditDone.Contains(point) || ShowLeft && _left.Contains(point) || _leftTools.Hit(point, !HideBookmarksForDrag) ||
         ShowRight && (_right.Contains(point) || _catalogControls.Contains(point)) ||
         LayoutEditHitBox(false).Contains(point) || LayoutEditResize(false).Contains(point) ||
         ShowRight && (LayoutEditHitBox(true).Contains(point) || LayoutEditResize(true).Contains(point)));

    private void HandleLayoutInput(Event e)
    {
        if (!_layoutEditing) return;
        Vector2 mouse = e.mousePosition;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (LayoutEditHitBox(false).Contains(mouse)) { BeginLayoutDrag(false, false, mouse); e.Use(); }
            else if (LayoutEditResize(false).Contains(mouse)) { BeginLayoutDrag(false, true, mouse); e.Use(); }
            else if (ShowRight && LayoutEditHitBox(true).Contains(mouse)) { BeginLayoutDrag(true, false, mouse); e.Use(); }
            else if (ShowRight && LayoutEditResize(true).Contains(mouse)) { BeginLayoutDrag(true, true, mouse); e.Use(); }
        }
        else if (e.type == EventType.MouseDrag && _layoutDrag is { } drag)
        {
            if (drag.Resize)
            {
                int width = drag.ResizedWidth(mouse);
                _layoutPreviewWidth = width;
            }
            else
            {
                _layoutPreviewOffset = new Vector2(drag.Offset.x + (mouse.x - drag.Start.x) * 1000f / _width,
                    drag.Offset.y + (mouse.y - drag.Start.y) * 1000f / _height);
            }
            PositionPanels();
            e.Use();
        }
        else if (e.type == EventType.MouseUp && _layoutDrag is { } finished)
        {
            if (finished.Resize)
            {
                if (finished.Right) _catalogWidth.Value = _layoutPreviewWidth;
                else _bookmarkWidth.Value = _layoutPreviewWidth;
            }
            else
            {
                Vector2 offset = new(finished.Offset.x + (mouse.x - finished.Start.x) * 1000f / _width,
                    finished.Offset.y + (mouse.y - finished.Start.y) * 1000f / _height);
                if (finished.Right)
                {
                    _catalogX.Value = Mathf.RoundToInt(offset.x);
                    _catalogY.Value = Mathf.RoundToInt(offset.y);
                }
                else
                {
                    _bookmarkX.Value = Mathf.RoundToInt(offset.x);
                    _bookmarkY.Value = Mathf.RoundToInt(offset.y);
                }
            }
            _layoutDrag = null;
            PositionPanels();
            e.Use();
        }
    }

    private void BeginLayoutDrag(bool right, bool resize, Vector2 mouse)
    {
        _layoutDrag = resize
            ? PanelLayoutDrag.ResizePanel(right, mouse, right ? _catalogWidth.Value : _bookmarkWidth.Value,
                right ? _right.width : _left.width)
            : PanelLayoutDrag.Move(right, mouse,
                NeiLayout.ToPanelUnits(right ? _rightOffset : _leftOffset, new Vector2(_width, _height)),
                right ? _right.width : _left.width);
        _layoutPreviewOffset = _layoutDrag.Value.Offset;
        _layoutPreviewWidth = _layoutDrag.Value.Width;
    }

    private void DrawLayoutHandles()
    {
        if (!_layoutEditing) return;
        DrawHandle(_left, false);
        if (ShowRight) DrawHandle(_right, true);
        var done = LayoutEditDone;
        NeiSkin.BookmarkTint(done, new Color32(45, 112, 128, 225));
        if (NeiSkin.Button(done, L("options.done"))) ToggleLayoutEditing();

        void DrawHandle(Rect panel, bool right)
        {
            var move = NeiLayout.MoveHandle(panel);
            NeiSkin.BookmarkTint(move, new Color32(45, 112, 128, 225));
            NeiSkin.Label(move, L(right ? "layout.drag_catalog" : "layout.drag_bookmarks"), TextAnchor.MiddleCenter, true);
            var resize = NeiLayout.ResizeHandle(panel, right);
            NeiSkin.BookmarkTint(resize, new Color32(226, 172, 73, 190));
        }
    }

    private void StopLayoutEditing()
    {
        _layoutEditing = false;
        _layoutDrag = null;
    }

    private void ToggleLayoutEditing()
    {
        _layoutEditing = !_layoutEditing;
        _layoutDrag = null;
        _options = !_layoutEditing;
        _skipFocusFrame = true;
    }
}
