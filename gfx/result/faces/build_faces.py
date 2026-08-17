#!/usr/bin/env python3
"""Build framed 128x128 result portraits from geometry-true GES images."""

from __future__ import annotations

import re
from pathlib import Path

from PIL import Image, ImageChops


SCRIPT_DIR = Path(__file__).resolve().parent
GFX_DIR = SCRIPT_DIR.parents[1]
SOURCE_DIR = GFX_DIR / "upscale-repaint" / "faces"
UI_DIR = GFX_DIR / "upscale-repaint" / "ui"
MASK_PATH = UI_DIR / "frame-mask.png"
OVERLAY_PATH = UI_DIR / "frame-overlay.png"
SOURCE_PATTERN = re.compile(r"ges_(\d+)_geometry_true\.png$", re.IGNORECASE)
FULL_SIZE = (256, 256)
RESULT_SIZE = (128, 128)


def build_face(source_path: Path, mask: Image.Image, overlay: Image.Image) -> Path:
    match = SOURCE_PATTERN.fullmatch(source_path.name)
    if match is None:
        raise ValueError(f"Unexpected source filename: {source_path.name}")

    face = Image.open(source_path).convert("RGBA")
    if face.size != FULL_SIZE:
        raise ValueError(f"{source_path} is {face.size}, expected {FULL_SIZE}")

    face.putalpha(ImageChops.multiply(face.getchannel("A"), mask))
    face.alpha_composite(overlay)
    result = face.resize(RESULT_SIZE, Image.Resampling.NEAREST)

    output_path = SCRIPT_DIR / f"ges_{int(match.group(1)):02d}.png"
    result.save(output_path)
    return output_path


def main() -> None:
    mask_image = Image.open(MASK_PATH).convert("RGBA")
    overlay = Image.open(OVERLAY_PATH).convert("RGBA")
    if mask_image.size != FULL_SIZE:
        raise ValueError(f"{MASK_PATH} is {mask_image.size}, expected {FULL_SIZE}")
    if overlay.size != FULL_SIZE:
        raise ValueError(f"{OVERLAY_PATH} is {overlay.size}, expected {FULL_SIZE}")
    mask = mask_image.getchannel("A")

    sources = sorted(
        path for path in SOURCE_DIR.glob("ges_*_geometry_true.png")
        if SOURCE_PATTERN.fullmatch(path.name)
    )
    if not sources:
        raise FileNotFoundError(f"No geometry-true faces found in {SOURCE_DIR}")

    for source in sources:
        output = build_face(source, mask, overlay)
        print(f"{source.name} -> {output.name}")


if __name__ == "__main__":
    main()
