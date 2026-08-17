#!/usr/bin/env python3
"""Build 60x72 transparent result images from geometry-true item repaints."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


DEFAULT_PALETTE = Path(
    "/Users/jakob/projects/burntime/releases/original/Burntime/BURN_GFX/MAT_000.RAW"
)


def edge_connected_background(image: Image.Image, threshold: int = 14) -> Image.Image:
    """Return an opaque-object mask by flood-filling near-black edge pixels."""
    rgb = image.convert("RGB")
    pixels = rgb.load()
    width, height = rgb.size
    background: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()

    def is_background(x: int, y: int) -> bool:
        return max(pixels[x, y]) <= threshold

    for x in range(width):
        if is_background(x, 0):
            queue.append((x, 0))
        if is_background(x, height - 1):
            queue.append((x, height - 1))
    for y in range(height):
        if is_background(0, y):
            queue.append((0, y))
        if is_background(width - 1, y):
            queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if (x, y) in background:
            continue
        background.add((x, y))
        for next_x, next_y in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if (
                0 <= next_x < width
                and 0 <= next_y < height
                and (next_x, next_y) not in background
                and is_background(next_x, next_y)
            ):
                queue.append((next_x, next_y))

    mask = Image.new("L", rgb.size, 255)
    mask_pixels = mask.load()
    for x, y in background:
        mask_pixels[x, y] = 0
    return mask


def load_palette(path: Path) -> Image.Image:
    data = path.read_bytes()[16 : 16 + 768]
    if len(data) != 768:
        raise ValueError(f"{path} does not contain a complete 256-color table")
    palette = [(value * 4) if value < 64 else 0 for value in data]
    image = Image.new("P", (1, 1))
    image.putpalette(palette)
    return image


def source_for(source_dir: Path, item: int) -> Path:
    backup = source_dir / f"gst_{item:02d}_geometry_true_before_palette_fix.png"
    if backup.exists():
        return backup
    return source_dir / f"gst_{item:02d}_geometry_true.png"


def convert_item(source: Path, output_dir: Path, item: int, palette: Image.Image) -> None:
    rgb = Image.open(source).convert("RGB")
    mask = edge_connected_background(rgb)
    alpha = mask.resize((60, 72), Image.Resampling.BILINEAR)

    nearest_rgb = rgb.resize((60, 72), Image.Resampling.NEAREST)
    nearest = nearest_rgb.convert("RGBA")
    nearest.putalpha(alpha)
    nearest.save(output_dir / f"gst_{item:02d}_nearest.png")

    linear_rgb = rgb.resize((60, 72), Image.Resampling.BILINEAR)
    indexed = linear_rgb.quantize(palette=palette, dither=Image.Dither.NONE)
    result = indexed.convert("RGBA")
    result.putalpha(alpha)
    result.save(output_dir / f"gst_{item:02d}.png")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--source",
        type=Path,
        default=Path("gfx/upscale-repaint/items"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("gfx/result/items"),
    )
    parser.add_argument("--palette", type=Path, default=DEFAULT_PALETTE)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    palette = load_palette(args.palette)
    for item in range(58):
        source = source_for(args.source, item)
        if not source.exists():
            raise FileNotFoundError(source)
        convert_item(source, args.output, item, palette)
        print(f"gst_{item:02d}: {source.name}")


if __name__ == "__main__":
    main()
