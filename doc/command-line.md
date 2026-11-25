# Command-line options

- Pass runtime options after `--` in `dotnet run`.

| Option | Purpose |
| --- | --- |
| `--steam-machine` | Half-display window, gamepad prompts, no fullscreen toggle |
| `--steam-deck` | 1280×800 window, 1.5× scaling, gamepad prompts, no fullscreen toggle |
| `--choose-language` | Open language selection |
| `--linear` | Use linear output scaling |
| `--fps` | Show FPS and texture memory |
| `--window-size WIDTHxHEIGHT` | Set the windowed client size |

### Normal-speed gameplay profiling (macOS/iOS)

On macOS, run `scripts/gameplay-profile.sh OUTPUT_DIRECTORY [30|60]`.
It builds Release and runs the map and a busy location sequentially, using the
normal two-thread game loop rather than the accelerated visual/soak-test loop.
Keep the game window active during each run. Each scenario warms up for at least
12 seconds and waits for loading/fades to finish before measuring 30 seconds.
The location contains a four-person party and relocated existing NPCs; the party
moves while NPCs retain their ordinary behavior. The map browses nearby camps.
These are within-turn measurements; strategic AI turns are excluded.

Each scenario writes `report.json`, a warmup `scene.png`, and isolated user
settings. Reports include whole-process CPU as a percentage of one core,
thread CPU and wall time by phase, allocations, collections, and achieved FPS.
Render submission and presentation are CPU-side timings, not GPU execution time.
Sorting, scene GPU-command submission, and scaling are nested inside render
submission; do not add those phases to their parent. Use Instruments separately
for sampling; collect untraced baselines to avoid including sampling overhead.

The opt-in environment variables also work in the iOS app:

| Variable | Values / default |
| --- | --- |
| `BURNTIME_PROFILE_GAMEPLAY` | `map` or `location`; unset disables profiling |
| `BURNTIME_PROFILE_FPS` | `30` (default) or `60`, independent of input mode |
| `BURNTIME_PROFILE_SECONDS` | Measurement duration, 10–120 seconds; default 30 |
| `BURNTIME_PROFILE_OUTPUT` | Output folder; default `BurntimeProfiles/SCENE-FPS` inside the temporary directory on macOS or Documents on iOS |
| `BURNTIME_PROFILE_MUSIC` | `1` enables music and the map playlist; default off |

The profiler never loads player saves. It creates a seeded fixture and directs
settings/saves to its output folder. iOS writes the report and stays open;
macOS exits after reporting. The native CPU clocks currently support macOS/iOS.
