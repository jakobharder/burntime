# Original Amiga/DOS AI

This describes the computer-player logic recovered from the examined original Amiga and DOS executables. It is separate from the modern Remaster AI documented in `ai-behavior.md`.

The primary DOS reference is now the German EXEPACK build. See the
[reference recheck](../reverse-engineering/burntime-dos-de/analysis/reference-audit.md)
for evidence and unresolved differences in the current Remaster implementation.
Statements about original behavior below are not a claim of exact implementation.

## Overview

The original AI is a compact strategic state machine, not a simulated human player. It directly manages a boss, a destination, owned locations, recruits, garrisons, and off-screen conflicts.

Its turn cycle is:

1. Inspect ownership and connected routes.
2. Select an expansion or contested destination.
3. Travel using the normal route and travel timers.
4. Recruit an available NPC or station someone as a guard.
5. Capture empty locations or resolve opposition with the edition's strategic
   combat routine.
6. Apply edition-specific maintenance and select another adjacent route.

The result is territorial and opportunistic. It has little long-term economic planning: no human-like production-chain planning, reserve management, equipment shopping, diplomacy, or tactical movement inside a location.

## What it knows

The AI reads the complete world state directly. This includes location ownership, characters, connected routes, and human territorial strength, even where a human player may not have visited. Amiga explicitly bases expansion pressure on the largest number of locations owned by any human player.

It therefore has strategic omniscience, but its decisions remain simple and local enough that it often behaves predictably.

## Expansion and travel

Normal DOS (confirmed in the German EXEPACK build) and Amiga use the real map
graph and travel timers. The previously supplied LZEXE DOS build contains an
immediate-arrival override affecting both AI and humans; it is excluded from
the faithful profile. See [the DOS build comparison](../reverse-engineering/burntime-dos-de/analysis/comparison.md).
Each decision considers the current location's adjacent route slots rather than
running a multi-hop path search. It moves its active group, claims empty
locations, and stations NPCs to retain territory.

DOS chooses one of four physical slots. An empty trailing slot ends the update
without travel: the decrement branch is JE, not JNE (German `0x6757`). The current
Remaster clamps to the last real neighbor; this fidelity mismatch is pending. Amiga retries slot zero or one according to the selected slot's low bit,
then slot zero. Amiga's neutral-camp return scan uses fixed slot order.

Amiga adjusts local expansion work relative to the strongest human player. Its target is the human's location count plus a random difficulty margin:

| Difficulty | Additional target margin |
|---|---:|
| Easy | 0–3 locations |
| Normal | 0–7 locations |
| Hard | 0–15 locations |

The target is rolled separately for local work and route-mode selection. Reaching
it changes route preference; it does not stop the AI. The last arrival day is
persisted: a normal arrival causes a one-update stay, while after three stationary
days the ownership filter is relaxed. DOS has no equivalent human-relative target.
DOS retains the last departure location and normally uses it as the return edge
after conflict or an unproductive neutral visit.

Human-relative limits use a shared progress benchmark. The strongest living
human is used while any human remains. Otherwise, each AI compares itself with
the strongest living AI opponent. A sole survivor has no relative restriction.
Amiga and Extended use this fallback; DOS has no equivalent relative target.

## Recruitment and garrisons

Recruitment is performed by directly assigning a free NPC to the AI:

- The requested recruitment item is not paid in either version.
- DOS also bypasses the normal boss-versus-NPC experience requirement.
- Amiga retains a loose experience check equivalent to the Amiga recruitment tolerance.
- Recruitment draws from the existing finite character records; it does not create unlimited NPCs.
- DOS tries mercenary, technician, then doctor, only at an eligible unowned
  location. German `0x6627..0x6649` rejects food-field byte `+0x18 == 0x33`,
  zero base water and nonzero hazard. Remaster currently omits these gates.
  The recruit is immediately stationed and claims that camp.

Amiga counts active-party members, including the boss; stationed employees do
not consume recruitment capacity. The limit is three party members on Easy and
four on Normal/Hard. Amiga increases its garrison target with difficulty:

| Difficulty | Garrison target |
|---|---:|
| Easy | 2 |
| Normal | 3 |
| Hard | 4 |

