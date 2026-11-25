"""Read NEI's C# frame declarations and resolve their PNG resources offline."""

from __future__ import annotations

import ast
import json
import re
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
STYLE = ROOT / "src/NotEnoughItems/UI/Styling"
BUNDLED = ROOT / "Assets/NotEnoughItems/textures"
ATLAS_GRID = {(255, 0, 170, 255), (255, 190, 170, 255)}


def uncomment(source):
    return re.sub(r"//[^\n]*", "", source)


def switch(source, name):
    return re.search(rf"\b{name}\([^)]*\)\s*=>\s*\w+\s+switch\s*{{(.*?)}};", source, re.S)[1]


def evaluate(expr, values):
    """The frame declarations use only literals, constants, addition and integer division."""
    def visit(node):
        if isinstance(node, ast.Constant) and type(node.value) in (int, float, str):
            return node.value
        if isinstance(node, ast.Name):
            return values[node.id]
        if isinstance(node, ast.Attribute) and isinstance(node.value, ast.Name) and node.value.id == "NeiAssets":
            return values[node.attr]
        if isinstance(node, ast.BinOp):
            a, b = visit(node.left), visit(node.right)
            if isinstance(node.op, ast.Add):
                return a + b
            if isinstance(node.op, ast.Div) and type(a) is type(b) is int:
                return a // b
        raise ValueError(f"Unsupported C# frame expression: {expr}")

    return visit(ast.parse(re.sub(r"(?<=\d)f\b", "", expr.strip()), mode="eval").body)


class Textures:
    def __init__(self, plugin_dir: Path | None = None):
        assets = uncomment((STYLE / "NeiAssets.cs").read_text(encoding="utf-8"))
        frames = uncomment((STYLE / "ModernityFrames.cs").read_text(encoding="utf-8"))
        self.constants = values = {}
        for line in re.findall(r"\bconst (?:int|float|string) (.*?);", assets + frames):
            for assignment in line.split(","):
                name, expr = assignment.split("=", 1)
                values[name.strip()] = evaluate(expr, values)

        # Expand the small helpers declared below Find, using the same source as Find itself.
        helpers = {}
        for name, arguments, body in re.findall(
            r"\bFrame (\w+)\(([^)]*)\)\s*=>\s*new\(([^)]+)\);", frames
        ):
            helpers[name] = ([arg.split()[-1] for arg in arguments.split(",")], body.split(","))

        self.frames = {}
        for line in switch(frames, "Find").splitlines():
            if not line.strip() or line.strip() == "_ => null":
                continue
            lhs, call, args = re.fullmatch(
                r"\s*(.*?)\s*=>\s*(?:new )?(\w+)\(([^)]*)\),\s*", line
            ).groups()
            args = [evaluate(arg, values) for arg in args.split(",")]
            if call != "Frame":
                arguments, body = helpers[call]
                env = values | dict(zip(arguments, args, strict=True))
                args = [evaluate(expr, env) for expr in body]
            resource, x, y, w, h = args
            for name in lhs.split(" or "):
                name = evaluate(name, values)
                self.frames[name] = (resource, (x, y, w, h))
        if not self.frames:
            raise ValueError("No texture frames found in ModernityFrames.Find")

        self.expected_sizes = {}
        for names, w, h in re.findall(r"([^\n]+?)\s*=>\s*\((\d+),\s*(\d+)\)", switch(assets, "ExpectedSize")):
            for name in names.split(" or "):
                self.expected_sizes[values[name.strip()]] = (int(w), int(h))
        self.previous_paths = {
            values[name]: re.findall(r'"([^"]+)"', paths)
            for name, paths in re.findall(r"(\w+)\s*=>\s*\[([^]]+)\]", switch(assets, "PreviousPackPaths"))
            if name != "_"
        }
        self.plugin_dir = plugin_dir.resolve() if plugin_dir else None
        self.sheets = {}
        self.extracted = {}
        self.manifest = {}

    def candidates(self, resource):
        if self.plugin_dir:
            prefix = self.constants["TextureRoot"]
            active = self.plugin_dir / self.constants["ActivePackDirectory"]
            if active.is_dir():
                for path in [resource, *self.previous_paths.get(resource, [])]:
                    yield active / prefix / path, "active-pack"
            yield self.plugin_dir / prefix / resource, "plugin-file"
            if resource.startswith("gui/backgrounds/"):
                file = Path(resource).name
                legacy = "background.png" if file == "default.png" else f"background-{file}"
                yield self.plugin_dir / "Textures" / legacy, "legacy-background"
        yield BUNDLED / resource, "repo-default"

    def sheet(self, resource):
        if resource not in self.sheets:
            result = None
            for path, kind in self.candidates(resource):
                try:
                    with Image.open(path) as img:
                        if resource in self.expected_sizes and img.size != self.expected_sizes[resource]:
                            continue
                        result = img.convert("RGBA"), path, kind
                        break
                except (OSError, ValueError, Image.DecompressionBombError):
                    continue  # Same fallback for missing or unreadable overrides as NeiSkin.
            self.sheets[resource] = result
        return self.sheets[resource]

    def frame(self, name):
        if name in self.extracted:
            return self.extracted[name]
        resource, (x, y, w, h) = self.frames[name]

        def crop(sheet):
            atlas, path, kind = sheet
            if min(x, y) < 0 or x + w > atlas.width or y + h > atlas.height:
                raise ValueError(f"{name} is outside {path}: {(x, y, w, h)}")
            image = atlas.crop((x, y, x + w, y + h))
            return image, path, kind

        def has_back_art(image):
            return any(px[3] and px not in ATLAS_GRID for _, px in image.getcolors(w * h))

        sheet = self.sheet(resource)
        if sheet is None:
            raise FileNotFoundError(resource)
        frame, path, kind = crop(sheet)
        if name == "back" and not has_back_art(frame):
            with Image.open(BUNDLED / resource) as default:
                frame, path, kind = crop((default.convert("RGBA"), BUNDLED / resource, "repo-default"))
            if not has_back_art(frame):
                raise ValueError("The default atlas is missing the Back artwork")
        if name in {"back", "favorite", "favorite_selected"}:
            ink = frame.getchannel("A").getbbox()
            if ink:
                left, top, right, bottom = ink
                frame = frame.crop(ink)
                x, y, w, h = x + left, y + top, right - left, bottom - top
        display_path = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.as_posix()
        self.manifest[name] = {"resource": resource, "source": display_path, "kind": kind,
                               "atlas_xywh": self.frames[name][1], "export_xywh": (x, y, w, h)}
        self.extracted[name] = frame
        return frame

    def export(self, output):
        folder = output / "frames"
        folder.mkdir(parents=True, exist_ok=True)
        for name in self.frames:
            self.frame(name).save(folder / f"{name}.png")
        (output / "frames.json").write_text(json.dumps(self.manifest, indent=2) + "\n", encoding="utf-8")

    def background(self, name):
        source = self.sheet(f"gui/backgrounds/{name}.png") or self.sheet("gui/backgrounds/default.png")
        return source[0] if source else None
