from __future__ import annotations

"""Generate release artwork from the repository's existing NEI UI textures.

This script deliberately composes only source textures already tracked under
Assets/NotEnoughItems/textures/gui. Decorative geometry is limited to layout
rules, labels, and masking needed to present those source textures.
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
SLATE_2 = (26, 42, 58)
CYAN = (53, 214, 232)
ICE = (189, 239, 244)
AMBER = (255, 180, 84)
MINT = (120, 224, 181)
OFFWHITE = (234, 242, 244)
MUTED = (154, 174, 188)

FONT_DIR = Path(r"C:\Windows\Fonts")


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


def asset(*parts: str) -> Image.Image:
    """Load a tracked UI texture as RGBA without altering its pixels."""
    return Image.open(ASSET_ROOT.joinpath(*parts)).convert("RGBA")


def text(draw, xy, value, f, fill, anchor=None, spacing=4):
    draw.text(xy, value, font=f, fill=fill, anchor=anchor, spacing=spacing)


def tracked(draw, xy, value, f, fill, tracking=2, anchor="la"):
    x, y = xy
    if anchor == "ma":
        widths = [draw.textlength(ch, font=f) for ch in value]
        x -= (sum(widths) + tracking * max(0, len(value) - 1)) / 2
    elif anchor == "ra":
        widths = [draw.textlength(ch, font=f) for ch in value]
        x -= sum(widths) + tracking * max(0, len(value) - 1)
    for ch in value:
        draw.text((x, y), ch, font=f, fill=fill, anchor="la")
        x += draw.textlength(ch, font=f) + tracking


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


def mask_atlas_placeholder(image: Image.Image) -> Image.Image:
    """Make the atlas' two documented placeholder colors transparent for display."""
    masked = image.copy().convert("RGBA")
    pixels = masked.load()
    for y in range(masked.height):
        for x in range(masked.width):
            r, g, b, a = pixels[x, y]
            if (r, g, b) in {(255, 0, 170), (255, 190, 170)}:
                pixels[x, y] = (r, g, b, 0)
    return masked


def crop_frame(image: Image.Image, box: tuple[int, int, int, int], size: tuple[int, int], tint=None) -> Image.Image:
    """Scale a source crop into a framed, readable evidence card."""
    crop = image.crop(box).resize(size, Image.Resampling.NEAREST)
    if tint:
        overlay = Image.new("RGBA", size, (*tint, 40))
        crop = Image.alpha_composite(crop, overlay)
    frame = Image.new("RGBA", (size[0] + 10, size[1] + 10), (8, 15, 23, 230))
    frame.alpha_composite(crop, (5, 5))
    ImageDraw.Draw(frame, "RGBA").rectangle((1, 1, frame.width - 2, frame.height - 2), outline=(*ICE, 100), width=1)
    return frame


def ui_artwork(size=(896, 640)) -> Image.Image:
    """A collage made from panel/sprite crops; no newly drawn iconography."""
    art = Image.new("RGBA", size, (8, 15, 23, 255))
    panels = asset("panels", "inventory.png")
    recipe = asset("panels", "recipe_background.png")
    states = mask_atlas_placeholder(asset("sprites", "interface_states.png"))
    buttons = asset("sprites", "button_backgrounds.png")
    search = asset("sprites", "search_and_tooltip.png")
    toggle = asset("sprites", "toggle_and_radio.png")
    potion = asset("panels", "potion_editor.png")
    recipe_slot = asset("panels", "recipe_slot.png")
    catalyst = asset("panels", "catalyst_tab.png")
    repair = asset("panels", "repair_recipe.png")

    # Source texture crops arranged as a quiet, functional specimen wall.
    art.alpha_composite(crop_frame(panels, (0, 0, 199, 204), (250, 254)), (24, 22))
    recipe_card = crop_frame(recipe, (0, 0, 184, 174), (300, 283))
    # Populate the otherwise open recipe field with existing slot/tab/repair
    # textures. Every visible mark here comes from a tracked source PNG.
    recipe_card.alpha_composite(catalyst.resize((48, 48), Image.Resampling.NEAREST), (22, 28))
    for x in (82, 132, 182):
        recipe_card.alpha_composite(recipe_slot.resize((42, 42), Image.Resampling.NEAREST), (x, 132))
    recipe_card.alpha_composite(recipe_slot.resize((42, 42), Image.Resampling.NEAREST), (238, 132))
    recipe_card.alpha_composite(repair.resize((168, 36), Image.Resampling.NEAREST), (64, 216))
    art.alpha_composite(recipe_card, (292, 22))
    art.alpha_composite(crop_frame(potion, (0, 0, 176, 207), (238, 280)), (616, 22))
    art.alpha_composite(crop_frame(states, (0, 0, 256, 160), (292, 182), tint=(13, 35, 48)), (44, 324))
    art.alpha_composite(crop_frame(buttons, (0, 0, 200, 146), (250, 182), tint=(13, 35, 48)), (352, 324))
    art.alpha_composite(crop_frame(search, (0, 0, 128, 36), (280, 79), tint=(13, 35, 48)), (616, 324))
    art.alpha_composite(crop_frame(toggle, (0, 0, 55, 52), (135, 127), tint=(13, 35, 48)), (616, 423))

    # Hairline captions are metadata, not invented artwork.
    d = ImageDraw.Draw(art, "RGBA")
    tracked(d, (24, 603), "SOURCE STUDY / GUI TEXTURES", font("mono", 10), (*MUTED, 220), tracking=1.4)
    tracked(d, (872, 603), "PIXEL-FAITHFUL / 01", font("mono", 10), (*CYAN, 220), tracking=1.2, anchor="ra")
    return art