At hazardous Amiga camps, stationing is stricter: from day 80 a gas camp can
receive at most two guards and the AI creates a gas mask for the new guard;
from day 200 a radiation camp can receive one guard and the AI creates a
protective suit. Creation still needs a free original item record.
Owned-camp reinforcement is additionally limited by the camp's production and
installed-tool staffing capacity; the nominal difficulty target is not enough
by itself.

Amiga bootstraps a captured camp with a knife trap and basic production. From
day 30 it can create the next location-supported production tool. It creates a
hand pump from day 100, or an industrial pump from day 300, when water output
prevents another guard from being stationed. These direct creations still use
the finite original item pool. It does not test whether that pump already
exists, so it can create another while water remains limiting.

## Supplies and recovery

The AI does not maintain food, water, and recovery through the same economy as a human:

- DOS directly resets the active AI boss to 10 water and either 9 or 18 food during strategic actions.
- Newly assigned DOS recruits receive 5 water and 9 food.
- Newly assigned Amiga recruits retain their existing food and water.
- DOS does not grant health in this maintenance routine.
- Amiga applies maintenance on arrival and while local work remains pending
  because its expansion target suppresses it. After local work completes,
  subsequent stationary updates perform routing only. A normal eligible
  arrival adds 9 food, capped at 9. Arrival in a city also heals 30. Only when entering a city after leaving a hostile camp do those values
  become +3 food and +20 health. Returning to an ordinary camp still grants
  +9 food and no healing. Food is not
  granted when the destination belongs to an opponent. Water is never granted
  because active Amiga AI parties bypass daily water consumption and automatic
  water resupply entirely. Stationed employees do not receive that exemption.
- Original DOS exempts all AI employees from daily hazards, including guards.
  Amiga exempts active parties only. Remaster currently applies the active-party
  exemption to every profile, leaving a DOS fidelity mismatch for guards.
- Under DOS or Amiga rules, Extended AI collects no dropped or room items when
  any active-party member lacks complete hazard protection. Water resupply is
  still allowed.
- Every Amiga AI employee receives ordinary +2 healing from health 50; this is
  based on computer-player status, not on whether the player is travelling.

The original has only one computer-player implementation, so its binary checks
the generic computer-player flag. The Remaster applies the active-party hazard
exemption to every AI profile. The Amiga-only water exemption remains attached
to the Amiga AI profile.

These shortcuts prevent routine starvation and recovery logistics from stopping expansion.

## Combat

DOS and Amiga AI conflicts are resolved off-screen by private strategic routines. They are not repetitions of the visible on-map attack animation and damage-table sequence. Their profiles own both the recovered damage shortcut and encounter tactics such as pairing, retaliation, exchange limits and withdrawal, regardless of the selected game rules. Extended AI instead uses the active rules profile for combat damage.

Strategic combat still considers actual character health, experience, and weapons, and deaths persist. However:

- DOS strategic damage is `boss XP + day pressure - (defender XP + weapon
  basis)`. Weapon bases are 10/20/30/40/60. Day pressure rises from 20 to 80
  at days 60, 100, 140, 160, 200 and 240. Negative results skip the attack; an
  exact zero becomes 10 in the original. Remaster corrects that exact-zero
  fallback to 1, avoiding a damage drop when attacker XP increases.
- DOS persists a 2/4/6 Easy/Normal/Hard conflict counter. It permits that many
  consecutive strategic attack passes, then skips one pass and resets. A pass
  which cannot inflict positive damage resets immediately.
- Amiga weapon bases are 5/9/12/14/20 for unarmed, knife, axe, pitchfork and
  loaded rifle; an unloaded rifle uses 6. Damage adds `basis * effective XP /
  100` and a small masked random value. Effective XP is halved unless the
  fighter is a mercenary. Every active-party fighter gains day-based floors of
  12, 18 and 25 at days 100, 200 and 300; stationed defenders do not. A weak
  attacker below 80 health skips an unsuitable defender but may still test a
  later defender during the same conflict pass.
- A loaded rifle is not converted or depleted by strategic combat, effectively allowing repeated off-screen rifle attacks without ammunition consumption.

On-map damage, weapon tables, NPC stats, and experience are not directly increased by difficulty.

