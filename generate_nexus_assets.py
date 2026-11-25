from __future__ import annotations

import math
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "nexus_assets"
OUT.mkdir(exist_ok=True)
SEED = 5977

# Palette: disciplined graphite / slate with cyan and restrained amber.
GRAPHITE = (9, 15, 23)
SLATE = (20, 32, 46)
SLATE_2 = (26, 42, 58)
GRID = (61, 87, 104)
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


def lerp(a, b, t):
    return tuple(round(a[i] * (1 - t) + b[i] * t) for i in range(3))


def gradient(size, left=GRAPHITE, right=SLATE):
    w, h = size
    im = Image.new("RGB", size)
    p = im.load()
    for x in range(w):
        t = x / max(1, w - 1)
        base = lerp(left, right, t)
        for y in range(h):
            # very subtle vertical lift in the middle, keeping the corners dark
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
                a = int(alpha * ((1 - d) ** 2))
                px[x, y] = (*color, a)
    return layer


def text(draw, xy, value, f, fill, anchor=None, spacing=4):
    draw.text(xy, value, font=f, fill=fill, anchor=anchor, spacing=spacing)


def tracked(draw, xy, value, f, fill, tracking=2, anchor="la"):
    # Pillow has no tracking. Draw glyph by glyph for stable, intentional rhythm.
    x, y = xy
    if anchor == "ma":
        widths = [draw.textlength(ch, font=f) for ch in value]
        total = sum(widths) + tracking * max(0, len(value) - 1)
        x -= total / 2
    for ch in value:
        draw.text((x, y), ch, font=f, fill=fill, anchor="la")
        x += draw.textlength(ch, font=f) + tracking


def grid(draw, size, step, inset=0, alpha=22):
    w, h = size
    line = (*GRID, alpha)
    for x in range(inset, w - inset + 1, step):
        draw.line((x, inset, x, h - inset), fill=line, width=1)
    for y in range(inset, h - inset + 1, step):
        draw.line((inset, y, w - inset, y), fill=line, width=1)


def ticks(draw, x, y, length, count, horizontal=True, color=(*MUTED, 100)):
    if horizontal:
        for i in range(count + 1):
            xx = x + length * i / count
            t = 8 if i % 5 == 0 else 4
            draw.line((xx, y, xx, y + t), fill=color, width=1)
    else:
        for i in range(count + 1):
            yy = y + length * i / count
            t = 8 if i % 5 == 0 else 4
            draw.line((x, yy, x + t, yy), fill=color, width=1)


