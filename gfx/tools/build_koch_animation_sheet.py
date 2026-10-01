#!/usr/bin/env python3
"""Build the ratio-corrected KOCH background and animation sheet."""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "gfx/result/scenes/koch"
OUTPUT_DIR = ROOT / "resources/game/classic_newgfx/gfx/backgrounds"

# Original placement is (186, 50), with a 76x43 animation frame. Converted
# from the 320x200 game canvas to the supplied 600x450 ratio-corrected canvas.
CROP_BOX = (349, 113, 492, 210)
FRAME_SIZE = (143, 97)
FRAME_COUNT = 12


def main() -> None:
    frames = [
        Image.open(SOURCE_DIR / f"koch_frame{i}.png").convert("RGB")
        for i in range(FRAME_COUNT)
    ]
    if any(frame.size != (600, 450) for frame in frames):
        raise ValueError("KOCH source frames must all be 600x450")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    frames[0].save(OUTPUT_DIR / "koch.png")

    sheet = Image.new("RGB", (FRAME_SIZE[0] * FRAME_COUNT, FRAME_SIZE[1]))
    for index, frame in enumerate(frames):
        sheet.paste(frame.crop(CROP_BOX), (index * FRAME_SIZE[0], 0))
    sheet.save(OUTPUT_DIR / "koch_ani.png")


if __name__ == "__main__":
    main()
