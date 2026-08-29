# Burntime

Burntime is a remaster and expansion of Max Design's PC strategy game 'Burntime' from 1993. It includes improvements like doubled resolution and wide screen support.

![](./doc/screens.jpg)

## How to get

- [Steam](https://store.steampowered.com/app/3269080/Burntime_Remastered/) (Windows & SteamOS Proton)
- [Direct Download](https://github.com/jakobharder/burntime/releases) (Windows, MacOS & Linux)

## Notes

- Recent changes: [Changelog.md](./resources/Changelog.md)
- Issues &amp; requests: [GitHub issues](https://github.com/jakobharder/burntime/issues) or [Burntime.org (German forum)](https://www.burntime.org/forum/viewtopic.php?t=323)
- [Feature Overview](./resources/Features.md)

## Development

### Prerequisites

- [Git](https://git-scm.com/downloads) (used in build process to get the version tag)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MonoGame 3.8.5 tools:

```sh
dotnet tool restore --tool-manifest source/Burntime.MonoGame/.config/dotnet-tools.json
```

MonoGame framework packages are restored automatically by .NET.

### Build and run

```sh
dotnet build source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Debug
dotnet run --project source/Burntime.MonoGame/Burntime.MonoGame.csproj
```

### Headless AI simulation

Run a deterministic four-AI game without opening a window:

```sh
scripts/ai-simulate.sh --turns 100 --difficulty hard --seed 123 --report ai-run.txt
```

- `--turns`: number of turns; default `100`.
- `--difficulty`: `easy`, `normal`, or `hard`; default `hard`.
- `--seed`: random seed for reproducible runs; default `1`.
- `--report`: optional output file; without it, the report is printed to the terminal.
- `--extended`: optionally use the extended-game item set instead of 1993 rules.
- `--load-save`: start the simulation from an existing `.sav` instead of a new game.
- `--save-at-end`: save the resulting game after the requested turns complete.

The report summarizes player condition, travel, camps, stationed NPCs, and major timeline events.

For example, continue a player save for 25 turns and write a new save:

```sh
scripts/ai-simulate.sh --load-save old.sav --turns 25 --save-at-end continued.sav
```

### Save-game compatibility tests

Place historical save fixtures below `tests/savegames`, grouped by release, and run:

```sh
scripts/savegame-test.sh
```

Every `.sav` is loaded recursively and advanced through at least one complete
turn, including human-controlled player slots. See
[`tests/savegames/README.md`](tests/savegames/README.md) for the fixture layout.

Check that the standard AI baseline has not changed:

```sh
scripts/ai-refactor-check.sh
```

### Publish

```sh
dotnet publish source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Release -r <platform> --self-contained true -p:PublishSingleFile=true
```

Use one of the following instead of `<platform>`:
- `osx-arm64`, `osx-x64`, `linux-x64`, `win-x64`

Visual Studio users: open `source/Burntime.sln` and select `Burntime.MonoGame` as the startup project.

## Credits

Burntime Remastered is developed and maintained by Jakob Harder, with contributions from the community.

This project is not affiliated in any way with Max Design and/or the original creators.
The original game, graphics and other assets are the property of Max Design and their original creators.

A big thanks to Martin Lasser, Wilfried Reiter and Hannes Seifert for allowing this community remaster to use the original graphics and music!

See the full [list of contributors](./resources/README.md#credits).
