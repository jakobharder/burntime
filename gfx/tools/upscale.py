#!/usr/bin/env python3
"""Faithful Real-ESRGAN wrapper for Burntime PNG and GIF assets."""

from __future__ import annotations

import argparse
import math
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageFilter


TOOLS_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = TOOLS_DIR / ".cache" / "realesrgan"
BINARY = RUNTIME_DIR / "realesrgan-ncnn-vulkan"
MODELS = RUNTIME_DIR / "models"

MODEL_ALIASES = {
    "conservative": "realesrnet-x4plus",
    "wdn": "RealESRGAN_General_WDN_x4_v3",
    "siax": "4x_NMKD-Siax_200k",
    "general": "realesrgan-x4plus",
}


def dimensions(value: str) -> tuple[int, int]:
    try:
        width, height = value.lower().split("x", 1)
        result = int(width), int(height)
    except (ValueError, AttributeError) as exc:
        raise argparse.ArgumentTypeError("expected WIDTHxHEIGHT") from exc
    if result[0] <= 0 or result[1] <= 0:
        raise argparse.ArgumentTypeError("dimensions must be positive")
    return result


def materialize_frame(image: Image.Image, index: int) -> Image.Image:
    image.seek(index)
    rgba = image.convert("RGBA")
    rgba.load()
    return Image.frombytes("RGBA", rgba.size, rgba.tobytes())


def anchor_offset(
    content: tuple[int, int], canvas: tuple[int, int], anchor: str
) -> tuple[int, int]:
    if content[0] > canvas[0] or content[1] > canvas[1]:
        raise ValueError(f"content {content} does not fit canvas {canvas}")
    if anchor == "top-left":
        return 0, 0
    return (canvas[0] - content[0]) // 2, (canvas[1] - content[1]) // 2


def run_model(
    sources: list[Image.Image], model: str, work_dir: Path, tta: bool
) -> list[Image.Image]:
    input_dir = work_dir / "input"
    output_dir = work_dir / "output"
    input_dir.mkdir()
    output_dir.mkdir()

    for index, source in enumerate(sources):
        # Alpha is processed separately. Keeping transparent RGB black is
        # appropriate for Burntime assets, whose silhouettes use dark outlines.
        rgb = Image.new("RGB", source.size, (0, 0, 0))
        rgb.paste(source.convert("RGB"), mask=source.getchannel("A"))
        rgb.save(input_dir / f"frame-{index:04d}.png")

    command = [
        str(BINARY),
        "-i",
        str(input_dir),
        "-o",
        str(output_dir),
        "-s",
        "4",
        "-t",
        "0",
        "-m",
        str(MODELS),
        "-n",
        model,
        "-f",
        "png",
    ]
    if tta:
        command.append("-x")
    subprocess.run(command, check=True)
    results: list[Image.Image] = []
    for index in range(len(sources)):
        result = Image.open(output_dir / f"frame-{index:04d}.png").convert("RGB")
        result.load()
        results.append(result)
    return results


def process_frame(
    frame: Image.Image,
    enhanced: Image.Image,
    content_size: tuple[int, int],
    canvas_size: tuple[int, int],
    anchor: str,
    alpha_mode: str,
    alpha_levels: int,
) -> Image.Image:
    enhanced = enhanced.resize(content_size, Image.Resampling.LANCZOS)
    alpha = resize_alpha(frame.getchannel("A"), content_size, alpha_mode, alpha_levels)

    content = enhanced.convert("RGBA")
    content.putalpha(alpha)
    output = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    output.alpha_composite(content, anchor_offset(content_size, canvas_size, anchor))
    return output


def resize_alpha(
    alpha: Image.Image,
    size: tuple[int, int],
    mode: str,
    levels: int,
) -> Image.Image:
    if mode == "nearest":
        result = alpha.resize(size, Image.Resampling.NEAREST)
    elif mode == "lanczos":
        result = alpha.resize(size, Image.Resampling.LANCZOS)
    else:
        # Original Burntime alpha is a one-bit coverage mask. Upscale that mask
        # to a dense intermediate grid, then integrate it back down to the exact
        # target size. The slight blur suppresses ringing without expanding the
        # silhouette noticeably.
        supersampled = alpha.resize(
            (size[0] * 4, size[1] * 4), Image.Resampling.NEAREST
        )
        result = supersampled.resize(size, Image.Resampling.LANCZOS)
        result = result.filter(ImageFilter.GaussianBlur(radius=0.35))

    if levels > 1:
        steps = levels - 1
        result = result.point(
            lambda value: round(round((value / 255) * steps) * 255 / steps)
        )
    return result


