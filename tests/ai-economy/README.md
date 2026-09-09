# Extended AI economy smoke tests

Run from the repository root:

```sh
scripts/ai-economy-test.sh
```

The Linux pull-request workflow runs this suite and uploads its reports even on failure.

Requires the .NET SDK and Python 3 (standard library only). The suite builds once,
then runs three fixed seeds (29, 71, 123) at Easy, Normal and Hard on Extended
rules. Per-player difficulty is explicitly set, so local `ai.txt` slot overrides
cannot silently change the test difficulty. There are 18 games:

- **Uncontested:** one Extended AI and three None slots, **200 turns**.
- **Competitive:** four Extended AIs, **500 turns**.

Economy simulations continue after victory or after only one faction remains.
They stop early if no Extended AI survives. This changes only the headless run's
stopping condition, not AI decisions or game mechanics.

For a smaller development run:

```sh
scripts/ai-economy-test.sh --modes solo --difficulties hard --seeds 71
```

Reports remain under `artifacts/ai-economy-test/results`: `Summary.md`, detailed
window measurements in `results.json`, and each game's raw JSON, timeline and
process log. A failed game process cannot reuse a stale successful snapshot.
The command exits nonzero on execution errors, insufficient suite coverage, or
failed economic thresholds. `--record-only` is for calibration, not CI.

## Checks

Measurements come directly from world state, not telemetry text. An eligible
camp is suitable for food production, has water, has been owned for **40 turns**,
and has had no observed combat damage for **20 turns**. Ownership changes reset
its age, including changes reversed within the same turn. Recovery grace applies
locally; an attack does not exempt the faction's rear camps.

After the initial 50/100 turns, evaluate non-overlapping **50-turn uncontested**
or **100-turn competitive** windows. A window needs at least 50/100 eligible
camp-days and 20/40 days with an eligible camp.

| Check | Required result |
|---|---|
| Economic base | At least one qualified window with a camp supplying **2 surplus food and 2 surplus water/day** for at least 20% of its days |
| Resident sustainability | **85%** of eligible camp-days can feed and water the stationed guards |
| Productive coverage | **50%** of eligible camp-days have working tools, a food surplus and sufficient water |
| Supply failure | No observed boss supply death outside combat/hazard grace; fewer than 3 supply deaths in any full window; guard distress on at most **10%** of eligible camp-days; fewer than **8/15 days** with supply damage while mature camps exist (uncontested/competitive) |

Uncontested factions must finish 200 turns alive and provide at least two
qualified windows. Competitive factions retain credit for progress before losing
territory or dying. Early military defeat without a qualified window is
**insufficient coverage**, never a passing result. Repeated supply deaths still
fail even if the resulting camp losses eliminate later measurement coverage.

For each difficulty, at least **two-thirds** of uncontested seeds must pass.
For competitive games, at least **half** the faction histories must provide
coverage, and **75%** of covered histories must pass. Thus a single unlucky seed
can vary, but widespread regressions and wholesale early elimination cannot
make the suite green. Smaller explicitly selected suites use rounded-up counts.

Supply damage is inferred from daily health loss while food or water is zero,
including lethal drops smaller than the ordinary 25-point penalty. Known hazard
exposure and damage within 20 turns of observed combat are excluded. This is an
economic warning signal, not a general engine death-cause classifier.

Food stock and net camp-food withdrawals during AI actions are diagnostic only.
Withdrawals can include initial loot or direct consumption and are not treated
as proven production income. Full stores are not a failure: the six-item cap can
indicate a healthy reserve. No particular trap, pump, trade count, inventory or
final camp count is required.

## Sensitivity and maintenance

The evaluator has small regression tests for defeated factions, local grace,
missing coverage, sustained shortages, late collapse and suite aggregation:

```sh
python3 -m unittest discover -s tests/ai-economy -p 'test_*.py'
```

After building the normal app, verify that real AI regressions are detected:

```sh
python3 scripts/ai-economy-mutation-test.py \
  --app artifacts/ai-economy-test/app/Burntime.dll \
  --output artifacts/ai-economy-test/mutations
```

This first requires an unmodified control to pass, then builds disposable source
copies with (1) production installation/construction disabled and (2) local loot,
produced-goods collection and explicit party provisioning disabled. It expects
both suites to fail economic assertions; build failures or crashes do not count
as successful detection. The working source and normal runtime are untouched.

Thresholds were calibrated against all 18 baseline games and verified with both
mutations. They are absolute economic floors, not golden timelines. Investigate
failed windows before changing them. DOS/Amiga rules and mixed opponent profiles
are not yet calibrated for this suite; use the existing general smoke matrix
for those combinations.
