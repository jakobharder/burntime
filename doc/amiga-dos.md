# Original gameplay reference: Amiga and DOS

This is a behavioral specification for testing a compatible implementation. It covers player-visible rules, formulas, static data and balance. AI decision-making is deliberately excluded; see [amiga-dos-ai.md](amiga-dos-ai.md).

Reference files:

- Amiga AGA `BURNTIME`: SHA-1 `0fa958be24a9aa7ab074f5678ec4eebdd2cc66fc`.
- Primary DOS: German EXEPACK `BURN.EXE`, SHA-256 `83ab33996d752cbccc7af1e7baa4db3b960bcbf068de96edbbb29ac13acd3e83`.
- Secondary DOS variant: LZEXE `BURN.EXE`, SHA-1 `0df348b0f01fc0ce2a2d3168f69cf3db3c7124b0`; contains an immediate-arrival override.
- DOS `GAM.DAT`: SHA-1 `dad578bd5c228bcd3bb7e63fe681b97ec635960e`.

The [German reference recheck](../reverse-engineering/burntime-dos-de/analysis/reference-audit.md) records coverage, corrected findings and pending Remaster fidelity gaps. Unqualified DOS addresses retained below refer to the legacy LZEXE image; German addresses are explicitly labeled. Original behavior and paragraphs labeled Remaster must be read separately.

Unless a difference is called out, the rule is the same in both versions. Division is unsigned integer division: discard the fractional part. Item title IDs below are the executable's one-based IDs; item indices are zero-based.

## Round and character state

A world round completes after every living player slot has taken its turn. World maintenance then advances the day, updates travel, locations, characters and traders, and recomputes boss experience. Difficulty does not change survival, production, healing or normal on-map combat.

| Stat | Range |
|---|---:|
| Health | 0–100 |
| Food | 0–9 |
| Water | 0–5 |
| Experience | 0–99 |
| Carried items | 6 per character |

Food and water points belong to individual characters. Food and drink items contain points which can be distributed to characters. A travelling party contains the boss and at most four companions.

### Daily healing and consumption

The Remaster processes each employed character as follows, except for the
Amiga AI-party exemption described below. Original DOS instead uses world-wide
healing, decrement/resupply, water-penalty and food-penalty passes (German
`0x6a97..0x6ff2`); ordering can affect shared supplies:

1. Heal naturally.
2. Reduce water by one and try automatic resupply if it fell below zero.
3. If still below zero, set water to zero and subtract 25 health.
4. If health is no longer positive, remove the character immediately.
5. For survivors, repeat steps 2–4 for food.

Both shortages therefore cost 50 health in one round. A stat at zero survives until the next daily decrement.

| Situation | DOS | Amiga |
|---|---|---|
| Any employed character; no applicable modifier | At least 70 health: +2 | At least 70 health: +2 |
| Computer-controlled employee | No special rule | At least 50 health: +2 |
| Doctor in the character's travelling group | At least 50 health: +4 | At least 50 health: +4 |
| Doctor stationed at the character's owned camp | At least 50 health: +4 | At least 50 health: +4 |

“Ordinary” is the fallback for every employed character, including the boss and
recruited NPCs; it does not mean unowned NPCs. On Amiga the lower 50-health
threshold applies to every computer-controlled employee, including stationed
employees. It is keyed by the nonzero computer-player/difficulty field, not by
travel state. Human employees have no special travelling-healing branch. DOS
has no corresponding AI threshold. Healing is capped at 100, and a doctor
replaces rather than adds to the +2.

The Remaster selects correctly typed food and water. Original DOS has a camp-food
resupply defect: German `0x6dbf..0x6e3d` selects water titles, feeds their contents
as food and deletes the container through `0x29f3`. This bug is excluded; see the
reference recheck for the complete call path.

Normal typed automatic resupply consumes the smallest accessible suitable item first: maggots before rats, snake and meat; water bottle before canteen and wineskin. Owned camp reserves and room items can supply stationed employees; travellers use carried inventory.

Manual food/drink distribution raises the lowest stat first, one point per character per pass. Selection order breaks ties. Food stops at 9 and water at 5.

### Environmental hazards

Hazards have both timed location-map damage and binary daily checks. An unprotected, exposed character that remains at a hazardous camp dies during maintenance. A human travelling party avoids the daily check after selecting a destination, but still loses health while unprotected on the location map. Amiga `0x9224..0x9274` and DOS `0x4256..0x42c7` decrement a timer and subtract one health when it expires. All three Remaster rules now share elapsed-time exposure damage: 0.5 health/second for gas and 1.35 for radiation, reduced by protection. This preserves the existing Extended rates rather than emulating the originals' hardware/frame-dependent timers. All rules retain face-10 gas immunity. All rules share the daily lethal check and timed exposure implementation. AI employees are exempt from on-map timed damage; stationed AI employees still face the daily check.

