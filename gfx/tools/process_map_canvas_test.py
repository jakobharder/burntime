#!/usr/bin/env python3
"""Crop and compare the square-canvas image-generation experiment."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
GENERATED = Path(
    "/Users/jakob/.codex/generated_images/01a081e7-d666-7b81-a933-c824502eb44c/"
    "exec-e69499cf-735a-4550-8e63-bc75f086212a.png"
)
ORIGINAL = ROOT / "gfx/original/maps/mat_000.png"
PALETTE_SOURCE = ROOT / "gfx/extended_map_8bit.png"
OUTPUT = ROOT / "gfx/canvas-generation-test"

FILTERS = {
    "nearest": Image.Resampling.NEAREST,
    "box": Image.Resampling.BOX,
    "bilinear": Image.Resampling.BILINEAR,
    "bicubic": Image.Resampling.BICUBIC,
    "lanczos": Image.Resampling.LANCZOS,
}


def load_comparison_helpers():
    path = ROOT / "gfx/tools/compare_map_downscaling.py"
    spec = importlib.util.spec_from_file_location("compare_map_downscaling", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def detect_map_rows(canvas: Image.Image) -> tuple[int, int]:
    array = np.asarray(canvas)
    magenta = (
        (array[:, :, 0] > 210)
        & (array[:, :, 1] < 80)
        & (array[:, :, 2] > 180)
    )
    map_rows = np.mean(magenta, axis=1) < 0.5
    indices = np.flatnonzero(map_rows)
    return int(indices[0]), int(indices[-1] + 1)


def build_comparison(files: list[str]) -> None:
    crop_boxes = [
        ("sand + objects", (0, 42, 170, 142)),
        ("mountains + seam", (112, 92, 292, 262)),
        ("wood + lowlands", (790, 220, 1000, 382)),
    ]
    scale = 2
    label_width = 190
    header_height = 30
    row_height = max(box[3] - box[1] for _, box in crop_boxes) * scale + 12
    width = label_width + sum((box[2] - box[0]) * scale for _, box in crop_boxes)
    height = header_height + len(files) * row_height
    sheet = Image.new("RGB", (width, height), (31, 31, 35))
    draw = ImageDraw.Draw(sheet)
    cursor_x = label_width
    for title, box in crop_boxes:
        draw.text((cursor_x + 5, 7), title, fill=(230, 230, 230))
        cursor_x += (box[2] - box[0]) * scale

    for row, filename in enumerate(files):
        image = Image.open(OUTPUT / filename).convert("RGB")
        top = header_height + row * row_height
        draw.text((8, top + 8), filename.removeprefix("canvas-").removesuffix(".png"), fill=(255, 255, 255))
        cursor_x = label_width
        for _, box in crop_boxes:
            crop = image.crop(box)
            crop = crop.resize((crop.width * scale, crop.height * scale), Image.Resampling.NEAREST)
            sheet.paste(crop, (cursor_x, top))
            cursor_x += crop.width
    sheet.save(OUTPUT / "comparison.png")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    canvas = Image.open(GENERATED).convert("RGB")
    shutil.copy2(GENERATED, OUTPUT / "generated-canvas-1254.png")
    top, bottom = detect_map_rows(canvas)
    crop = canvas.crop((0, top, canvas.width, bottom))
    crop.save(OUTPUT / "generated-map-crop.png")

    original = Image.open(ORIGINAL).convert("RGB")
    original_array = np.asarray(original)
    helpers = load_comparison_helpers()
    palette = helpers.used_palette_colors(Image.open(PALETTE_SOURCE))

    files = []
    for name, resampler in FILTERS.items():
        image = crop.resize((1024, 384), resampler)
        image.paste(original, (224, 0))
        path = OUTPUT / f"canvas-{name}-rgb.png"
        image.save(path)
        files.append(path.name)

    box = crop.resize((1024, 384), Image.Resampling.BOX)
    palette_array = helpers.map_to_palette(np.asarray(box), palette)
    palette_image = Image.fromarray(palette_array, "RGB")
    palette_image.paste(original, (224, 0))
    palette_path = OUTPUT / "canvas-box-palette-lab.png"
    palette_image.save(palette_path)
    files.append(palette_path.name)

    for filename in files:
        check = np.asarray(Image.open(OUTPUT / filename).convert("RGB"))[:, 224:672]
        if not np.array_equal(check, original_array):
            raise RuntimeError(f"Original region changed in {filename}")

    build_comparison(files)

    metadata = {
        "input_canvas_size": [1024, 1024],
        "returned_canvas_size": list(canvas.size),
        "returned_scale": canvas.width / 1024,
        "detected_map_rows": [top, bottom],
        "raw_crop_size": list(crop.size),
        "target_size": [1024, 384],
        "crop_to_target_scale_x": crop.width / 1024,
        "crop_to_target_scale_y": crop.height / 384,
        "original_locked_rectangle": [224, 0, 672, 384],
        "files": files,
    }
    (OUTPUT / "results.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print(json.dumps(metadata, indent=2))


if __name__ == "__main__":
    main()
