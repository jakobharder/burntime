Burntime face AI repaint context
================================

Source
------
- Use frame 0 from gfx/original/faces/GES_XX.ANI/0.png.
- Original face frames are 68x55 pixels.
- Treat the original as the sole source of truth for identity, pose, expression,
  hairstyle, clothing, lighting, and palette.

Geometry
--------
- Apply the ratio-correct 4x scale: 3.75 horizontal x 4.5 vertical.
- 68x55 has a nominal ratio-correct size of 255x248.
- Final files use a 256x256 canvas.
- If ImageGen produces useful painted content across its full canvas, keep it:
  resize the complete generated composition to 256x256 without center-cropping,
  clipping it to 255x248, or forcing right/bottom padding transparent.

Repaint prompt requirements
---------------------------
- Create a faithful high-resolution repaint in gritty 1990s hand-painted game-art
  style, with crisp controlled edges and varied painted color detail.
- Remove the complete blue/black octagonal frame.
- Extend the existing background and character naturally into all space formerly
  occupied by the frame, filling the 255x248 rectangular content area.
- Keep each background close to the original's simple colors, gradients, and marks.
  Do not replace sparse graphic backgrounds with invented landscapes, clouds, or
  detailed scenery. For GES_00 specifically: flat red-to-orange/yellow gradient
  with sparse bright diagonal streaks from the sides.
- Do not crop or unnecessarily zoom the character.
- Do not add objects, accessories, text, borders, checkerboards, or watermarks.
- Avoid anime styling, vector styling, smooth airbrushing, and facial redesign.
- Normalize generated output locally to 256x256, preserving filled edge content;
  do not rely on the generator to produce exact dimensions.

Geometry true face upscale
--------------------------
- Use two references for every repaint:
  1. Use frame 0 from gfx/original/faces/GES_XX.ANI/0.png as the dominant
     primary reference and sole source of truth for identity, pose, expression,
     injuries/scars, hair, clothing, lighting, palette, background, and every
     object or accessory that actually exists.
  2. Use gfx/upscale-repaint/ges_XX_nearest.png only as a ratio-correct geometry
     reference for silhouette, head angle, facial-feature placement, shoulder
     position, character scale, margins, and overall composition. Ignore its
     rendering style, enlarged pixels, and blue octagonal frame.
- Be rather faithful. The original image always overrides an interpretation
  suggested by the nearest-neighbor geometry reference.
- Completely omit the blue/black octagonal frame, but keep its geometry and
  reserved space. Removing the frame must reveal and extend the background in
  the frame area; it must not enlarge, zoom, or move the face and character to
  consume that space.
- Preserve enough margin around the head, face, hair, shoulders, and existing
  objects for the original frame geometry to be overlaid later without covering
  or intersecting them. Use the nearest reference to verify this safe area.
- Extend the original background to every image edge. Extend existing character
  or clothing artwork only where the source composition requires it; do not use
  character enlargement as a substitute for filling the removed-frame region.
- Prefer the organic hand-painted finish of the corrected GES_06 hybrid pass:
  gritty early-1990s post-apocalyptic game art, smooth natural painted contours,
  fine brush texture, nuanced material and skin detail, and controlled color
  variation. It should be crisp but not photorealistic, glossy, anime, vector,
  cyberpunk, or deliberately pixelated.
- Do not reproduce the nearest image's blocky squares, pixel grid, mosaic texture,
  nearest-neighbor appearance, or low-resolution simulation.
- Burntime is a grounded Mad Max-like wasteland. Existing ambiguous details may
  be interpreted using scars/injuries or crude practical materials such as worn
  cloth, leather belts, wood, bone/skulls, and simple battered weapons, but only
  when the original visibly supports that interpretation.
- Never invent scars, injuries, weapons, belts, skulls, jewelry, straps,
  technology, equipment, clothing, or other features. Do not heal, beautify,
  symmetrize, modernize, or redesign existing facial damage or anatomy.
- Preserve the original sparse background, palette, object count, and lighting.
  Do not add scenery, patterns, props, text, borders, transparency, checkerboards,
  or watermarks.
- Fill the complete square canvas with artwork, then normalize the complete result
  locally to 256x256 without cropping or changing the established geometry.
- Save experimental results under a new descriptive or versioned filename in
  gfx/upscale-repaint; never overwrite an existing variant unless explicitly
  requested.

Character-specific fidelity notes
---------------------------------
- GES_11 and GES_27: retain the original very dark, nearly black brown-purple
  interior background; do not invent bright scenery or patterned panels.
- GES_12: the viewer-right half of the face is exposed burned flesh. Preserve a
  naturally broad head and avoid horizontally squeezed facial proportions.
- GES_15: cloth turban and goggles, with a wooden staff topped by a real skull;
  loose cloth bands wave left. Nothing is technological.
- GES_21: a conjoined two-headed human mutant. Eyes remain normally shaped human
  eyes, not large round alien eyes.
- GES_24: preserve a broad natural head and face; avoid horizontal squeezing.
- GES_29: grounded, dirty wasteland / Mad Max styling with crude scrap, leather,
  wraps and battered pipe; never sleek cyberpunk technology.

Generation method
-----------------
- Built-in ImageGen edit workflow, one face per call.
- Final project files are PNG RGBA.

Geometry true item upscale
--------------------------
- Items GST_00 through GST_57 use the English names from BURN.TXT lines 51
  through 108 as interpretation hints. The names clarify ambiguous source
  pixels only; they must not cause new parts, objects, labels, or details to be
  invented.
- Use gfx/original/items/gst_XX.png as the dominant reference and sole truth for
  object identity, silhouette, orientation, component count, palette, wear,
  lighting, and composition.
- First composite the original transparency over pure black. Enlarge from 32x32
  to 128x128 with nearest-neighbor sampling, then ratio-correct with
  nearest-neighbor sampling to 120x144. Save this geometry reference as
  gfx/upscale-repaint/items/gst_XX_nearest.png.
- Use the nearest image only to lock the exact object footprint, proportions,
  placement, margins, and silhouette. Ignore its blocky rendering style.
- There is no blue frame to remove. Preserve all original empty margins and do
  not zoom, recenter, beautify, modernize, repair, add or remove components, or
  change the number of objects.
- Repaint as faithful gritty early-1990s post-apocalyptic hand-painted inventory
  art with natural contours, restrained fine texture, nuanced materials, and
  source-faithful colors. Avoid pixel grids, anime, vector, glossy catalog art,
  and invented science-fiction details.
- Ignore source transparency. Final item repaints are fully opaque RGB PNGs on
  a solid pure-black 120x144 canvas.
- After ImageGen, normalize the repaint back into the nearest reference's exact
  non-black bounding box and coordinates. Save it as
  gfx/upscale-repaint/items/gst_XX_geometry_true.png.
- Use one built-in ImageGen edit call per item.
