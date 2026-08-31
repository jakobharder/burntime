# Input layout

## Selection

- **Hover** and **focus** are the same navigation target: mouse mode presents it as
  hover, keyboard/gamepad mode as focus.
- Mouse mode has no focused item when nothing is hovered.
- Entering a page in keyboard/gamepad mode immediately focuses its first enabled item.
- Switching from mouse to keyboard/gamepad keeps focus on the hovered item, or uses
  the first enabled item when nothing is hovered.
- Directional input is applied relative to that focus.
- Mouse click activates the hovered item. Enter and Gamepad A activate only an item
  that had visible focus before the button press; otherwise they only establish focus.
- The resulting **active item**, such as a playing track, active tab or marked save,
  is persistent state and separate from hover/focus.

## Action mappings

| InputAction | Keyboard | Gamepad | Context
| --- | --- | --- | ---
| `Move*` - primary direction | Arrow keys | Left stick | all
| `PanCamera*` - secondary direction | W/A/S/D | Right stick | all
| `Primary` | Space / Enter | A | all
| `Secondary` | F | X | all except options; setup uses X and Shift+Up/Down
| `GlobalAction` | X | Y | all except options
| `Back` | Backspace / Escape* | B | all except setup
| `Options` | O | Menu | both maps, setup
| `Statistics` | Q / Shift+Left | D-pad left / Left shoulder | both maps
| `Inventory` | R / I | D-pad up | both maps
| `WorldMap` | V / M | View | location map
| `LocationInfo` | E / Shift+Right | D-pad right / Right shoulder | both maps
| `NextTurn` | Hold Tab | Hold D-pad down | both maps
| `ToggleInteractionMode` | C | — | both maps
| `LeftArea` | Q / Shift+Left | Left shoulder | all but maps
| `RightArea` | E / Shift+Right | Right shoulder | all but maps

The default keyboard and gamepad mappings are configured in the `[keyboard]` and `[gamepad]` sections of `settings.txt`.
Prompt rows and map-menu shortcut columns resolve their controls from the active mappings and omit unbound actions. Composite navigation hints and scene-specific controls such as map `Escape` are explicit exceptions.

When no input mode has been established yet, any keyboard press activates keyboard mode. The same applies when switching from gamepad to keyboard. While mouse mode is active, only an arrow-key press switches to keyboard mode; other keys remain shortcuts shared with mouse control and do not hide the mouse cursor or replace mouse mode.

On both maps, `Escape` opens the context menu and `O` opens Options directly. On the location map, `Backspace`, Gamepad B, `V`, `M`, or Gamepad View opens the world map. Contextual map actions advertise `Space`; `Enter` remains an equivalent `Primary` binding but is not shown in the prompt overlay.

Map context menus show available direct shortcuts beside their matching entries. Those shortcuts remain active while the menu is open. Entries without a direct binding leave the shortcut column blank. The world map omits its Travel/Info-mode entry when the menu is opened with keyboard or gamepad; a mouse-opened menu retains it and shows `C` as its toggle shortcut.

`GlobalAction` is a scene-level command: e.g. start game, accept trade, or open a context menu. The maps additionally map `Escape` to this action for their prompt overlays; the configured `X` binding remains functional.

On map scenes, tap `Tab` or `D-pad down` has no effect.
The logic for holding is to prevent a single tap to initiate a turn.
Travel on the world map - which also initiates turns - needs `Move*` then `Primary`, hence no extra holding required.

`ToggleInteractionMode` is a legacy keyboard shortcut retained for mouse play. It switches mouse clicks between primary and secondary behavior; pure keyboard and gamepad play do not require it.

## Text input

In text-input scenes, printable keyboard characters, `Space` and `Backspace` are reserved for text and never invoke their mapped actions.
When no text input is active, typing may activate the input associated with the current selection; the scene defines whether this behavior applies.
Text changes take effect immediately; leaving the input does not commit or cancel them. Active, selected and normal text inputs need distinct visual states.
Gamepad actions remain available; names are selected or generated without gamepad text entry.

### Game setup

- Player 1 starts enabled with a generated name, while Start has focus.
- Arrow keys, D-pad and the left stick navigate.
- `Tab` switches between Player 1 and Player 2. `Shift+Tab` switches in reverse. From the button group, Tab prefers Player 1 and Shift+Tab prefers Player 2 when both are enabled.
- While an enabled player is selected, `Shift+Left/Right` or Gamepad LB/RB selects the previous/next face. `Shift+Left/Right` is the general keyboard equivalent of LB/RB throughout the game.
- While an enabled player is selected, `Shift+Up/Down` or Gamepad X swaps the two player colors.
- Menu opens Options.
- `Enter` or Gamepad `A` toggles the selected player on/off. At least one player remains enabled.
- A mouse click on another player selects it without changing whether it is enabled. Clicking the selected player toggles it.
- Typing while a disabled player is selected enables them and starts a new name with the typed character.
- Manual names survive disabling and re-enabling a player. Backspacing a name to empty returns it to automatic-name behavior; leaving an enabled empty name field generates a new random name.

### Save and load

- The page contains one scrolling column. `[NEW SAVE]` is the first entry and creates an automatically named save for the first human player.
- Existing saves are ordered by file modification time, newest first. Up/down moves and scrolls the list.
- `Enter` or Gamepad `A` on `[NEW SAVE]` saves immediately. On an existing save it marks that save in blue and moves to the load/save/delete actions; `Enter` executes the selected action.
- The red keyboard cursor and mouse hover are transient. Load, save and delete act on the save marked in blue.
- While the list has focus, the bottom row shows details for the entry under the keyboard cursor. Mouse hover previews the same details. It changes to the action buttons when actions have focus or the mouse leaves the list.
- Two global autosaves rotate before committed travel. Their player names are shown in brackets. They can be loaded but not deleted; saving while one is selected creates a new manual save.
- Loading an autosave suppresses rotation before the next committed travel. Opening or cancelling travel does not consume this suppression.
- Left/right navigates the action buttons as displayed.
- Gamepad `A` follows `Enter`; D-pad and either stick follow arrow-key navigation.

### Options radio

- `Tab` cycles forward through Back, Saves, Jukebox, Settings and Give Up; `Shift+Tab` cycles backward.
- Gamepad LB/RB cycles backward/forward through the same entries. The Back entry shows an empty left panel; confirm it to leave Options.
- Arrow keys, D-pad and sticks navigate only inside the active page and never change the radio entry.
- The red bulb marks the active radio entry. Blue text is used only for mouse hover.

## Direction behavior

| Context | Primary direction | Secondary direction |
| --- | --- | --- |
| World map | Move location selection | Pan camera |
| Location map | Move the character | Pan camera |

On map scenes in keyboard or gamepad mode, releasing the secondary direction returns the camera to the controlled character or the player's current world-map location. In mouse mode, WASD panning leaves the camera at its new position.

Outside map scenes, primary and secondary directions behave identically. They remain separate actions to allow future differences.

## Context-sensitive shortcuts

| Context | Q / Shift+Left / Left shoulder | E / Shift+Right / Right shoulder |
| --- | --- | --- |
| World and location maps | `Statistics` | `LocationInfo` |
| Inventory, Trader | `LeftArea` | `RightArea` |
| Any other scene using areas | `LeftArea` | `RightArea` |
