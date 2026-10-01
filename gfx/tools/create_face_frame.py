#!/usr/bin/env python3
"""Create a smooth 4x ratio-correct overlay from the original GES frame."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "resources/game/classic_newgfx/gfx/faces/ges_frame_4x.png"
TARGET = (256, 256)
CONTENT = (255, 248)
SUPERSAMPLE = 8


def octagon(inset: float) -> list[tuple[float, float]]:
    """Original 68x55 frame geometry, inset without changing its slopes."""
    return [
        (13 + inset, 0 + inset),
        (55 - inset, 0 + inset),
        (68 - inset, 13 + inset),
        (68 - inset, 42 - inset),
        (55 - inset, 55 - inset),
        (13 + inset, 55 - inset),
        (0 + inset, 42 - inset),
        (0 + inset, 13 + inset),
    ]


def scaled(points: list[tuple[float, float]]) -> list[tuple[int, int]]:
    sx = CONTENT[0] / 68.0 * SUPERSAMPLE
    sy = CONTENT[1] / 55.0 * SUPERSAMPLE
    return [(round(x * sx), round(y * sy)) for x, y in points]


def main() -> None:
    work_size = (TARGET[0] * SUPERSAMPLE, TARGET[1] * SUPERSAMPLE)
    frame = Image.new("RGBA", work_size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(frame)

    # Nested bands sampled from the original frame's black/navy/purple highlights.
    bands = [
        (0.00, (9, 7, 12, 255)),       # exterior black outline
        (0.55, (22, 20, 45, 255)),     # deep navy
        (0.95, (70, 66, 119, 255)),    # blue-violet body
        (1.35, (119, 124, 184, 255)),  # narrow upper-style highlight
        (1.75, (76, 75, 132, 255)),    # medium blue return
        (2.35, (33, 30, 66, 255)),     # inner navy
        (3.05, (7, 6, 11, 255)),       # interior black outline
    ]
    for inset, color in bands:
        draw.polygon(scaled(octagon(inset)), fill=color)

    # The transparent center begins just inside the original black outline.
    draw.polygon(scaled(octagon(3.72)), fill=(0, 0, 0, 0))

    # Very slight color softening at working resolution; geometry stays crisp.
    alpha = frame.getchannel("A")
    rgb = frame.convert("RGB").filter(ImageFilter.GaussianBlur(0.65 * SUPERSAMPLE))
    frame = rgb.convert("RGBA")
    frame.putalpha(alpha)

    frame = frame.resize(TARGET, Image.Resampling.LANCZOS)
    # Keep the ratio-correct content rectangle exact; the remaining canvas is padding.
    clipped = Image.new("RGBA", TARGET, (0, 0, 0, 0))
    clipped.alpha_composite(frame.crop((0, 0, CONTENT[0], CONTENT[1])), (0, 0))
    frame = clipped
    frame.save(OUTPUT)
    print(f"Wrote {OUTPUT}: {TARGET[0]}x{TARGET[1]}, content={CONTENT[0]}x{CONTENT[1]}")


if __name__ == "__main__":
    main()