def draw_header():
    size = (1300, 372)
    base = gradient(size)
    base = Image.alpha_composite(base, radial_glow(size, (1080, 52), 330, CYAN, 95))
    base = Image.alpha_composite(base, radial_glow(size, (160, 355), 290, (45, 94, 132), 80))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 32, inset=24, alpha=15)
    d.line((58, 326, 1238, 326), fill=(*CYAN, 70), width=1)
    for x in range(58, 1239, 49):
        d.line((x, 326, x, 334 if (x - 58) % 245 else 338), fill=(*MUTED, 100), width=1)
    d.line((1237, 48, 1237, 324), fill=(*MUTED, 75), width=1)
    base = Image.alpha_composite(base, overlay)

    # Left identity and restrained release metadata.
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (60, 56), "NEXUS // UI RESOURCE STUDY", font("mono", 13), (*CYAN, 245), tracking=2.2)
    text(draw, (58, 92), "NOT ENOUGH", font("display", 53), (*OFFWHITE, 255))
    text(draw, (58, 145), "ITEMS", font("display", 53), (*OFFWHITE, 255))
    tracked(draw, (61, 229), "CATALOG  ·  RECIPES  ·  BOOKMARKS", font("body", 16), (*MUTED, 235), tracking=1.5)
    tracked(draw, (60, 285), "SEARCH / TRACE / CRAFT", font("mono", 10), (*ICE, 165), tracking=1.7)
    text(draw, (58, 300), "A procedural index for the things that make a world", font("body", 12), (*MUTED, 190))

    # The main visual is a crop wall made from the existing UI source files.
    specimen = ui_artwork((560, 300)).resize((560, 300), Image.Resampling.NEAREST)
    base.alpha_composite(specimen, (704, 42))
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (704, 352), "ASSET COMPOSITE / EXISTING UI ONLY", font("mono", 9), (*MUTED, 190), tracking=1.0)
    draw.rectangle((28, 22, 1271, 349), outline=(*ICE, 32), width=1)
    for x, y, sx, sy in [(28, 22, 1, 1), (1271, 22, -1, 1), (28, 349, 1, -1), (1271, 349, -1, -1)]:
        draw.line((x, y, x + sx * 14, y), fill=(*ICE, 100), width=1)
        draw.line((x, y, x, y + sy * 14), fill=(*ICE, 100), width=1)

    out = OUT / "nexus_header_1300x372.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


