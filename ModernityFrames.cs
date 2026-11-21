namespace NotEnoughItems;

// Source rectangles use physical pixels measured from the top left of the unmodified PNGs.
internal static class ModernityFrames
{
    internal readonly struct Frame(string file, int x, int y, int width, int height)
    {
        internal string File { get; } = file;
        internal int X { get; } = x;
        internal int Y { get; } = y;
        internal int Width { get; } = width;
        internal int Height { get; } = height;
    }

    // GuiNEIButton uses these three rows with 2 px edges; the hover row is also the active state.
    internal const int ButtonSlice = 2;

    internal static Frame? Find(string name) => name switch
    {
        "button_disabled" => new Frame("widgets.png", 0, 46, 200, 20),
        "button" => new Frame("widgets.png", 0, 66, 200, 20),
        "button_hover" => new Frame("widgets.png", 0, 86, 200, 20),
        "bookmark" => new Frame("nei_tabbed_sprites.png", 0, 0, 16, 16),
        "bookmark_selected" => new Frame("nei_tabbed_sprites.png", 16, 0, 16, 16),
        "options" or "wrench" => new Frame("nei_tabbed_sprites.png", 32, 0, 16, 16),
        "options_utility" => new Frame("nei_tabbed_sprites.png", 48, 0, 16, 16),
        "options_cheat" => new Frame("nei_tabbed_sprites.png", 64, 0, 16, 16),
        "search" => new Frame("nei_sprites.png", 0, 152, 20, 20),
        _ => null
    };

    // Upstream uses glyph captions on widget buttons when there is no dedicated sprite.
    internal static string Caption(string name) => name switch
    {
        "previous" => "<", "next" => ">", "add" => "+", "remove" => "−", "close" => "×", _ => string.Empty
    };
}