def rounded_panel(draw, box, fill=(20, 32, 46, 170), outline=(100, 160, 177, 75), radius=12, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def draw_node(draw, x, y, r=5, color=CYAN, selected=False):
    if selected:
        draw.ellipse((x - r * 3.2, y - r * 3.2, x + r * 3.2, y + r * 3.2), outline=(*color, 70), width=1)
        draw.ellipse((x - r * 1.9, y - r * 1.9, x + r * 1.9, y + r * 1.9), outline=(*color, 150), width=1)
    draw.ellipse((x - r, y - r, x + r, y + r), fill=(*color, 235))
    draw.ellipse((x - r * 0.34, y - r * 0.34, x + r * 0.34, y + r * 0.34), fill=(*ICE, 230))


def core_art(size, center, radius, rng, detail=1.0):
    w, h = size
    cx, cy = center
    art = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(art, "RGBA")

    # orbit rings and deliberately open arcs
    for i, rr in enumerate([radius * 0.72, radius * 0.91, radius * 1.12]):
        start = [205, 320, 28][i]
        extent = [245, 175, 250][i]
        d.arc((cx - rr, cy - rr, cx + rr, cy + rr), start=start, end=start + extent,
              fill=(*CYAN, [155, 105, 65][i]), width=max(1, int(2 * detail)))
        # offset amber segment on the outer orbit
        if i == 2:
            d.arc((cx - rr, cy - rr, cx + rr, cy + rr), start=198, end=228,
                  fill=(*AMBER, 220), width=max(1, int(3 * detail)))

    # square aperture and corner brackets
    half = radius * 0.24
    d.rounded_rectangle((cx - half, cy - half, cx + half, cy + half), radius=8,
                        fill=(8, 15, 23, 210), outline=(*ICE, 150), width=2)
    b = half + 18
    for sx, sy in [(-1, -1), (1, -1), (1, 1), (-1, 1)]:
        x1, y1 = cx + sx * b, cy + sy * b
        x2, y2 = cx + sx * (b - 18), cy + sy * b
        x3, y3 = cx + sx * b, cy + sy * (b - 18)
        d.line((x1, y1, x2, y2), fill=(*ICE, 115), width=2)
        d.line((x1, y1, x3, y3), fill=(*ICE, 115), width=2)

    # deterministic nodes around the core
    pts = []
    count = int(18 * detail)
    for i in range(count):
        ang = rng.uniform(0, math.tau)
        rr = rng.uniform(radius * 0.58, radius * 1.03)
        x = cx + math.cos(ang) * rr
        y = cy + math.sin(ang) * rr * 0.74
        pts.append((x, y))
    # a few short relational edges, mostly cyan, one amber route
    for i in range(0, len(pts) - 1, 2):
        p1, p2 = pts[i], pts[i + 1]
        midx = (p1[0] + p2[0]) / 2
        d.line((p1[0], p1[1], midx, p1[1], midx, p2[1], p2[0], p2[1]),
               fill=(*CYAN, 78), width=1)
    if len(pts) >= 5:
        path = [pts[1], (cx - radius * 0.18, cy - radius * 0.16), (cx + radius * 0.1, cy + radius * 0.05), pts[-2]]
        d.line(path, fill=(*CYAN, 205), width=max(1, int(2.2 * detail)), joint="curve")
        d.ellipse((pts[-2][0] - 7, pts[-2][1] - 7, pts[-2][0] + 7, pts[-2][1] + 7), outline=(*AMBER, 235), width=2)
    for i, (x, y) in enumerate(pts):
        draw_node(d, x, y, r=3.5 if i % 5 else 5, color=AMBER if i == len(pts) - 2 else CYAN, selected=(i % 7 == 0))

    # sparse flow particles
    for i in range(int(15 * detail)):
        ang = rng.uniform(0, math.tau)
        rr = rng.uniform(radius * 0.35, radius * 1.22)
        x = cx + math.cos(ang) * rr
        y = cy + math.sin(ang) * rr * 0.72
        r = rng.choice([1, 1, 2])
        d.ellipse((x - r, y - r, x + r, y + r), fill=(*ICE, rng.randint(80, 170)))
    return art


def draw_header():
    size = (1300, 372)
    rng = random.Random(SEED)
    base = gradient(size)
    base = Image.alpha_composite(base, radial_glow(size, (1080, 52), 330, CYAN, 95))
    base = Image.alpha_composite(base, radial_glow(size, (160, 355), 290, (45, 94, 132), 80))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 32, inset=24, alpha=15)
    # Fine horizontal scanline and right-side measurement rail.
    d.line((58, 326, 1238, 326), fill=(*CYAN, 70), width=1)
    ticks(d, 58, 326, 1180, 24, horizontal=True)
    for y in range(62, 302, 28):
        d.line((1218, y, 1238, y), fill=(*MUTED, 80), width=1)
    d.line((1237, 48, 1237, 324), fill=(*MUTED, 75), width=1)
    base = Image.alpha_composite(base, overlay)

    # Text block
    draw = ImageDraw.Draw(base, "RGBA")
    tracked(draw, (60, 56), "NEXUS // ITEM INTELLIGENCE", font("mono", 13), (*CYAN, 245), tracking=2.2)
    text(draw, (58, 92), "NOT ENOUGH", font("display", 53), (*OFFWHITE, 255))
    text(draw, (58, 145), "ITEMS", font("display", 53), (*OFFWHITE, 255))
    tracked(draw, (61, 229), "CATALOG  ·  RECIPES  ·  BOOKMARKS", font("body", 16), (*MUTED, 235), tracking=1.5)
    # tiny contextual labels
    tracked(draw, (60, 285), "SEARCH / TRACE / CRAFT", font("mono", 10), (*ICE, 165), tracking=1.7)
    text(draw, (58, 300), "A procedural index for the things that make a world", font("body", 12), (*MUTED, 190))

    # Core at center/right; title remains dominant.
    core = core_art(size, (823, 185), 94, rng, detail=1.0)
    base = Image.alpha_composite(base, core)
    draw = ImageDraw.Draw(base, "RGBA")

    # right matrix: abstract slots, no external iconography
    x0, y0 = 1060, 105
    tracked(draw, (x0, 60), "RECIPE GRAPH / 01", font("mono", 11), (*MUTED, 200), tracking=1.2)
    for row in range(3):
        for col in range(4):
            x = x0 + col * 40
            y = y0 + row * 40
            fill = (30, 47, 62, 120) if (row + col) % 3 else (36, 70, 80, 150)
            outline = (*CYAN, 100) if col == row else (*MUTED, 80)
            d.rounded_rectangle((x, y, x + 28, y + 28), radius=5, fill=fill, outline=outline, width=1)
            if (row * 4 + col) % 4 == 0:
                d.line((x + 7, y + 20, x + 21, y + 20), fill=(*ICE, 150), width=2)
            else:
                d.ellipse((x + 11, y + 11, x + 17, y + 17), fill=(*CYAN, 150))
    d.line((1048, 94, 1048, 236), fill=(*MUTED, 70), width=1)
    # a concise amber endpoint and route indicator
    d.ellipse((1228, 287, 1234, 293), fill=(*AMBER, 235))
    d.line((1102, 285, 1230, 290), fill=(*AMBER, 130), width=1)
    text(draw, (1098, 302), "LINK  07 → 12", font("mono", 10), (*AMBER, 190))

    # polish: soft 1px inner frame and subtle corner registration marks
    d.rectangle((28, 22, 1271, 349), outline=(*ICE, 32), width=1)
    for x, y, sx, sy in [(28, 22, 1, 1), (1271, 22, -1, 1), (28, 349, 1, -1), (1271, 349, -1, -1)]:
        d.line((x, y, x + sx * 14, y), fill=(*ICE, 100), width=1)
        d.line((x, y, x, y + sy * 14), fill=(*ICE, 100), width=1)

    out = OUT / "nexus_header_1300x372.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