| `GAM.DAT` value | Meaning | Protection |
|---:|---|---|
| 3 | Radiation | Protective suit |
| 7 | Toxic gas | Gas mask or protective suit |

Both originals contain face-ID-10 gas immunity. DOS nests its test inside an
owned-item scan (German `0x73c9..0x73e0`), so empty inventory misses the test;
Remaster deliberately makes this innate protection unconditional. Amiga sets the gas-protection bit at `0x3320..0x3328`; daily and timed hazard checks both honor it. The Remaster hired-character inventory displays this innate immunity as 100% gas protection, including without equipment. Stationed/interior state can exempt a character from the exposed check. Clothing gives no normal combat defence.

Original DOS exempts **all computer-controlled employees**, including stationed
guards (German `0x6ffd..0x7002`). Original Amiga exempts active computer-player
parties; stationed employees process hazards normally. The Remaster currently
uses the active-party exemption for both: the DOS discrepancy remains pending. Amiga AI active parties additionally bypass
daily water processing. Modern AI does not collect dropped or room items when
any traveller would fail the binary check, although it may still resupply water.

## Experience and recruitment

Boss experience is recalculated each world round and can decrease. Ordinary NPC experience never changes; employment and combat award no experience.

| | DOS | Amiga |
|---|---|---|
| Boss XP | `min(99, 37 + 3 * owned camps)` | `min(99, 38 + (food output + water output + 3 * employees) / 4)` |
| Recruitment | `floor(3 * boss XP / 2) >= NPC XP` | `boss XP >= NPC XP - 4` |

The Amiga employee term includes the boss, active followers and stationed
employees. The formula uses current tool/pump-adjusted output, not stored
supplies. Recruitment also requires an accepted offered item. Request 0 accepts
snake, meat or water bottle. Requests 1–3 retain those alternatives and add the
stored food (only request 1 adds a new option: rats). Higher requests accept
only that exact item. Success consumes it, fills the recruit to food 9/water 5
and attaches them to the player.

## Travel, ownership and victory

Reference caveat: the supplied LZEXE DOS executable contains an immediate-arrival
override. Normal DOS travel below is corroborated by the German EXEPACK build;
see [the DOS build comparison](../reverse-engineering/burntime-dos-de/analysis/comparison.md).

Selecting an adjacent destination starts travel for the route's listed days. The group stays together, receives daily healing/consumption, and arrives when the counter reaches zero. Topology and times are identical.

### Starting positions

Difficulty does not affect original starting positions. Both versions divide the
map into four fixed groups and assign exactly one group to each player. DOS picks
a random first group and gives subsequent player slots the next group cyclically;
Amiga draws an unused group separately for each player. Each player then starts at
a random location within that group. IDs here are the one-based IDs used by this
document.

| Group | DOS | Amiga |
|---:|---|---|
| 1 | Reststop, Nameless Town, Acid Town | DOS group plus Devil Rock |
| 2 | Paradise Camp, Refinery, Eagles Nest | DOS group plus Factory |
| 3 | Stove Vent, Left End, Bearwoods | DOS group plus Big Hole and Desert Point, but without Bearwoods |
| 4 | Nob Hill, Death Town, Lost Hope | DOS group plus Sana |

The grouping prevents two players from drawing the same group, but these groups
are not geographic regions and do not guarantee distant starts. Some candidates
are cities or hazardous camps. Extended rules retain the Remaster's
difficulty-dependent five-region start pools.

Cities are never owned. A player wins by owning every camp adjacent to each of the five cities:

- Sana, Bearwoods, Nob Hill and Nakkara.
- Stove Vent, Desert Point and Dead Wood.
- Numea, Dead Wood, Gold Town and Nameless Town.
- Sherwood, MAX Canyon and Dogshit City.
- Refinery, Reststop and Snake Hills.

Dead Wood appears in two city rings. There are 16 distinct required camps; all other camps are optional.

## Camp economy

### Food production

| Product | Item points | Tool | Output with 0/1/2 tools |
|---|---:|---|---:|
| Maggots | 3 | Knife, axe or pitchfork | 1/2/2; two employees raise it to 3 |
| Rats | 5 | Rat trap | 0/3/4 |
| Snake | 7 | Snake trap | DOS: 0/3/5; Amiga: 0/4/5 |
| Meat | 9 | Trap | 0/5/7 |

