using System;
using UnityEngine;

namespace NotEnoughItems;

// Source rectangles use physical pixels measured from the top left of each PNG.
internal static class ModernityFrames
{
    internal readonly struct Frame(string resourcePath, int x, int y, int width, int height)
    {
        internal string ResourcePath { get; } = resourcePath;
        internal int X { get; } = x;
        internal int Y { get; } = y;
        internal int Width { get; } = width;
        internal int Height { get; } = height;
    }

    // The original icon-button faces keep two pixels on each edge when stretched.
    internal const int ButtonSlice = 2;
    internal const int TextButtonBottom = 4;

    internal static bool HasWidgetBackplate(string name) => name is "previous" or "next" or "back" or "close";

    // Bookmark color reflects visibility; wrench color reflects mode, not pointer state.
    internal static string IconGraphic(string name, bool selected) => name switch
    {
        "bookmark" when selected => "bookmark_selected",
        "favorite" when selected => "favorite_selected",
        _ => name
    };

    internal static Frame? Find(string name) => name switch
    {
        "button_disabled" => new Frame(NeiAssets.WidgetsAtlas, 0, 46, 200, 20),
        "button" => new Frame(NeiAssets.WidgetsAtlas, 0, 66, 200, 20),
        "button_hover" => new Frame(NeiAssets.WidgetsAtlas, 0, 86, 200, 20),
        "previous" => State(48, 64, 16, 16),
        "next" or "recipe_arrow" => State(32, 64, 16, 16),
        "back" => State(0, 80, 16, 16),
        "recipe_background" => new Frame(NeiAssets.RecipeBackgroundTexture, 0, 0, NeiAssets.RecipeBackgroundWidth, NeiAssets.RecipeBackgroundHeight),
        "recipe_slot" => State(161, 209, 18, 18),
        "close" => State(16, 80, 16, 16),
        "bookmark" => State(0, 0, 32, 32),
        "bookmark_selected" => State(32, 0, 32, 32),
        "options" or "wrench" => State(64, 0, 32, 32),
        "options_cheat" => State(128, 0, 32, 32),
        "favorite" => State(32, 32, 16, 16),
        "favorite_selected" => State(48, 32, 16, 16),
        "copy" => State(0, 64, 16, 16),
        "paste" => State(16, 64, 16, 16),
        "recipe_tab_selected" => State(NeiAssets.RecipeTabX, NeiAssets.RecipeTabY, NeiAssets.RecipeTabWidth, NeiAssets.RecipeTabHeight),
        "recipe_tab_candidate" => State(NeiAssets.RecipeTabX + NeiAssets.RecipeTabWidth, NeiAssets.RecipeTabY, NeiAssets.RecipeTabWidth, NeiAssets.RecipeTabHeight),
        "checkbox_off" => Checkbox(0, 28),
        "checkbox_off_hover" => Checkbox(22, 28),
        "checkbox_on" => Checkbox(0, 40),
        "checkbox_on_hover" => Checkbox(22, 40),
        "scroll_horizontal_track" => State(161, 161, 22, 16),
        "scroll_vertical_track" => State(192, 161, 16, 22),
        "scroll_recipe_track" => State(209, 161, 26, 42),
        "scroll_horizontal_thumb" => State(161, 193, 18, 12),
        "scroll_vertical_thumb" => State(192, 192, 12, 18),
        // Only the first 36 rows have pixels. The light row at y=24 duplicates y=0.
        "search_field" => new Frame(NeiAssets.TextFieldAtlas, 0, 12, 128, 12),
        "search_field_focused" or "tooltip_background" => new Frame(NeiAssets.TextFieldAtlas, 0, 0, 128, 12),
        "dropdown_closed" => DropDown(0, 0, 20),
        "dropdown_open" => DropDown(112, 0, 20),
        "dropdown_closed_disabled" => DropDown(0, 32, 18),
        "dropdown_open_disabled" => DropDown(112, 32, 18),
        "dropdown_closed_hover" => DropDown(0, 64, 19),
        "dropdown_open_hover" => DropDown(112, 64, 19),
        "dropdown_option" => DropDown(0, 128, 19),
        "dropdown_option_disabled" => DropDown(0, 160, 17),
        "dropdown_option_hover" => DropDown(0, 192, 18),
        "square_checkbox_off" => SquareCheckbox(29, 1),
        "square_checkbox_off_hover" => SquareCheckbox(43, 1),
        "square_checkbox_on" => SquareCheckbox(29, 15),
        "square_checkbox_on_hover" => SquareCheckbox(43, 15),
        _ => null
    };

