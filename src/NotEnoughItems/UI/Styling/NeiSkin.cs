using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NotEnoughItems;

/// <summary>IMGUI controls backed by replaceable GUI atlases.</summary>
internal static class NeiSkin
{
    private static readonly Color32 OverlayInk = Rgb(0xF4F2FB);
    private static readonly Color32 OverlayMuted = Rgb(0xC3C6D8);
    private static readonly Color32 Shadow = Rgb(0x191827);
    private static readonly Color32 ButtonInk = Rgb(0x232538);
    private static readonly Color32 TooltipInk = Rgb(0xF4F2FB);
    private static readonly Color32 TooltipAccent = Rgb(0xBDB4E1);
    private static readonly Color32 TooltipFill = new(21, 16, 30, 248);
    private static readonly Color32 TooltipBorder = Rgb(0x635681);
    private static readonly Color32 FieldBorder = Rgb(0xA0A0A0);
    private static readonly Color32 FieldUnfocusedInk = Rgb(0x909090);
    private static readonly Color32 FieldFocusedInk = Rgb(0xE0E0E0);
    private static readonly Color SlotHoverInk = new Color32(255, 255, 255, 96);
    private static readonly Color SlotSelectionInk = Rgb(0xFFD663);
    private static readonly Color IconDisabledTint = new Color(1f, 1f, 1f, .42f);
    private static SkinStyles? _styles;
    private static readonly Dictionary<string, Texture2D?> Sources = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Frames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D?> Backgrounds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(GUISkin, bool), GUISkin> ScrollSkins = new();
    private static GUIStyle? _recipePanelStyle;
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

        // Do not scale GUI.matrix for an individual amount. Recipe slots are often
        // painted inside a scroll view, and ScaleAroundPivot resolves its pivot
        // through the current GUI clip. That makes a lower-left liquid label drift
        // outside the slot as the recipe scrolls. Pick a smaller text style instead,
        // then anchor the untransformed label rect to the slot corner.
        const float normalScale = .76f;
        const int largestFontSize = 10, smallestFontSize = 1;
        var alignment = amount.Liquid ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        var styles = Styles;
        var content = styles.LabelContent;
        float availableWidth = Mathf.Max(1f, rect.width - 2f);
        float availableHeight = Mathf.Max(1f, rect.height - 2f);

        // Keep the old 13px layout's notation decision: a label was previously
        // measured at 13px and rendered at 76% of that size. Selecting the text
        // before choosing the final font size prevents a smaller style from
        // changing 1250mL into a less precise suffix unnecessarily.
        var measurement = styles.Label(alignment, false);
        string text = amount.Fit(value => FitsScaled(measurement, value));
        GUIStyle? ink = null;
        GUIStyle? shadow = null;
        Vector2 size = default;
        for (int fontSize = largestFontSize; fontSize >= smallestFontSize; fontSize--)
        {
            var candidate = styles.Label(alignment, false, fontSize);
            content.text = text;
            Vector2 measured = candidate.CalcSize(content) + Vector2.one * 2f;
            if (!FitsSize(measured)) continue;
            ink = candidate;
            shadow = styles.LabelShadow(alignment, fontSize);
            size = measured;
            break;
        }

        // Very narrow custom layouts may still be smaller than the minimum
        // readable font. Clip to the slot rather than allowing text to escape it.
        ink ??= styles.Label(alignment, false, smallestFontSize);
        shadow ??= styles.LabelShadow(alignment, smallestFontSize);
        content.text = text;
        if (size == default)
        {
            size = ink.CalcSize(content) + Vector2.one * 2f;
            size.x = Mathf.Min(size.x, availableWidth);
            size.y = Mathf.Min(size.y, availableHeight);
        }

        var target = amount.LabelBounds(rect, size);
        Paint(shadow, Offset(target), content);
        Paint(ink, target, content);

        bool FitsScaled(GUIStyle style, string value)
        {
            content.text = value;
            return style.CalcSize(content).x + 2f <= availableWidth / normalScale;
        }

        bool FitsSize(Vector2 measured) => measured.x <= availableWidth && measured.y <= availableHeight;
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