Maggots use at most one alternative tool. Other products use at most two matching traps. The DOS executable requires at least one stationed employee for production, but a second trap does not require a second employee. Remaster relies on camp ownership requiring an NPC and does not repeat this check inside production. Amiga uses the trap count directly; employee count modifies the maggot bonus. There is no general one-tool-per-employee cap.

Remaster production sections define `allow_inventory` (default false). All
built-in rules now disable it: knives and other tools must be installed in a
room. A stationed NPC's weapon is not also a food trap. Output, map indicators
and AI tool counts share this policy; loading a save refreshes the flag from
its selected rules config. The flag remains available for custom rules.

DOS output first visits each stationed employee once and gives one point to each employee below food 9, until the day's output is exhausted. Remaining points enter the camp accumulator. Amiga does not feed employees from production: its entire output enters the accumulator. Each time the accumulator reaches the selected food's cost, the cost is removed and a physical item is created. Fractional progress survives rounds. All built-in Remaster rules cap production at six stored items of the selected food type, as in the originals. Food of other types does not count against this cap. Amiga counts the selected product at `0x77d4..0x77da` and checks the limit at `0x8492`; DOS counts the selected title through `0x2588` and checks six at `0x4f9a` and the other product branches. A full room or exhausted global item pool also blocks creation.

Daily food consumption is separate from production. When an original NPC needs food, both DOS and Amiga select the available item with the lowest positive food value. The Remaster applies that selection to every rules profile. A stationed NPC checks its own inventory first and only checks camp storage when its inventory contains no food. A travelling NPC checks the entire travelling group's inventory and never camp storage. This preserves more valuable food but may consume a small item without completely filling the NPC.

### Water

For base source `B`:

| Installed pump | DOS | Amiga |
|---|---|---|
| None | `B` | `B` |
| Hand | `B = 1 ? 3 : B + floor(B / 4)` | `max(3, B + floor(B / 4))` |
| Industrial | `B = 1 ? 6 : B + floor(B / 2)` | `max(6, B + floor(B / 2))` |

Industrial takes precedence. These differ for weak or dry sources: at `B = 3`,
industrial output is 4 on DOS and 6 on Amiga. At `B = 0`, DOS remains dry,
whereas Amiga produces 3/6 with pumps. Verified at DOS `0x4ce2..0x4d24`
and Amiga `0x8652..0x8680`.

The Remaster's DOS profile deliberately corrects the original pump formula:
hand pumps produce `B + max(1, floor(B / 4))`, industrial pumps produce
`B + max(2, floor(B / 2))`. These are total daily outputs, with industrial
taking precedence. At base zero the formulas yield 1/2 if a pump can be installed;
there is no dry-source exception. Amiga retains the original formula above.
Extended pumps add a fixed bonus: hand pumps produce `B + 2` and industrial
pumps produce `B + 5`. Industrial takes precedence; the bonuses do not stack.

Output is added to reserve. Empty containers in the water-source room are filled in item order if enough remains, then reserve is clamped to capacity.

| Empty | Full | Reserve cost |
|---|---|---:|
| Bottle | Water bottle | 2 |
| Empty canteen | Full canteen | 3 |
| Empty wineskin | Full wineskin | 5 |

## World data

Water is initial reserve/capacity/base daily output. Production letters are M=maggots, R=rats, S=snake, F=meat. Routes are `location ID:days`. Danger is stored as type/amount/life-reduction; survival itself uses the binary protection check above. This table is identical.

