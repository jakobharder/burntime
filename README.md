# Burntime

Burntime is a remaster and expansion of Max Design's PC strategy game 'Burntime' from 1993.

![](./doc/screens.png)

## Get the Game

Download it from [Releases](https://github.com/jakobharder/burntime/releases).

## Features

The game is a complete port of the original game to Windows with some adjustments and an extended game mode.

- improved graphics
  - wide-screen support with enlarged images, map view etc.
  - remastered graphics with doubled resolution
- play with DOS music or Amiga music
- game adjustments
  - attacks are always followed by counter attacks to avoid attack spamming
  - toggle to switch between original and extended game
  - toggle to disable AI players
- quality of life
  - attack/defense/protection values displayed in inventory
  - auto equip weapons and protection
- minor location adjustments
  - some locations are larger to fill wide screens
  - Monastery got another gap in the wall to go through
  - a new place called "New Village"

### Extended Game

The extended game mode adds new items, locations and gameplay features.
You can disable these changes in the start menu.

- additional items
- clothes provide a small defense boost

## Difficulty Levels

Generally, items are more rare on higher difficulty levels.
Respawning of NPCs, dog and others is adjusted.
Enemies are more aggressive.

### I Easy

- start items
  - meat, a full canteen, an empty bottle, and a knife
- respawns
  - trader after 20 days
  - NPCs after 10 days
  - dogs after 30 days
  - mutants after 30 days
- dropped items and in rooms
  - food, water, weapons and building materials can be found
- enemies
  - Make camps in the same pace of the player

### II Medium

- start items
  - snake, empty canteen and bottle, and a knife
- respawns
  - trader after 50 days
  - NPCs after 20 days
  - dogs after 100 days
  - mutants after 20 days
- weapons cannot be found
- dropped items and in rooms
  - no weapons
  - less food
- enemies:
  - make camps a bit faster than the player

### III Hard

- start items
  - maggots and a knife
- respawns:
  - trader after 150 days
  - NPCs after 30 days
  - dogs don't respawn
  - mutants after 5 days
- dropped items and in rooms:
  - no weapons or food
  - less materials
- enemies:
  - make camps independent of player progress

## Build and Debug

- open `source/Burntime.sln` in Visual Studio
- build solution (it will generate a `bin/burntime` folder)
- mark `Burntime` as the start-up project
- start

You can also mark the `Launcher` as start-up project to change settings and debug that part.

## Changes

See [Changelog.md](./resources/Changelog.md)

## Credits

This project is not affiliated in any way with Max Design and/or the original creators.
The original game, graphics and other assets are the property of Max Design and their original creators.

Thank you Martin Lasser for allowing non-commercial community remake efforts to use the original graphics!

See full [list of contributors](./resources/README.md#notes)
