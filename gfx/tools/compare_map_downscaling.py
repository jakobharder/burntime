#!/usr/bin/env python3
"""Build controlled 2x-to-1x map downscaling comparisons for the v5 render."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
HIGHRES = Path(
    "/Users/jakob/.codex/generated_images/01a081e7-d666-7b81-a933-c824502eb44c/"
    "exec-f626a0f9-1f67-42b5-9849-2c128f993503.png"
)
ORIGINAL = ROOT / "gfx/original/maps/mat_000.png"
PALETTE_SOURCE = ROOT / "gfx/extended_map_8bit.png"
OUTPUT = ROOT / "gfx/downscale-comparison-v5"

TARGET_SIZE = (1024, 384)
ORIGINAL_X = 224
ORIGINAL_WIDTH = 448

FILTERS = {
    "nearest": Image.Resampling.NEAREST,
    "box": Image.Resampling.BOX,
    "bilinear": Image.Resampling.BILINEAR,
    "bicubic": Image.Resampling.BICUBIC,
    "lanczos": Image.Resampling.LANCZOS,
}


def srgb_to_lab(rgb: np.ndarray) -> np.ndarray:
    values = rgb.astype(np.float32) / 255.0
    linear = np.where(
        values <= 0.04045,
        values / 12.92,
        ((values + 0.055) / 1.055) ** 2.4,
    )
    xyz = linear @ np.array(
        [
            [0.4124564, 0.3575761, 0.1804375],
            [0.2126729, 0.7151522, 0.0721750],
            [0.0193339, 0.1191920, 0.9503041],
        ],
        dtype=np.float32,
    ).T
    xyz /= np.array([0.95047, 1.0, 1.08883], dtype=np.float32)
    delta = 6 / 29
    f = np.where(xyz > delta**3, np.cbrt(xyz), xyz / (3 * delta**2) + 4 / 29)
    return np.stack(
        [116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])],
        axis=-1,
    )


def used_palette_colors(image: Image.Image) -> np.ndarray:
    indexed = image.convert("P")
    palette = np.asarray(indexed.getpalette(), dtype=np.uint8).reshape(-1, 3)
    used_indices = np.unique(np.asarray(indexed))
    return np.unique(palette[used_indices], axis=0)


def map_to_palette(rgb: np.ndarray, palette_rgb: np.ndarray) -> np.ndarray:
    flat_lab = srgb_to_lab(rgb.reshape(-1, 3))
    palette_lab = srgb_to_lab(palette_rgb)
    output = np.empty((flat_lab.shape[0], 3), dtype=np.uint8)
    chunk_size = 8192
    for start in range(0, flat_lab.shape[0], chunk_size):
        chunk = flat_lab[start : start + chunk_size]
        distance = np.sum((chunk[:, None, :] - palette_lab[None, :, :]) ** 2, axis=2)
        output[start : start + len(chunk)] = palette_rgb[np.argmin(distance, axis=1)]
    return output.reshape(rgb.shape)


def fixed_mood_transform(highres: Image.Image, original_array: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    reference = np.asarray(
        highres.resize(TARGET_SIZE, Image.Resampling.BOX), dtype=np.float32
    )[:, ORIGINAL_X : ORIGINAL_X + ORIGINAL_WIDTH]
    scale = np.empty(3, dtype=np.float32)
    offset = np.empty(3, dtype=np.float32)
    for channel in range(3):
        source_std = max(float(reference[:, :, channel].std()), 1.0)
        scale[channel] = float(original_array[:, :, channel].std()) / source_std
        offset[channel] = (
            float(original_array[:, :, channel].mean())
            - float(reference[:, :, channel].mean()) * scale[channel]
        )
    return scale, offset


def apply_mood(rgb: np.ndarray, scale: np.ndarray, offset: np.ndarray) -> np.ndarray:
    corrected = rgb.astype(np.float32) * scale[None, None, :] + offset[None, None, :]
    return np.clip(np.rint(corrected), 0, 255).astype(np.uint8)


def restore_original(image: Image.Image, original: Image.Image) -> Image.Image:
    image.paste(original, (ORIGINAL_X, 0))
    return image


def build_contact_sheet(paths: list[tuple[str, Path]], filename: str) -> None:
    crops = [
        ("sand + objects", (0, 42, 170, 142)),
        ("mountains + seam", (112, 92, 292, 262)),
        ("wood + lowlands", (790, 220, 1000, 382)),
    ]
    scale = 2
    label_width = 170
    header_height = 30
    row_height = max(box[3] - box[1] for _, box in crops) * scale + 12
    width = label_width + sum((box[2] - box[0]) * scale for _, box in crops)
    height = header_height + len(paths) * row_height
    sheet = Image.new("RGB", (width, height), (31, 31, 35))
    draw = ImageDraw.Draw(sheet)
    cursor_x = label_width
    for title, box in crops:
        draw.text((cursor_x + 5, 7), title, fill=(230, 230, 230))
        cursor_x += (box[2] - box[0]) * scale

    for row, (name, path) in enumerate(paths):
        image = Image.open(path).convert("RGB")
        top = header_height + row * row_height
        draw.text((8, top + 8), name, fill=(255, 255, 255))
        cursor_x = label_width
        for _, box in crops:
            crop = image.crop(box)
            crop = crop.resize(
                (crop.width * scale, crop.height * scale), Image.Resampling.NEAREST
            )
            sheet.paste(crop, (cursor_x, top))
            cursor_x += crop.width
    sheet.save(OUTPUT / filename)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    highres = Image.open(HIGHRES).convert("RGB")
    original = Image.open(ORIGINAL).convert("RGB")
    original_array = np.asarray(original, dtype=np.float32)
    palette_rgb = used_palette_colors(Image.open(PALETTE_SOURCE))
    mood_scale, mood_offset = fixed_mood_transform(highres, original_array)

    rgb_paths: list[tuple[str, Path]] = []
    palette_paths: list[tuple[str, Path]] = []
    variants = []

    for name, resampler in FILTERS.items():
        resized = highres.resize(TARGET_SIZE, resampler)
        corrected_array = apply_mood(np.asarray(resized), mood_scale, mood_offset)

        rgb_image = restore_original(Image.fromarray(corrected_array, "RGB"), original)
        rgb_path = OUTPUT / f"v5-{name}-rgb.png"
        rgb_image.save(rgb_path)
        rgb_paths.append((name, rgb_path))

        palette_array = map_to_palette(corrected_array, palette_rgb)
        palette_image = restore_original(Image.fromarray(palette_array, "RGB"), original)
        palette_path = OUTPUT / f"v5-{name}-palette-lab.png"
        palette_image.save(palette_path)
        palette_paths.append((f"{name} + Lab palette", palette_path))

        for mode, path in (("rgb", rgb_path), ("palette-lab", palette_path)):
            check = np.asarray(Image.open(path).convert("RGB"))[:, ORIGINAL_X : ORIGINAL_X + ORIGINAL_WIDTH]
            if not np.array_equal(check, np.asarray(original)):
                raise RuntimeError(f"original region changed in {path}")
            variants.append({"filter": name, "mode": mode, "file": path.name})

    build_contact_sheet(rgb_paths, "comparison-rgb.png")
    build_contact_sheet(palette_paths, "comparison-palette-lab.png")

    manifest = {
        "high_resolution_source": str(HIGHRES),
        "target_size": list(TARGET_SIZE),
        "original_locked_rectangle": [224, 0, 672, 384],
        "mood_transform": {
            "scale_rgb": mood_scale.tolist(),
            "offset_rgb": mood_offset.tolist(),
            "shared_by_all_variants": True,
        },
        "palette_source": str(PALETTE_SOURCE.relative_to(ROOT)),
        "palette_color_count": int(len(palette_rgb)),
        "palette_distance": "CIE Lab squared Euclidean, no dithering",
        "variants": variants,
    }
    (OUTPUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Wrote {len(variants)} variants to {OUTPUT}")
    print("palette colors", len(palette_rgb))
    print("original region pixel-perfect", True)


if __name__ == "__main__":
    main()
