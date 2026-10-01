#!/usr/bin/env python3
"""Embed the 1024x384 map at native size in a square image-generation canvas."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "gfx/extended_map_8bit.png"
OUTPUT_DIR = ROOT / "gfx/canvas-generation-test"
OUTPUT = OUTPUT_DIR / "input-canvas.png"

CANVAS_SIZE = (1024, 1024)
MAP_TOP = 320
GUIDE_COLOR = (255, 0, 255)


def main() -> None:
    source = Image.open(SOURCE).convert("RGB")
    if source.size != (1024, 384):
        raise RuntimeError(f"Unexpected map size: {source.size}")
    canvas = Image.new("RGB", CANVAS_SIZE, GUIDE_COLOR)
    canvas.paste(source, (0, MAP_TOP))
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    canvas.save(OUTPUT)
    print(OUTPUT)
    print("map rectangle", (0, MAP_TOP, 1024, MAP_TOP + 384))


if __name__ == "__main__":
    main()
