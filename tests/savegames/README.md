# Save-game compatibility fixtures

Store representative `.sav` files below a folder named for the release that
created them, for example:

```text
tests/savegames/
  v0.9/
    early-game.sav
  v1.0.4/
    established-ai.sav
```

Keep fixtures small and give each file a name that describes the relevant game
state. The compatibility test recursively loads every `.sav`, runs at least one
complete turn for all player slots (including human players), and fails if
loading or turn processing throws.

Run all fixtures with:

```sh
scripts/savegame-test.sh
```

Run more than one turn, or use another fixture folder, with:

```sh
scripts/savegame-test.sh tests/savegames --turns 5
```
