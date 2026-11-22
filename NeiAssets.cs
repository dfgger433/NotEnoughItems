using System;
using System.IO;

namespace NotEnoughItems;

// Client resources use NeoForge's namespace/texture paths, but Unity still decodes the PNGs.
internal static class NeiAssets
{
    internal const string Namespace = "notenoughitems";
    internal const string TextureRoot = "assets/" + Namespace + "/textures/";

    internal const string ModernityDirectory = "gui/modernity/";
    internal static string Modernity(string file) => ModernityDirectory + file;
    internal static string Background(string screen) => "gui/backgrounds/" + (screen.Length == 0 ? "default" : screen) + ".png";

    internal static string RelativePath(string resourcePath) => TextureRoot + resourcePath;
    internal static string ResourceName(string resourcePath) => RelativePath(resourcePath);

    internal static string? LegacyPath(string path)
    {
        if (path.StartsWith("gui/backgrounds/", StringComparison.Ordinal))
            return path == Background(string.Empty) ? "Textures/background.png" : "Textures/background-" + Path.GetFileName(path);
        return null;
    }
}
