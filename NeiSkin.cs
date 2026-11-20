using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>IMGUI controls backed by independently replaceable, original NEI textures.</summary>
internal static class NeiSkin
{
    private static readonly Color32 OverlayInk = Rgb(0xF4F2FB);
    private static readonly Color32 OverlayMuted = Rgb(0xC3C6D8);
    private static readonly Color32 Shadow = Rgb(0x191827);
    private static readonly Color32 TooltipInk = Rgb(0xF4F2FB);
    private static readonly Color32 TooltipAccent = Rgb(0xBDB4E1);
    private static SkinStyles? _styles;
    private static readonly Dictionary<string, Texture2D?> Sources = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Frames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Backgrounds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Paints only an interactive slot's transparent hover/selection border.</summary>
    internal static void Slot(Rect rect, bool hovered = false, bool selected = false)
    {
        if (!CanPaint(rect) || (!hovered && !selected))
            return;

        SkinStyles styles = Styles;
        Paint(selected ? styles.SlotSelected : styles.SlotHovered, rect);
    }

    /// <summary>Draws a raised action or option and returns Unity's native button click.</summary>
    internal static bool Button(Rect rect, string text, bool enabled = true, bool selected = false)
    {
        SkinStyles styles = Styles;
        return Click(rect, text, enabled, selected ? styles.ButtonSelected : styles.Button, styles.ButtonDisabled);
    }