| ID | Location | Kind | Water | Production | Routes | Danger |
|---:|---|---|---|---|---|---:|
| 1 | New Sandez | city | 0/0/+0 | — | 2:2; 5:2; 4:2; 8:2 | — |
| 2 | Sana | camp | 8/8/+5 | MR | 1:2; 3:4; 4:3; 6:4 | — |
| 3 | Death Town | camp | 10/10/+4 | M | 2:4; 11:4 | 7/95/95 |
| 4 | Bearwoods | camp | 6/6/+5 | MRSF | 1:2; 2:3; 5:3 | — |
| 5 | Nob Hill | camp | 0/6/+5 | MRS | 1:2; 4:3 | 3/45/25 |
| 6 | Muarab | camp | 0/8/+6 | MR | 2:4; 15:3; 7:4; 11:3 | — |
| 7 | Caves | camp | 2/7/+5 | MRS | 6:4; 8:3; 13:3 | — |
| 8 | Nakkara | camp | 6/6/+1 | MRS | 1:2; 7:3 | — |
| 9 | Lost Hope | city | 0/0/+0 | — | 11:2; 10:4; 16:4 | — |
| 10 | Desert Point | camp | 5/5/+1 | MR | 9:4; 11:3 | — |
| 11 | Stove Vent | camp | 2/6/+7 | MRSF | 9:2; 10:3; 6:3; 3:4 | — |
| 12 | Left End | camp | 6/6/+7 | MRSF | 18:3 | — |
| 13 | Monastery | camp | 8/8/+6 | MRS | 7:3; 18:3; 17:4; 15:4 | — |
| 14 | Antella | city | 0/0/+0 | — | 15:2; 16:2; 21:3; 34:3 | — |
| 15 | Numea | camp | 5/5/+6 | MR | 14:2; 6:3; 13:4 | 7/95/95 |
| 16 | Dead Wood | camp | 2/5/+6 | MRSF | 9:4; 14:2; 23:2 | — |
| 17 | Big Hole | camp | 20/20/+10 | MRS | 13:4 | — |
| 18 | Dogshit City | camp | 2/5/+4 | MR | 12:3; 13:3; 19:3 | — |
| 19 | Acid Town | city | 0/0/+0 | — | 25:2; 24:3; 18:3 | — |
| 20 | Devil Rock | camp | 5/5/+1 | MR | 21:1 | — |
| 21 | Gold Town | camp | 5/5/+5 | MRS | 20:1; 14:3 | — |
| 22 | Heavens Gate | city | 0/0/+0 | — | 28:2; 26:2; 30:2 | — |
| 23 | Factory | camp | 0/0/+0 | — | 16:2; 35:3 | 3/127/15 |
| 24 | MAX Canyon | camp | 4/4/+3 | MR | 19:3 | — |
| 25 | Sherwood | camp | 5/5/+7 | MRSF | 19:2; 26:4; 33:3 | — |
| 26 | Reststop | camp | 20/20/+5 | — | 25:4; 22:2 | — |
| 27 | One Man's Heaven | camp | 6/6/+1 | MRS | 34:2; 30:3 | — |
| 28 | Refinery | camp | 0/0/+0 | — | 22:2; 30:2; 31:2; 32:2 | 3/87/18 |
| 29 | Outback | camp | 10/10/+7 | M | 37:3; 30:4 | — |
| 30 | Snake Hills | camp | 20/20/+6 | MRS | 29:4; 22:2; 27:3; 28:2 | — |
| 31 | Paradise Camp | camp | 8/8/+7 | MRSF | 28:2 | — |
| 32 | Hard Mans Death | camp | 5/5/+4 | MR | 34:2; 28:2 | — |
| 33 | Eagles Nest | camp | 2/4/+4 | MRS | 25:3 | — |
| 34 | Nameless Town | camp | 9/9/+6 | MRS | 14:3; 32:2; 27:2 | — |
| 35 | Nirvana | camp | 4/4/+8 | MRSF | 23:3; 37:3 | — |
| 36 | Right End | camp | 15/15/+6 | MRSF | 37:3 | — |
| 37 | Anif | camp | 4/4/+3 | MR | 36:3; 35:3; 29:3 | — |

## Items and value scale

This 58-entry value table is byte-for-byte identical. `V` is the integer value used by barter and services, not combat damage.

| Index/title | Item | V | Index/title | Item | V |
|---|---|---:|---|---|---:|
| 00/33 | maggots | 3 | 29/50 | LCD-display | 8 |
| 01/34 | rats | 5 | 30/51 | wire | 8 |
| 02/35 | snake | 7 | 31/52 | woodpile | 6 |
| 03/36 | meat | 9 | 32/53 | screws | 7 |
| 04/37 | water bottle | 8 | 33/54 | tin | 17 |
| 05/38 | bottle | 6 | 34/55 | spring | 16 |
| 06/39 | full canteen | 12 | 35/56 | hose | 13 |
| 07/3A | empty canteen | 9 | 36/57 | rags | 6 |
| 08/3B | full wineskin | 17 | 37/58 | iron pipe | 13 |
| 09/3C | empty wineskin | 12 | 38/59 | ammunition | 7 |
| 10/3D | knife | 15 | 39/5A | gas mask | 25 |
| 11/3E | axe | 20 | 40/5B | protective suit | 65 |
| 12/3F | pitchfork | 27 | 41/5C | gloves | 8 |
| 13/40 | loaded rifle | 40 | 42/5D | protective overall | 13 |
| 14/41 | unloaded rifle | 33 | 43/5E | boots | 8 |
| 15/42 | mine | 20 | 44/5F | rope | 10 |
| 16/43 | rat trap | 30 | 45/60 | iron bars | 3 |
| 17/44 | trap | 50 | 46/61 | bones | 3 |
| 18/45 | snake trap | 40 | 47/62 | skull | 3 |
| 19/46 | hand pump | 41 | 48/63 | gas canister | 3 |
| 20/47 | industrial pump | 58 | 49/64 | gold | 1 |
| 21/48 | mine detector | 60 | 50/65 | tools | 33 |
| 22/49 | two-way radio | 45 | 51/66 | bible | 15 |
| 23/4A | broken pump | 17 | 52/67 | fur | 3 |
| 24/4B | spare parts | 21 | 53/68 | leather jacket | 21 |
| 25/4C | defective mine detector | 25 | 54/69 | tires | 2 |
| 26/4D | defective two-way radio | 15 | 55/6A | steel helmet | 17 |
| 27/4E | electrical odds-and-ends | 10 | 56/6B | sweater | 17 |
| 28/4F | batteries | 8 | 57/6C | pants | 15 |