def draw_showcase():
    size = (1920, 1080)
    base = gradient(size, left=(8, 14, 22), right=(18, 29, 43))
    base = Image.alpha_composite(base, radial_glow(size, (1110, 465), 560, CYAN, 90))
    base = Image.alpha_composite(base, radial_glow(size, (1660, 170), 390, (37, 83, 113), 80))
    base = Image.alpha_composite(base, radial_glow(size, (260, 960), 430, (24, 67, 91), 70))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 48, inset=72, alpha=13)
    for i in range(13):
        x = 96 + i * 144
        d.line((x, 78, x, 994), fill=(61, 87, 104, 22), width=1)
    d.line((96, 920, 1824, 920), fill=(*CYAN, 55), width=1)
    base = Image.alpha_composite(base, overlay)

    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (96, 44), "NEI / ASSET COMPOSITE", font("mono", 13), (*CYAN, 230), tracking=2)
    tracked(draw, (1824, 44), "BUILD 02  ·  SOURCE-LED STUDY", font("mono", 11), (*MUTED, 180), tracking=1.2, anchor="ra")
    tracked(draw, (96, 174), "NOT ENOUGH ITEMS  /  RELEASE VISUAL", font("mono", 15), (*CYAN, 240), tracking=2)
    text(draw, (92, 225), "Find the", font("display", 66), (*OFFWHITE, 255))
    text(draw, (92, 294), "chain.", font("display", 66), (*OFFWHITE, 255))
    text(draw, (96, 410), "Search. Trace. Craft.", font("body", 24), (*ICE, 230))
    text(draw, (96, 452), "A quiet index for complex worlds —", font("body", 16), (*MUTED, 210))
    text(draw, (96, 478), "where every recipe becomes a path.", font("body", 16), (*MUTED, 210))

    chips = [("CATALOG", CYAN), ("RECIPE GRAPH", ICE), ("BOOKMARKS", AMBER)]
    x = 96
    for label, c in chips:
        tw = draw.textlength(label, font=font("mono", 11)) + 34
        draw.rounded_rectangle((x, 554, x + tw, 588), radius=17, fill=(18, 30, 43, 150), outline=(*c, 150), width=1)
        tracked(draw, (x + 17, 565), label, font("mono", 11), (*c, 225), tracking=1.1)
        x += tw + 12
    d = draw
    d.line((96, 706, 330, 706), fill=(*MUTED, 80), width=1)
    tracked(d, (96, 730), "INDEX / 0007", font("mono", 11), (*MUTED, 170), tracking=1.2)
    text(d, (96, 758), "Input becomes relation.", font("body", 14), (*MUTED, 180))
    text(d, (96, 782), "Relation becomes possibility.", font("body", 14), (*MUTED, 180))

    # Right-hand source wall: enlarged crops of real panels and sprites.
    wall = ui_artwork((896, 640))
    base.alpha_composite(wall, (760, 165))
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (760, 825), "ASSET WALL / PANELS + SPRITES", font("mono", 11), (*MUTED, 190), tracking=1.1)

    # Three small source callouts use only the repository texture crops.
    source_rows = [
        ("RECIPE", "panels/recipe_background.png", (0, 0, 184, 174), (96, 860), (180, 128), CYAN),
        ("STATE", "sprites/interface_states.png", (0, 0, 128, 96), (300, 860), (160, 120), ICE),
        ("SEARCH", "sprites/search_and_tooltip.png", (0, 0, 128, 36), (480, 860), (160, 45), AMBER),
    ]
    for label, path, box, pos, wh, color in source_rows:
        src = asset(*path.split("/"))
        if path == "sprites/interface_states.png":
            src = mask_atlas_placeholder(src)
        thumb = src.crop(box).resize(wh, Image.Resampling.NEAREST)
        frame = Image.new("RGBA", (wh[0] + 8, wh[1] + 32), (8, 15, 23, 220))
        frame.alpha_composite(thumb, (4, 22))
        fd = ImageDraw.Draw(frame, "RGBA")
        tracked(fd, (5, 4), label, font("mono", 9), (*color, 210), tracking=1.0)
        fd.rectangle((1, 20, frame.width - 2, frame.height - 2), outline=(*color, 95), width=1)
        base.alpha_composite(frame, pos)

    draw.rectangle((72, 58, 1848, 1022), outline=(*ICE, 35), width=1)
    for x, y0, sx, sy in [(72, 58, 1, 1), (1848, 58, -1, 1), (72, 1022, 1, -1), (1848, 1022, -1, -1)]:
        draw.line((x, y0, x + sx * 20, y0), fill=(*ICE, 90), width=1)
        draw.line((x, y0, x, y0 + sy * 20), fill=(*ICE, 90), width=1)

    out = OUT / "nexus_showcase_1920x1080.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


if __name__ == "__main__":
    print(draw_header())
    print(draw_showcase())
