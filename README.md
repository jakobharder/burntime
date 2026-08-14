# Burntime

Burntime is a remaster and expansion of Max Design's PC strategy game 'Burntime' from 1993. It includes improvements like doubled resolution and wide screen support.

![](./doc/screens.jpg)

## How to get

- [Steam](https://store.steampowered.com/app/3269080/Burntime_Remastered/)
- [Zip Download](https://github.com/jakobharder/burntime/releases)

## Notes

- Recent changes: [Changelog.md](./resources/Changelog.md)
- Issues &amp; requests: [GitHub issues](https://github.com/jakobharder/burntime/issues) or [Burntime.org (German forum)](https://www.burntime.org/forum/viewtopic.php?t=323)
- [Feature Overview](./resources/Features.md)

## Development

### Prerequisites

- [Git](https://git-scm.com/downloads)
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

### Publish

```sh
# macOS, Apple Silicon
dotnet publish source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# macOS, Intel
dotnet publish source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# Windows, 64-bit
dotnet publish source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Visual Studio users: open `source/Burntime.sln` and select `Burntime.MonoGame` as the startup project.

## Credits

This project is not affiliated in any way with Max Design and/or the original creators.
The original game, graphics and other assets are the property of Max Design and their original creators.

A big thanks to Martin Lasser, Wilfried Reiter and Hannes Seifert for allowing this community remake effort to use the original graphics and music!

See full [list of contributors](./resources/README.md#notes)