## Items and trading

The original AI does not conduct normal trader negotiations. It obtains, assigns, consumes, or discards items through direct strategic rules instead of using the player-facing barter process.

DOS Easy performs no item cleanup. Each maintenance call on Normal/Hard releases
at most **one** eligible record, in global item order. Ground items are eligible;
Normal preserves room items, while Hard also permits room items outside title
IDs `0x37..0x47`. The function immediately returns after release. Remaster now preserves the one-record limit and invokes maintenance once on
the eligible controller branch. See [the implementation notes](original-stock-fidelity.md).

Amiga also has an arrival item routine at `0x7b1e`, called for ground items and
for unowned/self-owned rooms. It deletes food, preserves the current production
tool, picks up other weapons, and discards traps/other goods under a carrying-space
scan budget. Remaster now implements it. The earlier “none found” conclusion
was incomplete; see [the full rules and evidence](original-stock-fidelity.md).

## Difficulty summary

| Behavior | DOS | Amiga |
|---|---|---|
| Direct combat stat bonus | None | None |
| Strategic conflict timing | 2 / 4 / 6 attack-pass counter | 3 / 6 / 10 per-pair budget |
| Human-relative expansion target | None found | Human maximum + 0–3 / 0–7 / 0–15 |
| Difficulty-scaled garrison | None found | About 2 / 3 / 4 |
| Free recruitment | Yes; ignores item and experience gate | Yes; ignores item but retains loose experience gate |
| Free supplies/recovery | Boss food/water each stationary AI turn | Arrival food/healing; no water |
| Normal trader interaction | No | No |
| Strategic rifle ammunition use | No | No |
| Record-ownership item cleanup | One eligible deletion per maintenance call | Arrival pickup/discard scan with carrying-space budget |
| Difficulty-dependent hostile pursuit | None found | None found |

## Constraints it still obeys

Despite its shortcuts, the AI remains constrained by:

- the real location graph and travel time;
- the finite NPC and item records;
- actual character health, experience, and weapons in strategic combat;
- conquered locations and finite character records (DOS maintenance can recycle inactive records);
- actual camp ownership;
- static ordinary-NPC experience.

## AI starting state

Difficulty does not select starting experience or starting items in the examined originals. The DOS `GAM.DAT` contains slot-specific characters:

| Boss slot | Initial experience | Initial inventory |
|---:|---:|---|
| AI 1 | 65 | Empty wineskin, meat, two full wineskins, axe |
| AI 2 | 65 | Empty wineskin, meat, two full wineskins, pitchfork |

Boss experience is subsequently recalculated by the selected rules and may replace the initial value. Difficulty does not select between these templates.

The Remaster assigns these two templates alternately to every DOS or Amiga AI slot, independent of player number and rules selection. All profiles take their initial boss XP from the selected rules’ gamesettings.txt (37). Extended AI retains its Easy-derived inventory of meat, bottle, full canteen and two knives. Disabled slots receive no starting state.

## Remaster implementation

The DOS and Amiga profiles use separate serializable controller loops behind a
shared original-AI utility base. They do not call the Extended planner,
trading, construction or reserve systems. The implementation:

- use small, separate DOS and Amiga controller loops with shared utilities;
- permit global ownership knowledge and human-relative expansion pressure;
- select adjacent routes without long-horizon economic scoring;
- recruit from the finite NPC pool without paying the requested item;
- use explicit original-style supply, healing and camp-bootstrap grants;
- resolve AI conflicts strategically without consuming rifle ammunition;
- keep AI selection independent of the DOS, Amiga or Extended rules profile;
- retain the original persistent DOS conflict counter and Amiga arrival day,
  difficulty margins, employee/garrison limits, and strategic damage;
- permit a different AI profile for every player slot, including mixed headless simulations.

The game-setup selector remains a convenient default for every computer slot. Headless simulation accepts four explicit profiles with `--ai-profiles dos,amiga,extended,none`; these selections are stored in the save and restored independently.

Extended AI pays the same barter and doctor rates as the human player, using the selected rule set’s gamesettings.txt and world difficulty. Its city supply aid and the original profiles’ free supply/healing grants remain profile-specific.
