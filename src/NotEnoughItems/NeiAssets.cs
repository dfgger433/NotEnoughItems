using System;
using System.Collections.Generic;
using System.IO;

namespace NotEnoughItems;

// Client resources use NeoForge's namespace/texture paths, but Unity still decodes the PNGs.
internal static class NeiAssets
{
    internal const string Namespace = "notenoughitems";
    internal const string TextureRoot = "assets/" + Namespace + "/textures/";
    internal const string SpritesDirectory = "gui/sprites/";
    internal const string GuisDirectory = "guis/";
    internal const string StatesAtlas = GuisDirectory + "states.png";
    internal const string WidgetsAtlas = SpritesDirectory + "widgets.png";
    internal const string TextFieldAtlas = SpritesDirectory + "text_field.png";
    internal const string DropDownAtlas = SpritesDirectory + "drop_down_list.png";
    internal const string CheckboxAtlas = SpritesDirectory + "checkbox.png";
    internal const string RecipeTabSelected = "recipe_tab_selected";
    internal const string RecipeTabCandidate = "recipe_tab_candidate";
    internal const int RecipeTabX = 160, RecipeTabY = 112, RecipeTabStripWidth = 96;
    internal const int RecipeTabWidth = RecipeTabStripWidth / 2, RecipeTabHeight = 48;
    internal const float RecipeTabOffset = -1f;
    internal const string RecipeBackgroundTexture = GuisDirectory + "recipebg.png";
    internal const int RecipeBackgroundWidth = 184, RecipeBackgroundHeight = 174;
    internal const int RecipeBackgroundSlice = 9, RecipeBackgroundTransparentInset = 4;
    internal const string ActivePackDirectory = "resourcepacks/active";
    internal static string Background(string screen) => "gui/backgrounds/" + (screen.Length == 0 ? "default" : screen) + ".png";

    internal static string RelativePath(string resourcePath) => TextureRoot + resourcePath;
    internal static string ResourceName(string resourcePath) => RelativePath(resourcePath);

    // File candidates are relative to the DLL; embedded candidates are logical resource names.
    internal static IEnumerable<(string Path, bool Embedded)> TextureSources(string resourcePath, bool activePack)
    {
        string canonical = RelativePath(resourcePath);
        if (activePack)
        {
            yield return (ActivePackDirectory + "/" + canonical, false);
            // The relocated sheets retain the same canvas and frame coordinates.
            if (PreviousPackPath(resourcePath) is { } previous)
                yield return (ActivePackDirectory + "/" + RelativePath(previous), false);
        }

        yield return (canonical, false);
        if (LegacyPath(resourcePath) is { } legacy) yield return (legacy, false);
        yield return (ResourceName(resourcePath), true);
    }

    private static string? PreviousPackPath(string path) => path switch
    {
        WidgetsAtlas or RecipeBackgroundTexture => "gui/modernity/" + Path.GetFileName(path),
        TextFieldAtlas or DropDownAtlas or CheckboxAtlas => "gui/controls/" + Path.GetFileName(path),
        _ => null
    };

    // Reject incompatible canvases before caching a resource shared by multiple frames.
    internal static (int Width, int Height)? ExpectedSize(string path) => path switch
    {
        StatesAtlas or WidgetsAtlas or RecipeBackgroundTexture or DropDownAtlas => (256, 256),
        TextFieldAtlas => (128, 128),
        CheckboxAtlas => (64, 64),
        _ => null
    };

    internal static string? LegacyPath(string path)
    {
        if (path.StartsWith("gui/backgrounds/", StringComparison.Ordinal))
            return path == Background(string.Empty) ? "Textures/background.png" : "Textures/background-" + Path.GetFileName(path);
        return null;
    }
}
