from __future__ import annotations

"""Generate Nexus release artwork from the repository's existing NEI UI textures.

Every visible UI element is cut from a tracked PNG under
Assets/NotEnoughItems/textures/gui using the same frame coordinates the mod
itself reads (see src/NotEnoughItems/UI/Styling/ModernityFrames.cs). The
frames are assembled into a mock NEI screen at GUI scale 1 and then enlarged
by an integer factor with nearest-neighbour sampling, so no source pixel is
blurred or redrawn. Only the backdrop gradient, hairlines and typography are
generated; no new icons or textures are painted.
"""

import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "nexus_assets"
OUT.mkdir(exist_ok=True)
ASSET_ROOT = ROOT / "Assets" / "NotEnoughItems" / "textures" / "gui"

GRAPHITE = (9, 15, 23)
SLATE = (20, 32, 46)
CYAN = (53, 214, 232)
ICE = (189, 239, 244)
AMBER = (255, 180, 84)
OFFWHITE = (234, 242, 244)
MUTED = (154, 174, 188)
INK = (40, 42, 56)  # tooltip/label ink matching the UI's dark outline tone

FONT_DIR = Path(r"C:\Windows\Fonts")
_ASSET_CACHE: dict[str, Image.Image] = {}
USED_SOURCES: set[str] = set()


def font(name: str, size: int):
    candidates = {
        "display": [FONT_DIR / "segoeuib.ttf", FONT_DIR / "arialbd.ttf"],
        "body": [FONT_DIR / "segoeui.ttf", FONT_DIR / "arial.ttf"],
        "mono": [FONT_DIR / "consola.ttf", FONT_DIR / "cour.ttf"],
    }[name]
    for path in candidates:
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def asset(path: str) -> Image.Image:
    """Load a tracked UI texture as RGBA without altering its pixels."""
    if path not in _ASSET_CACHE:
        _ASSET_CACHE[path] = Image.open(ASSET_ROOT.joinpath(*path.split("/"))).convert("RGBA")
    USED_SOURCES.add(path)
    return _ASSET_CACHE[path]


def strip_placeholder(image: Image.Image) -> Image.Image:
    """Make the atlas' two documented editor-grid colors transparent."""
    out = image.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if (r, g, b) in {(255, 0, 170), (255, 190, 170)}:
                px[x, y] = (r, g, b, 0)
    return out


def frame(path: str, x: int, y: int, w: int, h: int) -> Image.Image:
    """Cut one frame exactly as ModernityFrames.cs addresses it."""
    im = asset(path)
    if path == "sprites/interface_states.png":
        im = strip_placeholder(im)
    return im.crop((x, y, x + w, y + h))


