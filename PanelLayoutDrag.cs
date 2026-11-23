using UnityEngine;

namespace NotEnoughItems;

internal readonly struct PanelLayoutDrag(bool resize, bool right, Vector2 start, Vector2 offset, int width, float panelWidth)
{
    internal bool Resize { get; } = resize;
    internal bool Right { get; } = right;
    internal Vector2 Start { get; } = start;
    internal Vector2 Offset { get; } = offset;
    internal int Width { get; } = width;
    internal float PanelWidth { get; } = panelWidth;

    internal static PanelLayoutDrag Move(bool right, Vector2 start, Vector2 offset, float panelWidth) => new(false, right, start, offset, 100, panelWidth);

    internal static PanelLayoutDrag ResizePanel(bool right, Vector2 start, int width, float panelWidth) => new(true, right, start, default, width, panelWidth);

    internal Vector2 MovedOffset(Vector2 position) => Offset + position - Start;

    internal int ResizedWidth(Vector2 position) =>
        NeiLayout.DragWidth(Width, position.x - Start.x, PanelWidth, Right);
}
