# Resolutions and Filters

## Resolutions and Pixel Ratio

### Original

The original game has a resolution of 320x200, usually displayed on 4:3 monitors. The game pixel width-to-height ratio is 1x1.2.

### Classic

The remaster also uses 320x200 internally for pixel-perfect rendering and positioning, but scales the output at a 1.875x2.25 ratio.
The ratio was chosen to avoid nearest-neighbor upscale artifacts while being close to the original pixel ratio.

Classic mode renders internally at non-upscaled square pixels.
It is ratio-corrected with 1.875x2.25 during render to native display.

### NewGfx

NewGfx mode uses already ratio-corrected graphics.
It therefore renders internally ratio-correct, which is then upscaled normally to native display.

### Internal Render Targets

The game renders to an internal target before scaling the result to the native display resolution.

For NewGfx, the target size is the native resolution divided by the selected scaling factor. The preferred target is at least 600x450 - the aspect-corrected, double-resolution equivalent of the original 320x200.

Classic uses the same scaling factor, but its internal target is also adjusted for the 1.875x2.25 aspect-ratio correction applied during presentation.

The Steam Deck is the exception: 2x would produce a target only 400 pixels high, so it uses 1.5x instead.

| Native display | Factor | NewGfx target | Classic target |
|---|---:|---:|---:|
| Steam Deck, 1280x800 | 1.5x | 853x533 | 455x237 |
| 1080p, 1920x1080 | 2x | 960x540 | 512x240 |
| 1440p, 2560x1440 | 3x | 853x480 | 455x213 |
| 4K, 3840x2160 | 4x | 960x540 | 512x240 |

## Upscaling Filters

| Mode | Filter | Note |
|---|---|---|
| Classic | `SharpBilinearShader` | One-pass sharp-bilinear upscaling |
| NewGfx | `Xbr2` | xBR2 upscale to 2x, followed by sharp-bilinear |
| `--no-shader` | `SharpBilinear` | Two-pass Sharp-bilinear upscaling without shaders |
| `--nearest-point` | `NearestPoint` | Direct nearest-neighbor upscaling |
| `--linear` | `Linear` | Direct bilinear upscaling |

### Sharp-bilinear

Sharp-bilinear uses nearest-neighbor when the output is an exact integer multiple of the internal target.

### xBR2

Fonts are not filtered with xBR2 because they loose their sharp corners. Instead, the game uses pre-upscaled font graphics.

The scene up to the font layer is filtered as a whole. Fonts and later sprites, such as the mouse cursor, are added separately to preserve their appearance and layer order.