def draw_showcase():
    size = (1920, 1080)
    rng = random.Random(SEED)
    base = gradient(size, left=(8, 14, 22), right=(18, 29, 43))
    base = Image.alpha_composite(base, radial_glow(size, (1110, 465), 560, CYAN, 90))
    base = Image.alpha_composite(base, radial_glow(size, (1660, 170), 390, (37, 83, 113), 80))
    base = Image.alpha_composite(base, radial_glow(size, (260, 960), 430, (24, 67, 91), 70))
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay, "RGBA")
    grid(d, size, 48, inset=72, alpha=13)
    # 12-column layout rails
    for i in range(13):
        x = 96 + i * 144
        d.line((x, 78, x, 994), fill=(*GRID, 22), width=1)
    d.line((96, 920, 1824, 920), fill=(*CYAN, 55), width=1)
    ticks(d, 96, 920, 1728, 36, horizontal=True, color=(*MUTED, 80))
    # top status band
    d.line((96, 78, 1824, 78), fill=(*ICE, 45), width=1)
    tracked(d, (96, 44), "NEI / NEXUS INDEX", font("mono", 13), (*CYAN, 230), tracking=2)
    tracked(d, (1824, 44), "BUILD 01  ·  STATIC STUDY", font("mono", 11), (*MUTED, 180), tracking=1.2, anchor="ra")
    base = Image.alpha_composite(base, overlay)

    draw = ImageDraw.Draw(base, "RGBA")
    # Left narrative panel
    tracked(draw, (96, 174), "NOT ENOUGH ITEMS  /  NEXUS", font("mono", 15), (*CYAN, 240), tracking=2)
    text(draw, (92, 225), "Find the", font("display", 66), (*OFFWHITE, 255))
    text(draw, (92, 294), "chain.", font("display", 66), (*OFFWHITE, 255))
    text(draw, (96, 410), "Search. Trace. Craft.", font("body", 24), (*ICE, 230))
    text(draw, (96, 452), "A quiet index for complex worlds —", font("body", 16), (*MUTED, 210))
    text(draw, (96, 478), "where every recipe becomes a path.", font("body", 16), (*MUTED, 210))

    # chips
    chips = [("CATALOG", CYAN), ("RECIPE GRAPH", ICE), ("BOOKMARKS", AMBER)]
    x = 96
    for label, c in chips:
        tw = draw.textlength(label, font=font("mono", 11)) + 34
        draw.rounded_rectangle((x, 554, x + tw, 588), radius=17, fill=(18, 30, 43, 150), outline=(*c, 150), width=1)
        tracked(draw, (x + 17, 565), label, font("mono", 11), (*c, 225), tracking=1.1)
        x += tw + 12

    # tiny note and key
    d = draw
    d.line((96, 706, 330, 706), fill=(*MUTED, 80), width=1)
    tracked(d, (96, 730), "INDEX / 0007", font("mono", 11), (*MUTED, 170), tracking=1.2)
    text(d, (96, 758), "Input becomes relation.", font("body", 14), (*MUTED, 180))
    text(d, (96, 782), "Relation becomes possibility.", font("body", 14), (*MUTED, 180))

    # Central nexus art, on a separate layer to keep glow distinct.
    core = core_art(size, (1045, 458), 230, rng, detail=1.45)
    # extra outer arcs on showcase
    cd = ImageDraw.Draw(core, "RGBA")
    cx, cy = 1045, 458
    for rr, st, en, col in [(300, 208, 340, CYAN), (325, 24, 112, ICE), (350, 142, 196, AMBER)]:
        cd.arc((cx - rr, cy - rr * .76, cx + rr, cy + rr * .76), st, en, fill=(*col, 100), width=2)
    base = Image.alpha_composite(base, core)
    draw = ImageDraw.Draw(base, "RGBA")

    # Glass HUD panels on the right
    panel_x, panel_w = 1450, 360
    rounded_panel(draw, (panel_x, 180, panel_x + panel_w, 365), fill=(17, 29, 42, 180), outline=(*CYAN, 80), radius=14)
    tracked(draw, (1476, 207), "QUERY", font("mono", 12), (*CYAN, 230), tracking=1.4)
    draw.rounded_rectangle((1476, 243, 1784, 282), radius=7, fill=(8, 15, 23, 200), outline=(*MUTED, 70), width=1)
    draw.ellipse((1492, 257, 1500, 265), outline=(*CYAN, 180), width=1)
    draw.line((1499, 264, 1506, 271), fill=(*CYAN, 180), width=1)
    text(draw, (1520, 252), "trace / relation", font("mono", 12), (*MUTED, 220))
    text(draw, (1476, 310), "12 RESULTS", font("mono", 11), (*ICE, 190))
    for i in range(6):
        x = 1476 + i * 46
        draw.rounded_rectangle((x, 334, x + 30, 351), radius=4, fill=(28, 54, 65, 150), outline=(*CYAN, 80), width=1)

    rounded_panel(draw, (1450, 390, 1810, 655), fill=(17, 29, 42, 180), outline=(*ICE, 70), radius=14)
    tracked(draw, (1476, 417), "RECIPE", font("mono", 12), (*ICE, 230), tracking=1.4)
    # Input -> Process -> Output chain
    labels = [("INPUT", 1490, CYAN), ("PROCESS", 1590, ICE), ("OUTPUT", 1710, AMBER)]
    for label, xx, cc in labels:
        draw.ellipse((xx, 492, xx + 24, 516), fill=(*cc, 175), outline=(*cc, 220), width=1)
        tracked(draw, (xx - 2, 531), label, font("mono", 9), (*cc, 190), tracking=0.8)
    draw.line((1514, 504, 1590, 504), fill=(*CYAN, 160), width=2)
    draw.line((1614, 504, 1710, 504), fill=(*ICE, 140), width=2)
    draw.polygon([(1583, 498), (1591, 504), (1583, 510)], fill=(*CYAN, 180))
    draw.polygon([(1703, 498), (1711, 504), (1703, 510)], fill=(*ICE, 160))
    for i in range(3):
        y = 578 + i * 18
        draw.line((1478, y, 1778 - i * 30, y), fill=(*MUTED, 70), width=1)
        draw.ellipse((1478, y - 2, 1482, y + 2), fill=(*MINT, 180) if i < 2 else (*AMBER, 190))

    rounded_panel(draw, (1450, 680, 1810, 860), fill=(17, 29, 42, 165), outline=(*AMBER, 65), radius=14)
    tracked(draw, (1476, 707), "BOOKMARK", font("mono", 12), (*AMBER, 230), tracking=1.4)
    text(draw, (1476, 747), "PATH 07", font("mono", 16), (*OFFWHITE, 220))
    text(draw, (1476, 779), "3 reachable steps", font("body", 14), (*MUTED, 190))
    for i, cc in enumerate([CYAN, CYAN, AMBER, MUTED]):
        xx = 1478 + i * 66
        draw.line((xx, 825, xx + 46, 825), fill=(*cc, 150), width=2)
        draw.ellipse((xx - 4, 821, xx + 4, 829), fill=(*cc, 215))

    # Bottom timeline, a restrained visual summary.
    tracked(draw, (430, 952), "RECIPE CHAIN / 01—05", font("mono", 11), (*MUTED, 180), tracking=1.2)
    y = 984
    start = 430
    for i in range(5):
        xx = start + i * 235
        cc = [CYAN, CYAN, ICE, MINT, AMBER][i]
        if i < 4:
            draw.line((xx + 10, y, xx + 220, y), fill=(*cc, 130), width=2)
            draw.polygon([(xx + 214, y - 5), (xx + 222, y), (xx + 214, y + 5)], fill=(*cc, 150))
        draw.ellipse((xx, y - 10, xx + 20, y + 10), fill=(11, 20, 28, 235), outline=(*cc, 210), width=2)
        draw.ellipse((xx + 7, y - 3, xx + 13, y + 3), fill=(*cc, 220))
        text(draw, (xx, y + 24), f"0{i + 1:02d}", font("mono", 10), (*cc, 175))

    # outer frame and registration marks
    draw.rectangle((72, 58, 1848, 1022), outline=(*ICE, 35), width=1)
    for x, y0, sx, sy in [(72, 58, 1, 1), (1848, 58, -1, 1), (72, 1022, 1, -1), (1848, 1022, -1, -1)]:
        draw.line((x, y0, x + sx * 20, y0), fill=(*ICE, 90), width=1)
        draw.line((x, y0, x, y0 + sy * 20), fill=(*ICE, 90), width=1)

    out = OUT / "nexus_showcase_1920x1080.png"
    base.convert("RGB").save(out, "PNG", optimize=True)
    return out


if __name__ == "__main__":
    h = draw_header()
    s = draw_showcase()
    print(h)
    print(s)
