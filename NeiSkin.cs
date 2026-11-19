using UnityEngine;

namespace NotEnoughItems;

/// <summary>Immediate-mode controls with an original, procedurally drawn NEI-style pixel skin.</summary>
internal static class NeiSkin
{
    private static readonly Color32 Ink = Rgb(0x282A38);
    private static readonly Color32 MutedInk = Rgb(0x505367);
    private static readonly Color32 TooltipInk = Rgb(0xF4F2FB);
    private static readonly Color32 TooltipAccent = Rgb(0xBDB4E1);

    private static SkinStyles? _styles;

    /// <summary>Paints a raised panel. Does not clip or consume input.</summary>
    internal static void Panel(Rect rect)
    {
        if (CanPaint(rect))
            Paint(Styles.Panel, rect);
    }

    /// <summary>Paints a recessed item slot. The caller controls its hover, selection and clicks.</summary>
    internal static void Slot(Rect rect, bool hovered = false, bool selected = false)
    {
        if (!CanPaint(rect))
            return;

        SkinStyles styles = Styles;
        Paint(selected ? styles.SlotSelected : hovered ? styles.SlotHovered : styles.Slot, rect);
    }

    /// <summary>Draws a raised action or option and returns Unity's native button click.</summary>
    internal static bool Button(Rect rect, string text, bool enabled = true, bool selected = false)
    {
        SkinStyles styles = Styles;
        return Click(rect, text, enabled, selected ? styles.ButtonSelected : styles.Button, styles.ButtonDisabled);
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
        Paint(styles.Label(alignment, muted), rect, styles.LabelContent);
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

        Paint(styles.Tooltip, new Rect(x, y, width, height));
        float textBottom = y + height - pad;
        float titleY = y + pad;
        Paint(styles.TooltipTitle, new Rect(x + pad, titleY, textWidth, Mathf.Min(titleHeight, textBottom - titleY)), title);
        float detailY = titleY + titleHeight + gap;
        if (hasDetail && detailY < textBottom)
            Paint(styles.TooltipDetail, new Rect(x + pad, detailY, textWidth, textBottom - detailY), detail);
    }

    /// <summary>Releases generated Unity textures on the main thread, for example during plug-in teardown.</summary>
    internal static void Release()
    {
        _styles?.Release();
        _styles = null;
    }

    private static SkinStyles Styles => _styles ??= new SkinStyles();

    private static bool CanPaint(Rect rect) => Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f;

    // Paint decorations without allocating control IDs; buttons/editors still run on every IMGUI event.
    private static void Paint(GUIStyle style, Rect rect, GUIContent? content = null) =>
        style.Draw(rect, content ?? GUIContent.none, false, false, false, false);

    private static bool Click(Rect rect, string text, bool enabled, GUIStyle style, GUIStyle disabledStyle)
    {
        bool previous = GUI.enabled;
        try
        {
            GUI.enabled = previous && enabled;
            return GUI.Button(rect, text, GUI.enabled ? style : disabledStyle);
        }
        finally
        {
            GUI.enabled = previous;
        }
    }