    /// <summary>Draws a raised action or option and returns whether the click completed.</summary>
    internal static bool Button(Rect rect, string text, bool enabled = true, bool selected = false, string? tooltip = null)
    {
        SkinStyles styles = Styles;
        return StatefulButton(rect, new GUIContent(text, tooltip), enabled,
            selected ? styles.ButtonSelected : styles.Button, styles.ButtonDisabled);
    }

    /// <summary>Draws an icon action with the same input and visual state as a text button.</summary>
    internal static bool IconButton(Rect rect, string icon, string tooltip, bool enabled = true, bool selected = false, bool backed = false,
        float maxIconSize = float.PositiveInfinity, bool enlargeIcon = false, Rect? inputClip = null)
    {
        bool clicked = HandleButtonEvent(rect, enabled, out var state, inputClip);
        if (CanPaint(rect))
        {
            SetTooltip(tooltip, state.Hovered);
            PaintIconButton(rect, icon, selected, backed, state, maxIconSize, enlargeIcon);
        }
        return clicked;
    }

    private static void PaintIconButton(Rect hit, string icon, bool selected, bool backed, ButtonVisual state,
        float maxSize, bool enlarge)
    {
        Rect face = state.Face(hit);
        if (!CanPaint(face)) return;
        bool builtIn = BuiltInControlArt.IsQuantitySymbol(icon);
        if (backed || builtIn || ModernityFrames.HasWidgetBackplate(icon))
        {
            var style = !state.Enabled ? Styles.WidgetDisabled : selected ? Styles.WidgetSelected : Styles.Widget;
            DrawButtonFrame(face, style, hovered: state.Hovered);
        }

        if (builtIn)
        {
            var (bar, stem) = BuiltInControlArt.QuantitySymbol(face, plus: icon == "add", maxSize);
            if (bar.width > 0f) Fill(bar, state.SymbolInk);
            if (stem.width > 0f) Fill(stem, state.SymbolInk);
        }
        else if (CropFrame(ModernityFrames.IconGraphic(icon, selected)) is { } texture)
        {
            DrawButtonSprite(face, texture, maxSize, enlarge, state.SpriteTint);
        }
    }

    private static bool StatefulButton(Rect rect, GUIContent content, bool enabled, GUIStyle normal, GUIStyle disabled)
    {
        bool clicked = HandleButtonEvent(rect, enabled, out var state);
        Rect face = state.Face(rect);
        if (CanPaint(face))
        {
            SetTooltip(content.tooltip, state.Hovered);
            DrawButtonFrame(face, state.Enabled ? normal : disabled, content, state.Hovered);
        }
        return clicked;
    }

    private static bool HandleButtonEvent(Rect rect, bool enabled, out ButtonVisual state, Rect? inputClip = null)
    {
        int id = GUIUtility.GetControlID(ButtonControlHint, FocusType.Passive, rect);
        var e = Event.current;
        bool interactive = GUI.enabled && enabled;
        bool inside = rect.Contains(e.mousePosition) && (inputClip == null || inputClip.Value.Contains(e.mousePosition));
        state = ButtonVisual.Resolve(interactive, inside);
        bool ownsPress = _pressedButton == id && GUIUtility.hotControl == id;
        var input = ButtonVisual.Input(e.GetTypeForControl(id), e.button, interactive, inside, ownsPress, GUIUtility.hotControl == 0);
        if (input == ButtonInput.Capture)
            GUIUtility.hotControl = _pressedButton = id;
        else if (input is ButtonInput.Release or ButtonInput.Click)
            CancelButtonPress();
        if (input != ButtonInput.None) e.Use();
        return input == ButtonInput.Click;
    }

