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
    private static readonly string[] IconNames = { "previous", "next", "options", "bookmark", "add", "remove", "search", "close", "machines", "history", "wrench" };

    private static SkinStyles? _styles;
    private static Texture2D? _transparent;
    private static Texture2D? _iconAtlas;
    private static Texture2D? _surfaceAtlas;
    private static readonly string[] SurfaceFrames =
    {
        "slot_hover", "slot_selected", "panel", "tooltip", "button", "button_hover", "button_pressed", "button_disabled",
        "tab", "tab_hover", "tab_pressed", "tab_selected", "field", "field_hover", "field_focus", "field_disabled"
    };
    private static readonly Dictionary<string, Texture2D?> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D?> Surfaces = new(StringComparer.OrdinalIgnoreCase);
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
        bool clicked = ClickContent(rect, new GUIContent(string.Empty, tooltip ?? string.Empty), enabled, style, styles.ButtonDisabled);
        if (CanPaint(rect))
        {
            float inset = Mathf.Max(2f, Mathf.Min(rect.width, rect.height) * 0.18f);
            Icon(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2), icon);
        }
        return clicked;
    }

    /// <summary>Draws one original icon sprite from the independent icons atlas.</summary>
    internal static void Icon(Rect rect, string icon)
    {
        if (!CanPaint(rect) || string.IsNullOrWhiteSpace(icon) || !IconNames.Contains(icon, StringComparer.OrdinalIgnoreCase))
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
        if (NativeTooltip.TryGetParts(subtitle, out var native) && native != null)
        {
            DrawNativeTooltip(mousePosition, native);
            return;
        }

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

    private static void DrawNativeTooltip(Vector2 mouse, NativeTooltip.Parts tip)
    {
        const float pad = 7f, gap = 3f, margin = 4f;
        var min = GUIUtility.ScreenToGUIPoint(Vector2.zero) + Vector2.one * margin;
        var max = GUIUtility.ScreenToGUIPoint(new Vector2(Screen.width, Screen.height)) - Vector2.one * margin;
        float availableWidth = Mathf.Min(380f, max.x - min.x) - 2 * pad;
        float availableHeight = max.y - min.y - 2 * pad;
        if (availableWidth <= 0 || availableHeight <= 0) return;

        var styles = Styles;
        var title = new TooltipLayout(tip.Title, styles.TooltipTitle, availableWidth, true);
        var detail = new TooltipLayout(tip.Body, styles.TooltipDetail, availableWidth, false);
        var footer = new TooltipLayout(tip.Shortcuts, styles.TooltipDetail, availableWidth, false);
        float width = Mathf.Min(availableWidth, Mathf.Max(44, title.Width, detail.Width, footer.Width)) + 2 * pad;
        float height = Mathf.Min(availableHeight, title.Height + detail.Height + footer.Height + 2 * gap) + 2 * pad;
        float x = mouse.x + 12, y = mouse.y + 15;
        if (x + width > max.x) x = mouse.x - width - 8;
        if (y + height > max.y) y = mouse.y - height - 8;
        x = Mathf.Clamp(x, min.x, max.x - width);
        y = Mathf.Clamp(y, min.y, max.y - height);
        var origin = new Vector2(x + pad, y + pad);
        float footerY = y + height - pad - footer.Height;
        float titleHeight = Mathf.Min(title.Height, footerY - origin.y - gap);
        float detailY = origin.y + Mathf.Max(0, titleHeight) + gap;
        title.Draw(origin, width - 2 * pad, titleHeight);
        detail.Draw(new Vector2(origin.x, detailY), width - 2 * pad, footerY - detailY - gap);
        footer.Draw(new Vector2(origin.x, Mathf.Max(origin.y, footerY)), width - 2 * pad,
            Mathf.Min(footer.Height, height - 2 * pad));
    }

    private sealed class TooltipLayout
    {
        private readonly List<(Rect Rect, NativeTooltip.Run Run, Texture? Atlas, Rect Uv)> _glyphs = new();
        private readonly GUIStyle _baseStyle;
        internal readonly float Width, Height;

        internal TooltipLayout(IReadOnlyList<NativeTooltip.Run> runs, GUIStyle baseStyle, float maxWidth, bool title)
        {
            _baseStyle = baseStyle;
            var content = new GUIContent();
            float x = 0, y = 0, widest = 0, row = baseStyle.lineHeight;
            void NewLine()
            {
                x = 0;
                y += row;
                row = baseStyle.lineHeight;
            }
            void Add(NativeTooltip.Run run, float w, float h, Texture? atlas = null, Rect uv = default)
            {
                if (x > 0 && x + w > maxWidth) NewLine();
                _glyphs.Add((new Rect(x, y, w, h), run, atlas, uv));
                x += w;
                row = Mathf.Max(row, h);
                widest = Mathf.Max(widest, x);
            }

            foreach (var run in runs)
            {
                float height = TooltipRunStyle(baseStyle, run).lineHeight;
                if (run.SpriteIndex >= 0)
                {
                    if (NativeTooltip.TryResolveSprite(run.SpriteIndex, title, out var atlas, out var uv, out float aspect))
                    {
                        float iconHeight = Mathf.Max(1f, height - 3f);
                        Add(run, Mathf.Min(maxWidth, aspect * iconHeight + 2), height, atlas, uv);
                    }
                    else
                    {
                        var dot = new NativeTooltip.Run("• ", run.Color, run.Alpha);
                        content.text = dot.Text;
                        Add(dot, baseStyle.CalcSize(content).x, height);
                    }
                    continue;
                }
                var measure = TooltipRunStyle(baseStyle, run);
                // Break at spaces or CJK glyphs; split genuinely overlong words by text
                // elements, not UTF-16 units (surrogates and combining accents stay intact).
                foreach (string word in TooltipWords(run.Text))
                {
                    if (word == "\r\n" || word == "\n" || word == "\r") { NewLine(); continue; }
                    string text = word == "\t" ? "    " : word;
                    if (x == 0 && string.IsNullOrWhiteSpace(text)) continue;
                    content.text = text;
                    float width = measure.CalcSize(content).x;
                    if (width <= maxWidth)
                    {
                        if (x + width > maxWidth && string.IsNullOrWhiteSpace(text)) { NewLine(); continue; }
                        Add(new NativeTooltip.Run(text, run.Color, run.Alpha, run.Font, run.Scale), width, height);
                        continue;
                    }
                    var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
                    while (elements.MoveNext())
                    {
                        content.text = elements.GetTextElement();
                        Add(new NativeTooltip.Run(content.text, run.Color, run.Alpha, run.Font, run.Scale), measure.CalcSize(content).x, height);
                    }
                }
            }
            Width = widest;
            Height = runs.Count == 0 ? 0 : y + row;
        }

        internal void Draw(Vector2 origin, float width, float height)
        {
            if (height <= 0 || width <= 0) return;
            var content = new GUIContent();
            Color previous = GUI.color;
            GUI.BeginGroup(new Rect(origin.x, origin.y, width, height));
            try
            {
                foreach (var glyph in _glyphs)
                {
                    var rect = glyph.Rect;
                    if (rect.yMax > height + .1f) continue;
                    var run = glyph.Run;
                    Color color = run.Color.HasValue ? run.Color.Value : _baseStyle.normal.textColor;
                    if (run.Alpha.HasValue) color.a = run.Alpha.Value / 255f;
                    if (glyph.Atlas)
                    {
                        GUI.color = previous * (run.TintSprite ? color : new Color(1, 1, 1, color.a));
                        GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y + 1, Mathf.Max(1, rect.width - 2), Mathf.Max(1, rect.height - 3)), glyph.Atlas!, glyph.Uv, true);
                        continue;
                    }
                    var style = TooltipRunStyle(_baseStyle, run);
                    content.text = run.Text;
                    GUI.color = previous;
                    style.normal.textColor = new Color(Shadow.r / 255f, Shadow.g / 255f, Shadow.b / 255f, color.a);
                    Paint(style, Offset(rect), content);
                    style.normal.textColor = color;
                    Paint(style, rect, content);
                }
            }
            finally { GUI.color = previous; GUI.EndGroup(); }
        }
    }

    private static IEnumerable<string> TooltipWords(string text)
    {
        int start = 0;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            int index = elements.ElementIndex;
            string element = elements.GetTextElement();
            char ch = element[0];
            if (!char.IsWhiteSpace(ch) && ch is not (>= '\u2E80' and <= '\u9FFF') && ch is not (>= '\uAC00' and <= '\uD7AF')) continue;
            if (index > start) yield return text.Substring(start, index - start);
            start = index + element.Length;
            if (ch == '\r' && element.Length == 1 && start < text.Length && text[start] == '\n')
            {
                elements.MoveNext();
                start++;
                yield return "\r\n";
            }
            else yield return element;
        }
        if (start < text.Length) yield return text.Substring(start);
    }

    private static GUIStyle TooltipRunStyle(GUIStyle basis, NativeTooltip.Run run)
    {
        var style = Styles.TooltipRun;
        style.font = basis.font;
        style.fontSize = Mathf.RoundToInt(basis.fontSize * run.Scale);
        style.fontStyle = run.Font;
        return style;
    }

    /// <summary>Releases loaded Unity textures on the main thread, for example during plug-in teardown.</summary>
    internal static void Release()
    {
        _styles?.Release();
        _styles = null;
        foreach (Texture2D? icon in Icons.Values)
            if (icon != null && icon) UnityEngine.Object.Destroy(icon);
        Icons.Clear();
        foreach (Texture2D? surface in Surfaces.Values)
            if (surface != null && surface) UnityEngine.Object.Destroy(surface);
        Surfaces.Clear();
        foreach (Texture2D? background in Backgrounds.Values)
            if (background != null && background) UnityEngine.Object.Destroy(background);
        Backgrounds.Clear();
        if (_transparent != null && _transparent) UnityEngine.Object.Destroy(_transparent);
        _transparent = null;
        if (_iconAtlas != null && _iconAtlas) UnityEngine.Object.Destroy(_iconAtlas);
        if (_surfaceAtlas != null && _surfaceAtlas) UnityEngine.Object.Destroy(_surfaceAtlas);
        _iconAtlas = _surfaceAtlas = null;
    }

    private static SkinStyles Styles => _styles ??= new SkinStyles();
    private static bool CanPaint(Rect rect) => Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f;

    private static Texture2D? GetIcon(string icon)
    {
        if (Icons.TryGetValue(icon, out Texture2D? cached)) return cached;
        int frame = Array.FindIndex(IconNames, name => string.Equals(icon, name, StringComparison.OrdinalIgnoreCase));
        string file = "icons/" + icon.ToLowerInvariant() + ".png";
        Texture2D? texture = frame < 0 ? null : TryLoadSidecar(file);
        if (frame >= 0 && texture == null)
        {
            _iconAtlas ??= LoadTexture("icons-atlas.png", readable: true);
            texture = Slice(_iconAtlas, frame, 32, 8, icon) ?? TryLoadEmbedded(file);
        }
        Icons.Add(icon, texture);
        return texture;
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

    private static Texture2D Surface(string name)
    {
        if (Surfaces.TryGetValue(name, out Texture2D? cached) && cached != null && cached)
            return cached;
        string file = "surfaces/" + name + ".png";
        Texture2D? texture = TryLoadSidecar(file);
        if (texture == null)
        {
            _surfaceAtlas ??= LoadTexture("ui-atlas.png", readable: true);
            texture = Slice(_surfaceAtlas, Array.IndexOf(SurfaceFrames, name), 16, 4, name) ?? TryLoadEmbedded(file);
        }
        if (texture == null || !texture)
            texture = Transparent;
        Surfaces[name] = texture;
        return texture!;
    }

    private static Texture2D Transparent => _transparent ??= LoadTexture("background.png")!;

    private static Texture2D? GetBackground(string screen)
    {
        if (Backgrounds.TryGetValue(screen, out Texture2D? cached))
            return cached;
        string file = screen.Length == 0 ? "background.png" : "background-" + screen + ".png";
        Texture2D? texture = LoadTexture(file);
        Backgrounds.Add(screen, texture);
        return texture;
    }

    private static string NormalizeScreen(string screen) => screen?.Trim().ToLowerInvariant() switch
    {
        "catalog" or "bookmarks" or "recipe" or "options" => screen.Trim().ToLowerInvariant(),
        _ => string.Empty
    };

    private static Texture2D? Slice(Texture2D? atlas, int frame, int size, int columns, string name)
    {
        if (atlas == null || frame < 0 || atlas.width != columns * size || atlas.height % size != 0) return null;
        int x = frame % columns * size, y = atlas.height - (frame / columns + 1) * size;
        if (y < 0) return null;
        var texture = NewTexture(size, size, "NEI atlas: " + name);
        texture.SetPixels(atlas.GetPixels(x, y, size, size));
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D? LoadTexture(string fileName, bool readable = false) =>
        TryLoadSidecar(fileName, readable) ?? TryLoadEmbedded(fileName, readable);

    private static Texture2D? TryLoadSidecar(string fileName, bool readable = false)
    {
        string? assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(assemblyPath)) return null;
        string path = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "Textures", fileName);
        try
        {
            if (!File.Exists(path)) return null;
            return DecodeTexture(File.ReadAllBytes(path), "NEI " + fileName + " (replaceable)", readable);
        }
        catch (Exception)
        {
            // A damaged or incompatible replacement must not prevent the embedded default from loading.
            return null;
        }
    }

    private static Texture2D? TryLoadEmbedded(string fileName, bool readable = false)
    {
        string resource = "NotEnoughItems.Textures." + fileName.Replace('/', '.').Replace('\\', '.');
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
        private const int Slice = 4;
        private readonly List<Texture2D> _textures = new();
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
        internal readonly GUIStyle TooltipRun;
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

            Texture2D normal = Atlas("button");
            Texture2D hover = Atlas("button_hover");
            Texture2D pressed = Atlas("button_pressed");
            Texture2D disabled = Atlas("button_disabled");
            Texture2D tab = Atlas("tab");
            Texture2D activeTab = Atlas("tab_selected");
            Button = ControlStyle(normal, hover, pressed, hover, TextAnchor.MiddleCenter, OverlayInk, 4);
            ButtonSelected = ControlStyle(activeTab, hover, pressed, hover, TextAnchor.MiddleCenter, OverlayInk, 4);
            ButtonDisabled = ControlStyle(disabled, disabled, disabled, disabled, TextAnchor.MiddleCenter, OverlayMuted, 4);
            Tab = ControlStyle(tab, Atlas("tab_hover"), Atlas("tab_pressed"), Atlas("tab_hover"), TextAnchor.MiddleCenter, OverlayInk, 4);
            TabSelected = ControlStyle(activeTab, activeTab, pressed, activeTab, TextAnchor.MiddleCenter, OverlayInk, 4);

            TextField = ControlStyle(Atlas("field"), Atlas("field_hover"), Atlas("field_focus"), Atlas("field_focus"),
                TextAnchor.MiddleLeft, TooltipInk, 6);
            TooltipTitle = TextStyle(TextAnchor.UpperLeft, TooltipInk, 13, wrap: true);
            TooltipDetail = TextStyle(TextAnchor.UpperLeft, TooltipAccent, 12, wrap: true);
            TooltipDetailShadow = TextStyle(TextAnchor.UpperLeft, Shadow, 12, wrap: true);
            TooltipRun = TextStyle(TextAnchor.UpperLeft, TooltipAccent, 12);
            TooltipRun.clipping = TextClipping.Overflow; // The tooltip group clips each block as a whole.
        }

        internal GUIStyle Label(TextAnchor alignment, bool muted)
        {
            int row = muted ? 1 : 0;
            return _labels[(int)alignment, row] ??= TextStyle(alignment, muted ? OverlayMuted : OverlayInk, 13);
        }

        internal GUIStyle LabelShadow(TextAnchor alignment) =>
            _labelShadows[(int)alignment] ??= TextStyle(alignment, Shadow, 13);

        internal void Release() { } // textures are owned and released by NeiSkin's shared resource cache

        private static Texture2D Atlas(string frame) => Surface(frame);

        private static GUIStyle FrameStyle(Texture2D background)
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

        private static GUIStyle ControlStyle(Texture2D normal, Texture2D hover, Texture2D active, Texture2D focused,
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

        private static void SetState(GUIStyleState state, Texture2D background, Color color)
        {
            state.background = background;
            state.textColor = color;
        }
    }
}
