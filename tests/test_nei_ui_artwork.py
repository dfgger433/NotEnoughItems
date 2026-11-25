import json
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

from PIL import Image

from generate_nexus_assets import export
from scripts.nei_ui_preview import Preview, Rect, nine_slice
from scripts.nei_ui_textures import BUNDLED, Textures


class NeiArtworkTests(unittest.TestCase):
    def test_source_crop_and_ink_bounds(self):
        textures = Textures()
        known = {
            "recipe_background": ("gui/panels/recipe_background.png", (0, 0, 184, 174)),
            "recipe_slot": ("gui/sprites/interface_states.png", (161, 209, 18, 18)),
            "recipe_tab_candidate": ("gui/sprites/interface_states.png", (208, 112, 48, 47)),
            "search_field_focused": ("gui/sprites/search_and_tooltip.png", (0, 0, 128, 12)),
        }
        for name, (source, (x, y, w, h)) in known.items():
            with self.subTest(frame=name), Image.open(BUNDLED / source) as image:
                reference = image.convert("RGBA").crop((x, y, x + w, y + h))
                self.assertEqual(textures.frame(name).tobytes(), reference.tobytes())
        self.assertIs(textures.frame("back"), textures.frame("back"))  # Caches like the skin.
        self.assertEqual(textures.frame("back").size, (8, 11))
        self.assertEqual(textures.frame("favorite").size, (14, 14))
        self.assertEqual(textures.frame("next").tobytes(), textures.frame("recipe_arrow").tobytes())
        self.assertNotIn("inventory", textures.frames)
        self.assertIsNone(textures.background("bookmarks"))

    def test_replacement_order_canvas_validation_and_back_fallback(self):
        resource = "assets/notenoughitems/textures/"
        with tempfile.TemporaryDirectory() as tmp:
            plugin = Path(tmp)
            active = plugin / "resourcepacks/active" / resource
            (active / "gui/sprites").mkdir(parents=True)
            (active / "guis").mkdir()
            loose = plugin / resource / "gui/sprites"
            loose.mkdir(parents=True)
            Image.new("RGBA", (256, 256), (10, 20, 30, 255)).save(loose / "interface_states.png")
            Image.new("RGBA", (1, 1)).save(active / "gui/sprites/interface_states.png")
            # Grid-only Back must fall back without replacing the other icon cells.
            sheet = Image.new("RGBA", (256, 256), (255, 190, 170, 255))
            sheet.save(active / "guis/states.png")
            t = Textures(plugin)
            self.assertEqual(t.frame("bookmark").getpixel((0, 0)), (255, 190, 170, 255))
            self.assertTrue(t.manifest["bookmark"]["source"].endswith("guis/states.png"))
            self.assertEqual(t.frame("back").tobytes(), Textures().frame("back").tobytes())
            self.assertEqual(t.manifest["back"]["kind"], "repo-default")
            sheet.paste((70, 80, 90, 255), (0, 0, 32, 32))
            sheet.save(active / "gui/sprites/interface_states.png")
            self.assertEqual(Textures(plugin).frame("bookmark").getpixel((0, 0)), (70, 80, 90, 255))

    def test_fixed_borders_and_panel_geometry(self):
        textures = Textures()
        preview = Preview(textures)
        screen = preview.paint()
        self.assertEqual(screen.size, (1280, 720))
        self.assertEqual(screen.mode, "RGBA")
        for name, expected in {
            "bookmarks": (5, 67.5, 370.5, 512.5), "catalog": (915.5, 67.5, 359.5, 521),
            "bookmark_grid": (8, 100.5, 364.5, 444.5), "catalog_grid": (918.5, 95.5, 353.5, 490),
            "recipe_bounds": (526, 147, 228, 426), "recipe_frame": (526, 165, 228, 408),
            "recipe_output": (666.5, 240, 28, 28), "recipe_content": (535, 218, 210, 346),
        }.items():
            self.assertEqual(preview.geometry[name], Rect(*expected), name)
        for i in range(9):
            slot = preview.geometry[f"recipe_input_{i + 1}"]
            self.assertEqual(slot, Rect(567.5 + i % 3 * 22, 221 + i // 3 * 22, 22, 22))
            self.assertEqual(screen.crop(slot.pixels()).tobytes(), textures.frame("recipe_slot").resize((22, 22), Image.Resampling.NEAREST).tobytes())
        self.assertEqual(screen.getpixel((200, 240))[3], 0)  # No fake left inventory panel.
        self.assertEqual(screen.getpixel((1140, 240))[3], 0)  # No fake right-side cell art.
        self.assertEqual(screen.getpixel((590, 170)), (242, 242, 242, 255))  # Tab seam aligns with panel.
        self.assertEqual(screen.getpixel((562, 169)), (65, 63, 84, 255))
        self.assertEqual(screen.getpixel((562, 170)), (242, 242, 242, 255))  # Tab edge, not open center.
        self.assertEqual(screen.getpixel((562, 171)), (203, 204, 212, 255))
        frame = textures.frame("recipe_background")
        panel = nine_slice(frame, (228, 408), (9, 9, 9, 9))
        self.assertEqual(panel.crop((0, 0, 9, 9)).tobytes(), frame.crop((0, 0, 9, 9)).tobytes())
        self.assertEqual(panel.crop((219, 399, 228, 408)).tobytes(), frame.crop((175, 165, 184, 174)).tobytes())
        self.assertEqual(panel.getbbox(), (4, 4, 224, 404))

    def test_sample_preserves_empty_recipe_cells(self):
        icons = {key: Image.new("RGBA", (8, 8), "red") for key in ("rippeddressing", "bandage")}
        preview = Preview(Textures(), icons)
        image = preview.paint()
        for name in ("recipe_input_1", "recipe_input_2", "recipe_output"):
            rect = preview.geometry[name]
            pixel = int(rect.x + rect.w / 2), int(rect.y + rect.h / 2)
            self.assertEqual(image.getpixel(pixel), (255, 0, 0, 255))
        self.assertNotEqual(image.getpixel((621, 232)), (255, 0, 0, 255))

    def test_zip_exports_only_declared_frames_and_documented_outputs(self):
        t = Textures()
        preview = Preview(t)
        with tempfile.TemporaryDirectory() as tmp:
            archive = export(t, preview, preview.paint(), Path(tmp))
            with ZipFile(archive) as bundle:
                self.assertIsNone(bundle.testzip())
                self.assertEqual({n for n in bundle.namelist() if n.startswith("frames/")},
                                 {f"frames/{name}.png" for name in t.frames})
                self.assertEqual(set(json.loads(bundle.read("frames.json"))), set(t.frames))
                self.assertIn("credits/modernity-ui/LICENSE.txt", bundle.namelist())
                self.assertNotIn("nei-ui-local-items.png", bundle.namelist())


if __name__ == "__main__":
    unittest.main()