# Frame table mirrored from src/NotEnoughItems/UI/Styling/ModernityFrames.cs
STATES = "sprites/interface_states.png"
WIDGETS = "sprites/button_backgrounds.png"
TEXTFIELD = "sprites/search_and_tooltip.png"
DROPDOWN = "sprites/language_dropdown.png"
CHECKBOX = "sprites/toggle_and_radio.png"
FRAMES = {
    "inventory": ("panels/inventory.png", 0, 0, 199, 204),
    "potion_editor": ("panels/potion_editor.png", 0, 0, 176, 207),
    "recipe_background": ("panels/recipe_background.png", 0, 0, 184, 174),
    "recipe_slot_panel": ("panels/recipe_slot.png", 0, 0, 18, 18),
    "repair_recipe": ("panels/repair_recipe.png", 0, 0, 168, 36),
    "catalyst_tab": ("panels/catalyst_tab.png", 0, 0, 36, 36),
    "button": (WIDGETS, 0, 66, 200, 20),
    "button_hover": (WIDGETS, 0, 86, 200, 20),
    "button_disabled": (WIDGETS, 0, 46, 200, 20),
    "bookmark": (STATES, 0, 0, 32, 32),
    "bookmark_selected": (STATES, 32, 0, 32, 32),
    "wrench": (STATES, 64, 0, 32, 32),
    "wrench_alt": (STATES, 96, 0, 32, 32),
    "options_cheat": (STATES, 128, 0, 32, 32),
    "search_icon": (STATES, 0, 32, 16, 16),
    "favorite": (STATES, 32, 32, 16, 16),
    "favorite_selected": (STATES, 48, 32, 16, 16),
    "copy": (STATES, 0, 64, 16, 16),
    "paste": (STATES, 16, 64, 16, 16),
    "next": (STATES, 32, 64, 16, 16),
    "previous": (STATES, 48, 64, 16, 16),
    "back": (STATES, 0, 80, 16, 16),
    "close": (STATES, 16, 80, 16, 16),
    "recipe_tab_selected": (STATES, 160, 112, 48, 47),
    "recipe_tab_candidate": (STATES, 208, 112, 48, 47),
    "recipe_slot": (STATES, 161, 209, 18, 18),
    "scroll_vertical_track": (STATES, 192, 161, 16, 22),
    "scroll_vertical_thumb": (STATES, 192, 192, 12, 18),
    "search_field": (TEXTFIELD, 0, 12, 128, 12),
    "search_field_focused": (TEXTFIELD, 0, 0, 128, 12),
    "tooltip_background": (TEXTFIELD, 0, 0, 128, 12),
    "dropdown_closed": (DROPDOWN, 0, 0, 112, 20),
    "dropdown_open": (DROPDOWN, 112, 0, 112, 20),
    "dropdown_option": (DROPDOWN, 0, 128, 112, 19),
    "dropdown_option_hover": (DROPDOWN, 0, 192, 112, 18),
    "checkbox_off": (CHECKBOX, 0, 28, 22, 12),
    "checkbox_on": (CHECKBOX, 0, 40, 22, 12),
    "square_checkbox_off": (CHECKBOX, 29, 1, 12, 12),
    "square_checkbox_on": (CHECKBOX, 29, 15, 12, 12),
}


def sprite(name: str) -> Image.Image:
    return frame(*FRAMES[name])


def nine_slice(src: Image.Image, size: tuple[int, int], border: int | tuple[int, int, int, int]) -> Image.Image:
    """Stretch a frame to `size` keeping its corners; edges/centre are tiled by
    nearest resampling so the pixel art stays crisp (same rule the mod uses)."""
    if isinstance(border, int):
        l = r = t = b = border
    else:
        l, r, t, b = border
    sw, sh = src.size
    tw, th = size
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    cols = [(0, l, 0, l), (l, sw - r, l, tw - r), (sw - r, sw, tw - r, tw)]
    rows = [(0, t, 0, t), (t, sh - b, t, th - b), (sh - b, sh, th - b, th)]
    for sx0, sx1, dx0, dx1 in cols:
        for sy0, sy1, dy0, dy1 in rows:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.Resampling.NEAREST)
            out.alpha_composite(piece, (dx0, dy0))
    return out


def pixel_text(draw: ImageDraw.ImageDraw, xy, value: str, fill, size: int = 8, anchor="la"):
    """Small monospace labels standing in for the game's bitmap font."""
    draw.text(xy, value, font=font("mono", size), fill=fill, anchor=anchor)


