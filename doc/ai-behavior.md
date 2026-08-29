# AI Behavior

Computer players aim to survive, expand, develop camps, and attack weak rivals.

## General Strategy

- **Survival:** Keep groups supplied and preserve a safe route home.
- **Expansion:** Prefer productive, well-watered camps that connect useful territory.
- **Production:** Establish food first, then traps, pumps, and construction.
- **Groups:** Recruit and provision only when the larger group remains sustainable.
- **Trade:** Buy survival supplies, mission equipment, and camp infrastructure.
- **Defense:** Garrison important or threatened camps.
- **Conflict:** Prefer safe targets, prepare before attacking, and retreat from bad plans.

## Difficulty Differences

| Behavior | Easy | Normal | Hard |
| --- | --- | --- | --- |
| Expansion | Limited | Moderate | Fast |
| Attack group | Up to 2 | Up to 3 | Up to 4 |
| Garrison | 1 guard | 2 guards | 3 guards |
| Human targets | Mostly empty camps | Weak camps | Any viable camp |
| Attack confidence | Cautious | Balanced | Aggressive |
| Trade advantage | None | Moderate | Strong |
| Failed attack retry | Very slow | Soon | Soon |

Actual plans remain limited by supplies, equipment, recruitment, and travel safety.

## Rule Exceptions

- Can construct without a technician.
- May receive limited city recovery and trade assistance.
- May generate a missing material after a prolonged deadlock.
- AI boss has 9 inventory slots.

## Configuration

- Profiles are set per player slot in `resources/game/classic/ai.txt`.
- Each slot can inherit the game difficulty or use `easy`, `normal`, or `hard`.
- Per-player difficulty is preserved in save games.
- Headless simulations can override the profiles for one run.

## Requirements

- hard AIs shall be able to successfully attack and conquer a camp within the first 100 turns
- AIs shall reach 3 camps within the first 30 turns
- AIs shall have 50% advanced trap coverage by turn 100
- AIs shall have on average at least 1 water container in stock per camp by turn 100
- AIs shall use only information human players would have
- AIs shall avoid cheating, only in rare stuck situations
- AIs shall use water pumps (if appropriate) by turn 200
- easy AIs shall not defeat human players, only infrequently attack to ensure human players learn to garrison
- easy AIs shall be easy to conquer
- AIs shall not starve on their own, only in combination of hostilities
- AIs shall attack with all they have till death when they are locked in between foreign camps (excluding cities and unsustainable locations)
  - except easy, there they shall simply die immediately
- AIs shall never be stuck in one place if there's nothing to do