### Item transformations

Transformations consume all ingredients. A technician is required except for rifle loading.

| Result | Ingredients |
|---|---|
| Rat trap | Wire + woodpile + screws |
| Trap | Spring + tin + wire |
| Hand pump | Broken pump + rags + hose |
| Industrial pump | Spare parts + iron pipe + rags + hose |
| Two-way radio | Defective radio + electrical odds-and-ends + batteries |
| Protective suit | Protective overall + gas mask + gloves + boots |
| Loaded rifle | Unloaded rifle + ammunition |

One ammunition item reloads a rifle to six shots. Each attack spends one shot; the rifle becomes unloaded when no shot and no ammunition item remain. Rope opens rope-gated rooms. Pumps and production tools work only while installed at a camp. The remaining objects have no direct stat effect: they are barter goods, construction components or fixed scenery/loot.

## Services and barter

Restaurant and pub payment can use any items. With `S = sum(V)`, both versions grant `floor(3 * S / 4)` food or water points, distributed lowest-first across the local group. All offered items are consumed even if points are wasted.

A doctor accepts food only. Let `P` be its total food points:

| | Result |
|---|---|
| DOS | Add `4 * P`; if result is at least 95, set health to 96 |
| Amiga | Add `3 * P`, capped at 95 |

Remaster intentionally simplifies the DOS saturation behavior to a cap of 95, matching Amiga. Treatment never lowers existing health above that cap, and empty payment leaves health unchanged. Doctor factors and caps are configured in each rules folder’s `game.txt`; payment uses `heal` values in `items.txt`.

One maggot/rat/snake/meat therefore heals 12/20/28/36 on DOS and 9/15/21/27 on Amiga. Treatment affects the selected character.

### Trader acceptance

For player offer `O` and wanted trader goods `W`, accept when `floor(O * factor / 100) >= W`.

| Difficulty | DOS | Amiga |
|---|---:|---:|
| Easy | 100 | 100 |
| Normal | 80 | 95 |
| Hard | 60 | 90 |

The discount applies only to the player's side. Inventory capacity must permit the exchange.

### Trader stock

Stock updates once per world round. Modulo tests use item title IDs; meat is separate.

| | DOS | Amiga |
|---|---|---|
| Restock | Every missing match for `round mod 4` | Every assortment match for `round mod 8`, including duplicates |
| Removal | First non-core title removed; existing assortment matches get a masked 1-in-8 check | Title residues `round` and `round + 5` modulo 8; non-food removed on 3 of 4 hash results |
| Food | Remove first food record, then allocate meat every 2 rounds | Clear, then meat every 4 rounds, in addition to specialties |

DOS first removes one food record, then one item not matching its first four
**assortment titles**, then visits all six assortment entries. These are not
inventory positions. Original direct allocations require a free global record
but do not check the six-carried-item limit. Remaster now follows these title-based passes and direct-allocation rules;
the earlier all-food clear, inventory-position removal and capacity guards have
been corrected. German `0x701a..0x70df`, `0x72f2..0x7358`.

Amiga removal runs across all traders in two complete passes before any
restocking; Remaster now preserves that global ordering. See
[stock fidelity implementation notes](original-stock-fidelity.md).

Amiga restocking resumes the assortment scan after each allocation (`0x6f98..0x6f9c`); later items with the same residue are not shadowed. Removal is called with offsets 5 and 0 at `0x6ffc..0x7002`. The Remaster uses seeded random low bits in place of the original frame/record hash, so exact deletion sequences differ.