    /// <summary>Draws a clickable atlas icon button; the supplied tooltip is attached to Unity's GUI content.</summary>
    internal static bool IconButton(Rect rect, string icon, string tooltip, bool enabled = true, bool selected = false)
    {
        SkinStyles styles = Styles;
        GUIStyle style = enabled ? (selected ? styles.ButtonSelected : styles.Button) : styles.ButtonDisabled;
        string graphic = icon == "bookmark" && selected ? "bookmark_selected" : icon;
        if (icon == "options") graphic = NeiActions.Mode switch
        {
            NeiActions.LocalMode.Utility => "options_utility",
            NeiActions.LocalMode.Cheat => "options_cheat",
            _ => icon
        };
        bool clicked = ClickContent(rect, new GUIContent(ModernityFrames.Caption(icon), tooltip), enabled, style, styles.ButtonDisabled);
        if (CanPaint(rect))
        {
            float inset = Mathf.Max(2f, Mathf.Min(rect.width, rect.height) * 0.18f);
            Icon(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2), graphic);
        }
        return clicked;
    }

    /// <summary>Draws one original icon sprite from the independent icons atlas.</summary>
    internal static void Icon(Rect rect, string icon)
    {
        if (!CanPaint(rect) || string.IsNullOrWhiteSpace(icon))
            return;
        Texture2D? texture = GetIcon(icon);
        if (texture == null || !texture)
            return;

        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }

    /// <summary>Draws the replaceable, alpha-zero default background or a named screen background variant.</summary>
    /// <remarks>Screen names are catalog, bookmarks, recipe, or options. A missing screen variant falls back to background.png.</remarks>
    internal static void Background(Rect rect, string screen)
    {
        if (!CanPaint(rect))
            return;

        string name = NormalizeScreen(screen);
        Texture2D? background = GetBackground(name);
        if ((background == null || !background) && name.Length > 0)
            background = GetBackground(string.Empty);
        if (background == null || !background)
            return;

        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(rect, background, ScaleMode.StretchToFill, true);
        GUI.color = previous;
    }

    /// <summary>Draws a recipe/option tab. The caller owns which tab is selected.</summary>
    internal static bool Tab(Rect rect, string text, bool selected, bool enabled = true)
    {
        SkinStyles styles = Styles;
        return Click(rect, text, enabled, selected ? styles.TabSelected : styles.Tab, styles.ButtonDisabled);
    }

    /// <summary>Draws a native single-line editor. An optional control name allows IMGUI focus management.</summary>
    internal static string TextField(Rect rect, string text, string? controlName = null, int maxLength = 128)
    {
        GUIStyle style = Styles.TextField;
        if (!string.IsNullOrEmpty(controlName))
            GUI.SetNextControlName(controlName);

        return GUI.TextField(rect, text, maxLength, style);
    }

    /// <summary>Draws a clipped label in the panel's normal or secondary text color.</summary>
    internal static void Label(Rect rect, string text, TextAnchor alignment = TextAnchor.MiddleLeft, bool muted = false)
    {
        if (!CanPaint(rect) || string.IsNullOrEmpty(text))
            return;

        SkinStyles styles = Styles;
        styles.LabelContent.text = text;
        Paint(styles.LabelShadow(alignment), Offset(rect), styles.LabelContent);
        Paint(styles.Label(alignment, muted), rect, styles.LabelContent);
    }

    /// <summary>Draws wrapping, legible description text without a panel fill.</summary>
    internal static void Text(Rect rect, string text)
    {
        if (!CanPaint(rect) || string.IsNullOrWhiteSpace(text)) return;
        var styles = Styles;
        styles.LabelContent.text = text;
        Paint(styles.WrappedShadow, Offset(rect), styles.LabelContent);
        Paint(styles.Wrapped, rect, styles.LabelContent);
    }

    private static Rect Offset(Rect rect)
    {
        rect.position += Vector2.one;
        return rect;
    }

    /// <summary>Draws a non-interactive tooltip; call last, outside any clipping groups.</summary>
    internal static void Tooltip(Vector2 mousePosition, string text, string? subtitle = null)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text))
            return;

        const float margin = 4f;
        const float pad = 7f;
        const float gap = 3f;
        Vector2 viewMin = GUIUtility.ScreenToGUIPoint(Vector2.zero);
        Vector2 viewMax = GUIUtility.ScreenToGUIPoint(new Vector2(Screen.width, Screen.height));
        float availableWidth = viewMax.x - viewMin.x - 2 * margin;
        float availableHeight = viewMax.y - viewMin.y - 2 * margin;
        if (availableWidth <= 2 * pad || availableHeight <= 2 * pad)
            return;

        SkinStyles styles = Styles;
        GUIContent title = styles.TooltipTitleContent;
        title.text = text;
        GUIContent detail = styles.TooltipDetailContent;
        detail.text = subtitle ?? string.Empty;
        bool hasDetail = detail.text.Length > 0;

        float wanted = styles.TooltipTitle.CalcSize(title).x;
        if (hasDetail)
            wanted = Mathf.Max(wanted, styles.TooltipDetail.CalcSize(detail).x);

        float width = Mathf.Min(Mathf.Clamp(Mathf.Ceil(wanted) + 2 * pad, 58f, 380f), availableWidth);
        float textWidth = width - 2 * pad;
        float titleHeight = Mathf.Ceil(styles.TooltipTitle.CalcHeight(title, textWidth));
        float detailHeight = hasDetail ? gap + Mathf.Ceil(styles.TooltipDetail.CalcHeight(detail, textWidth)) : 0f;
        float height = Mathf.Min(titleHeight + detailHeight + 2 * pad, availableHeight);

        float x = mousePosition.x + 12f;
        if (x + width > viewMax.x - margin)
            x = mousePosition.x - width - 8f;
        x = Mathf.Clamp(x, viewMin.x + margin, viewMax.x - width - margin);

        float y = mousePosition.y + 15f;
        if (y + height > viewMax.y - margin)
            y = mousePosition.y - height - 8f;
        y = Mathf.Clamp(y, viewMin.y + margin, viewMax.y - height - margin);

        float textBottom = y + height - pad;
        float titleY = y + pad;
        var titleRect = new Rect(x + pad, titleY, textWidth, Mathf.Min(titleHeight, textBottom - titleY));
        Paint(styles.WrappedShadow, Offset(titleRect), title);
        Paint(styles.TooltipTitle, titleRect, title);
        float detailY = titleY + titleHeight + gap;
        if (hasDetail && detailY < textBottom)
        {
            var detailRect = new Rect(x + pad, detailY, textWidth, textBottom - detailY);
            Paint(styles.TooltipDetailShadow, Offset(detailRect), detail);
            Paint(styles.TooltipDetail, detailRect, detail);
        }
    }

    /// <summary>Releases loaded Unity textures on the main thread, for example during plug-in teardown.</summary>
    internal static void Release()
    {
        _styles = null;
        foreach (var cache in new[] { Frames, Sources, Backgrounds })
        {
            foreach (Texture2D? texture in cache.Values)
                if (texture != null && texture) UnityEngine.Object.Destroy(texture);
            cache.Clear();
        }
    }

    private static SkinStyles Styles => _styles ??= new SkinStyles();
    private static bool CanPaint(Rect rect) => Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f;

    private static Texture2D? GetIcon(string icon)
    {
        return CropFrame(icon);
    }

    // Paint decorations without allocating control IDs; buttons/editors still run on every IMGUI event.
    private static void Paint(GUIStyle style, Rect rect, GUIContent? content = null) =>
        style.Draw(rect, content ?? GUIContent.none, false, false, false, false);

    private static bool Click(Rect rect, string text, bool enabled, GUIStyle style, GUIStyle disabledStyle) =>
        ClickContent(rect, new GUIContent(text), enabled, style, disabledStyle);

    private static bool ClickContent(Rect rect, GUIContent content, bool enabled, GUIStyle style, GUIStyle disabledStyle)
    {
        bool previous = GUI.enabled;
        try
        {
            GUI.enabled = previous && enabled;
            return GUI.Button(rect, content, GUI.enabled ? style : disabledStyle);
        }
        finally
        {
            GUI.enabled = previous;
        }
    }

    private static Texture2D? CropFrame(string name)
    {
        if (Frames.TryGetValue(name, out var cached)) return cached;
        if (ModernityFrames.Find(name) is not { } rect) return null;
        if (!Sources.TryGetValue(rect.File, out var source))
        {
            source = LoadTexture(NeiAssets.Modernity(rect.File), readable: true);
            Sources.Add(rect.File, source);
        }
        Texture2D? cropped = null;
        if (source != null && source.width >= rect.X + rect.Width && source.height >= rect.Y + rect.Height)
        {
            int bottom = source.height - rect.Y - rect.Height;
            cropped = NewTexture(rect.Width, rect.Height, "Modernity NEI: " + name);
            cropped.SetPixels(source.GetPixels(rect.X, bottom, rect.Width, rect.Height));
            cropped.Apply(false, true);
        }
        Frames.Add(name, cropped);
        return cropped;
    }

    private static Texture2D? GetBackground(string screen)
    {
        if (Backgrounds.TryGetValue(screen, out Texture2D? cached))
            return cached;
        string file = NeiAssets.Background(screen);
        Texture2D? texture = LoadTexture(file);
        Backgrounds.Add(screen, texture);
        return texture;
    }

    private static string NormalizeScreen(string screen) => screen?.Trim().ToLowerInvariant() switch
    {
        "catalog" or "bookmarks" or "recipe" or "options" => screen.Trim().ToLowerInvariant(),
        _ => string.Empty
    };

    private static Texture2D? LoadTexture(string fileName, bool readable = false) =>
        TryLoadSidecar(fileName, readable) ?? TryLoadEmbedded(fileName, readable);

    private static Texture2D? TryLoadSidecar(string fileName, bool readable = false)
    {
        string? assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(assemblyPath)) return null;
        string directory = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
        var canonical = TryLoadFile(Path.Combine(directory, NeiAssets.RelativePath(fileName)), readable);
        if (canonical != null) return canonical;
        string? legacy = NeiAssets.LegacyPath(fileName);
        return legacy == null ? null : TryLoadFile(Path.Combine(directory, legacy), readable);
    }

    private static Texture2D? TryLoadFile(string path, bool readable)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return DecodeTexture(File.ReadAllBytes(path), "NEI " + Path.GetFileName(path) + " (replaceable)", readable);
        }
        catch (Exception)
        {
            // A damaged or incompatible replacement must not prevent the embedded default from loading.
            return null;
        }
    }

    private static Texture2D? TryLoadEmbedded(string fileName, bool readable = false)
    {
        string resource = NeiAssets.ResourceName(fileName);
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null) return null;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return DecodeTexture(bytes.ToArray(), "NEI " + fileName + " (embedded)", readable);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Texture2D NewTexture(int width, int height, string name) => new(width, height, TextureFormat.RGBA32, false)
    {
        name = name,
        hideFlags = HideFlags.HideAndDontSave,
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp
    };

    private static Texture2D? DecodeTexture(byte[] bytes, string textureName, bool readable)
    {
        var texture = NewTexture(2, 2, textureName);
        if (UnityEngine.ImageConversion.LoadImage(texture, bytes, !readable))
            return texture;
        UnityEngine.Object.Destroy(texture);
        return null;
    }

    private static Color32 Rgb(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    private sealed class SkinStyles
    {
        private const int Slice = 2;
        private readonly GUIStyle?[,] _labels = new GUIStyle[9, 2];
        private readonly GUIStyle?[] _labelShadows = new GUIStyle[9];
        internal readonly GUIStyle Wrapped;
        internal readonly GUIStyle WrappedShadow;
        internal readonly GUIStyle SlotHovered;
        internal readonly GUIStyle SlotSelected;
        internal readonly GUIStyle Button;
        internal readonly GUIStyle ButtonSelected;
        internal readonly GUIStyle ButtonDisabled;
        internal readonly GUIStyle Tab;
        internal readonly GUIStyle TabSelected;
        internal readonly GUIStyle TextField;
        internal readonly GUIStyle TooltipTitle;
        internal readonly GUIStyle TooltipDetail;
        internal readonly GUIStyle TooltipDetailShadow;
        internal readonly GUIContent LabelContent = new();
        internal readonly GUIContent TooltipTitleContent = new();
        internal readonly GUIContent TooltipDetailContent = new();

        internal SkinStyles()
        {
            // Inline text uses a one-pixel shadow over the transparent game world.
            Wrapped = TextStyle(TextAnchor.UpperLeft, OverlayInk, 13, wrap: true);
            WrappedShadow = TextStyle(TextAnchor.UpperLeft, Shadow, 13, wrap: true);
            SlotHovered = FrameStyle(Atlas("slot_hover"));
            SlotSelected = FrameStyle(Atlas("slot_selected"));

            var normal = Atlas("button");
            var hover = Atlas("button_hover");
            var pressed = Atlas("button_pressed");
            var disabled = Atlas("button_disabled");
            var tab = Atlas("tab");
            var activeTab = Atlas("tab_selected");
            var darkInk = Rgb(0x232538);
            Button = ControlStyle(normal, hover, pressed, hover, TextAnchor.MiddleCenter, darkInk, 4);
            ButtonSelected = ControlStyle(activeTab, hover, pressed, hover, TextAnchor.MiddleCenter, darkInk, 4);
            ButtonDisabled = ControlStyle(disabled, disabled, disabled, disabled, TextAnchor.MiddleCenter, OverlayMuted, 4);
            Tab = ControlStyle(tab, hover, pressed, hover, TextAnchor.MiddleCenter, darkInk, 4);
            TabSelected = ControlStyle(activeTab, activeTab, pressed, activeTab, TextAnchor.MiddleCenter, darkInk, 4);

            TextField = ControlStyle(Atlas("field"), Atlas("field_hover"), Atlas("field_focus"), Atlas("field_focus"),
                TextAnchor.MiddleLeft, TooltipInk, 6);
            TooltipTitle = TextStyle(TextAnchor.UpperLeft, TooltipInk, 13, wrap: true);
            TooltipDetail = TextStyle(TextAnchor.UpperLeft, TooltipAccent, 12, wrap: true);
            TooltipDetailShadow = TextStyle(TextAnchor.UpperLeft, Shadow, 12, wrap: true);
        }

        internal GUIStyle Label(TextAnchor alignment, bool muted)
        {
            int row = muted ? 1 : 0;
            return _labels[(int)alignment, row] ??= TextStyle(alignment, muted ? OverlayMuted : OverlayInk, 13);
        }

        internal GUIStyle LabelShadow(TextAnchor alignment) =>
            _labelShadows[(int)alignment] ??= TextStyle(alignment, Shadow, 13);

        private static Texture2D? Atlas(string frame) => CropFrame(frame);

        private static GUIStyle FrameStyle(Texture2D? background)
        {
            var style = new GUIStyle { border = new RectOffset(Slice, Slice, Slice, Slice) };
            style.normal.background = background;
            return style;
        }

        private static GUIStyle TextStyle(TextAnchor alignment, Color color, int size, bool wrap = false)
        {
            var style = new GUIStyle();
            SetText(style, alignment, size, wrap);
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle ControlStyle(Texture2D? normal, Texture2D? hover, Texture2D? active, Texture2D? focused,
            TextAnchor alignment, Color color, int horizontalPadding)
        {
            GUIStyle style = FrameStyle(normal);
            SetText(style, alignment, 13, false);
            style.padding = new RectOffset(horizontalPadding, horizontalPadding, 2, 2);
            SetState(style.normal, normal, color);
            SetState(style.hover, hover, color);
            SetState(style.active, active, color);
            SetState(style.focused, focused, color);
            SetState(style.onNormal, normal, color);
            SetState(style.onHover, hover, color);
            SetState(style.onActive, active, color);
            SetState(style.onFocused, focused, color);
            return style;
        }

        private static void SetText(GUIStyle style, TextAnchor alignment, int size, bool wrap)
        {
            style.alignment = alignment;
            style.fontSize = size;
            style.clipping = TextClipping.Clip;
            style.wordWrap = wrap;
            style.richText = false;
        }

        private static void SetState(GUIStyleState state, Texture2D? background, Color color)
        {
            state.background = background;
            state.textColor = color;
        }
    }
}
