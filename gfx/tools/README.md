# Burntime graphics upscaling tools

This directory contains the reproducible part of the local AI-upscaling
pipeline. Third-party executables, model weights, and the Python virtual
environment live under `.cache/` and are intentionally ignored by Git.

## Install

The installer currently supports macOS on Apple Silicon and Intel. It downloads
the official universal Real-ESRGAN NCNN/Vulkan executable, official RealESRNet
weights, two Upscayl comparison models, and Pillow.

```sh
gfx/tools/install.sh
```

Installed model aliases:

- `conservative`: `realesrnet-x4plus`, the default for tiny face features.
- `wdn`: `RealESRGAN_General_WDN_x4_v3`, a lightweight comparison.
- `siax`: `4x_NMKD-Siax_200k`, a sharper painted-art comparison.
- `general`: `realesrgan-x4plus`, a more generative general model.

## GES_00 example

This applies the `1.875x2.25` project scale, producing 128x124 content from the
68x55 input. It places that content at the top-left of a transparent 128x128
canvas and writes an undithered 40-frame GIF.

```sh
gfx/tools/upscale.sh \
  gfx/original/faces/GES_00.ANI.gif \
  resources/game/classic_newgfx/gfx/faces/ges_00_realesrnet.gif \
  --model conservative \
  --canvas 128x128 \
  --anchor top-left
```

Run the same source through the comparison models:

```sh
gfx/tools/upscale.sh gfx/original/faces/GES_00.ANI.gif /tmp/ges_00_wdn.gif \
  --model wdn --canvas 128x128
gfx/tools/upscale.sh gfx/original/faces/GES_00.ANI.gif /tmp/ges_00_siax.gif \
  --model siax --canvas 128x128
```

PNG inputs work the same way. Use `--content WIDTHxHEIGHT` to override the
ratio-derived content dimensions. Alpha is always resized separately. The
default `--alpha-resample smooth --alpha-levels 5` treats the original one-bit
mask as pixel coverage and produces `0, 64, 128, 191, 255` alpha. Use
`--alpha-levels 0` for continuous PNG alpha, `--alpha-resample lanczos` for a
direct filtered resize, or `--alpha-resample nearest --alpha-levels 2` for the
old hard-edged behavior.

GIF supports only binary transparency. Give an animated input a `.png` output
extension to write APNG and retain the smoothed five-level alpha mask:

```sh
gfx/tools/upscale.sh \
  gfx/original/faces/GES_00.ANI.gif \
  /tmp/ges_00_realesrnet.png \
  --model conservative --canvas 128x128
```

Use `--tta` only for final comparisons. It is slower and should not be mixed
between frames or assets in the same set.

## Third-party sources

- Real-ESRGAN and RealESRNet: <https://github.com/xinntao/Real-ESRGAN> (BSD-3-Clause).
- Real-ESRGAN NCNN/Vulkan: <https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan> (MIT).
- Upscayl custom NCNN models: <https://github.com/upscayl/custom-models>.
- Pillow: <https://python-pillow.github.io/> (MIT-CMU).

Versions, model commits, download hashes, and Python dependencies are pinned in
`install.sh` and `requirements.txt`.

## Downscaled item results

`downscale_items.py` produces the final 60x72 transparent item comparisons
from the 120x144 geometry-true repaints:

```sh
gfx/tools/.cache/venv/bin/python gfx/tools/downscale_items.py
```

For every GST item, the script prefers
`gst_XX_geometry_true_before_palette_fix.png` when that backup exists and
otherwise uses `gst_XX_geometry_true.png`.

It writes two RGBA files to `gfx/result/items`:

- `gst_XX_nearest.png`: RGB artwork reduced with nearest-neighbor sampling.
- `gst_XX.png`: RGB artwork reduced with bilinear sampling and then mapped,
  without dithering, to the map-0 Burntime palette from `MAT_000.RAW`.

Both variants use the same alpha mask. The mask removes only near-black
background pixels connected to a canvas edge and is reduced separately with
bilinear filtering, producing a soft transparency boundary. No extra black
outline is added.

## World-map paths

`upscale_paths.py` reproduces the in-game xBR2 shader for the red path pieces
in `SYST.RAW` frames 28–111. Multi-piece paths are joined before filtering and
split afterwards, so xBR sees across the original 32-pixel seams. A temporary
one-pixel transparent margin prevents point-clamped outer endpoints; its exact
2x margin is removed before ratio correction, so placement and 60-pixel slice
boundaries do not change. The 2x xBR result is resized to the newgfx
`1.875x2.25` ratio-correct scale, converted to the original `(208, 0, 0)` red,
and its grayscale result is stored as alpha.

```sh
gfx/tools/.cache/venv/bin/python gfx/tools/upscale_paths.py
```