| Trader | DOS pool | Amiga pool |
|---|---|---|
| Mad Marty | Loaded rifle, knife, broken pump, hose, snake trap, protective overall | Loaded rifle, knife, broken pump, hose, snake trap, boots |
| Chuck | Wire, screws, rags, batteries, tools, boots | Wire, screws, rags, gloves, axe |
| Jack the Nepper | Empty canteen, knife, ammunition, axe, snake trap, woodpile | Wire, knife, ammunition, axe, snake trap, protective overall |
| Sharky | Pitchfork, empty canteen, screws, woodpile, tools, gas mask | Pitchfork, empty canteen, screws, woodpile, rags, gloves |
| Lester | Spring, empty wineskin, tin, screws, snake trap, axe | Spring, empty wineskin, tin, rope, snake trap, protective overall |
| Wolf | Pants, sweater, leather jacket, gloves, wire, pitchfork | Pants, sweater, leather jacket, gloves, wire |
| Ralf Gier | Pitchfork, empty canteen, screws, woodpile, gas mask, tin | Pitchfork, empty canteen, screws, woodpile, gas mask, boots |
| Holger | Screws, empty canteen, tin, knife, boots, broken pump | Bottle, empty canteen, tin, knife, boots, gloves |
| Zocker Joe | Batteries, empty wineskin, wire, axe, snake trap, protective overall | Axe, empty wineskin, boots, tin, snake trap |
| Django | Wire, screws, rags, batteries, protective suit, axe | Wire, screws, rags, protective overall, boots |
| Don Camillo | Wire, knife, tin, electrical odds-and-ends, screws, iron pipe | Wire, knife, tin, woodpile, rope |
| John | Iron pipe, spare parts, empty wineskin, hose, snake trap, pitchfork | Iron pipe, spare parts, empty wineskin, hose, snake trap, protective suit |
| Monkey | Rags, knife, screws, woodpile, gas mask, hose | Rags, knife, screws, woodpile, gas mask, boots |
| Kaiser | Knife, wire, tin, hose, broken pump, woodpile | Knife, wire, tin, hose, broken pump |
| Trademan | Rags, pitchfork, water bottle, woodpile, wire, spare parts | Rags, pitchfork, water bottle, woodpile, wire |
| Black Eye | Wire, axe, rags, woodpile, empty wineskin, gold | Wire, axe, rags, woodpile, empty wineskin |
| Iceman | Wire, knife, tin, woodpile, broken pump, industrial pump | Wire, knife, tin, woodpile, broken pump, gloves |
| Donald | Bottle, empty canteen, empty wineskin, knife, broken pump, wire | Spring, empty canteen, empty wineskin, knife, broken pump |
| Mark | Wire, knife, empty wineskin, hose, spring, gloves | Wire, knife, empty wineskin, hose, spring, gas mask |
| Willy | Iron pipe, spare parts, empty wineskin, hose, gas mask, pitchfork | Iron pipe, spare parts, empty wineskin, hose, gas mask, boots |
| Ivan | Knife, axe, loaded rifle, ammunition, tools, woodpile | Knife, axe, loaded rifle, ammunition, tools, protective overall |
| Steelman | Spring, empty wineskin, tin, knife, gas mask, screws | Spring, empty wineskin, tin, knife, gas mask, protective overall |

## Combat

Normal on-map combat uses direct damage lookup, not attack minus defence. Defender stats and clothing do not reduce it; retaliation is a separate attack.

Mercenaries use full XP. Bosses, technicians and doctors use `floor(XP / 2)`.

| Tier | DOS effective XP | Amiga effective XP |
|---:|---:|---:|
| 1 | 0–24 | 0–25 |
| 2 | 25–49 | 26–51 |
| 3 | 50–74 | 52–77 |
| 4 | 75–99 | 78–99 |

Each attack randomly selects one value:

| Weapon | Tier 1 | Tier 2 | Tier 3 | Tier 4 |
|---|---|---|---|---|
| Unarmed | 2, 3, 4, 6 | 6, 8, 10, 11 | 8, 10, 12, 15 | 9, 11, 13, 15 |
| Knife | 9, 11, 14, 16 | 10, 13, 16, 18 | 12, 14, 17, 20 | 14, 16, 18, 20 |
| Axe | 9, 11, 15, 20 | 9, 12, 17, 22 | 10, 14, 19, 25 | 13, 17, 21, 25 |
| Pitchfork | 4, 7, 10, 20 | 5, 8, 11, 21 | 6, 9, 12, 25 | 9, 12, 16, 25 |
| Loaded rifle | 0, 6, 14, 15 | 0, 10, 17, 20 | 5, 13, 19, 25 | 8, 16, 25, 40 |

