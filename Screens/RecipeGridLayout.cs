using System;
using UnityEngine;

namespace NotEnoughItems;

// In scroll-content coordinates. Both measurement and painting use the same geometry.
internal readonly struct RecipeGridLayout
{
    private const int MaxInputColumns = 3;
    private const float Pad = 3, PreferredCell = 22, ArrowWidth = 33, SingleOutputSize = 28;
    internal const float ActionSize = 16f;
    private const float ActionGap = 2f;
    internal const float ActionColumn = ActionSize + ActionGap;
    private readonly int _inputColumns, _outputColumns;
    internal readonly int InputRows, OutputRows, ChoiceColumns;
    internal readonly float Cell;
    internal readonly Rect Plate, Arrow;
    private readonly float _inputX, _inputY, _outputX, _outputY, _outputCell;

    /// <summary>Builds a three-column ingredient plate with at least three rows.</summary>
    internal RecipeGridLayout(Rect area, int inputs, int outputs, float iconScale = 1f)
    {
        inputs = Math.Max(0, inputs);
        outputs = Math.Max(0, outputs);
        float room = Math.Max(1f, area.width - ActionColumn - 2 * Pad - ArrowWidth);
        _inputColumns = MaxInputColumns;
        // Give results up to three columns, without shrinking inputs to fit them all at once.
        float preferredCell = PreferredCell * iconScale;
        int available = Math.Max(1, (int)(room / preferredCell) - _inputColumns);
        _outputColumns = Math.Clamp(outputs, 1, Math.Min(3, available));
        Cell = Math.Max(1f, Math.Min(preferredCell, (float)Math.Floor(room / (_inputColumns + _outputColumns))));
        InputRows = Math.Max(3, RowCount(inputs, _inputColumns));
        OutputRows = Math.Max(1, RowCount(outputs, _outputColumns));
        ChoiceColumns = Math.Max(1, (int)((area.width - 2 * Pad) / Cell));

        _outputCell = outputs <= 1 ? Math.Max(Cell, Math.Min(SingleOutputSize * iconScale, room - _inputColumns * Cell)) : Cell;
        float width = _inputColumns * Cell + _outputColumns * _outputCell + ArrowWidth + 2 * Pad;
        float outputHeight = OutputRows * _outputCell;
        // Keep a second action below the first even at the smallest icon size.
        float firstOutputCenter = (_outputCell - outputHeight) * .5f;
        float actionBottom = firstOutputCenter + 1.5f * ActionSize + ActionGap;
        float outputWithClearance = outputHeight + Math.Max(0f, ActionSize - _outputCell);
        float innerHeight = Math.Max(36f, Math.Max(InputRows * Cell, Math.Max(outputWithClearance, 2f * actionBottom)));
        // The plate and its action column form one composition, centered as a unit.
        Plate = new Rect(area.x + (area.width - width - ActionColumn) * .5f, area.y, width, innerHeight + 2 * Pad);
        _inputX = Plate.x + Pad;
        _inputY = Plate.center.y - InputRows * Cell * .5f;
        _outputX = _inputX + _inputColumns * Cell + ArrowWidth;
        _outputY = Plate.center.y - outputHeight * .5f;
        Arrow = new Rect(_outputX - ArrowWidth, Plate.center.y - 18, ArrowWidth, 36);
    }

    internal static int RowCount(int count, int columns) => count <= 0 ? 0 : 1 + (count - 1) / columns;
    // Every slot uses the same phase; equal candidate sets always show the same item.
    internal static int CandidateIndex(int step, int count) => step % count;
    internal Rect Input(int i) => Slot(i, _inputColumns, _inputX, _inputY);
    internal Rect Output(int i) => new(_outputX + i % _outputColumns * _outputCell,
        _outputY + i / _outputColumns * _outputCell, _outputCell, _outputCell);
    // All outputs share the same action column; scrolling over the heart switches its target.
    internal Rect Favorite => new(Plate.xMax, Output(0).center.y - ActionSize * .5f, ActionSize, ActionSize);
    internal Rect Transfer => new(Plate.xMax, Favorite.yMax + ActionGap, ActionSize, ActionSize);
    internal Rect Choice(int i, float x, float y) => Slot(i, ChoiceColumns, x, y);
    private Rect Slot(int i, int columns, float x, float y) =>
        new(x + i % columns * Cell, y + i / columns * Cell, Cell, Cell);

    // Only enumerate slots in rows intersecting the viewport, including the two partial edges.
    internal (int Start, int End) VisibleInputs(Rect clip) => Visible(clip, _inputY, InputRows * _inputColumns, _inputColumns, Cell);
    internal (int Start, int End) VisibleOutputs(Rect clip, int count) => Visible(clip, _outputY, Math.Max(count, 1), _outputColumns, _outputCell);
    internal (int Start, int End) VisibleChoices(Rect clip, float y, int count) => Visible(clip, y, count, ChoiceColumns, Cell);
    private static (int Start, int End) Visible(Rect clip, float y, int count, int columns, float cell)
    {
        int rows = RowCount(count, columns);
        int first = Math.Clamp((int)Math.Floor((clip.yMin - y) / cell), 0, rows);
        int end = Math.Clamp((int)Math.Ceiling((clip.yMax - y) / cell), 0, rows);
        return (Math.Min(count, first * columns), Math.Min(count, end * columns));
    }
}
