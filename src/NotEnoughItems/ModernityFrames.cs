using System;
using UnityEngine;

namespace NotEnoughItems;

// Source rectangles use physical pixels measured from the top left of the unmodified PNGs.
internal static class ModernityFrames
{
    internal readonly struct Frame(string file, int x, int y, int width, int height, string directory = NeiAssets.ModernityDirectory)
    {
        internal string File { get; } = file;
        internal string ResourcePath { get; } = directory + file;
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
        "recipe_background" => new Frame("recipebg.png", 0, 0, NeiAssets.RecipeBackgroundWidth, NeiAssets.RecipeBackgroundHeight),
        "recipe_slot" => new Frame("slot.png", 0, 0, 18, 18),
        "bookmark" => new Frame("nei_tabbed_sprites.png", 0, 0, 16, 16),
        "bookmark_selected" => new Frame("nei_tabbed_sprites.png", 16, 0, 16, 16),
        "options" or "wrench" => new Frame("nei_tabbed_sprites.png", 32, 0, 16, 16),
        "options_cheat" => new Frame("nei_tabbed_sprites.png", 64, 0, 16, 16),
        "search" => new Frame("nei_sprites.png", 0, 152, 20, 20),
        "favorite" => new Frame("nei_sprites.png", 20, 152, 18, 20, "gui/original/"),
        "favorite_selected" => new Frame("nei_sprites.png", 38, 152, 18, 20, "gui/original/"),
        // Only the first 36 rows have pixels. The light row at y=24 duplicates y=0.
        "search_field" => new Frame("text_field.png", 0, 12, 128, 12, "gui/controls/"),
        "search_field_focused" => new Frame("text_field.png", 0, 0, 128, 12, "gui/controls/"),
        "tooltip_background" => new Frame("text_field.png", 0, 0, 128, 12, "gui/controls/"),
        "dropdown_closed" => DropDown(0, 0, 20),
        "dropdown_open" => DropDown(112, 0, 20),
        "dropdown_closed_hover" => DropDown(0, 64, 19),
        "dropdown_open_hover" => DropDown(112, 64, 19),
        "dropdown_option" => DropDown(0, 128, 19),
        "dropdown_option_hover" => DropDown(0, 192, 18),
        "checkbox_off" => Checkbox(0, 28),
        "checkbox_off_hover" => Checkbox(22, 28),
        "checkbox_on" => Checkbox(0, 40),
        "checkbox_on_hover" => Checkbox(22, 40),
        "square_checkbox_off" => SquareCheckbox(29, 1),
        "square_checkbox_off_hover" => SquareCheckbox(43, 1),
        "square_checkbox_on" => SquareCheckbox(29, 15),
        "square_checkbox_on_hover" => SquareCheckbox(43, 15),
        _ => null
    };

    // Unity GetPixels enumerates each row left to right, starting at the bottom.
    internal static (int X, int Y, int Width, int Height) InkBounds(int width, int height, Color[] pixels)
    {
        int left = width, bottom = height, right = -1, top = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= 0) continue;
                left = Math.Min(left, x);
                bottom = Math.Min(bottom, y);
                right = Math.Max(right, x);
                top = Math.Max(top, y);
            }
        return right < left ? (0, 0, width, height) : (left, bottom, right - left + 1, top - bottom + 1);
    }

    private static Frame DropDown(int x, int y, int height) => new("drop_down_list.png", x, y, 112, height, NeiAssets.ControlsDirectory);

    // Each 22×12 switch is a complete track and knob, including its original transparent pixels.
    private static Frame Checkbox(int x, int y) => new(NeiAssets.CheckboxFile, x, y, 22, 12, NeiAssets.ControlsDirectory);

    // Four 12×12 faces in the 26×26 block at (29, 1), separated by two-pixel gutters.
    private static Frame SquareCheckbox(int x, int y) => new(NeiAssets.CheckboxFile, x, y, 12, 12, NeiAssets.ControlsDirectory);

    internal static string CheckboxFace(bool on, bool hovered, bool square = false) => (square, on, hovered) switch
    {
        (true, true, true) => "square_checkbox_on_hover",
        (true, true, false) => "square_checkbox_on",
        (true, false, true) => "square_checkbox_off_hover",
        (true, false, false) => "square_checkbox_off",
        (false, true, true) => "checkbox_on_hover",
        (false, true, false) => "checkbox_on",
        (false, false, true) => "checkbox_off_hover",
        _ => "checkbox_off"
    };

    // Upstream uses glyph captions on widget buttons when there is no dedicated sprite.
    internal static string Caption(string name) => name switch
    {
        "previous" => "<", "next" => ">", "add" => "+", "remove" => "−", "back" => "↶", "close" => "×", _ => string.Empty
    };
}
