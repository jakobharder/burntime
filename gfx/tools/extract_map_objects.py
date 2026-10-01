#!/usr/bin/env python3
"""Extract exact world-map object rectangles from the original MAT_000 assets."""

from __future__ import annotations

import json
from pathlib import Path
import struct

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
MAP_IMAGE = ROOT / "gfx/original/maps/mat_000.png"
MAP_RAW = ROOT.parent / "releases/original/Burntime/BURN_GFX/MAT_000.RAW"
OUTPUT = ROOT / "gfx/map-objects"

# Classification is based on the native artwork in the source rectangles.
# Indices refer to door/object order in MAT_000.RAW.
CATEGORIES = {
    "cities": [0, 8, 13, 18, 21],
    "camps": [5, 7, 10, 14, 15, 23, 30, 31, 32, 36],
    "huts": [2, 3, 9, 24, 26, 34],
    "ruins": [1, 16, 20, 25, 28, 33],
}

SINGULAR = {
    "cities": "city",
    "camps": "camp",
    "huts": "hut",
    "ruins": "ruin",
}


def read_objects(raw: bytes) -> list[dict[str, int]]:
    cursor = struct.unpack_from("<H", raw, 8)[0]
    objects: list[dict[str, int]] = []
    while cursor + 20 <= len(raw):
        object_id, x, y, width, height = struct.unpack_from("<HHHHH", raw, cursor)
        if x == 0:
            break
        objects.append(
            {
                "index": len(objects),
                "object_id": object_id,
                "x": x,
                "y": y,
                "width": width,
                "height": height,
            }
        )
        cursor += 20
    return objects


def build_contact_sheet(entries: list[dict[str, int | str]], source: Image.Image) -> None:
    scale = 5
    cell_width, cell_height = 190, 105
    columns = 5
    rows = (len(entries) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * cell_width, rows * cell_height), (35, 35, 39))
    draw = ImageDraw.Draw(sheet)

    for number, entry in enumerate(entries):
        x = int(entry["x"])
        y = int(entry["y"])
        width = int(entry["width"])
        height = int(entry["height"])
        crop = source.crop((x, y, x + width, y + height))
        preview = crop.resize((width * scale, height * scale), Image.Resampling.NEAREST)
        left = (number % columns) * cell_width
        top = (number // columns) * cell_height
        draw.text(
            (left + 5, top + 4),
            f'{entry["category"]}/{entry["filename"]}',
            fill=(255, 255, 255),
        )
        draw.text(
            (left + 5, top + 17),
            f'#{entry["source_index"]:02d} ({x},{y}) {width}x{height}',
            fill=(178, 184, 192),
        )
        sheet.paste(preview, (left + 5, top + 33))

    sheet.save(OUTPUT / "contact-sheet.png")


def main() -> None:
    source = Image.open(MAP_IMAGE).convert("RGB")
    objects = read_objects(MAP_RAW.read_bytes())
    OUTPUT.mkdir(parents=True, exist_ok=True)

    entries: list[dict[str, int | str]] = []
    for category, indices in CATEGORIES.items():
        category_dir = OUTPUT / category
        category_dir.mkdir(exist_ok=True)
        for ordinal, source_index in enumerate(indices):
            obj = objects[source_index]
            x, y = obj["x"], obj["y"]
            width, height = obj["width"], obj["height"]
            filename = f"{SINGULAR[category]}_{ordinal:02d}.png"
            source.crop((x, y, x + width, y + height)).save(category_dir / filename)
            entries.append(
                {
                    "category": category,
                    "filename": filename,
                    "source_index": source_index,
                    "source_object_id": obj["object_id"],
                    "x": x,
                    "y": y,
                    "width": width,
                    "height": height,
                    "mode": "RGB",
                    "transparent": False,
                }
            )

    manifest = {
        "source_image": str(MAP_IMAGE.relative_to(ROOT)),
        "source_raw": str(MAP_RAW),
        "notes": (
            "Exact native-resolution RGB crops using object rectangles encoded in "
            "MAT_000.RAW. Background pixels are retained deliberately; alpha masks "
            "require a separately reviewed segmentation pass."
        ),
        "objects": entries,
    }
    (OUTPUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    build_contact_sheet(entries, source)
    print(f"Extracted {len(entries)} objects to {OUTPUT}")


if __name__ == "__main__":
    main()
