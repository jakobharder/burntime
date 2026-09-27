# Rendered scene regression tests

This suite runs the real MonoGame window and resource loader in separate classic,
newgfx, and classic-no-hints processes. It requires .NET 10, a graphical desktop/OpenGL context,
and Python with Pillow. It does not work with the headless simulation renderer.

From the repository root:

```sh
python3 -m venv artifacts/visual-build/venv
artifacts/visual-build/venv/bin/pip install -r tests/visual/requirements.txt
scripts/visual-test.sh
```

Alternatively set `PYTHON` to an existing interpreter with Pillow installed.
Use `--mode classic`, `--mode newgfx`, or `--mode classic-no-hints` for a smaller
run, and `--skip-build` when the dedicated build is already current. The default
is `--mode all` (all three variants); `--mode both` still runs classic and newgfx.
`classic-no-hints` uses classic graphics and the normal Hide UI hints setting
(`prompts=2`), disabling tooltip panels and input prompts. The other variants
explicitly use Full hints (`prompts=0`).
The game's legacy item-name labels remain visible with hints off.

Each run writes a new directory under `artifacts/visual/` and prints the path to
`report.html`. It always keeps actual PNGs, including partial results after a
failure. The report shows the baseline, actual image, and magenta changed pixels.
`results.json` records pixel counts and failures; `process.log` and
`actual/resources.log` retain process output and resource requests. All resource
warnings fail the run independently of pixel comparisons. A hung process is
terminated after four minutes; an unsettled scene times out after one minute.

## Baseline review

Baselines live in `tests/visual/baselines/classic`, `newgfx`, and
`classic-no-hints`. Missing baselines
are failures, including the first run: review the generated images before
establishing the initial baseline. The test never rewrites baselines.

After inspecting a report, explicitly accept selected images:

```sh
scripts/visual-accept.sh --run artifacts/visual/RUN --mode newgfx --scenario inventory
# Repeat --scenario to select several; --all explicitly selects the whole mode.
scripts/visual-accept.sh --run artifacts/visual/RUN --mode classic --all
```

Commit the resulting PNGs alongside intentional visual changes. Acceptance
refuses any mode with resource errors, a crash, or an incomplete capture suite.
Fix those first and capture again. For experimental comparisons, both commands
support `--baselines PATH` to use a disposable baseline directory.

The original `GES_12.ANI` trader portrait currently contains an offset beyond the
end of the file. The classic trader scenario exposes the existing loader warning
`frame out of index in ges_12.ani`. This is deliberately reported as a resource
failure, not ignored or approved by updating images.

## Determinism and coverage

The suite uses Steam Deck test mode at 1280x800, with production xBR2 output for
newgfx and nearest-point output for classic variants, English,
keyboard prompts, seeded generation, disabled music, and an isolated user folder
inside each run. It never reads or writes the player's settings or saves. The
options screen uses a constant version label so a new commit does not cause a
visual difference. Gameplay and sprite animation time stay at zero except for
60 fixed 1/60-second menu frames after loading, allowing the portrait cover to
open before capture. Scene fades
advance at a fixed rate, and the wall-clock sprite loading fade is disabled.
Real delayed resource loading stays active. The test drives scene updates and
rendering on the graphics thread, then waits for loading and scene fades to
finish and three complete settled frames before reading the final backbuffer.
This includes classic's direct-to-framebuffer elements.
Captured pixels are made opaque without changing their RGB values: the backbuffer
has already composited translucent UI, so PNG viewers must not blend it a second time.

`source/Burntime.Remaster/VisualTestScenes.cs` contains the ordered, deliberately
fixture list: menu in keyboard and mouse modes, all three setup-notes pages, options, world map, field manual, location, inventory, room, trader,
doctor, pub, restaurant, information, statistics, church, and return to the map.
Options opens the settings page. Doctor offers a snake with patient health at 30, pub offers an empty
bottle, and restaurant offers a knife using normal navigation and Space/Enter
actions. Offers are returned and inventory order restored before leaving each
fixture, and patient health is restored; the service is not purchased.
Add scenarios there using normal scene activation and known game state. No
serialized state fields or save fixtures are needed.

Comparison uses exact decoded RGBA pixels, including alpha and dimensions, not
PNG file bytes. Keep a consistent OS/GPU/driver environment for approved images;
this suite does not promise identical rendering across graphics drivers. It
checks fixed animation states and the selected scenes, not every resource,
animation frame, display filter, resolution, or gameplay-thread race.

Test the comparer and acceptance safeguards without opening the game:

```sh
artifacts/visual-build/venv/bin/python -m unittest discover -s tests/visual -p 'test_*.py'
```

## Setup notes

Game setup opens Notes with the global Notes prompt (F1 / gamepad View by default).
`resources/game/classic/lang/en/setupnotes.txt` contains its tab labels, comparison
with the originals, and credits. Patch Notes reads the embedded
`resources/Changelog.md` directly: edit that changelog and rebuild, with no second
copy to maintain. The small renderer supports the changelog's headings and bullets
and wraps text to the manual width. Patch notes currently use the changelog's English.

The setup-notes fixture opens the modal through physical F1 and gamepad View input,
and the menu-mouse capture checks that the Notes shortcut stays visible in mouse mode.

Newgfx captures use production xBR2 by default, including the smaller setup-notes
body font. The viewport is 455x237 logical pixels, with an 853x533 internal target,
a 1706x1066 xBR2 pass, and 1280x800 output. Older point-filtered newgfx baselines
need explicit review and replacement. `--native-filter` additionally enables
normal game filtering for classic variants; it is redundant for newgfx.
The tests still use keyboard prompts and fixed Xbox glyph settings, not live
Steam Deck input detection, and desktop GPU captures are not hardware certification.
Font descriptors define `line_height` in logical pixels; setup body text uses
`font-small.txt`, while headings and controls retain the regular font.
