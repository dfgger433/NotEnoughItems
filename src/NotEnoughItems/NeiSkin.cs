using System;
using System.Collections.Generic;
using System.IO;
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
    private static readonly Color32 TooltipFill = new(21, 16, 30, 248);
    private static readonly Color32 TooltipBorder = Rgb(0x635681);
    private static readonly Color32 FieldBorder = Rgb(0xA0A0A0);
    private static readonly Color32 FieldUnfocusedInk = Rgb(0x909090);
    private static readonly Color32 FieldFocusedInk = Rgb(0xE0E0E0);
    private static readonly Color SlotHoverInk = new Color32(255, 255, 255, 96);
    private static readonly Color SlotSelectionInk = Rgb(0xFFD663);
    private static readonly Color IconHoverTint = Rgb(0xE0E0E0);
    private static readonly Color IconDisabledTint = new Color(1f, 1f, 1f, .42f);
    private static SkinStyles? _styles;
    private static readonly Dictionary<string, Texture2D?> Sources = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Frames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Backgrounds = new(StringComparer.OrdinalIgnoreCase);
    private static GUIStyle? _recipePanelStyle, _inspectorPanelStyle;
    private static GUIStyle? _tooltipBackgroundStyle;
    private static bool? _activePack;

    /// <summary>Paints only an interactive slot's transparent hover/selection border.</summary>
    internal static void Slot(Rect rect, bool hovered = false, bool selected = false)
    {
        if (!CanPaint(rect) || (!hovered && !selected))
            return;

        // A catalyst slot sprite has an opaque interior; the overlay grid only needs an outline.
        Outline(rect, selected ? SlotSelectionInk : SlotHoverInk);
    }

    internal static void SlotWarning(Rect rect)
    {
        if (CanPaint(rect)) Fill(new Rect(rect.x + 1, rect.y + 1, 3, 3), Rgb(0xF5A64A));
    }

    internal static void SlotAmount(Rect rect, SlotQuantity amount)
    {
        if (!CanPaint(rect) || rect.width <= 2f || rect.height <= 2f) return;
        const float normalScale = .76f;
        var alignment = amount.Liquid ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        var styles = Styles;
        var ink = styles.Label(alignment, false);
        var content = styles.LabelContent;
        float availableWidth = rect.width - 2f;
        content.text = amount.Fit(FitsAtNormalSize);
        Vector2 size = ink.CalcSize(content) + Vector2.one * 2f;
        float scale = Mathf.Min(normalScale, availableWidth / size.x, (rect.height - 2f) / size.y);
        var target = amount.LabelBounds(rect, size);
        var corner = new Vector2(amount.Liquid ? target.xMin : target.xMax, target.yMax);
        Matrix4x4 previous = GUI.matrix;
        try
        {
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, corner);
            Paint(styles.LabelShadow(alignment), Offset(target), content);
            Paint(ink, target, content);
        }
        finally { GUI.matrix = previous; }

        bool FitsAtNormalSize(string text)
        {
            content.text = text;
            return (ink.CalcSize(content).x + 2f) * normalScale <= availableWidth;
        }
    }

    internal static void BookmarkTint(Rect rect, Color tint)
    {
        if (CanPaint(rect)) Fill(rect, tint);
    }

    internal static void BookmarkBracket(Rect rect, bool start, bool end, bool enabled, bool highlighted)
    {
        if (!CanPaint(rect)) return;
        Color tint = enabled ? Rgb(highlighted ? 0x96E6A2u : 0x57AF67u) : Rgb(highlighted ? 0xDAD9E2u : 0x92939Bu);
        float x = rect.x + 3;
        float top = rect.y + (start ? 2 : 0);
        float bottom = rect.yMax - (end ? 2 : 0);
        Fill(new Rect(x, top, 1.5f, bottom - top), tint);
        if (start) Fill(new Rect(x, top, 6, 1.5f), tint);
        if (end) Fill(new Rect(x, bottom - 1.5f, 6, 1.5f), tint);
    }

    /// <summary>Draws a raised action or option and returns Unity's native button click.</summary>
    internal static bool Button(Rect rect, string text, bool enabled = true, bool selected = false)
    {
        SkinStyles styles = Styles;
        return Click(rect, text, enabled, selected ? styles.ButtonSelected : styles.Button, styles.ButtonDisabled);
    }

    /// <summary>Draws a clickable atlas icon button; the supplied tooltip is attached to Unity's GUI content.</summary>
    internal static bool IconButton(Rect rect, string icon, string tooltip, bool enabled = true, bool selected = false, bool backed = false,
        float maxIconSize = float.PositiveInfinity, bool enlargeIcon = false)
    {
        SkinStyles styles = Styles;
        string caption = ModernityFrames.Caption(icon);
        var content = new GUIContent(caption, tooltip);
        // NEI renders paging and quantity controls as captioned widgets, not sprites.
        if (caption.Length > 0)
            return ClickContent(rect, content, enabled, selected ? styles.ButtonSelected : styles.Button, styles.ButtonDisabled);

        string graphic = icon switch
        {
            "bookmark" when selected => "bookmark_selected",
            "favorite" when selected => "favorite_selected",
            "options" or "wrench" when NeiActions.Mode == NeiActions.LocalMode.Cheat => "options_cheat",
            _ => icon
        };
        // Only toolbar icons opt into an existing widget background; inline sprites stay transparent.
        var background = backed ? (selected ? styles.ButtonSelected : styles.Button) : GUIStyle.none;
        bool clicked = ClickContent(rect, content, enabled, background, backed ? styles.ButtonDisabled : GUIStyle.none);
        if (CanPaint(rect) && CropFrame(graphic) is { } texture)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color tint = !(GUI.enabled && enabled) ? IconDisabledTint : hovered ? IconHoverTint : Color.white;
            DrawButtonSprite(rect, texture, maxIconSize, enlargeIcon, tint);
        }
        return clicked;
    }

    private static void DrawButtonSprite(Rect rect, Texture2D texture, float maxSize, bool enlarge, Color tint)
    {
        float scale = Mathf.Min(Mathf.Min(maxSize, rect.width - 2f) / texture.width,
            Mathf.Min(maxSize, rect.height - 2f) / texture.height);
        if (!enlarge) scale = Mathf.Min(1f, scale);
        if (scale <= 0f) return;
        float width = texture.width * scale, height = texture.height * scale;
        DrawTexture(new Rect(rect.center.x - width * .5f, rect.center.y - height * .5f, width, height), texture,
            ScaleMode.ScaleToFit, tint);
    }

    // Keep the whole row clickable, but draw only the label and its indicator.
    internal static bool Checkbox(Rect rect, string text, bool on)
    {
        var style = Styles.CheckboxLabel;
        bool clicked = Click(rect, text, true, style, style);
        if (CanPaint(rect))
        {
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            var indicator = new Rect(rect.x + 3f, rect.y + 1f, style.padding.left - 6f, rect.height - 2f);
            if (CropFrame(ModernityFrames.CheckboxFace(on, hovered)) is { } texture)
                DrawButtonSprite(indicator, texture, float.PositiveInfinity, enlarge: true, GUI.enabled ? Color.white : IconDisabledTint);
            else Label(indicator, on ? "✓" : "–", TextAnchor.MiddleCenter);
        }
        return clicked;
    }

    private const float SpawnChoiceInset = 24f, SpawnChoiceTitleHeight = 20f;

    internal static float SpawnModeChoiceHeight(string description, float width) =>
        SpawnChoiceTitleHeight + OptionsNoteHeight(description, width - SpawnChoiceInset - 2f) + 2f;

    internal static bool SpawnModeChoice(Rect rect, string title, string description, bool selected)
    {
        // The square and both lines of copy are one hit target, with no row backplate.
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (!CanPaint(rect)) return clicked;

        bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
        var square = new Rect(rect.x + 3f, rect.y + 2f, 16f, 16f);
        if (CropFrame(ModernityFrames.CheckboxFace(selected, hovered, square: true)) is { } texture)
            DrawTexture(square, texture, ScaleMode.StretchToFill, GUI.enabled ? Color.white : IconDisabledTint);
        else
        {
            Fill(square, Rgb(selected ? 0x4C91FFu : 0x92939Bu));
            Outline(square, Rgb(0x232538));
        }

        float textX = rect.x + SpawnChoiceInset, textWidth = rect.xMax - textX - 2f;
        OptionsHeading(new Rect(textX, rect.y, textWidth, SpawnChoiceTitleHeight), title);
        OptionsNote(new Rect(textX, rect.y + SpawnChoiceTitleHeight, textWidth, rect.height - SpawnChoiceTitleHeight), description);
        return clicked;
    }

    internal static void OptionsHeading(Rect rect, string text) => OptionsText(rect, text, Styles.OptionsHeading);

    internal static void OptionsNote(Rect rect, string text) => OptionsText(rect, text, Styles.OptionsNote);

    internal static float OptionsNoteHeight(string text, float width)
    {
        var styles = Styles;
        styles.LabelContent.text = text;
        return Mathf.Ceil(styles.OptionsNote.CalcHeight(styles.LabelContent, Mathf.Max(1f, width)));
    }

    private static void OptionsText(Rect rect, string text, GUIStyle style)
    {
        if (!CanPaint(rect)) return;
        Styles.LabelContent.text = text;
        Paint(style, rect, Styles.LabelContent);
    }

    internal static bool DropDownButton(Rect rect, string text, bool expanded) =>
        Click(rect, text, true, expanded ? Styles.DropDownOpen : Styles.DropDownClosed, Styles.ButtonDisabled);

    internal static bool DropDownOption(Rect rect, string text, bool selected) =>
        Click(rect, text, true, selected ? Styles.DropDownSelected : Styles.DropDownOption, Styles.ButtonDisabled);

    // Match NEI's dimmed output with the heart centered over the hovered choice.
    // A saved output shows a small mark instead, leaving its icon and quantity legible.
    internal static void FavoriteMark(Rect slot, bool selected, bool preview)
    {
        if (!CanPaint(slot)) return;
        if (preview) Fill(slot, new Color32(51, 51, 51, 102));
        float size = Mathf.Min(preview ? 14f : 8f, Mathf.Min(slot.width, slot.height) - 2f);
        var mark = preview ? new Rect(slot.center.x - size * .5f, slot.center.y - size * .5f, size, size) :
            new Rect(slot.x + 1f, slot.y + 1f, size, size);
        Icon(mark, selected ? "favorite_selected" : "favorite");
    }

    /// <summary>Draws one original icon sprite from the independent icons atlas.</summary>
    internal static void Icon(Rect rect, string icon)
    {
        if (!CanPaint(rect) || string.IsNullOrWhiteSpace(icon))
            return;
        if (CropFrame(icon) is { } texture)
            DrawTexture(rect, texture, ScaleMode.ScaleToFit, Color.white);
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

        DrawTexture(rect, background, ScaleMode.StretchToFill, Color.white);
    }

    /// <summary>Draws the original NEI recipe panel frame without stretching its border.</summary>
    internal static void RecipePanel(Rect rect, bool inspector = false)
    {
        if (!CanPaint(rect)) return;
        GUIStyle style = inspector ? _inspectorPanelStyle ??= CreateRecipePanelStyle("inspector_background") :
            _recipePanelStyle ??= CreateRecipePanelStyle("recipe_background");
        if (style.normal.background == null) Fill(rect, Rgb(inspector ? 0xC6C6C6u : 0xC8CAD4u));
        Paint(style, rect);
    }

    private static GUIStyle? TooltipBackgroundStyle => _tooltipBackgroundStyle ??= CreateTooltipBackgroundStyle();

    private static GUIStyle? CreateTooltipBackgroundStyle()
    {
        Texture2D? frame = CropFrame("tooltip_background");
        if (frame == null) return null;
        var style = new GUIStyle
        {
            // Preserve the focused text-field row's one-pixel sides, three-pixel top inset,
            // and one-pixel bottom edge while the center stretches to the tooltip bounds.
            border = new RectOffset(1, 1, 3, 1)
        };
        style.normal.background = frame;
        return style;
    }

    private static GUIStyle CreateRecipePanelStyle(string frameName)
    {
        var style = new GUIStyle();
        Texture2D? frame = CropFrame(frameName);
        style.border = new RectOffset(NeiAssets.RecipeBackgroundSlice, NeiAssets.RecipeBackgroundSlice,
            NeiAssets.RecipeBackgroundSlice, NeiAssets.RecipeBackgroundSlice);
        style.normal.background = frame;
        style.normal.textColor = Color.white;
        return style;
    }

    internal static void RecipeBar(Rect rect)
    {
        if (CanPaint(rect)) Fill(rect, new Color32(0, 0, 0, 0x30));
    }

    internal static void RecipeSlot(Rect rect)
    {
        if (!CanPaint(rect)) return;
        Fill(rect, Rgb(0xAAAEBD));
        Outline(rect, Rgb(0xE7E8EF));
    }

    internal static void RecipeArrow(Rect rect)
    {
        if (!CanPaint(rect)) return;
        Color ink = Rgb(0x737A90);
        float y = rect.center.y;
        Fill(new Rect(rect.x + 3, y - 2, rect.width - 13, 4), ink);
        for (int i = 0; i < 9; i++)
            Fill(new Rect(rect.xMax - 11 + i, y - 9 + i, 1, 18 - 2 * i), ink);
    }

    /// <summary>Draws an option tab. The caller owns which tab is selected.</summary>
    internal static bool Tab(Rect rect, string text, bool selected, bool enabled = true) =>
        Button(rect, text, enabled, selected);

    internal static bool RecipeTab(Rect rect, bool selected, string tooltip)
    {
        bool clicked = ClickContent(rect, new GUIContent(string.Empty, tooltip), true, GUIStyle.none, GUIStyle.none);
        if (CanPaint(rect))
        {
            if (CropFrame(selected ? "method_tab_selected" : "method_tab") is { } frame)
                DrawTexture(rect, frame, ScaleMode.StretchToFill, Color.white);
            else
            {
                Fill(rect, Rgb(selected ? 0xC6C6C6u : 0x8B8B8Bu));
                Outline(rect, Color.black);
            }
        }
        return clicked;
    }

    // Minecraft's default widgets aren't part of NEI's atlas. Draw a small bevel
    // here rather than recoloring every Modernity button or shipping Minecraft art.
    internal static bool RecipeNavButton(Rect rect, string caption, string tooltip, bool enabled = true, bool selected = false, string? icon = null)
    {
        bool clicked = ClickContent(rect, new GUIContent(string.Empty, tooltip), enabled, GUIStyle.none, GUIStyle.none);
        if (CanPaint(rect))
        {
            bool hover = enabled && GUI.enabled && rect.Contains(Event.current.mousePosition);
            bool active = enabled && GUI.enabled;
            Fill(rect, Color.black);
            var face = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            Fill(face, Rgb(!active ? 0x2A2A2Au : hover || selected ? 0x7C86BEu : 0x6D6D6Du));
            Fill(new Rect(face.x, face.y, face.width, 1), Rgb(!active ? 0x414141u : hover || selected ? 0xB1BAE5u : 0xA7A7A7u));
            Fill(new Rect(face.x, face.y + 1, 1, face.height - 1), Rgb(!active ? 0x383838u : 0x939393u));
            Fill(new Rect(face.x + 1, face.yMax - 1, face.width - 1, 1), Rgb(0x333333));
            if (icon != null && CropFrame(icon) is { } texture)
                DrawButtonSprite(rect, texture, 13f, enlarge: false, active ? Color.white : IconDisabledTint);
            else Label(rect, caption, TextAnchor.MiddleCenter, muted: !active);
        }
        return clicked;
    }

    /// <summary>Draws a native single-line editor. An optional control name allows IMGUI focus management.</summary>
    internal static string TextField(Rect rect, string text, string? controlName = null, int maxLength = 128)
    {
        // NEI's FormattedTextField draws a one-pixel gray border and a black interior;
        // widgets.png starts with the hotbar, not a text-field sprite.
        if (CanPaint(rect))
        {
            Fill(rect, FieldBorder);
            Rect inside = new(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            if (inside.width > 0f && inside.height > 0f)
                Fill(inside, Color.black);
        }

        if (!string.IsNullOrEmpty(controlName))
            GUI.SetNextControlName(controlName);

        return GUI.TextField(rect, text, maxLength, Styles.TextField);
    }

    /// <summary>Uses the search skin while keeping the named native editor on every IMGUI event.</summary>
    internal static string SearchField(Rect rect, string text, string controlName, int maxLength)
    {
        SkinStyles styles = Styles;
        GUIStyle? style = GUI.GetNameOfFocusedControl() == controlName ? styles.SearchFocused : styles.SearchIdle;
        if (style == null) return TextField(rect, text, controlName, maxLength);
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

    internal static float TextHeight(string text, float width)
    {
        var styles = Styles;
        styles.LabelContent.text = text;
        return Mathf.Ceil(styles.Wrapped.CalcHeight(styles.LabelContent, Mathf.Max(1f, width)));
    }

    private static Rect Offset(Rect rect)
    {
        rect.position += Vector2.one;
        return rect;
    }

    internal static (string Rich, string Shadow, string Plain) FormatItemText(string text, bool description = false) =>
        TooltipText.Format(text, description ? "BDB4E1" : "F4F2FB");

    internal static float ItemTextHeight(string rich, float width, bool description = false) =>
        Mathf.Ceil((description ? Styles.ItemDetail : Styles.ItemTitle).CalcHeight(new GUIContent(rich), Mathf.Max(1f, width)));

    internal static void ItemText(Rect rect, string rich, string shadow, bool description = false)
    {
        var styles = Styles;
        styles.LabelContent.text = shadow;
        Paint(description ? styles.ItemDetailShadow : styles.ItemTitleShadow, Offset(rect), styles.LabelContent);
        styles.LabelContent.text = rich;
        Paint(description ? styles.ItemDetail : styles.ItemTitle, rect, styles.LabelContent);
    }

    internal static (string Rich, string Shadow, string Plain) FormatItemBody(NativeTooltip.Content content)
    {
        var body = FormatItemText(content.Description, description: true);
        if (content.Note is { } note)
        {
            var gap = body.Plain.Length > 0 ? "\n\n" : string.Empty;
            var annotation = FormatItemText(note, description: true);
            body.Rich += gap + annotation.Rich;
            body.Shadow += gap + annotation.Shadow;
            body.Plain += gap + annotation.Plain;
        }
        return body;
    }

    internal static (Rect Box, Rect IdRow, Rect ModRow) ItemTooltip(Vector2 anchor, Vector2 guiSize, NativeTooltip.Content content,
        Rect protectedControls, bool copyIdEnabled, bool copyModEnabled)
    {
        var title = FormatItemText(content.Name);
        var body = FormatItemBody(content);
        return Tooltip(anchor, guiSize, title.Rich, body.Rich, boxed: true, title.Shadow, body.Shadow, content,
            copyIdEnabled, copyModEnabled, protectedControls);
    }

    /// <summary>Draws a non-interactive tooltip; call last, outside any clipping groups.</summary>
    internal static void Tooltip(Vector2 mousePosition, Vector2 guiSize, string text, string? subtitle = null, bool boxed = false,
        Rect protectedControls = default) =>
        Tooltip(mousePosition, guiSize, text, subtitle, boxed, null, null, null, protectedControls: protectedControls);

    private static (Rect Box, Rect IdRow, Rect ModRow) Tooltip(Vector2 mousePosition, Vector2 guiSize, string text, string? subtitle, bool boxed,
        string? titleShadow, string? bodyShadow, NativeTooltip.Content? item, bool copyIdEnabled = false, bool copyModEnabled = false,
        Rect protectedControls = default)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text))
            return default;

        const float margin = 4f;
        const float pad = 7f;
        const float gap = 3f;
        // The caller owns the GUI scale and logical screen size. Do not use
        // ScreenToGUIPoint: desktop/window offsets do not describe this viewport.
        var viewport = new Rect(margin, margin, guiSize.x - 2 * margin, guiSize.y - 2 * margin);
        if (viewport.width <= 2 * pad || viewport.height <= 2 * pad)
            return default;

        SkinStyles styles = Styles;
        GUIContent title = styles.TooltipTitleContent;
        title.text = text;
        GUIContent detail = styles.TooltipDetailContent;
        detail.text = subtitle ?? string.Empty;
        bool hasDetail = detail.text.Length > 0;
        string? idText = item?.IdText, modText = item?.ModText;
        var titleStyle = titleShadow == null ? styles.TooltipTitle : styles.ItemTitle;
        var detailStyle = bodyShadow == null ? styles.TooltipDetail : styles.ItemDetail;

        float wanted = titleStyle.CalcSize(title).x;
        if (hasDetail) wanted = Mathf.Max(wanted, detailStyle.CalcSize(detail).x);
        wanted = Mathf.Max(wanted, Mathf.Max(FooterWidth(idText), FooterWidth(modText)));

        float width = Mathf.Min(Mathf.Clamp(Mathf.Ceil(wanted) + 2 * pad, 58f, 380f), viewport.width);
        float textWidth = width - 2 * pad;
        float titleHeight = Mathf.Ceil(titleStyle.CalcHeight(title, textWidth));
        float detailHeight = hasDetail ? gap + Mathf.Ceil(detailStyle.CalcHeight(detail, textWidth)) : 0f;
        var (idHeight, modHeight) = ItemFooterHeights(item, textWidth);
        float footerHeight = idHeight + modHeight;
        const float footerGap = 6f;
        float footerSpace = footerHeight > 0f ? footerHeight + footerGap : 0f;
        var box = TooltipPlacement.AtPointer(mousePosition, new Vector2(width, titleHeight + detailHeight + 2 * pad + footerSpace), viewport);
        box = TooltipPlacement.Avoid(box, viewport, protectedControls);
        if (boxed) TooltipBox(box);

        float titleY = box.y + pad;
        float textBottom = box.yMax - pad - footerSpace;
        var titleRect = new Rect(box.x + pad, titleY, textWidth, Mathf.Min(titleHeight, textBottom - titleY));
        if (titleRect.height > 0f)
        {
            if (titleShadow == null)
            {
                Paint(styles.WrappedShadow, Offset(titleRect), title);
                Paint(titleStyle, titleRect, title);
            }
            else ItemText(titleRect, title.text, titleShadow);
        }
        float detailY = titleY + titleHeight + gap;
        if (hasDetail && detailY < textBottom)
        {
            var detailRect = new Rect(box.x + pad, detailY, textWidth, textBottom - detailY);
            if (bodyShadow == null)
            {
                Paint(styles.TooltipDetailShadow, Offset(detailRect), detail);
                Paint(detailStyle, detailRect, detail);
            }
            else ItemText(detailRect, detail.text, bodyShadow, description: true);
        }
        var footer = new Rect(box.x + pad, Mathf.Max(titleY, box.yMax - pad - footerHeight),
            textWidth, Mathf.Min(footerHeight, box.height - 2 * pad));
        var (idRow, modRow) = DrawItemFooter(footer, item, idHeight, copyIdEnabled, copyModEnabled);
        return (box, idRow, modRow);

        float FooterWidth(string? value)
        {
            if (value == null) return 0f;
            styles.TooltipFooterContent.text = value;
            return styles.TooltipFooter.CalcSize(styles.TooltipFooterContent).x;
        }
    }

    internal static (float Id, float Mod) ItemFooterHeights(NativeTooltip.Content? item, float width) =>
        (LineHeight(item?.IdText, width), LineHeight(item?.ModText, width));

    private static float LineHeight(string? text, float width)
    {
        if (text == null) return 0f;
        var styles = Styles;
        styles.TooltipFooterContent.text = text;
        return Mathf.Ceil(styles.TooltipFooter.CalcHeight(styles.TooltipFooterContent, Mathf.Max(1f, width)));
    }

    internal static (Rect Id, Rect Mod) DrawItemFooter(Rect rect, NativeTooltip.Content? item, float idHeight,
        bool copyIdEnabled, bool copyModEnabled)
    {
        var idRow = new Rect(rect.x, rect.y, rect.width, Mathf.Min(idHeight, rect.height));
        var modRow = Rect.MinMaxRect(rect.x, idRow.yMax, rect.xMax, rect.yMax);
        DrawLine(idRow, item?.IdText, copyIdEnabled, mod: false);
        DrawLine(modRow, item?.ModText, copyModEnabled, mod: true);
        return (item?.IdText != null && idRow.width > 0 && idRow.height > 0 ? idRow : default,
            item?.ModText != null && modRow.width > 0 && modRow.height > 0 ? modRow : default);

        static void DrawLine(Rect line, string? text, bool copy, bool mod)
        {
            if (text == null || !CanPaint(line)) return;
            var styles = Styles;
            styles.TooltipFooterContent.text = text;
            if (copy && line.Contains(Event.current.mousePosition)) Fill(line, Rgb(0x51466A));
            Paint(styles.TooltipDetailShadow, Offset(line), styles.TooltipFooterContent);
            Paint(mod ? styles.TooltipMod : copy ? styles.TooltipDetail : styles.TooltipFooter, line, styles.TooltipFooterContent);
        }
    }

    internal static void TooltipBox(Rect box)
    {
        if (!CanPaint(box)) return;
        GUIStyle? style = TooltipBackgroundStyle;
        if (style != null)
        {
            style.Draw(box, GUIContent.none, false, false, false, false);
            return;
        }
        Fill(box, TooltipFill);
        Outline(box, TooltipBorder);
    }

    /// <summary>Releases loaded Unity textures on the main thread, for example during plug-in teardown.</summary>
    internal static void Release()
    {
        _styles = null;
        _recipePanelStyle = _inspectorPanelStyle = null;
        _tooltipBackgroundStyle = null;
        _activePack = null;
        foreach (var cache in new[] { Frames, Sources, Backgrounds })
        {
            foreach (Texture2D? texture in cache.Values)
                if (texture != null && texture) UnityEngine.Object.Destroy(texture);
            cache.Clear();
        }
    }

    private static SkinStyles Styles => _styles ??= new SkinStyles();
    private static bool CanPaint(Rect rect) => Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f;

    private static void Outline(Rect rect, Color color)
    {
        float edge = Mathf.Min(1f, rect.width * .5f, rect.height * .5f);
        Fill(new Rect(rect.x, rect.y, rect.width, edge), color);
        Fill(new Rect(rect.x, rect.yMax - edge, rect.width, edge), color);
        float middle = rect.height - 2f * edge;
        if (middle <= 0f)
            return;
        Fill(new Rect(rect.x, rect.y + edge, edge, middle), color);
        Fill(new Rect(rect.xMax - edge, rect.y + edge, edge, middle), color);
    }

    private static void Fill(Rect rect, Color color) =>
        DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, color);

    private static void DrawTexture(Rect rect, Texture2D texture, ScaleMode scaleMode, Color color)
    {
        Color previous = GUI.color;
        try
        {
            GUI.color = color;
            GUI.DrawTexture(rect, texture, scaleMode, true);
        }
        finally
        {
            GUI.color = previous;
        }
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

    private static Texture2D? GetSource(string resourcePath, bool readable)
    {
        if (Sources.TryGetValue(resourcePath, out var source)) return source;
        source = LoadTexture(resourcePath, readable);
        Sources.Add(resourcePath, source);
        return source;
    }

    private static Texture2D? CropFrame(string name)
    {
        if (Frames.TryGetValue(name, out var cached)) return cached;
        if (ModernityFrames.Find(name) is not { } rect) return null;
        var source = GetSource(rect.ResourcePath, readable: true);
        Texture2D? cropped = null;
        if (source != null && source.width >= rect.X + rect.Width && source.height >= rect.Y + rect.Height)
        {
            int bottom = source.height - rect.Y - rect.Height;
            int x = rect.X, width = rect.Width, height = rect.Height;
            if (name is "favorite" or "favorite_selected")
            {
                // The upstream 18×20 frames have uneven transparent padding. Center the
                // visible pixels, not that padded rectangle, including on custom packs.
                var ink = ModernityFrames.InkBounds(width, height, source.GetPixels(x, bottom, width, height));
                x += ink.X;
                bottom += ink.Y;
                width = ink.Width;
                height = ink.Height;
            }
            cropped = NewTexture(width, height, "NEI: " + name);
            cropped.SetPixels(source.GetPixels(x, bottom, width, height));
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

    private static Texture2D? LoadTexture(string resourcePath, bool readable = false)
    {
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        string? directory = assemblyPath.Length == 0 ? null : Path.GetDirectoryName(assemblyPath);
        bool hasDirectory = !string.IsNullOrEmpty(directory);
        bool activePack = _activePack ??= hasDirectory && Directory.Exists(Path.Combine(directory!, NeiAssets.ActivePackDirectory));
        var size = NeiAssets.ExpectedSize(resourcePath);
        foreach (var (path, embedded) in NeiAssets.TextureSources(resourcePath, activePack))
        {
            var texture = embedded ? TryLoadEmbedded(path, readable) :
                hasDirectory ? TryLoadFile(Path.Combine(directory!, path), readable) : null;
            if (texture == null) continue;
            if (size is not { } expected || (texture.width == expected.Width && texture.height == expected.Height))
                return texture;
            UnityEngine.Object.Destroy(texture);
        }
        return null;
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

    private static Texture2D? TryLoadEmbedded(string resource, bool readable)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null) return null;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return DecodeTexture(bytes.ToArray(), "NEI " + resource + " (embedded)", readable);
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
        bool loaded = false;
        try
        {
            loaded = UnityEngine.ImageConversion.LoadImage(texture, bytes, !readable);
            return loaded ? texture : null;
        }
        finally
        {
            if (!loaded) UnityEngine.Object.Destroy(texture);
        }
    }

    private static Color32 Rgb(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    private sealed class SkinStyles
    {
        private readonly GUIStyle?[,] _labels = new GUIStyle[9, 2];
        private readonly GUIStyle?[] _labelShadows = new GUIStyle[9];
        internal readonly GUIStyle Wrapped;
        internal readonly GUIStyle WrappedShadow;
        internal readonly GUIStyle Button;
        internal readonly GUIStyle ButtonSelected;
        internal readonly GUIStyle ButtonDisabled;
        internal readonly GUIStyle CheckboxLabel, OptionsHeading, OptionsNote;
        internal readonly GUIStyle DropDownClosed, DropDownOpen, DropDownOption, DropDownSelected;
        internal readonly GUIStyle TextField;
        internal readonly GUIStyle? SearchIdle;
        internal readonly GUIStyle? SearchFocused;
        internal readonly GUIStyle TooltipTitle;
        internal readonly GUIStyle TooltipDetail;
        internal readonly GUIStyle TooltipDetailShadow;
        internal readonly GUIStyle ItemTitle, ItemTitleShadow, ItemDetail, ItemDetailShadow, TooltipFooter, TooltipMod;
        internal readonly GUIContent LabelContent = new();
        internal readonly GUIContent TooltipTitleContent = new();
        internal readonly GUIContent TooltipDetailContent = new();
        internal readonly GUIContent TooltipFooterContent = new();

        internal SkinStyles()
        {
            // Inline text uses a one-pixel shadow over the transparent game world.
            Wrapped = TextStyle(TextAnchor.UpperLeft, OverlayInk, 13, wrap: true);
            WrappedShadow = TextStyle(TextAnchor.UpperLeft, Shadow, 13, wrap: true);
            var normal = CropFrame("button");
            var active = CropFrame("button_hover");
            var disabled = CropFrame("button_disabled");
            var darkInk = Rgb(0x232538);
            Button = ButtonStyle(normal, active, darkInk);
            ButtonSelected = ButtonStyle(active, active, darkInk);
            ButtonDisabled = ButtonStyle(disabled, disabled, OverlayMuted);
            CheckboxLabel = ContentStyle(TextAnchor.MiddleLeft, 4);
            CheckboxLabel.padding.left = 48;
            ApplyStates(CheckboxLabel, null, null, darkInk, darkInk);
            OptionsHeading = new GUIStyle(TextStyle(TextAnchor.MiddleLeft, darkInk, 13)) { fontStyle = FontStyle.Bold };
            OptionsNote = TextStyle(TextAnchor.UpperLeft, Rgb(0x404459), 12, wrap: true);

            var choice = CropFrame("dropdown_option");
            var choiceHighlight = CropFrame("dropdown_option_hover");
            DropDownClosed = DropDownStyle(CropFrame("dropdown_closed"), CropFrame("dropdown_closed_hover"), darkInk, arrow: true, fallback: Button);
            DropDownOpen = DropDownStyle(CropFrame("dropdown_open"), CropFrame("dropdown_open_hover"), darkInk, arrow: true, fallback: ButtonSelected);
            DropDownOption = DropDownStyle(choice, choiceHighlight, darkInk, arrow: false, fallback: Button);
            DropDownSelected = DropDownStyle(choiceHighlight, choiceHighlight, darkInk, arrow: false, fallback: ButtonSelected);

            TextField = ContentStyle(TextAnchor.MiddleLeft, 6);
            ApplyStates(TextField, null, null, FieldUnfocusedInk, FieldFocusedInk);
            SearchIdle = SearchStyle(CropFrame("search_field"), OverlayInk);
            SearchFocused = SearchStyle(CropFrame("search_field_focused"), darkInk);
            TooltipTitle = TextStyle(TextAnchor.UpperLeft, TooltipInk, 13, wrap: true);
            TooltipDetail = TextStyle(TextAnchor.UpperLeft, TooltipAccent, 12, wrap: true);
            TooltipDetailShadow = TextStyle(TextAnchor.UpperLeft, Shadow, 12, wrap: true);
            ItemTitle = new GUIStyle(TooltipTitle) { richText = true };
            ItemTitleShadow = new GUIStyle(WrappedShadow) { richText = true };
            ItemDetail = new GUIStyle(TooltipDetail) { richText = true };
            ItemDetailShadow = new GUIStyle(TooltipDetailShadow) { richText = true };
            TooltipFooter = TextStyle(TextAnchor.UpperLeft, OverlayMuted, 12, wrap: true);
            TooltipMod = TextStyle(TextAnchor.UpperLeft, new Color32(76, 145, 255, 255), 12, wrap: true);
        }

        internal GUIStyle Label(TextAnchor alignment, bool muted)
        {
            int row = muted ? 1 : 0;
            return _labels[(int)alignment, row] ??= TextStyle(alignment, muted ? OverlayMuted : OverlayInk, 13);
        }

        internal GUIStyle LabelShadow(TextAnchor alignment) =>
            _labelShadows[(int)alignment] ??= TextStyle(alignment, Shadow, 13);

        private static GUIStyle TextStyle(TextAnchor alignment, Color color, int size, bool wrap = false)
        {
            var style = new GUIStyle();
            SetText(style, alignment, size, wrap);
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle ButtonStyle(Texture2D? normal, Texture2D? active, Color ink)
        {
            var style = ContentStyle(TextAnchor.MiddleCenter, 4);
            const int edge = ModernityFrames.ButtonSlice;
            // Every row is cropped into an isolated point-filtered texture before Unity slices it.
            style.border = new RectOffset(edge, edge, edge, edge);
            ApplyStates(style, normal, active, ink, ink);
            return style;
        }

        private static GUIStyle DropDownStyle(Texture2D? normal, Texture2D? highlight, Color ink, bool arrow, GUIStyle fallback)
        {
            if (normal == null) return fallback;
            int right = arrow ? 21 : 7; // Keep the arrow inside the fixed right edge when the row stretches.
            var style = ContentStyle(TextAnchor.MiddleLeft, 8);
            style.padding.right = right;
            style.border = new RectOffset(2, right, 2, 5);
            ApplyStates(style, normal, highlight ?? normal, ink, ink);
            return style;
        }

        private static GUIStyle? SearchStyle(Texture2D? frame, Color ink)
        {
            if (frame == null) return null;
            var style = ContentStyle(TextAnchor.MiddleLeft, 6);
            // One-pixel outline and two extra rows of inset shadow along the top.
            style.border = new RectOffset(1, 1, 3, 1);
            ApplyStates(style, frame, frame, ink, ink);
            return style;
        }

        private static GUIStyle ContentStyle(TextAnchor alignment, int horizontalPadding)
        {
            var style = new GUIStyle { padding = new RectOffset(horizontalPadding, horizontalPadding, 2, 2) };
            SetText(style, alignment, 13, false);
            return style;
        }

        private static void ApplyStates(GUIStyle style, Texture2D? normal, Texture2D? active, Color ink, Color focusInk)
        {
            SetState(style.normal, normal, ink);
            SetState(style.hover, active, ink);
            SetState(style.active, active, focusInk);
            SetState(style.focused, active, focusInk);
            SetState(style.onNormal, normal, ink);
            SetState(style.onHover, active, ink);
            SetState(style.onActive, active, focusInk);
            SetState(style.onFocused, active, focusInk);
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
