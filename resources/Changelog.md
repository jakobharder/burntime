﻿# Burntime Changelog

## 1.0.4 (2024-12-03)

### Changes

- Ensure players don't start too close
- Location specific item placement at game start
- Show cheated games in statistics

### Fixes

- Fixed severial music related issues
- Fixed issue with item names not rendered correctly in inventory
- Fixed dialog with Socks in Nameless Town
- Fixed various smaller text issues

## 1.0 - Feature complete (2024-02-25)

### 1.0.3

- Fixed crash when NPC tried to eat in New Village
- Show message when a rope is required
- Enable cheats at runtime
- Added separate skin for technicians
- Various text corrections

### 1.0.2

- Fixed crash with 6 people in your group - or rather prevent that
- Fixed crash when starting without AI players
- Fixed crash log
- Corrected "Inventar" in German translation

### 1.0.1

- Fixed New Village water source for older saves
- Corrected a few German spelling errors
- You can now safely copy over Burntime versions again

### Changes

- Added ropes to a few traders to sell
- Danger warnings in UI are less prominent
- Remastered info scene background, context menu
- Reduced hit sound volume
- Added version to radio and start screens

### Fixes

- Fixed AI player's shadow remaining after death
- Fixed doctors not healing stationed NPCs
- Fixed production miscalculation in locations where AI players died
- Fixed New Village name, rooms etc.
- Fixed radio construction
- Fixed Nob Hill room background
- Fixed "X Tag", now says "Tag X"
- Fixed item flickering when rooms are very full
- Fixed context menu language not changing

## 0.9 - Balancing and some graphics remaster (2023-11-05)

### 0.9.1

- Fixed non-existing rifle_1 showing in 1993 mode

### Changes

- Increase trader, dog and mutant health and damage on higher difficulties
- Adjust attack/defense value display to already include experience
- Camps automatically select better traps if production is too low
- Show gas and radiation warning in main UI
- Remastered graphics in start menu and options
- Rebalance items in rooms after takeover

### Fixes

- Fixed crash when starting with music set to off
- Fixed font reloading after language change

## 0.8 - Gameplay Improvements (2023-11-01)

### Changes

- Automatically equip weapons and protection
- Don't use guns when dogs or mutants attack you
- Right click on an rope item to get a hint
- Support German letter `ß` in texts
- Improved randomness of trader items
- Show food and water reserves in main UI
- Show day and camp numbers in save game menu

### Fixes

- Fixed a potential crash when entering a trader with intro
- Fixed inventory not opening after killing a dog

## 0.7 - Options, Trader and Amiga (2023-10-29)

### Changes

- Improved trader
  - Show both trader and player inventory on wide screens
- Reworked options
  - Change language in options
  - Toggle fullscreen in options
  - Show fullscreen and remaster graphics shortcuts
  - Jukebox to play all songs
- Remastered graphics
  - Player flags
  - Cursor animations
  - Trader intro scenes
  - Inventory UIs
- Added Amiga sounds, music
  - Amiga version hit, bark and die sounds
  - Switch to Amiga music in options
- Start game with language selection
- Save user settings
- Scroll maps with right mouse button

### Fixes

- Fixed draw order of characters on map
- Fixed music in monastery
- Ensure 4:3 screen portion is always on screen, independent of window size

## 0.6 - Music Support (2023-10-21)

### Changes

- Added music support
- Remastered graphics
  - Player icons on maps
  - Dropped items
  - Statistics screen
- Toggle fullscreen with F11 as well

### Fixes

- Fixed flickering items in inventory with groups
- Fixed not localized text input

## 0.5 - Remastered Characters (2023-10-15)

### 0.5.1

- Fixed scaling issue in environments using comma as decimal separator (e.g. German)

### Changes

- Toggle fullscreen/windows with Alt+Enter
- Remastered graphics
  - All remaining location maps
  - Characters
  - Ruin scenes
- Don't center mouse when the game is not focused

## 0.4 - Remastered Graphics (2023-10-10)

### Game

- Remastered graphics
  - Items, room backgrounds
  - Location maps
  - Toggle between remastered and original with `F2` or in options menu.
- Clothes increase defense value when equipped
- Replaced "Sie" with "du"
- Removed dish from random spawn

### Technical

- Fixed a crash when immediately loading a save
- Ported game to MonoGame because SlimDX stopped development ~2012. This also means a step back to .NET 6
- Adjust resolution on window resize
- Improved path finding performance
- Centered dialog on all resolutions
- Fixed NPC characters going into unwalkable areas
- Fixed location names on main map positioned too low
- Preload map tiles to avoid pop in
- Remove settings.txt from `<user>/Save Games/Burntime`

### Known Issues

- Sound has not been ported yet to MonoGame

## 0.3 - Comeback (2023-09-24)

