"""Render release art and export the actual NEI frames without starting Unity."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image, ImageDraw, ImageFont

from scripts.nei_ui_preview import POINT, Preview
from scripts.nei_ui_textures import ROOT, Textures

OUT = ROOT / "nexus_assets"
RAW = ROOT / "dist/nei-ui"
ICE = "#bdeff4"
CYAN = "#35d6e8"
INK = "#eaf2f4"
MUTED = "#9aaebc"


def font(size, face="regular"):
    name = {"regular": "segoeui.ttf", "bold": "segoeuib.ttf", "mono": "consola.ttf"}[face]
    try:
        return ImageFont.truetype(name, size)
    except OSError:
        return ImageFont.load_default(size=size)


def backdrop(size):
    row = Image.new("RGB", (256, 1))
    left, right = (9, 15, 23), (20, 34, 48)
    row.putdata([tuple(round(a + (b - a) * i / 255) for a, b in zip(left, right)) for i in range(256)])
    return row.resize(size, Image.Resampling.BILINEAR).convert("RGBA")


def caption(draw, xy, text, size=14, color=MUTED, face="regular", anchor="la"):
    draw.text(xy, text, font=font(size, face), fill=color, anchor=anchor)


def save(image, path):
    image.save(path, "PNG", optimize=True)
    return path


def boxshot(screen, size):
    image = backdrop(size)
    draw = ImageDraw.Draw(image)
    for x in range(0, size[0], 32):
        draw.line((x, 0, x, size[1]), fill="#21313f")
    for y in range(0, size[1], 32):
        draw.line((0, y, size[0], y), fill="#21313f")
    image.alpha_composite(screen.resize(size, POINT))
    draw.rectangle((0, 0, size[0] - 1, size[1] - 1), outline="#475f70")
    return image


def render_header(screen, output):
    base = backdrop((1300, 372))
    draw = ImageDraw.Draw(base)
    draw.rectangle((28, 22, 1271, 349), outline="#475f70")
    caption(draw, (58, 57), "NOT ENOUGH ITEMS  /  CASUALTIES: UNKNOWN", 14, CYAN, "mono")
    caption(draw, (55, 94), "Every recipe,\none keystroke away.", 48, INK, "bold")
    caption(draw, (58, 231), "ITEM CATALOG   ·   RECIPE VIEWER   ·   BOOKMARKS", 15, ICE, "mono")
    caption(draw, (58, 285), "Offline UI detail  /  original GUI textures", 14)
    crop = screen.crop((518, 140, 762, 295))
    base.alpha_composite(crop.resize((488, 310), POINT), (683, 29))
    return save(base.convert("RGB"), output / "nexus_header_1300x372.png")


def render_showcase(screen, textures, output):
    base = backdrop((1920, 1080))
    draw = ImageDraw.Draw(base)
    draw.rectangle((72, 58, 1848, 1022), outline="#475f70")
    caption(draw, (96, 83), "NOT ENOUGH ITEMS  /  CASUALTIES: UNKNOWN", 15, CYAN, "mono")
    caption(draw, (1824, 83), "BEPINEX 5  ·  CUCORELIB 1.1+", 14, ICE, "mono", "ra")
    caption(draw, (96, 169), "Find the\nchain.", 74, INK, "bold")
    caption(draw, (98, 361), "Search. Trace. Craft.", 25, ICE)
    for i, (name, description) in enumerate([
        ("CATALOG", "Browse and search the item catalog."),
        ("RECIPES", "See how items are made and used."),
        ("BOOKMARKS", "Keep recipes and materials on hand."),
    ]):
        y = 445 + i * 67
        caption(draw, (99, y), name, 15, CYAN, "mono")
        caption(draw, (99, y + 23), description, 18, INK)

    frame_x, frame_y = 546, 170
    base.alpha_composite(boxshot(screen, screen.size), (frame_x, frame_y))
    for x, label, alignment in ((8, "01  BOOKMARKS", "la"), (640, "02  RECIPE VIEWER", "ma"), (1272, "03  ITEM CATALOG", "ra")):
        caption(draw, (frame_x + x, 140), label, 15, ICE, "mono", alignment)
    caption(draw, (frame_x, 911), "OFFLINE UI RECONSTRUCTION  /  GAME WORLD AND ITEM ICONS NOT SHOWN", 14, MUTED, "mono")

    caption(draw, (99, 774), "EXTRACTED GUI FRAMES", 14, ICE, "mono")
    for i, name in enumerate(("bookmark_selected", "options", "favorite", "recipe_slot")):
        x = 99 + i * 91
        icon = textures.frame(name)
        draw.rectangle((x, 812, x + 66, 878), outline="#475f70")
        scale = 2 if max(icon.size) <= 32 else 1
        w, h = icon.width * scale, icon.height * scale
        base.alpha_composite(icon.resize((w, h), POINT), (x + (67 - w) // 2, 812 + (67 - h) // 2))
    caption(draw, (99, 960), "Original PNG pixels, cropped by the mod's frame table.", 15)
    return save(base.convert("RGB"), output / "nexus_showcase_1920x1080.png")


def frame_sheet(textures):
    """Overview only: each individual PNG in the ZIP is unscaled."""
    cell_w, cell_h, columns = 206, 100, 5
    names = list(textures.frames)
    rows = (len(names) + columns - 1) // columns
    sheet = backdrop((columns * cell_w + 32, rows * cell_h + 62))
    draw = ImageDraw.Draw(sheet)
    caption(draw, (16, 13), f"NEI GUI FRAMES  /  {len(names)} FRAME NAMES FROM MODERNITYFRAMES.FIND", 17, ICE, "mono")
    for i, name in enumerate(names):
        x, y = 16 + (i % columns) * cell_w, 47 + (i // columns) * cell_h
        draw.rectangle((x, y, x + cell_w - 7, y + cell_h - 7), fill="#172431", outline="#475f70")
        image = textures.frame(name)
        scale = min(3, (cell_w - 19) / image.width, 60 / image.height)
        w, h = max(1, round(scale * image.width)), max(1, round(scale * image.height))
        sheet.alpha_composite(image.resize((w, h), POINT), (x + (cell_w - 6 - w) // 2, y + (65 - h) // 2))
        caption(draw, (x + 7, y + 68), name, 13, INK, "mono")
    return sheet.convert("RGB")


def export(textures, preview, screen, output):
    textures.export(output)
    save(screen, output / "nei-ui-transparent.png")
    save(boxshot(screen, screen.size).convert("RGB"), output / "nei-ui-preview.png")
    save(frame_sheet(textures), output / "nei-ui-frame-sheet.png")
    (output / "layout.json").write_text(json.dumps(preview.geometry, indent=2) + "\n", encoding="utf-8")
    archive = output / "nei-ui-textures.zip"
    with ZipFile(archive, "w", ZIP_DEFLATED) as bundle:
        for name in textures.frames:
            bundle.write(output / "frames" / f"{name}.png", f"frames/{name}.png")
        for name in ("frames.json", "layout.json", "nei-ui-transparent.png"):
            bundle.write(output / name, name)
        for name in ("release/nexus/NEXUS_ASSET_CREDITS.md", "modernity-ui/SOURCES.md", "modernity-ui/LICENSE.txt",
                     "ae2/SOURCES.md", "ae2/LICENSE.txt", "THIRD_PARTY_NOTICES.md"):
            bundle.write(ROOT / "docs" / name, "credits/" + name)
    return archive


def item_sample(textures, game_data, output):
    # Keep base-game artwork separate from the public Nexus assets.
    from scripts.nei_vanilla_icons import load_icons

    icons = load_icons(game_data, ("rippeddressing", "bandage"))
    preview = Preview(textures, icons=icons)
    save(boxshot(preview.paint(), (1280, 720)).convert("RGB"), output / "nei-ui-local-items.png")
    for item, image in icons.items():
        save(image, output / f"vanilla-{item}.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugin-dir", type=Path, help="Read replacement PNGs beside this NEI DLL directory")
    parser.add_argument("--game-data", type=Path, help="Also export a local-only sample using vanilla item sprites (UnityPy)")
    parser.add_argument("--output", type=Path, default=RAW, help="Raw export directory (default: dist/nei-ui)")
    parser.add_argument("--extract-only", action="store_true", help="Do not replace the tracked Nexus images")
    args = parser.parse_args()
    textures = Textures(args.plugin_dir)
    preview = Preview(textures)
    screen = preview.paint()
    print(export(textures, preview, screen, args.output))
    if args.game_data:
        try:
            item_sample(textures, args.game_data, args.output)
        except (RuntimeError, ValueError) as exc:
            parser.exit(1, f"Local vanilla sample: {exc}\n")
        print(args.output / "nei-ui-local-items.png")
    if not args.extract_only:
        OUT.mkdir(exist_ok=True)
        print(render_header(screen, OUT))
        print(render_showcase(screen, textures, OUT))


if __name__ == "__main__":
    main()
