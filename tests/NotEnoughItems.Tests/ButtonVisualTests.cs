using NotEnoughItems;
using UnityEngine;

internal static class ButtonVisualTests
{
    internal static void Input()
    {
        Check(Do(EventType.MouseDown) == ButtonInput.Capture && Do(EventType.MouseUp, owns: true) == ButtonInput.Click,
            "An ordinary button, including recipe Back, clicks only on its owned left release.");
        Check(Do(EventType.MouseUp) == ButtonInput.None && Do(EventType.MouseDown, button: 1) == ButtonInput.None &&
            Do(EventType.MouseDown, inside: false) == ButtonInput.None && Do(EventType.MouseDown, canCapture: false) == ButtonInput.None &&
            Do(EventType.MouseDown, enabled: false) == ButtonInput.None,
            "A disabled or obstructed button, right button, or bare release cannot activate an action.");
        Check(Do(EventType.MouseDrag, inside: false, owns: true) == ButtonInput.Consume &&
            Do(EventType.MouseUp, inside: false, owns: true) == ButtonInput.Release &&
            Do(EventType.MouseUp, enabled: false, owns: true) == ButtonInput.Release,
            "Dragging outside (or disabling before release) cancels the click and releases capture.");

        static ButtonInput Do(EventType type, int button = 0, bool enabled = true, bool inside = true, bool owns = false, bool canCapture = true) =>
            ButtonVisual.Input(type, button, enabled, inside, owns, canCapture);
    }

    internal static void ClickKeepsHoverArtwork()
    {
        var hit = new Rect(20, 32, 16, 16);
        var hover = ButtonVisual.Resolve(enabled: true, inside: true);
        bool captured = false;
        foreach (var (type, inside, action, phase) in new[]
        {
            (EventType.MouseDown, true, ButtonInput.Capture, ButtonPhase.Hover),
            (EventType.Repaint, true, ButtonInput.None, ButtonPhase.Hover),
            (EventType.MouseDrag, false, ButtonInput.Consume, ButtonPhase.Idle),
            (EventType.Repaint, false, ButtonInput.None, ButtonPhase.Idle),
            (EventType.MouseDrag, true, ButtonInput.Consume, ButtonPhase.Hover),
            (EventType.Repaint, true, ButtonInput.None, ButtonPhase.Hover),
            (EventType.MouseUp, true, ButtonInput.Click, ButtonPhase.Hover),
            (EventType.MouseUp, true, ButtonInput.None, ButtonPhase.Hover)
        })
        {
            var input = ButtonVisual.Input(type, 0, true, inside, captured, !captured);
            Check(input == action, "The click must still complete once on release inside the target.");
            if (input == ButtonInput.Capture) captured = true;
            else if (input is ButtonInput.Click or ButtonInput.Release) captured = false;

            var visual = ButtonVisual.Resolve(enabled: true, inside);
            Check(visual.Phase == phase, "Dragging out removes hover; dragging back restores it.");
            if (inside)
                Check(visual.Face(hit) == hover.Face(hit) && visual.SpriteTint.Equals(hover.SpriteTint) &&
                    visual.SymbolInk.Equals(hover.SymbolInk) && ModernityFrames.DropDownState(visual) == ("_hover", 1),
                    "Mouse input must not change the hovered button geometry, colors or dropdown frame.");
        }
        Check(!captured, "The completed click releases capture.");
    }

    internal static void Artwork()
    {
        var hit = new Rect(20, 32, 16, 16);
        var spriteTints = new HashSet<Color>();
        var symbolInks = new HashSet<Color32>();
        foreach (var (enabled, inside, phase, offset) in new[]
        {
            (true, false, ButtonPhase.Idle, 0f),
            (true, true, ButtonPhase.Hover, 1f),
            (false, true, ButtonPhase.Disabled, 1f)
        })
        {
            var state = ButtonVisual.Resolve(enabled, inside);
            var face = state.Face(hit);
            Check(state.Phase == phase && face == new Rect(hit.x, hit.y + offset, hit.width, hit.height - offset),
                "The plate and icon share the same visual state without covering the next row.");
            var sprite = ModernityFrames.FitButtonSprite(face, 8, 11, 12f, enlarge: false);
            var (bar, stem) = BuiltInControlArt.QuantitySymbol(face, plus: true, 10f);
            var (minus, noStem) = BuiltInControlArt.QuantitySymbol(face, plus: false, 10f);
            Check(sprite.center == face.center && bar.center == face.center && stem.center == face.center &&
                bar == minus && noStem == default && bar.width == stem.height && bar.height == stem.width,
                "Sprites, plus and minus stay centered on the moving face; plus/minus share the same bar.");
            Check(spriteTints.Add(state.SpriteTint) && symbolInks.Add(state.SymbolInk),
                "Idle, hovered and disabled states must all visibly affect the icon (not just its backplate).");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
