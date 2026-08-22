# AI Strategy

Computer players aim to expand and attack weak rivals.
Difficulty changes pace and combat aggressiveness.

## General Strategy

- **Survival:** Keeps groups supplied; recovers or retreats before trading or expanding. Uses only routes with a safe return. Doctors treat serious injuries; restaurants and pubs are ignored. No rescue supplies are generated.
- **Camps:** Travellers eat before surplus is exported. The AI favors productive, well-watered sites and links between territory, cities and future expansion. Camps need sustainable food, suitable equipment or neighboring support; stored food alone is insufficient. It prioritizes early maggot production, then traps, pumps and construction. Hazards require protection.
- **Cities:** Can recruit, barter or use a doctor, but never waits. Followers are raised to 3 food and 3 water each city turn; the boss gets this only while owning a camp. This aid is not portable.
- **Groups:** Normal travel groups have at most two people. A second traveller is used when sustainable or needed for a settlement, garrison or attack. Recruits use real goods or surplus; each starts with 5 food and 5 water.
- **Trade and conflict:** Trades real goods for useful equipment, especially trap parts, and always keeps a return route. Prefers neutral expansion, but prepares for weak or valuable enemy camps; it abandons unsafe attacks, retaliates when attacked, garrisons victories and retreats after failure.

## Difficulty Differences

| Behavior | I Easy | II Normal | III Hard |
| --- | --- | --- | --- |
| Normal travelling-group target | Up to 2 people | Up to 2 people | Up to 2 people |
| Temporary attack-group target | Up to 2 people | Up to 3 people | Up to 4 people |
| Preferred recruit experience | 0–40% | 20–60% | 40% or more |
| Pause after establishing a camp | 3–5 turns | 1–4 turns | 0–2 turns |
| Expansion relative to the leading human | At most 2 camps ahead | At most 5 camps ahead | No practical limit |
| Important-camp garrison target | 1 guard | 2 guards | 3 guards |
| Camp considered threatened | Opponent directly adjacent | Opponent within 2 connected locations | Opponent within 2 connected locations |
| Human camps normally considered for attack | Empty, or a lone unarmed/knife guard | At most 1 defender | Any defender count |
| Required estimated attack strength | About 135% of defenders | About 105% of defenders | About 75% of defenders, using a more detailed estimate |
| Preference for strategic enemy camps | None | Moderate | Strong |
| Pause after capturing an enemy camp | 4 turns | 2 turns | None |
| Heavy weapons | No pitchforks or guns | At most 1 pitchfork; no guns | At most 1 pitchfork; camps may use 1 gun |
| Effective value of AI trade offers | 100% | 120% | 150% |
| Effective value of AI recovery payments | 150% | 180% | 225% |

- Targets can be limited by food, recruitment, equipment and travel safety.
- Expansion limits do not apply without human players.

## Where does AI cheat?

Computer player generally must follow the same rules, with these exceptions:

- Can construct without a technician.
- Can pay recruits also with non-food/water items.
- May generate a missing trap component if it gets stuck.
- Normal and Hard receive a trade advantage
- AI player recover some food/water for free in cities - if they stil own a camp

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