    private static void DrawButtonFrame(Rect rect, GUIStyle style, GUIContent? caption = null, bool hovered = false)
    {
        var border = style.border;
        bool fits = ModernityFrames.FitsButtonBorders(rect, border.left, border.right, border.top, border.bottom);
        // The language selector intentionally has a wider right slice to carry its arrow.
        if (fits && border.left != border.right)
        {
            style.Draw(rect, caption ?? GUIContent.none, hovered, false, false, false);
            return;
        }

        var state = hovered ? style.hover : style.normal;
        if (state.background is { } texture && texture &&
            (!fits || !DrawPixelAlignedButton(rect, texture, border.left, border.top, border.bottom)))
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        if (caption != null)
            Styles.ButtonCaption(style).Draw(rect, caption, hovered, false, false, false);
    }

    private static bool DrawPixelAlignedButton(Rect rect, Texture2D texture, int side, int top, int bottom)
    {
        if (texture.width <= 2 * side || texture.height <= top + bottom) return false;
        // Align only the artwork in the current GUI clip/scale. Input and captions use the original rect.
        Rect aligned = GUIUtility.AlignRectToDevice(rect, out int width, out int height);
        if (width <= 0 || height <= 0 || aligned.width <= 0 || aligned.height <= 0) return false;
        float scaleX = width / aligned.width, scaleY = height / aligned.height;
        int topPx = ButtonSlicePixels.EdgePixels(top, scaleY), bottomPx = ButtonSlicePixels.EdgePixels(bottom, scaleY);
        if (width <= 2 * ButtonSlicePixels.EdgePixels(side, scaleX) || height <= topPx + bottomPx) return false;

        float dx = aligned.width / width, dy = aligned.height / height;
        for (int column = 0; column <= 2 * side; column++)
        {
            var (start, end, sourceStart, sourceEnd) = ButtonSlicePixels.Column(column, width, texture.width, side, scaleX);
            if (start == end) continue;
            float x0 = aligned.x + start * dx, x1 = aligned.x + end * dx;
            DrawStrip(0, topPx, 0, top);
            DrawStrip(topPx, height - bottomPx, top, texture.height - bottom);
            DrawStrip(height - bottomPx, height, texture.height - bottom, texture.height);

            void DrawStrip(int y0, int y1, int srcTop, int srcBottom)
            {
                if (y0 == y1) return;
                var dst = Rect.MinMaxRect(x0, aligned.y + y0 * dy, x1, aligned.y + y1 * dy);
                var uv = Rect.MinMaxRect((float)sourceStart / texture.width, 1f - (float)srcBottom / texture.height,
                    (float)sourceEnd / texture.width, 1f - (float)srcTop / texture.height);
                GUI.DrawTextureWithTexCoords(dst, texture, uv);
            }
        }
        return true;
    }

    private static void SetTooltip(string tooltip, bool hovered)
    {
        if (hovered && !string.IsNullOrEmpty(tooltip)) GUI.tooltip = tooltip;
    }

    private static void DrawButtonSprite(Rect rect, Texture2D texture, float maxSize, bool enlarge, Color tint)
    {
        Rect target = ModernityFrames.FitButtonSprite(rect, texture.width, texture.height, maxSize, enlarge);
        if (target.width > 0f) DrawTexture(target, texture, ScaleMode.ScaleToFit, tint);
    }

    private static readonly int ButtonControlHint = "NeiButton".GetHashCode();
    private static int _pressedButton;

    // A control may stop drawing before MouseUp (for example, if Escape closes options).
    internal static void CancelButtonPress()
    {
        if (_pressedButton == 0) return;
        if (GUIUtility.hotControl == _pressedButton) GUIUtility.hotControl = 0;
        _pressedButton = 0;
    }

