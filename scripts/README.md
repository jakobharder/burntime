# Development scripts

- Run commands from the repository root.

## Headless AI simulation

Run a deterministic four-AI game without opening a window:

```sh
scripts/ai-simulate.sh --turns 100 --difficulty hard --seed 123 --report ai-run.txt
```

- `--turns`: number of turns; default `100`.
- `--difficulty`: `easy`, `normal`, or `hard`; default `hard`.
- `--ai-difficulties`: four comma-separated per-player difficulties.
- `--ai`: `none`, `dos`, `amiga`, or `modern`; default `modern`.
- `--ai-profiles`: four comma-separated per-player profiles, overriding `--ai`.
- `--rules`: `dos`, `amiga`, `classic`, or `extended`; default `dos`.
- `--seed`: random seed for reproducible runs; default `1`.
- `--report`: optional output file; without it, the report is printed to the terminal.
- `--economy-report`: write structured per-turn economy JSON and continue past victory while a Modern AI remains.
- `--extended`: compatibility shorthand for `--rules extended`.
- `--load-save`: start the simulation from an existing `.sav` instead of a new game.
- `--save-at-end`: save the resulting game after the requested turns complete.
- `--smoke-test`: assert the selected rules/profiles and reject early non-combat
  deaths for DOS and Modern AI; Amiga AI is currently exempt.
- `--early-death-turn`: last turn considered early by `--smoke-test`; default `60`.

The report summarizes player condition, travel, camps, stationed NPCs, and major timeline events.

Continue a player save for 25 turns and write a new save:

```sh
scripts/ai-simulate.sh --load-save old.sav --turns 25 --save-at-end continued.sav
```

## Tests

### Save-game compatibility

Place historical save fixtures below `tests/savegames`, grouped by release, and run:

```sh
scripts/savegame-test.sh
```

Every `.sav` is loaded recursively and advanced through at least one complete
turn, including human-controlled player slots. See
[`tests/savegames/README.md`](../tests/savegames/README.md) for the fixture layout.

### AI baselines

```sh
scripts/ai-refactor-check.sh
```

### Gameplay formulas

```sh
scripts/rules-test.sh
```

### Rules and AI smoke tests

```sh
scripts/ai-smoke-test.sh
```

### Modern AI economy

```sh
scripts/ai-economy-test.sh
```

See [economy test checks and mutation tests](../tests/ai-economy/README.md) for thresholds,
coverage rules, smaller development runs and failure reports.