def global_palette(frames: list[Image.Image]) -> Image.Image:
    columns = min(8, len(frames))
    rows = math.ceil(len(frames) / columns)
    width, height = frames[0].size
    montage = Image.new("RGB", (width * columns, height * rows), (0, 0, 0))
    for index, frame in enumerate(frames):
        montage.paste(frame.convert("RGB"), ((index % columns) * width, (index // columns) * height))
    return montage.quantize(
        colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE
    )


def gif_frame(frame: Image.Image, palette: Image.Image) -> Image.Image:
    indexed = frame.convert("RGB").quantize(
        palette=palette, dither=Image.Dither.NONE
    )
    alpha = frame.getchannel("A")
    pixels = indexed.load()
    alpha_pixels = alpha.load()
    for y in range(frame.height):
        for x in range(frame.width):
            if alpha_pixels[x, y] < 128:
                pixels[x, y] = 255
    indexed.info["transparency"] = 255
    return indexed


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Upscale a Burntime PNG or GIF with separate alpha handling."
    )
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--model",
        default="conservative",
        help="conservative, wdn, siax, general, or an installed NCNN model name",
    )
    parser.add_argument("--x-scale", type=float, default=1.875)
    parser.add_argument("--y-scale", type=float, default=2.25)
    parser.add_argument("--content", type=dimensions, help="override content size")
    parser.add_argument("--canvas", type=dimensions, help="transparent output canvas")
    parser.add_argument(
        "--anchor", choices=("top-left", "center"), default="top-left"
    )
    parser.add_argument(
        "--alpha-resample",
        choices=("smooth", "lanczos", "nearest"),
        default="smooth",
        help="smooth is supersampled coverage filtering; default: smooth",
    )
    parser.add_argument(
        "--alpha-levels",
        type=int,
        default=5,
        help="number of alpha coverage levels; 0 keeps continuous alpha; default: 5",
    )
    parser.add_argument("--tta", action="store_true", help="slower test-time augmentation")
    args = parser.parse_args()

    if not BINARY.is_file():
        parser.error(f"missing runtime; run {TOOLS_DIR / 'install.sh'}")
    if not args.input.is_file():
        parser.error(f"input not found: {args.input}")

    model = MODEL_ALIASES.get(args.model, args.model)
    if not (MODELS / f"{model}.param").is_file() or not (
        MODELS / f"{model}.bin"
    ).is_file():
        parser.error(f"model is not installed: {model}")

    source = Image.open(args.input)
    source_size = source.size
    content_size = args.content or (
        round(source_size[0] * args.x_scale),
        round(source_size[1] * args.y_scale),
    )
    canvas_size = args.canvas or content_size
    if args.alpha_levels == 1 or args.alpha_levels < 0 or args.alpha_levels > 256:
        parser.error("--alpha-levels must be 0 or an integer from 2 to 256")

    frame_count = getattr(source, "n_frames", 1)
    loop = source.info.get("loop", 0)
    durations: list[int] = []
    output_frames: list[Image.Image] = []
    args.output.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="burntime-upscale-") as temporary:
        work_dir = Path(temporary)
        input_frames: list[Image.Image] = []
        for index in range(frame_count):
            frame = materialize_frame(source, index)
            durations.append(source.info.get("duration", 100))
            input_frames.append(frame)

        enhanced_frames = run_model(input_frames, model, work_dir, args.tta)
        for frame, enhanced in zip(input_frames, enhanced_frames, strict=True):
            output_frames.append(
                process_frame(
                    frame,
                    enhanced,
                    content_size,
                    canvas_size,
                    args.anchor,
                    args.alpha_resample,
                    args.alpha_levels,
                )
            )

    if args.output.suffix.lower() == ".gif":
        palette = global_palette(output_frames)
        indexed = [gif_frame(frame, palette) for frame in output_frames]
        indexed[0].save(
            args.output,
            save_all=True,
            append_images=indexed[1:],
            duration=durations,
            loop=loop,
            transparency=255,
            disposal=2,
            optimize=False,
        )
        if any(
            value not in (0, 255)
            for value in output_frames[0].getchannel("A").getdata()
        ):
            print(
                "Warning: GIF supports only binary transparency; use an animated "
                "PNG output to retain smoothed alpha."
            )
    elif frame_count > 1 and args.output.suffix.lower() == ".png":
        output_frames[0].save(
            args.output,
            save_all=True,
            append_images=output_frames[1:],
            duration=durations,
            loop=loop,
            disposal=1,
            blend=0,
            optimize=False,
        )
    else:
        if frame_count != 1:
            parser.error("animated input requires a GIF or animated PNG output")
        output_frames[0].save(args.output)

    print(
        f"Wrote {args.output}: {canvas_size[0]}x{canvas_size[1]}, "
        f"{frame_count} frame(s), model={model}"
    )


if __name__ == "__main__":
    main()
