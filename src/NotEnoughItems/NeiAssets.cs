using System;
using System.Collections.Generic;
using System.IO;

namespace NotEnoughItems;

// Client resources use NeoForge's namespace/texture paths, but Unity still decodes the PNGs.
internal static class NeiAssets
{
    internal const string Namespace = "notenoughitems";
    internal const string TextureRoot = "assets/" + Namespace + "/textures/";

    internal const string ModernityDirectory = "gui/modernity/";
    internal const string BookAtlasFile = "book.png";
    internal const string BookAtlas = ModernityDirectory + BookAtlasFile;
    internal const string MinecraftBookAtlas = "assets/minecraft/textures/gui/" + BookAtlasFile;
    internal const string IconsDirectory = "gui/icons/";
    internal const string RecipeBackgroundTexture = ModernityDirectory + "recipebg.png";
    internal const int RecipeBackgroundWidth = 184;
    internal const int RecipeBackgroundHeight = 174;
    internal const int RecipeBackgroundSlice = 9;
    internal const int RecipeBackgroundTransparentInset = 4;
    internal const string OriginalDirectory = "gui/original/";
    internal const string ControlsDirectory = "gui/controls/";
    internal const string DropDownAtlas = ControlsDirectory + "drop_down_list.png";
    internal const string CheckboxFile = "checkbox.png";
    internal const string CheckboxAtlas = ControlsDirectory + CheckboxFile;
    internal const string RecipeTabSelected = ControlsDirectory + "vertical_buttons_bg.png";
    internal const string RecipeTabCandidate = ControlsDirectory + "vertical_buttons_bg_candidate.png";
    internal const int RecipeTabWidth = 48, RecipeTabSelectedHeight = 46, RecipeTabCandidateHeight = 43;
    internal const string ActivePackDirectory = "resourcepacks/active";
    internal static string Modernity(string file) => ModernityDirectory + file;
    internal static string Icon(string file) => IconsDirectory + file + ".png";
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
            // Also accept the original Minecraft path when using an intact Modernity pack.
            if (resourcePath == BookAtlas) yield return (ActivePackDirectory + "/" + MinecraftBookAtlas, false);
        }

        if (OriginalFallback(resourcePath) is { } original &&
            (activePack || resourcePath.StartsWith(OriginalDirectory, StringComparison.Ordinal)))
            yield return (ResourceName(original), true);

        if (resourcePath.StartsWith(OriginalDirectory, StringComparison.Ordinal)) yield break;
        yield return (canonical, false);
        if (LegacyPath(resourcePath) is { } legacy) yield return (legacy, false);
        yield return (ResourceName(resourcePath), true);
    }

    internal static string? OriginalFallback(string path) => path switch
    {
        ModernityDirectory + "nei_sprites.png" or OriginalDirectory + "nei_sprites.png" => OriginalDirectory + "nei_sprites.png",
        ModernityDirectory + "nei_tabbed_sprites.png" or OriginalDirectory + "nei_tabbed_sprites.png" => OriginalDirectory + "nei_tabbed_sprites.png",
        ModernityDirectory + "slot.png" or OriginalDirectory + "slot.png" => OriginalDirectory + "slot.png",
        _ => null
    };

    // The frame table measures pixels from the top-left, not normalized UVs.
    // Reject incompatible atlases before caching a resource shared by multiple frames.
    internal static (int Width, int Height)? ExpectedSize(string path) => path switch
    {
        ModernityDirectory + "nei_sprites.png" or OriginalDirectory + "nei_sprites.png" => (512, 512),
        ModernityDirectory + "nei_tabbed_sprites.png" or OriginalDirectory + "nei_tabbed_sprites.png" or
            ModernityDirectory + "widgets.png" or BookAtlas => (256, 256),
        ModernityDirectory + "slot.png" or OriginalDirectory + "slot.png" => (18, 18),
        ControlsDirectory + "text_field.png" => (128, 128),
        CheckboxAtlas => (64, 64),
        RecipeBackgroundTexture or DropDownAtlas => (256, 256),
        IconsDirectory + "back.png" or IconsDirectory + "close.png" => (16, 16),
        IconsDirectory + "recipe_arrow.png" => (33, 36),
        RecipeTabSelected => (RecipeTabWidth, RecipeTabSelectedHeight),
        RecipeTabCandidate => (RecipeTabWidth, RecipeTabCandidateHeight),
        _ => null
    };

    internal static string? LegacyPath(string path)
    {
        if (path.StartsWith("gui/backgrounds/", StringComparison.Ordinal))
            return path == Background(string.Empty) ? "Textures/background.png" : "Textures/background-" + Path.GetFileName(path);
        return null;
    }
}