    private static Color32 Rgb(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    private sealed class SkinStyles
    {
        private const int PatchSize = 10;
        private const int Slice = 4;
        private readonly System.Collections.Generic.List<Texture2D> _textures = new();
        private readonly GUIStyle?[,] _labels = new GUIStyle[9, 2];

        internal readonly GUIStyle Panel;
        internal readonly GUIStyle Slot;
        internal readonly GUIStyle SlotHovered;
        internal readonly GUIStyle SlotSelected;
        internal readonly GUIStyle Button;
        internal readonly GUIStyle ButtonSelected;
        internal readonly GUIStyle ButtonDisabled;
        internal readonly GUIStyle Tab;
        internal readonly GUIStyle TabSelected;
        internal readonly GUIStyle TextField;
        internal readonly GUIStyle Tooltip;
        internal readonly GUIStyle TooltipTitle;
        internal readonly GUIStyle TooltipDetail;
        internal readonly GUIContent LabelContent = new();
        internal readonly GUIContent TooltipTitleContent = new();
        internal readonly GUIContent TooltipDetailContent = new();

        internal SkinStyles()
        {
            // Silver shell, slate controls and sky-blue focus. The patches are geometry, not imported art.
            Panel = FrameStyle(Patch(0xC9CAD3, 0xFBFBFD, 0x858896, 0x656874, cutCorners: true));
            Slot = FrameStyle(Patch(0xA5A9BD, 0x767B91, 0xE5E7EF, 0xB8BBC9));
            SlotHovered = FrameStyle(Patch(0xC1D8EE, 0x6689A7, 0xF3FAFF, 0x819FB7));
            SlotSelected = FrameStyle(Patch(0xA4C8EA, 0x4F779B, 0xE4F6FF, 0x587A99));

            Texture2D neutral = Patch(0xA0A5BA, 0xDADCE9, 0x646881, 0x51556C);
            Texture2D hover = Patch(0xA8D6FB, 0xF1FAFF, 0x6298C1, 0x5A80A3);
            Texture2D pressed = Patch(0x8EBCE3, 0x517899, 0xC9E9FC, 0x4F6B89);
            Texture2D disabled = Patch(0x9195A7, 0xB3B6C4, 0x747787, 0x717487);
            Texture2D tab = Patch(0x9B9FB2, 0xC2C6D5, 0x6C7189, 0x60647A);
            Texture2D activeTab = Patch(0xCBCCD6, 0xF8F8FC, 0x9598AA, 0x616478);

            Button = ControlStyle(neutral, hover, pressed, hover, TextAnchor.MiddleCenter, Ink, 6);
            ButtonSelected = ControlStyle(pressed, hover, pressed, hover, TextAnchor.MiddleCenter, Ink, 6);
            ButtonDisabled = ControlStyle(disabled, disabled, disabled, disabled, TextAnchor.MiddleCenter, Ink, 6);
            Tab = ControlStyle(tab, hover, pressed, hover, TextAnchor.MiddleCenter, Ink, 5);
            TabSelected = ControlStyle(activeTab, activeTab, pressed, hover, TextAnchor.MiddleCenter, Ink, 5);

            Texture2D field = Patch(0x161528, 0x555D77, 0xCDCFE0, 0x757990);
            Texture2D fieldHover = Patch(0x161528, 0x656F8D, 0xDFE7F7, 0x8994AE);
            Texture2D fieldFocus = Patch(0x161528, 0x37638D, 0xC2E7FF, 0x8EBBE4);
            TextField = ControlStyle(field, fieldHover, fieldFocus, fieldFocus, TextAnchor.MiddleLeft, TooltipInk, 7);

            Tooltip = FrameStyle(Patch(0x171120, 0x67508A, 0x3F2C58, 0x100B18, cutCorners: true));
            TooltipTitle = TextStyle(TextAnchor.UpperLeft, TooltipInk, 13, wrap: true);
            TooltipDetail = TextStyle(TextAnchor.UpperLeft, TooltipAccent, 12, wrap: true);
        }

        internal GUIStyle Label(TextAnchor alignment, bool muted)
        {
            int row = muted ? 1 : 0;
            return _labels[(int)alignment, row] ??= TextStyle(alignment, muted ? MutedInk : Ink, 13);
        }

        internal void Release()
        {
            foreach (Texture2D texture in _textures)
                Object.Destroy(texture);

            _textures.Clear();
        }

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

        private Texture2D Patch(uint center, uint topLeft, uint bottomRight, uint outline, bool cutCorners = false)
        {
            Color32 face = Rgb(center);
            Color32 light = Rgb(topLeft);
            Color32 dark = Rgb(bottomRight);
            Color32 rim = Rgb(outline);
            const int last = PatchSize - 1;
            var pixels = new Color32[PatchSize * PatchSize];
            for (int y = 0; y <= last; y++)
            for (int x = 0; x <= last; x++)
            {
                bool outerX = x == 0 || x == last;
                bool outerY = y == 0 || y == last;
                Color32 pixel = face;
                if (outerX || outerY)
                    pixel = cutCorners && outerX && outerY ? default : rim;
                else if (x == last - 1 || y == 1)
                    pixel = dark;
                else if (x == 1 || y == last - 1)
                    pixel = light;

                // Unity textures start at the bottom left, unlike IMGUI coordinates.
                pixels[y * PatchSize + x] = pixel;
            }

            var texture = new Texture2D(PatchSize, PatchSize, TextureFormat.RGBA32, false)
            {
                name = "NEI procedural bevel",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _textures.Add(texture);
            return texture;
        }
    }
}
