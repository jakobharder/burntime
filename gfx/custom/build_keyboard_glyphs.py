#!/usr/bin/env python3
"""Build Burntime's keyboard prompt atlas from Kenney outline key images."""

import argparse
from pathlib import Path

from PIL import Image


KEYBOARD_GLYPHS = (
    [f"keyboard_{letter}_outline.png" for letter in "abcdefghijklmnopqrstuvwxyz"]
    + [f"keyboard_{digit}_outline.png" for digit in "0123456789"]
    + [
        "keyboard_escape_outline.png",
        "keyboard_enter_outline.png",
        "keyboard_tab_outline.png",
        "keyboard_space_outline.png",
    ]
)

# The first 40 glyphs occupy the left 8x5 block. The remaining keyboard and
# mouse glyphs fill the two right-hand columns from top to bottom.
EXTRA_GLYPHS = (
    ("keyboard_ctrl_outline.png", (8, 0)),
    ("keyboard_shift_outline.png", (8, 1)),
    ("keyboard_alt_outline.png", (8, 2)),
    ("keyboard_arrow_up_outline.png", (8, 3)),
    ("keyboard_arrow_down_outline.png", (8, 4)),
    ("keyboard_arrow_left_outline.png", (9, 0)),
    ("keyboard_arrow_right_outline.png", (9, 1)),
    ("mouse_left_outline.png", (9, 2)),
    ("mouse_right_outline.png", (9, 3)),
)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Kenney Keyboard & Mouse/Default directory")
    parser.add_argument("output", type=Path, help="Output PNG atlas")
    parser.add_argument("--cell-size", type=int, default=22,
                        help="Atlas cell size (22 normally, 44 for the 2x atlas)")
    args = parser.parse_args()

    cell_size = args.cell_size
    columns = 10
    rows = 5
    atlas = Image.new("RGBA", (columns * cell_size, rows * cell_size))

    for index, filename in enumerate(KEYBOARD_GLYPHS):
        source = Image.open(args.source / filename).convert("RGBA")
        glyph = source.resize((cell_size, cell_size), Image.Resampling.LANCZOS)
        atlas.alpha_composite(glyph, ((index % 8) * cell_size,
                                      (index // 8) * cell_size))

    for filename, (column, row) in EXTRA_GLYPHS:
        source = Image.open(args.source / filename).convert("RGBA")
        glyph = source.resize((cell_size, cell_size), Image.Resampling.LANCZOS)
        atlas.alpha_composite(glyph, (column * cell_size, row * cell_size))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(args.output, optimize=True)


if __name__ == "__main__":
    main()