### Game

- Added no AI toggle to start menu
- Added extended game mode to start menu
- Center maps when they are smaller than the screen
- Enlarged very small locations to fill 16:9 screens

### Technical

- Included original graphics package - no original game install is required anymore
- Enabled fullscreen
- Updated to .NET 7

## Burntime v0.2.3 (2015-5-30)

- Added exit button to the main menu
- Fixed issue with AI getting stuck after a while
- Fixed 9 food with maggots issue
- Added cheats to the Launcher
- Fixed some NPC selection and path finding issues
- Paper helmet now protects only 20% radiation and gas
- updated code to work with .NET 4.5
known issues:
- fullscreen does not work with Windows 8 / 10

## Burntime v0.2.2 (2011-11-13)

- Fixed missing walkable info for new tiles
- Fixed item numbers in info screen.

## Burntime v0.2.1 (2011-11-13)

- Blocked 8 pixel borders on map for NPCs and items
- Improved ranges for entering rooms in a group
- Axe and pitchfork increase maggots production
- Rat trap and trap construction hint now visible when at least one item is available
- Fixed mouse over issue in technician dialog

## Burntime v0.2 (2011-10-24)

- Increased size of all other maps to fit screen size
- Added a small gap in the Monastry's walls on the east side
- Fixed glitch in intro video

## Burntime v0.1.5 (2011-10-22)

- Removed outdated pages and Deluxe from Launcher
- Increased size of Hard Mans Death to fit screen size

## Burntime v0.1.4 (2011-10-20)

- Changed screen ratio from 8:5 to 4:3
- Merged versions/packages into one

## Burntime Classic v0.1.3 (2011-10-18)

- Increased resolution
- Added widescreen support
- crash when entering location where AI player died fixed

## Burntime Classic v0.1.2.1 (2009-09-08)

- Fixed eating in rooms (again)
- Fixed AI crash when hiring NPCs
- Completed high-res font

## Burntime Classic v0.1.2 (2009-09-06)

- Fixed eating/drinking with full food/water when right-clicking on an item in rooms
- Fixed crash after creating a camp at reststop
- Added greeting to when talking to a npc the first time
- Added high-res font

## Burntime Classic v0.1.1.2 (2009-09-02)

- Fixed crash when opening inventory
- Added new gfx support

## Burntime Classic v0.1.1.1 (2009-09-01)

- Fixed crash when using protection suits
- Fixed wrong drinking
- Fixed wrong pump boost
- Fixed industrial pump construction
- Fixed items disappearing in full rooms
- Fixed npc/player selection problems in own camps
- Various other small bugs fixed

## Burntime Classic v0.1.1 (2009-09-01)

- First AI routines
- Added enable/disable AI switch to options
- Fixed crash when construction a rat trap
- Fixed wrong drinking/eating in groups
- Further balancing of start parameters
- Adjusted food/water values of hired npcs
- Various small bugs fixed

## Burntime Classic v0.1.0.1 (2009-08-26)

- Added special character support for other languages

## Burntime Classic v0.1 (2009-08-24)

- New launcher with download and auto-update
- Balancing of start parameters
- Data is now stored in pak files
- Various small bugs fixed

## Burntime Classic v0.0.10 (2009-08-13)

- Difficulty
- Random item generation at game start
- Rebalanced fighting
- User path (User/My Games/Burntime Classic) for settings and savegames
- New special items
- Fixed various bugs in game logic
- Fixed various crashes
- Fixed corrupt save game when overwriting a bigger save game with a smaller one fixed
- Fixed crash when using 64 bit with music on
- Many other bugs fixed

## Burntime Classic v0.0.9 (2009-07-30)

- Intro added
- Progress of a turn is now saved properly
- Trader sell only predefined items (not randomly anymore)
- Main character waits for fellowers before entering a room
- Mutants/Dogs attack player character
- Application recover after alt+tab
- Other small bugs fixed

## Burntime Classic v0.0.8 (2009-07-19)

- Ogg music support
- Character will now talk/attack/open inventory/enter room automatically when the object is reached
- Group member will now join fights
- Damage depending on weapon and experience
- Killed dogs drop meat
- Bug report button in launcher
- Bug fixed in reststop. It is not possible to enter view
- Game freezes fixed when viewing two different scenes (trader,
  view, ruin)
- Other small bugs fixed

## Burntime Classic v0.0.7a (2009-07-06)

- Victory added
- (Simple) fighting added
- Hiring npcs will change body gfx according their class
- Adjusted rope distance for Snake Hills and Big Hole
- Protection and weapons are now selectable
- Radiation, gas damage/protection added
- Npcs/player drop items on death
- Pathfinding improved
- Random crash fixed when trading
- Crash fixed in info when no production is available
- Other small bugs fixed
- Crash in info scene fixed