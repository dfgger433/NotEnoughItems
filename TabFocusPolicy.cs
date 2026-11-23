namespace NotEnoughItems;

internal static class TabFocusPolicy
{
    internal static bool Consume(bool keyDown, bool isTab) => keyDown && isTab;
}
