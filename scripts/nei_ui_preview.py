"""Offline repaint of NEI's default 1280×720 overlay, with no live item data."""

from __future__ import annotations

import json
from math import floor
from typing import NamedTuple

from PIL import Image, ImageDraw, ImageFont

from .nei_ui_textures import ROOT

SIZE = (1280, 720)  # NeiLayout.GuiScale = 1 for default settings at this resolution.
DARK = "#232538"
MUTED = "#c3c6d8"
INK = "#f4f2fb"
POINT = Image.Resampling.NEAREST


class Rect(NamedTuple):
    x: float
    y: float
    w: float
    h: float

    @property
    def right(self):
        return self.x + self.w

    @property
    def bottom(self):
        return self.y + self.h

    def pixels(self):
        # Quantize edges independently so abutting rectangles never leave gaps.
        return tuple(floor(v + .5) for v in (self.x, self.y, self.right, self.bottom))

    def inset(self, n):
        return Rect(self.x + n, self.y + n, self.w - 2 * n, self.h - 2 * n)


def nine_slice(source, size, borders):
    left, right, top, bottom = borders
    sw, sh = source.size
    dw, dh = size
    if min(sw - left - right, sh - top - bottom, dw - left - right, dh - top - bottom) <= 0:
        raise ValueError("Nine-slice borders do not fit their source or destination")
    sx, sy = (0, left, sw - right, sw), (0, top, sh - bottom, sh)
    dx, dy = (0, left, dw - right, dw), (0, top, dh - bottom, dh)
    out = Image.new("RGBA", size)
    for j in range(3):
        for i in range(3):
            patch = source.crop((sx[i], sy[j], sx[i + 1], sy[j + 1]))
            patch = patch.resize((dx[i + 1] - dx[i], dy[j + 1] - dy[j]), POINT)
            out.paste(patch, (dx[i], dy[j]))  # Preserve even the RGB of transparent source pixels.
    return out