    private static Frame State(int x, int y, int width, int height) =>
        new(NeiAssets.StatesAtlas, x, y, width, height);

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

    private static Frame DropDown(int x, int y, int height) => new(NeiAssets.DropDownAtlas, x, y, 112, height);

    internal const int DropDownArrowRight = 21, DropDownArrowTop = 6, DropDownArrowBottom = 11;

    // Option rows omit the trigger's top outline. Their bottom highlight begins
    // one row sooner, and the thickness of the shadow below it varies by state.
    internal static (int Left, int Right, int Top, int Bottom) DropDownBorders(bool arrow, int height) =>
        (2, arrow ? DropDownArrowRight : 2, arrow ? 2 : 1, height - (arrow ? 15 : 14));

    // Shorter faces sink without moving the hit target. A selected option keeps its highlight at rest.
    internal static (string Suffix, int Offset) DropDownState(ButtonVisual visual, bool selected = false) =>
        !visual.Enabled ? ("_disabled", 2) : visual.Hovered ? ("_hover", 1) :
        (selected ? "_hover" : string.Empty, 0);

    // The full-size 22×12 switch cells retain their source transparent margin.
    private static Frame Checkbox(int x, int y) => new(NeiAssets.CheckboxAtlas, x, y, 22, 12);

    internal static string CheckboxFace(bool on, bool hovered) => (on, hovered) switch
    {
        (true, true) => "checkbox_on_hover",
        (true, false) => "checkbox_on",
        (false, true) => "checkbox_off_hover",
        _ => "checkbox_off"
    };

    // Four 12x12 radio faces occupy a 26x26 block with two-pixel gutters.
    internal const int RadioFaceSize = 12;

    private static Frame SquareCheckbox(int x, int y) =>
        new(NeiAssets.CheckboxAtlas, x, y, RadioFaceSize, RadioFaceSize);

    internal static string SquareCheckboxFace(bool selected, bool hovered) => (selected, hovered) switch
    {
        (true, true) => "square_checkbox_on_hover",
        (true, false) => "square_checkbox_on",
        (false, true) => "square_checkbox_off_hover",
        _ => "square_checkbox_off"
    };

    // Incomplete replacement atlases may leave the original Back cell blank or filled with the editor grid.
    internal static bool HasBackArtwork(Color[] pixels)
    {
        foreach (Color color in pixels)
        {
            // Permit those colors as accents in a custom skin; reject a cell filled only by the grid.
            if (color.a <= 0f) continue;
            Color32 pixel = color;
            if (pixel.a != 255 || pixel.r != 255 || pixel.b != 170 || pixel.g is not (0 or 190))
                return true;
        }
        return false;
    }

    // Fit the whole sprite with one scale, even in the rail's narrow arrow caps.
    internal static Rect FitButtonSprite(Rect rect, int width, int height, float maxSize, bool enlarge)
    {
        float scale = Math.Min(Math.Min(maxSize, rect.width - 2f) / width,
            Math.Min(maxSize, rect.height - 2f) / height);
        if (!enlarge) scale = Math.Min(1f, scale);
        if (scale <= 0f) return default;
        float w = width * scale, h = height * scale;
        return new Rect(rect.center.x - w * .5f, rect.center.y - h * .5f, w, h);
    }

    // When a destination is smaller than its fixed borders, uniformly scale the entire
    // frame down instead of letting the renderer squeeze the corners independently.
    internal static bool FitsButtonBorders(Rect rect, int left, int right, int top, int bottom) =>
        rect.width > left + right && rect.height > top + bottom;
}
