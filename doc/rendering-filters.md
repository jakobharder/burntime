# Rendering filters and resolution pipeline

Burntime renders through an internal target before presenting the completed frame
to the native back buffer. The internal target depends on the graphics mode and
the display resolution. Output filtering controls the passes between that target
and the back buffer.

## Command-line modes

Normal startup couples filtering to the graphics profile: classic uses `SHARP`
and new-gfx uses `SMOOTH`. The settings menu has no separate filtering control.

The following mutually exclusive command-line switches restrict the renderer to
one safety or diagnostic mode:

| Switch | Menu label | Internal filter | Shader loading |
|---|---|---|---|
| none | `SHARP` | `SharpBilinearShader` | enabled |
| none | `SMOOTH` | `Xbr2` | enabled |
| `--no-shader` | `SHARP` | `SharpBilinear` | disabled completely |
| `--nearest-point` | `POINT` | `NearestPoint` | enabled, but not used by the filter |
| `--linear` | `LINEAR` | `Linear` | enabled, but not used by the filter |

If shader assets are unexpectedly unavailable during normal startup, the game
falls back to a supported sharp path, ultimately software `SHARP`
(`SharpBilinear`). The saved graphics-profile toggle overrides any older saved
filter value.

`--no-shader` is stronger than a filter selection: the renderer does not create
a shader content manager, inspect shader files, or attempt to load either effect.

## Filter implementations

### SHARP with shaders

The base target is normally presented with `SharpBilinearShader`. When the base
target fits the native back buffer at the same exact integer factor on both axes
(2x, 3x, 4x, and so on), the shader is bypassed and point sampling is used. This
keeps clean integer scaling exact and avoids an unnecessary shader pass.

### SHARP without shaders

`SharpBilinear` first enlarges the base target with point sampling to the smallest
integer size that meets or exceeds the required presentation scale. That target
is then linearly resampled to the native back buffer.

### SMOOTH

`Xbr2` enlarges the base target to exactly 2x using the XBR-Lv2 shader. The 2x
result is then presented with the sharp-bilinear shader.

Classic graphics are the only special case. Font glyphs and the software cursor
are excluded from the scene-wide XBR pass, then rendered onto the 2x XBR target
before presentation. The standard `font.txt` request resolves to `font2x.txt` in
this mode, retaining the 2x atlas detail without allowing XBR to reshape the
glyphs. Sprite order is preserved: the first deferred glyph starts the deferred
part of the queue. Explicitly deferred sprites use point sampling there, while
all later ordinary sprites, including the cursor and interaction-mode animation,
are individually processed by the alpha-aware XBR shader.

In new-gfx XBR2 mode, `font.txt` and `highres-font.txt` resolve to the 2x
`highres-font2x.txt` resource. It carries the same deferred marker, so the
layer-ordered cutoff and deferred rendering behavior is shared by both graphics
modes.

### POINT and LINEAR

`NearestPoint` and `Linear` directly present the base target using the matching
sampler. They are explicit diagnostic or compatibility modes and are only exposed
through `--nearest-point` and `--linear` respectively.

## Resolution selection

The automatic desktop output scale is selected from the native resolution.
Steam Deck and `--steam-deck` force an output scale of 1.5x.

| Native display | Output scale | New-gfx base target | Classic base target |
|---|---:|---:|---:|
| Steam Deck, 1280x800 | 1.5x | 853x533 | 455x237 |
| 1080p, 1920x1080 | 2x | 960x540 | 512x240 |
| 1440p, 2560x1440 | 3x | 853x480 | 455x213 |
| 4K, 3840x2160 | 4x | 960x540 | 512x240 |

New-gfx renders into `Resolution.BackBuffer`. Classic renders into
`Resolution.Game`. The classic target incorporates Burntime's non-square pixel
ratio correction before final presentation.

The 853-pixel targets are rounded from a fractional back-buffer size. At 1440p,
853 multiplied by 3 is 2559, so the final presentation accounts for the remaining
horizontal pixel.

## Pass matrix

| Graphics mode | SHARP shader | SHARP `--no-shader` | SMOOTH shader | POINT / LINEAR |
|---|---|---|---|---|
| Classic | Sharp shader, or point at a clean integer factor | Integer point pre-scale, then linear | XBR2 at 2x, deferred `font2x`, alpha-aware XBR cursor, then sharp shader | Direct presentation |
| New-gfx | Sharp shader, or point at a clean integer factor | Integer point pre-scale, then linear | XBR2 at 2x, deferred `highres-font2x`, alpha-aware XBR suffix, then sharp shader | Direct presentation |

Steam Deck uses the same pass matrix as other displays. Its only rendering-policy
difference is the forced 1.5x output scale; it does not have a separate filter
implementation.
