#!/usr/bin/env python3
"""Pre-render the SYST.RAW world-map paths with the game's xBR2 filter."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "gfx/original/SYST.RAW"
OUTPUT_DIR = ROOT / "resources/game/classic_newgfx/gfx/ways"

# One entry per WayProcessor path. Pieces are drawn from left to right at
# 32-pixel intervals by the game. Frame 67 is not used by any path.
PATHS = (
    (28,), (30,), (31, 32), (33, 34, 35), (36, 37, 38), (39,), (40,),
    (41, 42), (43, 44, 45), (46, 47), (48,), (49, 50), (51, 52),
    (53, 54), (29,), (55, 56), (57,), (58,), (59, 60), (61, 62),
    (63,), (64, 65, 66), (68, 69), (70,), (71, 72), (73, 74, 75, 76),
    (77,), (78, 79, 80), (81,), (82, 83), (84,), (85, 86), (87,),
    (88,), (89, 90), (91,), (92,), (93, 94), (95, 96), (97,),
    (98, 99), (100,), (101, 102), (103, 104, 105, 106), (107, 108),
    (109,), (110, 111),
)

RED = (208, 0, 0)
X_SCALE = 1.875
Y_SCALE = 2.25
LUMA = 48.0
EQ_THRESHOLD = 25.0
LV2_COEFFICIENT = 2.0


def _swizzle(values: list, order: str) -> list:
    indices = {"x": 0, "y": 1, "z": 2, "w": 3}
    return [values[indices[channel]] for channel in order]


def _difference(a: list[float], b: list[float]) -> list[float]:
    return [abs(x - y) for x, y in zip(a, b, strict=True)]


def _weighted_distance(
    a: list[float], b: list[float], c: list[float], d: list[float],
    e: list[float], f: list[float], g: list[float], h: list[float],
) -> list[float]:
    return [
        abs(av - bv) + abs(av - cv) + abs(dv - ev) + abs(dv - fv)
        + 4.0 * abs(gv - hv)
        for av, bv, cv, dv, ev, fv, gv, hv in zip(
            a, b, c, d, e, f, g, h, strict=True
        )
    ]


def _mix(a: float, b: float, amount: float) -> float:
    return a + (b - a) * amount


def xbr2(mask: Image.Image) -> Image.Image:
    """CPU port of source/Burntime.MonoGame/Content/Xbr2.fx for a B/W mask."""
    source = mask.convert("L")
    pixels = source.load()
    width, height = source.size
    output = Image.new("L", (width * 2, height * 2))
    result_pixels = output.load()

    def sample(x: int, y: int) -> float:
        x = min(width - 1, max(0, x))
        y = min(height - 1, max(0, y))
        return pixels[x, y] / 255.0

    for output_y in range(height * 2):
        y = output_y // 2
        fraction_y = 0.25 if output_y % 2 == 0 else 0.75
        for output_x in range(width * 2):
            x = output_x // 2
            fraction_x = 0.25 if output_x % 2 == 0 else 0.75

            a1, b1, c1 = sample(x - 1, y - 2), sample(x, y - 2), sample(x + 1, y - 2)
            a, b, c = sample(x - 1, y - 1), sample(x, y - 1), sample(x + 1, y - 1)
            d, center, f = sample(x - 1, y), sample(x, y), sample(x + 1, y)
            g, h, i = sample(x - 1, y + 1), sample(x, y + 1), sample(x + 1, y + 1)
            g5, h5, i5 = sample(x - 1, y + 2), sample(x, y + 2), sample(x + 1, y + 2)
            a0, d0, g0 = sample(x - 2, y - 1), sample(x - 2, y), sample(x - 2, y + 1)
            c4, f4, i4 = sample(x + 2, y - 1), sample(x + 2, y), sample(x + 2, y + 1)

            bv = [b, d, h, f]
            cv = [c, a, g, i]
            ev = [center] * 4
            dv = _swizzle(bv, "yzwx")
            fv = _swizzle(bv, "wxyz")
            gv = _swizzle(cv, "zwxy")
            hv = _swizzle(bv, "zwxy")
            iv = _swizzle(cv, "wxyz")
            i4v = [i4, c1, a0, g5]
            i5v = [i5, c4, a1, g0]
            h5v = [h5, f4, b1, d0]
            f4v = _swizzle(h5v, "yzwx")

            bv = [value * LUMA for value in bv]
            cv = [value * LUMA for value in cv]
            ev = [value * LUMA for value in ev]
            dv = [value * LUMA for value in dv]
            fv = [value * LUMA for value in fv]
            gv = [value * LUMA for value in gv]
            hv = [value * LUMA for value in hv]
            iv = [value * LUMA for value in iv]
            i4v = [value * LUMA for value in i4v]
            i5v = [value * LUMA for value in i5v]
            h5v = [value * LUMA for value in h5v]
            f4v = [value * LUMA for value in f4v]

            ao = [1.0, -1.0, -1.0, 1.0]
            bo = [1.0, 1.0, -1.0, -1.0]
            co = [1.5, 0.5, -0.5, 0.5]
            ax = [1.0, -1.0, -1.0, 1.0]
            bx = [0.5, 2.0, -0.5, -2.0]
            cx = [1.0, 1.0, -0.5, 0.0]
            ay = [1.0, -1.0, -1.0, 1.0]
            by = [2.0, 0.5, -2.0, -0.5]
            cy = [2.0, 0.0, -1.0, 0.5]
            ci = [0.25] * 4
            fx = [ao[n] * fraction_y + bo[n] * fraction_x for n in range(4)]
            fx_left = [ax[n] * fraction_y + bx[n] * fraction_x for n in range(4)]
            fx_up = [ay[n] * fraction_y + by[n] * fraction_x for n in range(4)]

            restriction0 = [ev[n] != fv[n] and ev[n] != hv[n] for n in range(4)]
            restriction_left = [ev[n] != gv[n] and dv[n] != gv[n] for n in range(4)]
            restriction_up = [ev[n] != cv[n] and bv[n] != cv[n] for n in range(4)]
            delta = [0.5] * 4
            delta_left = [0.25, 0.5, 0.25, 0.5]
            delta_up = _swizzle(delta_left, "yxwz")

            def coverage(value: float, change: float, edge: float) -> float:
                return min(1.0, max(0.0, (value + change - edge) / (2.0 * change)))

            fx45i = [coverage(fx[n], delta[n], co[n] + ci[n]) for n in range(4)]
            fx45 = [coverage(fx[n], delta[n], co[n]) for n in range(4)]
            fx30 = [coverage(fx_left[n], delta_left[n], cx[n]) for n in range(4)]
            fx60 = [coverage(fx_up[n], delta_up[n], cy[n]) for n in range(4)]

            wd1 = _weighted_distance(ev, cv, gv, iv, h5v, f4v, hv, fv)
            wd2 = _weighted_distance(hv, dv, i5v, fv, i4v, bv, ev, iv)
            edri = [wd1[n] <= wd2[n] and restriction0[n] for n in range(4)]
            edr = [wd1[n] < wd2[n] and restriction0[n] for n in range(4)]
            edri_yzwx = _swizzle(edri, "yzwx")
            edri_wxyz = _swizzle(edri, "wxyz")
            edr = [edr[n] and (not edri_yzwx[n] or not edri_wxyz[n]) for n in range(4)]
            diff_fg = _difference(fv, gv)
            diff_hc = _difference(hv, cv)
            equal_ec = [abs(ev[n] - cv[n]) < EQ_THRESHOLD for n in range(4)]
            equal_eg = [abs(ev[n] - gv[n]) < EQ_THRESHOLD for n in range(4)]
            edr_left = [
                LV2_COEFFICIENT * diff_fg[n] <= diff_hc[n]
                and restriction_left[n] and edr[n] and not edri_yzwx[n] and equal_ec[n]
                for n in range(4)
            ]
            edr_up = [
                diff_fg[n] >= LV2_COEFFICIENT * diff_hc[n]
                and restriction_up[n] and edr[n] and not edri_wxyz[n] and equal_eg[n]
                for n in range(4)
            ]
            blend = [
                max(
                    fx30[n] if edr_left[n] else 0.0,
                    fx60[n] if edr_up[n] else 0.0,
                    fx45[n] if edr[n] else 0.0,
                    fx45i[n] if edri[n] else 0.0,
                )
                for n in range(4)
            ]
            use_f = [abs(ev[n] - fv[n]) <= abs(ev[n] - hv[n]) for n in range(4)]

            result1 = center
            result1 = _mix(result1, _mix(h, f, float(use_f[0])), blend[0])
            result1 = _mix(result1, _mix(b, d, float(use_f[2])), blend[2])
            result2 = center
            result2 = _mix(result2, _mix(f, b, float(use_f[1])), blend[1])
            result2 = _mix(result2, _mix(d, h, float(use_f[3])), blend[3])
            result = result2 if abs(center - result2) >= abs(center - result1) else result1
            result_pixels[output_x, output_y] = round(min(1.0, max(0.0, result)) * 255)

    return output


def load_piece(frame: int) -> Image.Image:
    image = Image.open(SOURCE_DIR / f"{frame}.png").convert("RGBA")
    # SYST path pixels are binary red/transparent. Use only coverage so xBR2
    # cannot invent colors at the transparent boundary.
    return image.getchannel("A").point(lambda alpha: 255 if alpha >= 128 else 0)


def render_path(frames: tuple[int, ...]) -> dict[int, Image.Image]:
    pieces = [(frame, load_piece(frame)) for frame in frames]
    source_width = 32 * (len(pieces) - 1) + pieces[-1][1].width
    source_height = max(piece.height for _, piece in pieces)
    joined = Image.new("L", (source_width, source_height), 0)
    for index, (_, piece) in enumerate(pieces):
        joined.paste(piece, (index * 32, 0), piece)

    # Point-clamping a path that touches the combined texture edge makes xBR2
    # see the red endpoint as continuing forever. Give the filter one source
    # pixel of transparent context, then remove its exact 2x representation
    # before ratio correction. This does not change final sizes or slice points.
    padded = Image.new("L", (source_width + 2, source_height + 2), 0)
    padded.paste(joined, (1, 1))
    filtered = xbr2(padded).crop(
        (2, 2, 2 + source_width * 2, 2 + source_height * 2)
    )
    target_size = (round(source_width * X_SCALE), round(source_height * Y_SCALE))
    alpha = filtered.resize(target_size, Image.Resampling.LANCZOS)
    results: dict[int, Image.Image] = {}
    for index, (frame, piece) in enumerate(pieces):
        left = index * 60
        size = (round(piece.width * X_SCALE), round(piece.height * Y_SCALE))
        piece_alpha = alpha.crop((left, 0, left + size[0], size[1]))
        output = Image.new("RGBA", size, (*RED, 0))
        output.putalpha(piece_alpha)
        results[frame] = output
    return results


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT_DIR)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    expected = set(range(28, 112)) - {67}
    configured = {frame for path in PATHS for frame in path}
    if configured != expected:
        raise RuntimeError(f"PATHS mismatch: missing={expected - configured}, extra={configured - expected}")

    generated: dict[int, Image.Image] = {}
    for path in PATHS:
        generated.update(render_path(path))
    for frame, image in sorted(generated.items()):
        image.save(args.output / f"syst_{frame}.png", optimize=True)
    print(f"Wrote {len(generated)} ratio-correct xBR2 path pieces to {args.output}")


if __name__ == "__main__":
    main()
