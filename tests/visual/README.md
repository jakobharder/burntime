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

Original portrait tables such as `GES_12.ANI` can retain a stale 41st offset
(`0x460d`) before zero padding. The loader excludes that entry only when it is
out of bounds or points backward; valid frames with the same offset are retained.
Other invalid offsets still produce resource warnings and fail this suite.

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
fixture list: menu in keyboard mode, all three setup-notes pages, options, world map, field manual, location, inventory, room, trader,
doctor, pub, restaurant, information, statistics, church, and return to the map.
Both world-map fixtures press Left once after activation to select a neighboring
camp and display travel time. Options opens the settings page. Doctor offers a snake with patient health at 30, pub offers an empty
bottle, and restaurant offers a knife using normal navigation and Space/Enter
actions. Offers are returned and inventory order restored before leaving each
fixture, and patient health is restored; the service is not purchased.
Add scenarios there using normal scene activation and known game state. No
serialized state fields or save fixtures are needed.

Inventory and room open with the first inventory page active and no item focused.
The fixtures press Left to select the first inventory item; the room fixture then
presses Right three times to retain the room item tooltip in its capture.

`construction-undiscovered` and `construction-discovered` show a technician
focusing a spring with no tin or wire available. The first captures the unknown
trap recipe and Inspect prompt. The second uses the normal investigation
action, verifies that the recipe was learned, closes the dialog, and captures
the same material with the known recipe and Check materials prompt. Both also
run with hints disabled, where neither tooltip should appear.

The inventory fixture equips a steel helmet (armour), and the room fixture equips
a protective suit (gas and radiation protection). Each restores the original
inventory before the following fixture. Classic stats use `Dmg: minimum-maximum` and
`Def: percent` in both languages; newgfx retains localized ranges and uses the
small font for combat and hazard protection rows. All stat labels use colons;
hazard values follow their labels (`Gas: 100%`). Small type spells out Radiation
(Strahlung in German), while classic uses Rad. German supply labels use Essen
and Wasser with the compact day suffix T (9T in classic, 9 T in newgfx).
XP/EP are point counts without a percent sign. Newgfx English spells out Defense. For German review captures,
the private game runner also accepts `--language=de` after its output directory.

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

Game setup opens Notes with the NOTES button opposite EXIT. Ctrl / gamepad View
shortcuts remain available. The button participates in keyboard/gamepad focus navigation.
`resources/game/classic/lang/en/setupnotes.txt` contains its tab labels, comparison
with the originals, and credits. Patch Notes reads the embedded
`resources/Changelog.md` directly: edit that changelog and rebuild, with no second
copy to maintain. The small renderer supports the changelog's headings and bullets
and wraps text to the manual width. Patch notes currently use the changelog's English.

The setup-notes fixture selects NOTES with Down/Down/Left and activates it with Primary.
The following fixture also exercises physical Ctrl and gamepad View input.

Newgfx captures use production xBR2 by default, including the smaller setup-notes
body font. The viewport is 455x237 logical pixels, with an 853x533 internal target,
a 1706x1066 xBR2 pass, and 1280x800 output. Older point-filtered newgfx baselines
need explicit review and replacement. `--native-filter` additionally enables
normal game filtering for classic variants; it is redundant for newgfx.
The tests still use keyboard prompts and fixed Xbox glyph settings, not live
Steam Deck input detection, and desktop GPU captures are not hardware certification.
Font descriptors define `line_height` in logical pixels; setup body text uses
`font-small.txt`, while headings and controls retain the regular font.
