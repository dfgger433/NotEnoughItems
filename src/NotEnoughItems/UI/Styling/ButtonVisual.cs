using UnityEngine;

namespace NotEnoughItems;

// Resolve the artwork and its position independently of mouse capture.
internal enum ButtonPhase { Idle, Hover, Disabled }
internal enum ButtonInput { None, Capture, Consume, Release, Click }

internal readonly struct ButtonVisual(ButtonPhase phase)
{
    internal ButtonPhase Phase { get; } = phase;
    internal bool Hovered => Phase == ButtonPhase.Hover;
    internal bool Enabled => Phase != ButtonPhase.Disabled;

    internal static ButtonVisual Resolve(bool enabled, bool inside) => new(
        !enabled ? ButtonPhase.Disabled : inside ? ButtonPhase.Hover : ButtonPhase.Idle);

    internal static ButtonInput Input(EventType type, int button, bool enabled, bool inside, bool ownsPress, bool canCapture) => type switch
    {
        EventType.MouseDown when button == 0 && enabled && inside && canCapture => ButtonInput.Capture,
        EventType.MouseDrag when ownsPress => ButtonInput.Consume,
        EventType.MouseUp when button == 0 && ownsPress => enabled && inside ? ButtonInput.Click : ButtonInput.Release,
        _ => ButtonInput.None
    };

    internal Rect Face(Rect hit)
    {
        float offset = Phase == ButtonPhase.Idle ? 0f : 1f;
        // Sink into the hit target so the face cannot cover a neighboring row or the panel seam.
        return Rect.MinMaxRect(hit.xMin, hit.yMin + offset, hit.xMax, hit.yMax);
    }

    internal Color SpriteTint => Phase switch
    {
        ButtonPhase.Disabled => new Color(1f, 1f, 1f, .42f),
        ButtonPhase.Hover => new Color(.9f, .9f, .9f),
        _ => Color.white
    };

    internal Color32 SymbolInk => Phase switch
    {
        ButtonPhase.Disabled => new Color32(195, 198, 216, 255),
        ButtonPhase.Hover => new Color32(37, 68, 110, 255),
        _ => new Color32(35, 37, 56, 255)
    };
}

internal static class BuiltInControlArt
{
    internal static bool IsQuantitySymbol(string name) => name is "add" or "remove";

    // Both signs share the same bar. Plus has one additional perpendicular stroke.
    internal static (Rect Bar, Rect Stem) QuantitySymbol(Rect face, bool plus, float maxSize)
    {
        float size = Mathf.Min(Mathf.Min(10f, maxSize), Mathf.Min(face.width, face.height) - 2f);
        if (size <= 0) return default;
        float stroke = Mathf.Min(2f, size);
        Vector2 center = face.center;
        return (new Rect(center.x - size / 2, center.y - stroke / 2, size, stroke),
            plus ? new Rect(center.x - stroke / 2, center.y - size / 2, stroke, size) : default);
    }


}