def mock_screen() -> Image.Image:
    """Assemble a plausible NEI session at GUI scale 1 from tracked frames.

    Layout (logical pixels): the player's inventory panel sits on the left, a
    recipe viewer in the middle, and the NEI item catalog column with its
    search field, paging buttons, bookmark/options strip and scrollbar on the
    right. All glyph cells are left empty: the mod ships no item icons, so we
    show the chrome only.
    """
    W, H = 528, 262
    scr = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # -- settings row: language dropdown and the two checkbox styles ----------
    scr.alpha_composite(nine_slice(sprite("dropdown_closed"), (84, 20), (2, 21, 2, 5)), (10, 0))
    d = ImageDraw.Draw(scr, "RGBA")
    pixel_text(d, (15, 5), "English", (*INK, 255), 9)
    scr.alpha_composite(sprite("checkbox_on"), (102, 4))
    scr.alpha_composite(sprite("square_checkbox_on"), (130, 4))
    scr.alpha_composite(sprite("square_checkbox_off"), (146, 4))

    # -- left: the player's inventory panel -----------------------------------
    scr.alpha_composite(sprite("inventory"), (10, 26))

    # -- middle: recipe viewer with handler tabs on top and a catalyst tab ----
    rx, ry = 246, 62
    # The 48x47 tab faces are flat fills with a 2px outline, so cutting them
    # down with a 6px nine-slice keeps every edge pixel intact.
    for i, name in enumerate(["recipe_tab_selected", "recipe_tab_candidate", "recipe_tab_candidate"]):
        scr.alpha_composite(nine_slice(sprite(name), (30, 26), 6), (rx + 10 + i * 32, ry - 22))
    scr.alpha_composite(nine_slice(sprite("recipe_background"), (184, 174), 9), (rx, ry))
    # recipe_background keeps a 4px transparent inset; butt the tab against the border.
    scr.alpha_composite(sprite("catalyst_tab"), (rx - 27, ry + 14))
    scr.alpha_composite(sprite("previous"), (rx + 8, ry + 8))
    scr.alpha_composite(sprite("next"), (rx + 184 - 24, ry + 8))
    d = ImageDraw.Draw(scr, "RGBA")
    pixel_text(d, (rx + 92, ry + 10), "Shaped Crafting", (*INK, 255), 9, anchor="ma")
    # 3x3 crafting grid -> result, built from the real slot frames at 1x.
    g0x, g0y = rx + 26, ry + 36
    for row in range(3):
        for col in range(3):
            scr.alpha_composite(sprite("recipe_slot"), (g0x + col * 18, g0y + row * 18))
    scr.alpha_composite(sprite("next"), (g0x + 66, g0y + 19))
    scr.alpha_composite(sprite("recipe_slot_panel"), (g0x + 98, g0y + 18))
    # Second handler entry: the repair row texture.
    scr.alpha_composite(sprite("repair_recipe"), (rx + 8, ry + 100))
    scr.alpha_composite(nine_slice(sprite("button_hover"), (72, 20), 3), (rx + 14, ry + 144))
    scr.alpha_composite(nine_slice(sprite("button"), (72, 20), 3), (rx + 98, ry + 144))
    d = ImageDraw.Draw(scr, "RGBA")
    pixel_text(d, (rx + 50, ry + 150), "Overlay", (*INK, 255), 9, anchor="ma")
    pixel_text(d, (rx + 134, ry + 150), "Bookmark", (*INK, 255), 9, anchor="ma")

    # -- right: NEI catalog column --------------------------------------------
    cx, cy = 444, 6
    # Paging buttons and page label above the grid.
    scr.alpha_composite(nine_slice(sprite("button"), (18, 14), 3), (cx - 6, cy))
    scr.alpha_composite(nine_slice(sprite("button"), (18, 14), 3), (cx + 40, cy))
    d = ImageDraw.Draw(scr, "RGBA")
    pixel_text(d, (cx + 3, cy + 3), "<", (*INK, 255), 9, anchor="la")
    pixel_text(d, (cx + 46, cy + 3), ">", (*INK, 255), 9, anchor="la")
    pixel_text(d, (cx + 26, cy + 3), "3/41", (*OFFWHITE, 255), 8, anchor="ma")
    # Item grid: 3 columns x 12 rows of recipe slots.
    for row in range(12):
        for col in range(3):
            scr.alpha_composite(sprite("recipe_slot"), (cx - 8 + col * 18, cy + 18 + row * 18))
    # Scrollbar on the far right.
    scr.alpha_composite(nine_slice(sprite("scroll_vertical_track"), (16, 216), (2, 2, 4, 4)), (cx + 48, cy + 18))
    scr.alpha_composite(sprite("scroll_vertical_thumb"), (cx + 50, cy + 60))
    # Search field along the bottom edge of the column, focused.
    scr.alpha_composite(nine_slice(sprite("search_field_focused"), (72, 12), 2), (cx - 8, cy + 238))
    d = ImageDraw.Draw(scr, "RGBA")
    scr.alpha_composite(sprite("search_icon").resize((10, 10), Image.Resampling.NEAREST), (cx - 7, cy + 239))
    pixel_text(d, (cx + 8, cy + 240), "@mod iron", (*INK, 255), 8)

    # -- bottom-left: utility strip (bookmark, options, favorite, copy/paste) --
    sx, sy = 14, 236
    for name, size in [("bookmark_selected", 16), ("wrench", 16), ("favorite_selected", 16), ("copy", 16), ("paste", 16)]:
        im = sprite(name)
        if im.width != size:
            im = im.resize((size, size), Image.Resampling.NEAREST)
        scr.alpha_composite(im, (sx, sy))
        sx += size + 6

    # -- tooltip hovering over a catalog cell ---------------------------------
    tip = nine_slice(sprite("tooltip_background"), (108, 28), 2)
    tx, ty = cx - 46, cy + 30
    scr.alpha_composite(tip, (tx, ty))
    d = ImageDraw.Draw(scr, "RGBA")
    pixel_text(d, (tx + 5, ty + 4), "Iron Ingot", (*INK, 255), 9)
    pixel_text(d, (tx + 5, ty + 16), "minecraft:iron_ingot", (62, 70, 92, 255), 7)
    return scr