    // Keep the whole row clickable; draw the original hover-sensitive switch sprite without a backplate.
    internal static bool Checkbox(Rect rect, string text, bool on)
    {
        bool clicked = HandleButtonEvent(rect, enabled: true, out var state);
        if (!CanPaint(rect)) return clicked;

        var styles = Styles;
        styles.LabelContent.text = text;
        Paint(styles.CheckboxLabel, rect, styles.LabelContent);
        var indicator = new Rect(rect.x + 3f, rect.y + 1f, styles.CheckboxLabel.padding.left - 6f, rect.height - 2f);
        if (CropFrame(ModernityFrames.CheckboxFace(on, state.Hovered)) is { } texture)
            DrawButtonSprite(indicator, texture, float.PositiveInfinity, enlarge: true, GUI.enabled ? Color.white : IconDisabledTint);
        else
            Label(indicator, on ? "✓" : "–", TextAnchor.MiddleCenter);
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
        // Center the full 12×12 source frame in the original 16×16 indicator lane.
        const float radioSize = ModernityFrames.RadioFaceSize;
        var square = new Rect(rect.x + 5f, rect.y + (SpawnChoiceTitleHeight - radioSize) * .5f, radioSize, radioSize);
        string face = ModernityFrames.SquareCheckboxFace(selected, hovered);
        if (CropFrame(face) is { } texture)
            DrawTexture(square, texture, ScaleMode.ScaleToFit, GUI.enabled ? Color.white : IconDisabledTint);

        // Keep both choices' copy aligned; hovering changes only the face, not its content.
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
        DropDownRow(rect, text, expanded ? "dropdown_open" : "dropdown_closed", arrow: true);

    internal static bool DropDownOption(Rect rect, string text, bool selected) =>
        DropDownRow(rect, text, "dropdown_option", arrow: false, selected: selected);

    private static bool DropDownRow(Rect rect, string text, string frame, bool arrow, bool selected = false)
    {
        bool clicked = HandleButtonEvent(rect, enabled: true, out var state);
        if (!CanPaint(rect)) return clicked;

        var (suffix, offset) = ModernityFrames.DropDownState(state, selected);
        var styles = Styles;
        styles.LabelContent.text = text;
        var visual = new Rect(rect.x, rect.y + offset, rect.width, rect.height - offset);
        if (visual.height > 0f)
            DrawButtonFrame(visual, styles.DropDownFace(frame + suffix, arrow, selected || state.Hovered), styles.LabelContent);
        return clicked;
    }

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

    /// <summary>Draws one icon sprite from the shared atlas.</summary>
    internal static void Icon(Rect rect, string icon)
    {
        if (!CanPaint(rect) || string.IsNullOrWhiteSpace(icon))
            return;
        if (CropFrame(icon) is { } texture)
            DrawTexture(rect, texture, ScaleMode.ScaleToFit, Color.white);
    }

    // Sidebars are transparent unless the player supplies a background override.
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

    /// <summary>Draws the shared Modernity panel frame without stretching its border.</summary>
    internal static void RecipePanel(Rect rect)
    {
        if (!CanPaint(rect)) return;
        GUIStyle style = _recipePanelStyle ??= CreateRecipePanelStyle();
        if (style.normal.background == null) Fill(rect, Rgb(0xC8CAD4));
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

    private static GUIStyle CreateRecipePanelStyle()
    {
        var style = new GUIStyle();
        Texture2D? frame = CropFrame("recipe_background");
        style.border = new RectOffset(NeiAssets.RecipeBackgroundSlice, NeiAssets.RecipeBackgroundSlice,
            NeiAssets.RecipeBackgroundSlice, NeiAssets.RecipeBackgroundSlice);
        style.normal.background = frame;
        style.normal.textColor = Color.white;
        return style;
    }

    internal static readonly Color32 RecipeBarFill = Rgb(0xA5A6AC);

    internal static void RecipeBar(Rect rect)
    {
        if (CanPaint(rect)) Fill(rect, RecipeBarFill);
    }

    // Use the native scrolling/clipping logic, but swap the track and thumb art
    // for this call only. The game's shared skin and its styles stay untouched.
    internal static Vector2 BeginScrollView(Rect viewport, Vector2 scroll, Rect content, bool horizontal, bool vertical, bool recipe = false)
    {
        var original = GUI.skin;
        if (!ScrollSkins.TryGetValue((original, recipe), out var skin))
        {
            skin = UnityEngine.Object.Instantiate(original);
            skin.hideFlags = HideFlags.HideAndDontSave;
            skin.horizontalScrollbar = ScrollFace(original.horizontalScrollbar, "scroll_horizontal_track", 2, 3);
            skin.horizontalScrollbarThumb = ScrollFace(original.horizontalScrollbarThumb, "scroll_horizontal_thumb", 2, 2);
            skin.verticalScrollbar = ScrollFace(original.verticalScrollbar, recipe ? "scroll_recipe_track" : "scroll_vertical_track", 4, 3);
            skin.verticalScrollbarThumb = ScrollFace(original.verticalScrollbarThumb, "scroll_vertical_thumb", 2, 2);
            ScrollSkins.Add((original, recipe), skin);
        }

        try
        {
            GUI.skin = skin;
            return GUI.BeginScrollView(viewport, scroll, content, horizontal, vertical);
        }
        finally { GUI.skin = original; }
    }

    private static GUIStyle ScrollFace(GUIStyle original, string frame, int xBorder, int yBorder)
    {
        var style = new GUIStyle(original);
        if (CropFrame(frame) is not { } texture) return style;
        style.border = new RectOffset(xBorder, xBorder, yBorder, yBorder);
        SkinStyles.ApplyStates(style, texture, texture, original.normal.textColor, original.focused.textColor);
        return style;
    }

    internal static void RecipeSlot(Rect rect)
    {
        if (!CanPaint(rect)) return;
        if (CropFrame("recipe_slot") is { } texture)
            DrawTexture(rect, texture, ScaleMode.StretchToFill, Color.white);
    }

    internal static void RecipeArrow(Rect rect)
    {
        if (CanPaint(rect) && CropFrame("recipe_arrow") is { } texture)
            DrawButtonSprite(rect, texture, maxSize: 16f, enlarge: false, Color.white);
    }

    /// <summary>Draws an option tab. The caller owns which tab is selected.</summary>
    internal static bool Tab(Rect rect, string text, bool selected, bool enabled = true) =>
        Button(rect, text, enabled, selected);

    // The tab pager handles presses separately, so draw only the original widget face and icon here.
    internal static void RecipeTabArrow(Rect rect, string icon)
    {
        if (!CanPaint(rect)) return;
        var state = ButtonVisual.Resolve(GUI.enabled, rect.Contains(Event.current.mousePosition));
        PaintIconButton(rect, icon, selected: false, backed: true, state, float.PositiveInfinity, enlarge: false);
    }

    internal static bool RecipeTab(RecipePanelLayout panel, Rect rect, bool selected, string tooltip)
    {
        var content = new GUIContent(string.Empty, tooltip);
        // Keep native input, but never let GUI.Button paint a pressed frame, even without the tab texture.
        bool clicked = GUI.Button(rect, content, GUIStyle.none);
        if (CanPaint(rect))
        {
            var (body, highlight, fill) = panel.TabFace(rect);
            if (CropFrame(selected ? NeiAssets.RecipeTabSelected : NeiAssets.RecipeTabCandidate) is { } texture)
            {
                Color previous = GUI.color;
                try
                {
                    GUI.color = Color.white;
                    // Draw the single highlight texel as its own strip so point sampling cannot skip it.
                    const int seam = NeiAssets.RecipeTabHighlightRow;
                    DrawBand(body, 0, seam);
                    DrawBand(highlight, seam, seam + 1);
                    DrawBand(fill, seam + 1, texture.height);
                }
                finally { GUI.color = previous; }

                void DrawBand(Rect target, int startRow, int endRow)
                {
                    // Snap shared edges, not the input or caption rectangles.
                    Rect aligned = GUIUtility.AlignRectToDevice(target, out int width, out int height);
                    if (width <= 0 || height <= 0) return;
                    var uv = new Rect(0, 1f - (float)endRow / texture.height, 1,
                        (float)(endRow - startRow) / texture.height);
                    GUI.DrawTextureWithTexCoords(aligned, texture, uv);
                }
            }
            else
            {
                bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
                var face = Rect.MinMaxRect(body.xMin, body.yMin, body.xMax, fill.yMax);
                DrawButtonFrame(face, selected ? Styles.ButtonSelected : Styles.Button, hovered: hovered);
            }
        }
        return clicked;
    }

    internal static void RecipeMethodTitle(Rect rect, string text) => DrawRecipeCaption(rect, text, Styles.RecipeMethodTitle);

    internal static void RecipeTabLabel(Rect rect, string text) => DrawRecipeCaption(rect, text, Styles.RecipeTabLabel);

    private static void DrawRecipeCaption(Rect rect, string text, GUIStyle style)
    {
        if (!CanPaint(rect)) return;
        Styles.LabelContent.text = text;
        Paint(style, rect, Styles.LabelContent);
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
        bool copyIdEnabled, bool copyModEnabled)
    {
        var title = FormatItemText(content.Name);
        var body = FormatItemBody(content);
        return DrawTooltip(anchor, guiSize, title.Rich, body.Rich, title.Shadow, body.Shadow, content,
            copyIdEnabled, copyModEnabled);
    }

    /// <summary>Draws a non-interactive tooltip with a background; call outside any clipping groups.</summary>
    internal static void Tooltip(Vector2 mousePosition, Vector2 guiSize, string text, string? subtitle = null) =>
        DrawTooltip(mousePosition, guiSize, text, subtitle, null, null, null);

    private static (Rect Box, Rect IdRow, Rect ModRow) DrawTooltip(Vector2 mousePosition, Vector2 guiSize, string text, string? subtitle,
        string? titleShadow, string? bodyShadow, NativeTooltip.Content? item, bool copyIdEnabled = false, bool copyModEnabled = false)
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
        TooltipBox(box);

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
        _recipePanelStyle = null;
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
        if (ModernityFrames.Find(name) is not { } frame) return null;
        bool back = name == "back";
        var source = GetSource(frame.ResourcePath, readable: true);
        Texture2D? cropped = Crop(source);
        if (cropped == null && back)
        {
            // Keep the rest of a replacement atlas; fall back only for its missing Back artwork.
            string resource = NeiAssets.ResourceName(frame.ResourcePath);
            string key = "embedded:" + resource;
            if (!Sources.TryGetValue(key, out var embedded))
            {
                embedded = TryLoadEmbedded(resource, readable: true);
                Sources.Add(key, embedded);
            }
            cropped = Crop(embedded);
        }
        Frames.Add(name, cropped);
        return cropped;

        Texture2D? Crop(Texture2D? atlas)
        {
            if (atlas == null || atlas.width < frame.X + frame.Width || atlas.height < frame.Y + frame.Height) return null;
            int x = frame.X, width = frame.Width, height = frame.Height;
            int bottom = atlas.height - frame.Y - height;
            var pixels = atlas.GetPixels(x, bottom, width, height);
            if (back && !ModernityFrames.HasBackArtwork(pixels)) return null;
            if (back || name is "favorite" or "favorite_selected")
            {
                // Fit and center the actual artwork, not the transparent atlas padding.
                var ink = ModernityFrames.InkBounds(width, height, pixels);
                width = ink.Width;
                height = ink.Height;
                pixels = atlas.GetPixels(x + ink.X, bottom + ink.Y, width, height);
            }
            var texture = NewTexture(width, height, "NEI: " + name);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
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
        private readonly GUIStyle?[,,] _labelsBySize = new GUIStyle?[9, 2, 14];
        private readonly GUIStyle?[,] _labelShadowsBySize = new GUIStyle?[9, 14];
        internal readonly GUIStyle Wrapped;
        internal readonly GUIStyle WrappedShadow;
        internal readonly GUIStyle Button;
        internal readonly GUIStyle ButtonSelected;
        internal readonly GUIStyle ButtonDisabled;
        internal readonly GUIStyle Widget, WidgetSelected, WidgetDisabled;
        internal readonly GUIStyle CheckboxLabel, OptionsHeading, OptionsNote;
        private readonly Dictionary<string, GUIStyle?> _dropDownFaces = new(StringComparer.Ordinal);
        private readonly Dictionary<GUIStyle, GUIStyle> _buttonCaptions = new();
        internal readonly GUIStyle RecipeMethodTitle, RecipeTabLabel;
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
            Color darkInk = ButtonInk;
            Button = ButtonStyle(normal, active, darkInk);
            ButtonSelected = ButtonStyle(active, active, darkInk);
            ButtonDisabled = ButtonStyle(disabled, disabled, OverlayMuted);
            Widget = ButtonStyle(normal, active, darkInk, originalWidget: true);
            WidgetSelected = ButtonStyle(active, active, darkInk, originalWidget: true);
            WidgetDisabled = ButtonStyle(disabled, disabled, OverlayMuted, originalWidget: true);

            CheckboxLabel = ContentStyle(TextAnchor.MiddleLeft, 4);
            CheckboxLabel.padding.left = 48;
            ApplyStates(CheckboxLabel, null, null, darkInk, darkInk);
            OptionsHeading = new GUIStyle(TextStyle(TextAnchor.MiddleLeft, darkInk, 13)) { fontStyle = FontStyle.Bold };
            OptionsNote = TextStyle(TextAnchor.UpperLeft, Rgb(0x404459), 12, wrap: true);

            RecipeTabLabel = TextStyle(TextAnchor.MiddleCenter, darkInk, 13);
            RecipeMethodTitle = ContentStyle(TextAnchor.MiddleCenter, 2);
            RecipeMethodTitle.normal.textColor = darkInk;

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

        internal GUIStyle Label(TextAnchor alignment, bool muted, int fontSize)
        {
            int row = muted ? 1 : 0;
            fontSize = Mathf.Clamp(fontSize, 1, 13);
            return _labelsBySize[(int)alignment, row, fontSize] ??= TextStyle(alignment, muted ? OverlayMuted : OverlayInk, fontSize);
        }

        internal GUIStyle LabelShadow(TextAnchor alignment) =>
            _labelShadows[(int)alignment] ??= TextStyle(alignment, Shadow, 13);

        internal GUIStyle LabelShadow(TextAnchor alignment, int fontSize)
        {
            fontSize = Mathf.Clamp(fontSize, 1, 13);
            return _labelShadowsBySize[(int)alignment, fontSize] ??= TextStyle(alignment, Shadow, fontSize);
        }

        private static GUIStyle TextStyle(TextAnchor alignment, Color color, int size, bool wrap = false)
        {
            var style = new GUIStyle();
            SetText(style, alignment, size, wrap);
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle ButtonStyle(Texture2D? normal, Texture2D? active, Color ink, bool originalWidget = false)
        {
            var style = ContentStyle(TextAnchor.MiddleCenter, originalWidget ? 4 : 1);
            const int edge = ModernityFrames.ButtonSlice;
            style.border = new RectOffset(edge, edge, edge, originalWidget ? edge : ModernityFrames.TextButtonBottom);
            ApplyStates(style, normal, active, ink, ink);
            return style;
        }

        internal GUIStyle ButtonCaption(GUIStyle style)
        {
            if (_buttonCaptions.TryGetValue(style, out var caption)) return caption;
            caption = new GUIStyle(style);
            ApplyStates(caption, null, null, style.normal.textColor, style.focused.textColor);
            _buttonCaptions.Add(style, caption);
            return caption;
        }

        internal GUIStyle DropDownFace(string name, bool arrow, bool highlighted)
        {
            if (!_dropDownFaces.TryGetValue(name, out var style))
            {
                if (CropFrame(name) is { } face)
                {
                    // Trigger glyphs occupy x=94–102, y=6–10 in every source frame.
                    // Keep them entirely in the fixed top-right corner, not a stretched edge strip.
                    int right = arrow ? 21 : 7;
                    style = ContentStyle(TextAnchor.MiddleLeft, 8);
                    style.padding.right = right;
                    style.border = new RectOffset(2, right, arrow ? 11 : 2, 5);
                    style.normal.background = face;
                    style.normal.textColor = Rgb(0x232538);
                }
                _dropDownFaces.Add(name, style);
            }
            return style ?? (!GUI.enabled ? ButtonDisabled : highlighted ? ButtonSelected : Button);
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

        internal static void ApplyStates(GUIStyle style, Texture2D? normal, Texture2D? active, Color ink, Color focusInk)
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