Remaster deliberately uses 25-point tiers for all rulesets; the Amiga executable’s 26-point spacing below is not reproduced. Hiring dots use full XP, while combat still applies the class XP adjustment.

The table is identical. DOS uses `floor(effective XP / 25)` and Amiga `/ 26`, capped at tier index 3. Rifle zeroes are misses but consume a shot. Unloaded rifles and non-weapons are unarmed.

## Character balance and setup

The shared template contains 309 records:

| Class | Count | Health | Experience distribution |
|---|---:|---:|---|
| Boss | 4 | 95 | 37×2, 65×2 |
| Mercenary | 116 | 97 | 9×1, 18×4, 27×11, 36×21, 45×39, 54×10, 63×10, 72×9, 81×5, 90×3, 99×3 |
| Technician | 37 | 97 | 36×1, 45×13, 54×4, 63×6, 72×7, 81×5, 90×1 |
| Doctor | 20 | 97 | 45×1, 54×6, 63×8, 72×3, 81×2 |
| Mutant/dog slots | 110 | 31 | 0×17, 9×5, 18×43, 23×43, 27×2 |
| Trader | 22 | 97 | 99×22 |

DOS daily maintenance also recycles inactive finite character records (German
`0x719f..0x72ee`), including ordinary NPC reinitialization. The table describes
initial templates, not a guarantee that every field or death is permanent.

The first two boss records are the human templates. They start at XP 37 and food 9/water 5. Most recruitable NPCs start at 0/0. Twelve are supplied exceptions: ten mercenaries, one technician and one doctor. Their roster, class, location, face, dialogue, request, coordinates and stats are fixed template fields, not difficulty-generated.

Setup maps the two optional human names/faces/colors to the first two boss slots. In both DOS and Amiga, human slot 1 has meat/bottle/full canteen/knife and human slot 2 has snake/bottle/full canteen/knife. Extended rules use configured human inventories; all rules now start human and enemy bosses at 37 XP.

The only confirmed non-AI difficulty effect is barter. Difficulty does not change starting positions, the formulas, combat table, survival costs, world data or character records above.

## Statistics

The screen reports day; employed mercenaries, technicians and doctors; produced maggots/rats/snakes/meat stored at owned camps; owned camps; controlled cities; and total cities. Travelling party members count as employed people. Carried loose food does not count as camp stock.

## Data-layout checklist

DOS `GAM.DAT` is the canonical compact template; Amiga embeds equivalent tables.

| Block | Count | Size |
|---|---:|---:|
| Header/global | 1 | 22 bytes before locations |
| Locations | 37 | 50 bytes each |
| Gap | 1 | 2 bytes |
| Players | 4 | 62 bytes each |
| Gap | 1 | 400 bytes |
| Characters | 309 | 46 bytes each |
| Gap | 1 | 14 bytes |
| Items | 1,199 | 12 bytes each |

Item records hold type, point value, title ID, owner, damage/ammunition and room/X/Y. Owner references distinguish free pool, dropped items, rooms and character inventories. The finite 1,199-record pool is gameplay-relevant: item creation can fail if no reusable record exists.

Initial ownership divides those records into 792 free pool entries, 163 room items, 138 dropped map items and 106 character-held items. These are fixed placements. A newly created item reuses a free record and may overwrite its old type, so the total free-record count, not merely the number of pre-typed food records, limits future production.

Character records hold location, water, face, food, name, class, employer, movement, request, dialogue, sprite, XP and health. Location records hold owner, water reserve/capacity/source, city flag, production flags/current product, four neighbors/times/paths and hazard data.

## Confirmed differences

| System | DOS | Amiga |
|---|---|---|
| Boss XP | `37 + 3C` | Economy/workforce formula |
| Recruitment XP | 150% of boss XP | NPC XP minus four |
| Combat tier width | 25 | 26 |
| Computer-player natural healing | No special case | +2 from health 50, owner-wide |
| Active AI party hazard processing | Bypassed | Bypassed |
| Active AI party water processing | Normal | Bypassed |
| Doctor | `4P`, saturates at 96 | `3P`, caps at 95 |
| Barter factors | 100/80/60 | 100/95/90 |
| Trader refresh | Modulo 4, multiple additions | Modulo 8, all assortment matches |
| Start groups | Four groups of three; cyclic assignment | Four groups of four; unused-group draws |
| Toxic-gas special case | Face ID 10 immune | Face ID 10 immune |