class Preview:
    def __init__(self, textures, icons=None):
        self.textures = textures
        self.icons = icons or {}
        self.image = Image.new("RGBA", SIZE)
        self.geometry = {}
        self.lang = json.loads((ROOT / "Lang/en_us.json").read_text(encoding="utf-8-sig"))
        try:
            self.font = ImageFont.truetype("arial.ttf", 13)  # Approximate Unity's native label rasterization.
        except OSError:
            self.font = ImageFont.load_default(size=13)

    def record(self, name, rect):
        self.geometry[name] = rect
        return rect

    def blit(self, source, rect, borders=None):
        x, y, end_x, end_y = rect.pixels()
        size = end_x - x, end_y - y
        if min(size) <= 0:
            return
        patch = source.resize(size, POINT) if borders is None else nine_slice(source, size, borders)
        self.image.alpha_composite(patch, (x, y))

    def sprite(self, name, rect, borders=None):
        self.blit(self.textures.frame(name), rect, borders)

    def fill(self, rect, color):
        self.blit(Image.new("RGBA", (1, 1), color), rect)

    def label(self, rect, text, color=INK, left=False, shadow=False):
        x, y, end_x, end_y = rect.pixels()
        w, h = end_x - x, end_y - y
        if min(w, h) <= 0:
            return
        anchor = "lm" if left else "mm"
        tx = 0 if left else w / 2
        # Composite glyph coverage onto the existing backdrop instead of replacing its alpha.
        for dx, dy, ink in ([(1, 1, "#191827")] if shadow else []) + [(0, 0, color)]:
            layer = Image.new("RGBA", (w, h))
            ImageDraw.Draw(layer).text((tx, h / 2), text, font=self.font, fill=ink, anchor=anchor)
            self.image.alpha_composite(layer, (x + dx, y + dy))

    def background(self, name, rect):
        image = self.textures.background(name)
        if image is not None:
            self.blit(image, rect)

    def icon(self, name, rect, maximum, enlarge=False, disabled=False):
        src = self.textures.frame(name)
        scale = min(min(maximum, rect.w - 2) / src.width, min(maximum, rect.h - 2) / src.height)
        if not enlarge:
            scale = min(1, scale)
        w, h = src.width * scale, src.height * scale
        if disabled:
            src = src.copy()
            src.putalpha(src.getchannel("A").point(lambda a: round(a * .42)))
        self.blit(src, Rect(rect.x + (rect.w - w) / 2, rect.y + (rect.h - h) / 2, w, h))

    def item(self, name, rect):
        src = self.icons[name]
        rect = rect.inset(4)
        scale = min(rect.w / src.width, rect.h / src.height)
        w, h = src.width * scale, src.height * scale
        self.blit(src, Rect(rect.x + (rect.w - w) / 2, rect.y + (rect.h - h) / 2, w, h))

    def button(self, rect, name=None, text=None, selected=False, enabled=True, maximum=float("inf"), enlarge=False):
        face = rect if enabled else Rect(rect.x, rect.y + 1, rect.w, rect.h - 1)
        source = "button_disabled" if not enabled else "button_hover" if selected else "button"
        c = self.textures.constants
        edge = c["ButtonSlice"]
        self.sprite(source, face, (edge, edge, edge, edge if name else c["TextButtonBottom"]))
        if text:
            self.label(face, text, DARK if enabled else MUTED)
        elif name in {"add", "remove"}:
            # BuiltInControlArt paints these as strokes; there are no atlas frames for them.
            size = min(10, maximum, face.w - 2, face.h - 2)
            x, y = face.x + face.w / 2, face.y + face.h / 2
            color = DARK if enabled else MUTED
            self.fill(Rect(x - size / 2, y - 1, size, 2), color)
            if name == "add":
                self.fill(Rect(x - 1, y - size / 2, 2, size), color)
        elif name:
            self.icon(name, face, maximum, enlarge, not enabled)

    def bookmarks(self, panel):
        # BookmarkScreen.DrawBookmarkPanel / DrawBookmarkNavigation.
        self.background("bookmarks", panel)
        x, w, size = panel.x + 3, panel.w - 6, 28
        top = self.record("bookmark_paging", Rect(x, panel.y + 2, w, size))
        foot = self.record("bookmark_footer", Rect(x, panel.bottom - size - 4, w, size))
        grid = self.record("bookmark_grid", Rect(x, top.bottom + 3, w, foot.y - top.bottom - 6))
        button = min(size, w / 4)
        for row in (top, foot):
            self.button(Rect(row.x, row.y, button, size), "previous", enabled=False)
            self.button(Rect(row.right - button, row.y, button, size), "next", enabled=False)
        self.button(Rect(top.x + button + 1, top.y, w - 2 * button - 2, size), text="1/1", selected=True)
        self.button(Rect(foot.x + button + 2, foot.y, w - 2 * button - 4, size),
                    text=f"{self.lang['bookmark.page.default']}  1/1")
        if self.icons:
            for i, name in enumerate(self.icons):
                self.item(name, Rect(grid.x + 16 + 34 * i, grid.y, 33, 33))
        else:
            self.label(Rect(grid.x + 4, grid.y, grid.w - 8, 22), self.lang["bookmark.add_hint"].format("A"),
                       MUTED, left=True, shadow=True)

    def tools(self, tools):
        side = min(30, tools.h, (tools.w - 3) / 2)
        y = tools.y + (tools.h - side) / 2
        settings = self.record("settings_button", Rect(tools.x, y, side, side))
        bookmark = self.record("bookmark_button", Rect(tools.x + side + 3, y, side, side))
        self.button(settings, "options", maximum=22, enlarge=True)
        self.button(bookmark, "bookmark_selected", selected=True, maximum=22, enlarge=True)

    def catalog(self, panel, controls):
        # NeiLayout.CatalogPanel / DrawCatalogControls / DrawCatalog (default history enabled).
        self.background("catalog", controls)
        left, right, top = controls.x + 3, controls.right - 3, controls.y + 2
        w, gap = right - left, 2
        row = min(23, (controls.h - 7) / 2)
        button = min(row, w / 6, max(0, (w - 3 * gap - 30 - 22) / 2))
        editor = w - 2 * button - 3 * gap
        reserve = min(max(w / 3, 22), max(22, editor - 30))
        amount = min(max(editor - reserve, 0), min(max(w * .24, 44), 85))
        history = min(28, controls.bottom - 3 - top - row - 2 * gap)
        y = top + 2 * gap + history
        history_rect = self.record("catalog_history", Rect(left, top + gap, w, history))
        if self.icons:
            self.item("bandage", Rect(history_rect.x, history_rect.y, 27, 27))
        for i in range(0, int(w), 9):
            self.fill(Rect(left + i, top - 1, min(5, w - i), 1), (195, 198, 216, 190))
        decrease = Rect(right - button, y, button, row)
        increase = Rect(decrease.x - gap - button, y, button, row)
        quantity = self.record("catalog_quantity", Rect(increase.x - gap - amount, y, amount, row))
        search = self.record("catalog_search", Rect(left, y, quantity.x - gap - left, row))
        self.sprite("search_field", search, (1, 1, 3, 1))
        self.label(Rect(search.x + 6, y, search.w - 12, row), self.lang["catalog.search_placeholder"], MUTED,
                   left=True, shadow=True)
        self.fill(quantity, "#a0a0a0")
        self.fill(quantity.inset(1), "#000000")
        self.label(Rect(quantity.x + 6, y, amount - 12, row), "1", "#909090", left=True)
        self.button(increase, "add")
        self.button(decrease, "remove", enabled=False)

        self.background("catalog", panel)
        left, right, top = panel.x + 3, panel.right - 3, panel.y + 2
        nav = min(23, (right - left) / 6)
        self.button(Rect(left, top, nav, nav), "previous", enabled=False)
        self.button(Rect(right - nav, top, nav, nav), "next", enabled=False)
        self.label(Rect(left + nav + 2, top, right - left - 2 * nav - 4, nav), "1/1", shadow=True)
        y = top + nav + 3
        grid = self.record("catalog_grid", Rect(left, y, right - left, panel.bottom - 3 - y))
        if self.icons:
            for i, name in enumerate(sorted(self.icons)):
                self.item(name, Rect(grid.x + 28 * i, grid.y, 27, 27))
        else:
            self.label(Rect(grid.x, grid.y + 4, grid.w, 24), self.lang["catalog.no_matching_items"], MUTED, shadow=True)

    def recipe(self, available):
        # RecipePanelLayout + DrawInspector. This sample is a one-method, one-page,
        # one-output diagram with optional vanilla sprites, not live game data.
        c = self.textures.constants
        w, h = min(228, available.w), min(426, available.h)
        x = min(max(SIZE[0] / 2 - w / 2, available.x), available.right - w)
        y = available.y + (available.h - h) / 2
        bounds = self.record("recipe_bounds", Rect(x, y, w, h))
        border, transparent = c["RecipeBackgroundSlice"], c["RecipeBackgroundTransparentInset"]
        top = y + 24 - transparent - 2
        frame = self.record("recipe_frame", Rect(x, top, w, bounds.bottom - top))
        self.sprite("recipe_background", frame, (border,) * 4)

        left, right = x + border, bounds.right - border
        method, pages = Rect(left, top + border, right - left, 19), Rect(left, top + border + 21, right - left, 19)
        self.fill(method, "#a5a6ac")
        self.fill(pages, "#a5a6ac")
        self.button(Rect(x + 2, y + 2, 16, 20), "back", maximum=12)
        for bar, title, color in [(method, self.lang["method.crafting"], DARK),
                                  (pages, self.lang["recipe.page"].format(1, 1), INK)]:
            self.button(Rect(left, bar.y, 18, 19), "previous", enabled=False)
            self.button(Rect(right - 18, bar.y, 18, 19), "next", enabled=False)
            self.label(Rect(left + 18, bar.y, bar.w - 36, 19), title, color, shadow=bar == pages)

        tab = self.record("recipe_tab", Rect(x + 36, y, 24, 24))
        seam = top + transparent + 1
        src = self.textures.frame("recipe_tab_selected")
        stripe = c["RecipeTabHighlightRow"]
        tab_y = y + c["RecipeTabOffset"]
        for a, b, ty, height in ((0, stripe, tab_y, seam - tab_y), (stripe, stripe + 1, seam, 1),
                                 (stripe + 1, src.height, seam + 1, 1)):
            self.blit(src.crop((0, a, src.width, b)), Rect(tab.x, ty, tab.w, height))
        self.label(tab, self.lang["method.crafting"][:2], DARK)

        # RecipeGridLayout at IconSize=100. Only the diagram uses opaque slots.
        content_y = pages.bottom + 4
        content = self.record("recipe_content", Rect(left, content_y, right - left, bounds.bottom - border - content_y))
        area_x, area_w = content.x + 23, content.w - 46
        padding, arrow, action = 3, 33, 18
        room = area_w - action - 2 * padding - arrow
        cell = min(22, floor(room / 4))
        out_size = max(cell, min(28, room - 3 * cell))
        plate_w = 3 * cell + out_size + arrow + 2 * padding
        input_x = area_x + (area_w - plate_w - action) / 2 + padding
        center = content.y + padding + max(36, 3 * cell, out_size, 2 * (24 + 2)) / 2
        input_y = center - 3 * cell / 2
        arrow_x = input_x + 3 * cell
        output = self.record("recipe_output", Rect(arrow_x + arrow, center - out_size / 2, out_size, out_size))
        self.icon("recipe_arrow", Rect(arrow_x, center - 18, arrow, 36), 16)
        for i in range(9):
            slot = self.record(f"recipe_input_{i + 1}", Rect(input_x + i % 3 * cell, input_y + i // 3 * cell, cell, cell))
            self.sprite("recipe_slot", slot)
            if self.icons and i < 2:
                self.item("rippeddressing", slot)
        self.sprite("recipe_slot", output)
        if self.icons:
            self.item("bandage", output)
        self.button(Rect(output.right + padding, center - 8, 16, 16), "favorite", maximum=10)
        self.button(Rect(output.right + padding, center + 10, 16, 16), "add", enabled=False)

    def paint(self):
        # Project NeiLayout's 2560×1440 reference rectangles at the default scale.
        def project(name, rect):
            return self.record(name, Rect(*(v / 2 for v in rect)))

        left = project("bookmarks", (10, 135, 741, 1025))
        right = project("catalog", (1831, 135, 719, 1042))
        tools = project("tools", (104, 1198, 640, 96))
        controls = project("catalog_controls", (1831, 1178, 719, 184))
        self.bookmarks(left)
        self.catalog(right, controls)
        self.tools(tools)
        self.recipe(Rect(left.right + 5, 8, right.x - left.right - 10, SIZE[1] - 16))
        return self.image
