using UnityEngine;

namespace NotEnoughItems;

internal static class TabFocusPolicy
{
    // IMGUI keyboard traversal can use a character event without a physical key code.
    internal static bool Consume(bool keyDown, KeyCode key, char character) =>
        keyDown && (key == KeyCode.Tab || character == '\t');
}