Everything else specified above is shared unless its section says otherwise.

## Rules resources and save compatibility

Each `rules/dos`, `rules/amiga` and `rules/extended` folder owns its
`game.txt`, `trader.txt`, `items.txt` and `production.txt`.
Amiga's single-snake-trap output is defined by `amount=0 4 5` in its file.

Legacy saved resource IDs `items@items_original.txt` and `items@items.txt`
resolve through loader aliases to DOS and Extended items respectively. Item
IDs and serialized item/state links are unchanged. Production arrays are
refreshed from the selected rule folder after loading without replacing the
production objects or camp accumulators, including saves from before the move.

### Configurable rule values in Remaster

Each `rules/{dos,amiga,extended}/game.txt` sets difficulty-specific
`barter_factor`, doctor `healing_factor`/`health_cap`, original combat
`experience_tier_width`, and hazard damage per second/immune face IDs.
Doctor item healing values stay 12/20/28/36; DOS and Extended multiply by 1,
Amiga by 0.75. DOS deliberately caps treatment at 95 instead of reproducing 96.

The original `items.txt` files store `damage` as 16 space-separated integers:
four equiprobable rolls for each of four ascending XP tiers. The unnamed root
section holds unarmed damage without creating a selectable weapon. Extended uses the same vectors, including the rifle vector for pistols and the legacy one-shot rifle. `weapon_priority` preserves automatic weapon
preference independently of the damage vector; absent priorities use the scalar
or average damage. Extended clothing reduces rolled damage by 5% (sweater), 10% (pants), 20% (leather jacket), or 25% (steel helmet); only the strongest carried item applies. Defence does not scale with XP. Successful hits retain at least one damage, while zero rifle rolls remain misses. Local and Modern-AI off-screen combat share this formula. Rule settings are cached per game and reloaded after loading
a save; item resource IDs and legacy path aliases are unchanged.

Remaster starts every boss at 37 XP, independent of difficulty, configured under `[players]` in each rules folder’s `game.txt`. Original AI retains its template inventory and health. This applies to new games; subsequent XP recalculation still follows the selected rules.


### Legacy settings aliases

`GameSettings` resolves `gamesettings_original.txt` to
`rules/dos/game.txt`, and `gamesettings_extended.txt` to
`rules/extended/game.txt`. The duplicate legacy files have been removed;
the rules-folder files are the sole source of settings. Canonical paths pass
through unchanged. This is a loader alias, like the legacy item-file mapping.

### Current Remaster ammunition lifecycle

Visible/table combat spends one round per firearm attack, even when damage is
zero. DOS and Amiga rifles start with six rounds; each automatic reload consumes
one carried ammunition item and restores six rounds.

Extended rifles and pistols now also hold six rounds. Both manual and automatic
reloads restore six rounds per ammunition item. Loaded weapons use one item type
with an instance counter: two-bullet graphics represent 2–6 rounds, the optional
`image_last_round` graphic represents one round, and zero switches to the unloaded
type. Inventory tooltips display the exact remaining/capacity count.

The existing serialized `Item.ammo` field stores remaining ammunition. Older saves
retain their stored counters; `item_loaded_rifle_1` is migrated to the canonical
rifle type. The legacy rifle ID remains a loading-only compatibility alias, while
new items and reloads use the canonical six-round type.

DOS and Amiga original AI strategic routines do not consume ammunition. Modern
AI strategic combat does, using the selected rules' weapon definitions. Thus AI
profile determines off-screen consumption, while the ruleset determines capacity.

### Extended pistol balance

The pistol keeps rifle priority 55 and six rounds, but trades peak damage for
consistency. Its four tier rows are `4 7 9 11`, `6 9 12 15`, `9 12 16 18`, and
`13 18 22 26`: roughly 11–12% less average damage than the rifle, with higher
minimums and lower maximums. This is a balance starting point, not a guarantee
of equal effectiveness against every target.

Modern AI counts pistols and rifles together for firearm purchasing limits:
Easy and Normal allow one firearm-carrying group member, Hard has no fixed cap.
Mixed rifle/pistol purchases in one trade also share the pending firearm count.
Acquired weapons are retained even when above the purchasing preference.

Extended trader supply uses a one-for-one replacement: Ivan (199) stocks the
pistol in his former rifle slot; Mad Marty (179) keeps his rifle slot. Assortment
sizes and stock-generation counts are unchanged. Existing saves refresh Extended
assortments on load without replacing inventory. A firearm already in stock
blocks another firearm restock until it leaves, including Ivan's old rifle.
