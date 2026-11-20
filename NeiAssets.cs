using System;
using System.IO;

namespace NotEnoughItems;

// Client resources use NeoForge's namespace/texture paths, but Unity still decodes the PNGs.
internal static class NeiAssets
{
    internal const string Namespace = "notenoughitems";
    internal const string TextureRoot = "assets/" + Namespace + "/textures/";

    internal static string Icon(string name) => "gui/sprites/icons/" + name.ToLowerInvariant() + ".png";
    internal static string Control(string name) => "gui/sprites/controls/" + name + ".png";
    internal static string Background(string screen) => "gui/backgrounds/" + (screen.Length == 0 ? "default" : screen) + ".png";
    internal static string Atlas(string name) => "gui/atlas/" + name + ".png";

    internal static string RelativePath(string resourcePath) => TextureRoot + resourcePath;
    internal static string ResourceName(string resourcePath) => RelativePath(resourcePath);

    internal static string? LegacyPath(string path)
    {
        if (path.StartsWith("gui/sprites/icons/", StringComparison.Ordinal)) return "Textures/icons/" + Path.GetFileName(path);
        if (path.StartsWith("gui/sprites/controls/", StringComparison.Ordinal)) return "Textures/surfaces/" + Path.GetFileName(path);
        if (path.StartsWith("gui/backgrounds/", StringComparison.Ordinal))
            return path == Background(string.Empty) ? "Textures/background.png" : "Textures/background-" + Path.GetFileName(path);
        if (path == Atlas("ui")) return "Textures/ui-atlas.png";
        if (path == Atlas("icons")) return "Textures/icons-atlas.png";
        return null;
    }
}
