# Input layout

## Selection

- Hover and focus share one navigation target.
- Active is persistent and separate from hover/focus.
- Keyboard/gamepad mode: if nothing is focused, the first item is focused when entering a page or switching from mouse.

## Action mappings

| InputAction | Role | Uses |
| --- | --- | --- |
| `Primary` | Primary interaction | Select, enter, open, talk |
| `Secondary` | Information | Inspect, details, info |
| `Action` | Consequential action | Travel, fight, buy, trade, consume, heal |
| `Back` | Leave current context | Back, close, cancel, map actions |

| InputAction | Keyboard | Gamepad | Context |
| --- | --- | --- | --- |
| `Move*` | Arrow keys | Left stick | All |
| `PanCamera*` | W/A/S/D | Right stick | All |
| `Primary` | Space / Enter | A | All |
| `Secondary` | F | X | All except Options |
| `Action` | Q | Y | Scene-specific |
| `Back` | Escape | B | All |
| `Options` | O | Menu | Maps, setup |
| `Statistics` | H | D-pad left | Maps |
| `Inventory` | E / I | D-pad up | Maps |
| `WorldMap` | V / M | View | Location map |
| `LocationInfo` | R | D-pad right | Maps |
| `NextTurn` | Hold T | Hold D-pad down | Maps |
| `ToggleInteractionMode` | C | — | Maps |
| `ShowEntrances` | Hold Alt | Hold Left Trigger | Maps |
| `LeftArea` | Shift+Left | Left shoulder | Previous character/page |
| `RightArea` | Shift+Right | Right shoulder | Next character/page |
| `PreviousTarget` | Shift+Tab | — | Previous nearby target |
| `NextTarget` | Tab | Right trigger | Next nearby target |

On the location map, `Action` (Q/Y) fights and `Secondary` (F/X) opens the
available group actions.

Prompt position is normally inferred from the action. A dynamic prompt with
different actions specifies its shared position explicitly. On the world map,
Enter (`Primary`) and Travel (`Action`) share the Primary prompt position.

LB/RB maps to Shift+Left/Right for horizontal choices and Shift+Up/Down for vertical choices.

## Input mode activation

| Current mode | Input | Result |
| --- | --- | --- |
| Unset | Any keyboard key | Keyboard mode |
| Gamepad | Any keyboard key | Keyboard mode |
| Mouse | Arrow key | Keyboard mode |
| Mouse | Other shortcut | Mouse mode retained |
| Any | Mouse movement | Mouse mode |

## Text input

- Printable keys, Space, and Backspace edit text and do not trigger mapped actions.
- Text changes apply immediately.
- Active, selected, and normal inputs have distinct visual states.

## Direction behavior

| Context | Primary direction | Secondary direction |
| --- | --- | --- |
| World map | Move location selection | Pan camera |
| Location map | Move character | Pan camera |
| Other scenes | Navigate | Navigate |

Releasing gamepad/keyboard camera input on maps returns the camera to the controlled target.

## Location interaction mode

- Talk is the scene-local default and is restored whenever the location scene activates.
- Right click always opens the actions menu. C and the menu toggle left click between Talk and Fight.
- Auto remains implemented but is currently not entered by the location scene.