def gradient(size, left=GRAPHITE, right=SLATE):
    w, h = size
    im = Image.new("RGB", size)
    p = im.load()
    for x in range(w):
        t = x / max(1, w - 1)
        base = tuple(round(left[i] * (1 - t) + right[i] * t) for i in range(3))
        for y in range(h):
            v = 0.92 + 0.08 * math.sin(math.pi * y / max(1, h - 1))
            p[x, y] = tuple(round(c * v) for c in base)
    return im.convert("RGBA")


def radial_glow(size, center, radius, color, alpha):
    w, h = size
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    px = layer.load()
    cx, cy = center
    for y in range(max(0, int(cy - radius)), min(h, int(cy + radius))):
        for x in range(max(0, int(cx - radius)), min(w, int(cx + radius))):
            d = math.hypot(x - cx, y - cy) / radius
            if d < 1:
                px[x, y] = (*color, int(alpha * ((1 - d) ** 2)))
    return layer


def grid(draw, size, step, inset=0, alpha=22):
    w, h = size
    line = (61, 87, 104, alpha)
    for x in range(inset, w - inset + 1, step):
        draw.line((x, inset, x, h - inset), fill=line, width=1)
    for y in range(inset, h - inset + 1, step):
        draw.line((inset, y, w - inset, y), fill=line, width=1)


def tracked(draw, xy, value, f, fill, tracking=2, anchor="la"):
    x, y = xy
    widths = [draw.textlength(ch, font=f) for ch in value]
    total = sum(widths) + tracking * max(0, len(value) - 1)
    if anchor == "ma":
        x -= total / 2
    elif anchor == "ra":
        x -= total
    for ch, w in zip(value, widths):
        draw.text((x, y), ch, font=f, fill=fill, anchor="la")
        x += w + tracking


