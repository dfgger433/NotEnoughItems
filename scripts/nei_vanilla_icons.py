"""Read exact vanilla item sprites from a local Unity game install without launching it."""

from __future__ import annotations

from collections.abc import Iterable, Iterator
from pathlib import Path

from PIL import Image


def load_icons(game_data: Path, ids: Iterable[str]) -> dict[str, Image.Image]:
    """Decode requested Resources prefab IDs to RGBA images, in request order.

    ``game_data`` is the CasualtiesUnknown_Data directory. Only the vanilla
    prefab sprite is read; runtime registry overrides are not applied. IDs must
    match the resource keys exactly. No image files are written.
    """
    requested = tuple(dict.fromkeys(ids))
    if not requested:
        return {}

    game_data = game_data.resolve()
    required = [game_data / name for name in (
        "globalgamemanagers", "resources.assets", "globalgamemanagers.assets",
    )]
    missing = [path.name for path in required if not path.is_file()]
    if missing:
        raise ValueError(
            f"Expected a CasualtiesUnknown_Data directory at {game_data}; "
            f"missing {', '.join(missing)}"
        )

    try:
        import UnityPy
    except ImportError as exc:
        raise RuntimeError("Vanilla item icons require the optional UnityPy package") from exc

    try:
        files = required + sorted(game_data.glob("sharedassets*.assets"))
        env = UnityPy.load(*(str(path) for path in files))
        manager = next((obj for obj in env.objects if obj.type.name == "ResourceManager"), None)
        if manager is None:
            raise ValueError("ResourceManager was not found")
        resources = {item_id: [] for item_id in requested}
        for name, pointer in manager.read().m_Container:
            if name in resources and pointer:
                resources[name].append(pointer)
    except Exception as exc:
        raise RuntimeError(f"Could not read the vanilla Unity resource table in {game_data}: {exc}") from exc

    images = {}
    for item_id in requested:
        try:
            prefab = next((obj for pointer in resources[item_id]
                           if (obj := pointer.deref()).type.name == "GameObject"), None)
            if prefab is None:
                raise ValueError(f"no exact Resources prefab in {game_data}")
            sprite = next(_sprite_pointers(prefab.read()), None)
            if sprite is None:
                raise ValueError("prefab has no SpriteRenderer with an assigned sprite")
            # UnityPy handles texture cropping, packing rotation, alpha, and Unity's Y axis.
            image = sprite.read().image.convert("RGBA")
            if not image.getchannel("A").getbbox():
                raise ValueError("sprite has no visible pixels")
        except Exception as exc:
            raise ValueError(f"Cannot decode the vanilla item sprite for {item_id!r}: {exc}") from exc
        images[item_id] = image
    return images


def _sprite_pointers(prefab) -> Iterator:
    """Prefer the root renderer, then the first child renderer in hierarchy order."""
    children = []
    for reference in prefab.m_Component:
        component = reference.component.deref()
        if component.type.name == "SpriteRenderer":
            sprite = component.read().m_Sprite
            if sprite.m_PathID:
                yield sprite
        elif component.type.name in {"Transform", "RectTransform"}:
            children = component.read().m_Children
    for child in children:
        yield from _sprite_pointers(child.read().m_GameObject.read())
