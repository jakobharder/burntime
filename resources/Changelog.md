# Burntime Changelog

## [Unreleased]

### Game

- NewGfx setting in options to toggle between original and remastered graphics (incomplete!)
- Clothes increase defense value when equipped
- Replaced "Sie" with "du"
- Dishes now spawned rarely

### Technical

- Ported game to MonoGame because SlimDX stopped development ~2012. This means also a step back to .NET 6
- Improved path finding performance
- Centered dialog on all resolutions
- Fixed NPC characters going into unwalkable areas
- Fixed location names on main map positioned too low

### Known Issues

- Sound has not been ported yet

## Burntime v0.3 (2023-09-24)

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