def screen_at(scale: int, shadow: int = 18, crop: tuple[int, int, int, int] | None = None) -> Image.Image:
    """Integer-upscale the mock screen (or a crop of it) with a soft drop shadow."""
    scr = mock_screen()
    if crop:
        scr = scr.crop(crop)
    big = scr.resize((scr.width * scale, scr.height * scale), Image.Resampling.NEAREST)
    pad = shadow * 2
    canvas = Image.new("RGBA", (big.width + pad * 2, big.height + pad * 2), (0, 0, 0, 0))
    alpha = big.split()[3]
    sh = Image.new("RGBA", big.size, (0, 0, 0, 0))
    sh.putalpha(alpha.point(lambda a: int(a * 0.55)))
    from PIL import ImageFilter
    sh_layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sh_layer.alpha_composite(sh, (pad, pad + shadow // 2))
    sh_layer = sh_layer.filter(ImageFilter.GaussianBlur(shadow))
    canvas.alpha_composite(sh_layer)
    canvas.alpha_composite(big, (pad, pad))
    return canvas


def corner_marks(draw, box, length, color):
    x0, y0, x1, y1 = box
    draw.rectangle(box, outline=(*ICE, 32), width=1)
    for x, y, sx, sy in [(x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)]:
        draw.line((x, y, x + sx * length, y), fill=color, width=1)
        draw.line((x, y, x, y + sy * length), fill=color, width=1)


def draw_header():
    size = (1300, 372)
    base = gradient(size)
    base = Image.alpha_composite(base, radial_glow(size, (1040, 60), 360, CYAN, 80))
    base = Image.alpha_composite(base, radial_glow(size, (160, 360), 300, (45, 94, 132), 80))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 32, inset=24, alpha=14)
    base = Image.alpha_composite(base, overlay)

    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (60, 54), "NOT ENOUGH ITEMS  //  CASUALTIES: UNKNOWN", font("mono", 12), (*CYAN, 240), tracking=2.0)
    draw.text((58, 88), "Every recipe,", font=font("display", 50), fill=(*OFFWHITE, 255))
    draw.text((58, 142), "one keystroke away.", font=font("display", 50), fill=(*OFFWHITE, 255))
    tracked(draw, (61, 224), "ITEM CATALOG  ·  RECIPE VIEWER  ·  BOOKMARKS  ·  SEARCH", font("body", 15), (*MUTED, 235), tracking=1.4)
    draw.line((60, 262, 420, 262), fill=(*CYAN, 70), width=1)
    draw.text((60, 276), "In-game interface assembled from the mod's own GUI textures.", font=font("body", 12), fill=(*MUTED, 200))
    draw.text((60, 296), "Requires BepInEx 5 (Mono) and CUCoreLib 1.1+.", font=font("body", 12), fill=(*MUTED, 200))

    # Right: the recipe viewer and catalog column at 2x, cropped from the mock
    # screen so the banner shows the mod's actual working area, not a thumbnail.
    # Recipe viewer + catalog column at 2x: 160 logical rows -> 320px, which
    # sits inside the 28..349 frame with the tooltip just clipping the top edge.
    shot = screen_at(2, shadow=0, crop=(206, 36, 528, 196))
    base.alpha_composite(shot, (1300 - shot.width - 44, 26))
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (1240, 352), "GUI SCALE 2  ·  SOURCE TEXTURES ONLY", font("mono", 9), (*MUTED, 190), tracking=1.0, anchor="ra")
    corner_marks(draw, (28, 22, 1271, 349), 14, (*ICE, 100))

    out = OUT / "nexus_header_1300x372.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


