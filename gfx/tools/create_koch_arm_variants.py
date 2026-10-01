#!/usr/bin/env python3
"""Create exact horizontal arm/rat offsets for the KOCH v2 repaint."""

from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "gfx/upscale/scenes/koch_pac_4x_faithful_repaint_v2.png"

# Tight silhouette around the rat and the character's extended left arm.
ARM_POLYGON = [
    (704, 363), (716, 341), (753, 326), (791, 339), (807, 367),
    (829, 390), (860, 391), (900, 357), (927, 323), (950, 322),
    (970, 354), (959, 389), (928, 421), (893, 454), (855, 467),
    (824, 445), (800, 422), (773, 427), (735, 423), (705, 403),
]


def shifted_variant(source: Image.Image, dx: int) -> Image.Image:
    mask = Image.new("L", source.size, 0)
    ImageDraw.Draw(mask).polygon(ARM_POLYGON, fill=255)
    # A one-pixel soft edge avoids a cutout seam without changing the offset.
    mask = mask.filter(ImageFilter.GaussianBlur(0.65))

    moved = Image.new("RGB", source.size)
    moved.paste(source, (dx, 0))
    moved_mask = Image.new("L", source.size)
    moved_mask.paste(mask, (dx, 0))

    # Replace only the thin strip uncovered by the translation using the nearest
    # pixels immediately outside the moving silhouette.
    hole = ImageChops.subtract(mask, moved_mask)
    fill = Image.new("RGB", source.size)
    fill.paste(source, (-dx, 0))
    result = Image.composite(fill, source, hole)
    return Image.composite(moved, result, moved_mask)


def main() -> None:
    source = Image.open(SOURCE).convert("RGB")
    for label, dx in (("right_02px", 2), ("left_02px", -2), ("left_04px", -4)):
        output = SOURCE.with_name(f"koch_pac_4x_faithful_repaint_v2_arm_{label}.png")
        shifted_variant(source, dx).save(output)
        print(output)


if __name__ == "__main__":
    main()
