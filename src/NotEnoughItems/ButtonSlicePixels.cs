using System;

namespace NotEnoughItems;

// Device-pixel boundaries for the fixed bands and stretchable center of a button.
internal static class ButtonSlicePixels
{
    internal static int EdgePixels(int texels, float pixelsPerUnit) =>
        (int)Math.Floor(texels * pixelsPerUnit + .5f);

    // Quantize both sides from their respective outer edges. Drawing each side texel
    // separately also avoids a point-sampling tie when two texels span three pixels.
    internal static (int Start, int End, int SourceStart, int SourceEnd) Column(
        int column, int width, int sourceWidth, int side, float pixelsPerUnit)
    {
        if (column < side)
            return (EdgePixels(column, pixelsPerUnit), EdgePixels(column + 1, pixelsPerUnit), column, column + 1);
        if (column == side)
        {
            int edge = EdgePixels(side, pixelsPerUnit);
            return (edge, width - edge, side, sourceWidth - side);
        }
        int fromRight = 2 * side - column;
        return (width - EdgePixels(fromRight + 1, pixelsPerUnit), width - EdgePixels(fromRight, pixelsPerUnit),
            sourceWidth - fromRight - 1, sourceWidth - fromRight);
    }
}