def draw_showcase():
    size = (1920, 1080)
    base = gradient(size, left=(8, 14, 22), right=(18, 29, 43))
    base = Image.alpha_composite(base, radial_glow(size, (1180, 520), 620, CYAN, 70))
    base = Image.alpha_composite(base, radial_glow(size, (1700, 150), 400, (37, 83, 113), 80))
    base = Image.alpha_composite(base, radial_glow(size, (240, 980), 440, (24, 67, 91), 70))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 48, inset=72, alpha=12)
    base = Image.alpha_composite(base, overlay)

    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (96, 44), "NOT ENOUGH ITEMS  //  CASUALTIES: UNKNOWN", font("mono", 13), (*CYAN, 230), tracking=2)
    tracked(draw, (1824, 44), "v0.1.32  ·  BEPINEX 5  ·  CUCORELIB 1.1+", font("mono", 11), (*MUTED, 180), tracking=1.2, anchor="ra")
    draw.text((92, 190), "Find the", font=font("display", 64), fill=(*OFFWHITE, 255))
    draw.text((92, 258), "chain.", font=font("display", 64), fill=(*OFFWHITE, 255))
    draw.text((96, 366), "Search. Trace. Craft.", font=font("body", 24), fill=(*ICE, 230))

    features = [
        ("CATALOG", "Every item in the world, paged and searchable", CYAN),
        ("RECIPES", "Click any item to see how it is made and used", ICE),
        ("BOOKMARKS", "Pin the things you are hunting for", AMBER),
        ("SEARCH", "@mod, #tag and plain-text filters", MUTED),
    ]
    y = 440
    for label, blurb, c in features:
        draw.rounded_rectangle((96, y, 96 + 118, y + 26), radius=13, fill=(18, 30, 43, 150), outline=(*c, 150), width=1)
        tracked(draw, (155, y + 7), label, font("mono", 11), (*c, 225), tracking=1.1, anchor="ma")
        draw.text((228, y + 4), blurb, font=font("body", 15), fill=(*MUTED, 215))
        y += 44
    draw.line((96, 660, 330, 660), fill=(*MUTED, 80), width=1)
    draw.text((96, 676), "Shown: inventory panel, recipe viewer, item catalog with", font=font("body", 13), fill=(*MUTED, 180))
    draw.text((96, 696), "search field, utility strip and tooltip, all at GUI scale 2.", font=font("body", 13), fill=(*MUTED, 180))

    # Hero: the mock screen at 2x.
    shot = screen_at(2, shadow=22)
    base.alpha_composite(shot, (1888 - shot.width + 44, 540 - shot.height // 2))

    # Source strip: the untouched frames the screen was cut from, labelled.
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (96, 790), "FRAMES USED  /  ModernityFrames.cs", font("mono", 10), (*MUTED, 190), tracking=1.1)
    samples = [
        ("bookmark_selected", 2, "BOOKMARK"), ("wrench", 2, "OPTIONS"), ("favorite_selected", 3, "FAVORITE"),
        ("next", 3, "NEXT"), ("close", 3, "CLOSE"), ("recipe_slot", 3, "SLOT"),
        ("checkbox_on", 3, "SWITCH"), ("square_checkbox_on", 3, "RADIO"), ("scroll_vertical_thumb", 3, "SCROLL"),
    ]
    x = 96
    for name, s, label in samples:
        im = sprite(name)
        im = im.resize((im.width * s, im.height * s), Image.Resampling.NEAREST)
        cell = Image.new("RGBA", (72, 72), (8, 15, 23, 200))
        cell.alpha_composite(im, ((72 - im.width) // 2, (72 - im.height) // 2))
        ImageDraw.Draw(cell, "RGBA").rectangle((0, 0, 71, 71), outline=(*ICE, 60), width=1)
        base.alpha_composite(cell, (x, 812))
        tracked(draw, (x + 36, 892), label, font("mono", 8), (*MUTED, 170), tracking=0.8, anchor="ma")
        x += 84

    draw = ImageDraw.Draw(base, "RGBA")
    corner_marks(draw, (72, 58, 1848, 1022), 20, (*ICE, 90))
    tracked(draw, (1824, 1000), "SCREEN MOCK-UP  ·  NO ITEM ICONS SHIPPED  ·  SOURCE TEXTURES ONLY", font("mono", 9), (*MUTED, 170), tracking=1.0, anchor="ra")

    out = OUT / "nexus_showcase_1920x1080.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


if __name__ == "__main__":
    print(draw_header())
    print(draw_showcase())
    print("sources:", ", ".join(sorted(USED_SOURCES)))
