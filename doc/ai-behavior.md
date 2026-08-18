# AI Behavior

AI players manage survival, inventory, construction, travel, trade, combat and camps every turn.
Difficulty changes how quickly they expand, how large their groups and garrisons become, and how much risk they accept.

## General Behavior

- survival
  - seeks an owned camp or city when health falls below 40, food falls to 3 or water falls to 2
  - only chooses routes for which every group member has enough food and water
  - reserves 3 additional days of food and water when travelling to attack a camp
  - [cheat] at owned camps and cities, emergency recovery raises food and water to at least 10 and heals up to 25 health
  - [cheat] can receive one meat or full wineskin there when reserves are critically low
- group
  - recruits available NPCs and equips followers before expanding
  - never attacks without armed followers
  - chooses randomly among NPCs in the difficulty-specific experience range
  - chooses randomly among all available NPCs when none match that range
- camps
  - claims only sustainable locations and stations one follower as the first guard
  - requires suitable protection before claiming a location with gas or radiation
  - collects surplus food and useful items, improves production and delivers equipment
  - recruits and stations additional guards at threatened or strategically useful camps
- attacks
  - remembers a strategic target between turns while the route and attack remain safe
  - prefers weak, nearby camps; routes through hostile locations are not allowed by the game
  - treats a recently contested camp as strategically important for 16 days
  - retaliates against a human player for 20 days after one of its defenders is attacked
  - retaliation ignores the normal limit on human camp defenders, but still requires sufficient strength
  - captures a defeated camp and stations the weakest surviving follower as its guard
  - retreats to the previous location when an attack is no longer safe or fails
- economy
  - gathers items from the ground, its group and its camps instead of receiving unlimited equipment
  - [cheat] builds weapons, protection and food-production tools from materials shared across the group and reserve without class restrictions
  - keeps food, water, production tools and construction materials needed for future expansion
  - [cheat] can generate the cheapest required hiring item when recruiting in a city
  - visits city traders deliberately but uses roaming traders only when already passing through their location
  - trades with the fixed city trader and every roaming trader currently in the city

## I Easy

- group
  - travels with up to 2 people, including the leader
  - recruits NPCs with 0-40% experience when available
- camps
  - sets an expansion wait of 3-5 turns after creating a camp
  - can control at most 2 camps more than the leading human player
  - ignores this limit in an all-AI game
  - uses 1 guard per camp
  - guards use only knives or axes, never pitchforks or guns
  - considers a camp threatened when an opponent is directly adjacent
- attacks
  - normally attacks a human camp with a single guard armed with at most a knife
  - attacks with knives and axes, never pitchforks or guns
  - requires about 135% of the estimated defender strength
  - waits 4 turns after capturing an enemy camp before attacking again
- economy
  - trades surplus goods at normal item value

## II Normal

- group
  - travels with up to 3 people when its camps can support them
  - recruits NPCs with 20-60% experience when available
- camps
  - sets an expansion wait of 1-4 turns after creating a camp
  - can control at most 5 camps more than the leading human player
  - ignores this limit in an all-AI game
  - aims for 2 guards in threatened or strategically useful camps
  - each camp uses at most 1 pitchfork and no guns
  - considers a camp threatened when an opponent is within 2 connected locations
- attacks
  - normally attacks human camps with at most 1 defender
  - at most 1 group member uses a pitchfork and none use guns
  - requires about 105% of the estimated defender strength
  - gives strategically useful enemy camps a small preference
  - waits 2 turns after capturing an enemy camp before attacking again
- economy
  - [cheat] values offered goods at 120% when trading for needed equipment

## III Hard

- group
  - travels with up to 4 people when at least 4 camps and 2 self-supporting camps can feed them
  - recruits NPCs with at least 40% experience when available
- camps
  - sets an expansion wait of 0-2 turns after creating a camp
  - expands independently of human camp progress
  - aims for 3 guards in threatened or strategically useful camps
  - each camp uses at most 1 gun and 1 pitchfork
  - considers a camp threatened when an opponent is within 2 connected locations
- attacks
  - has no fixed limit on the number of human camp defenders it may challenge
  - at most 1 group member uses a pitchfork and none use guns
  - attacks with about 75% of the estimated defender strength when the risk is otherwise acceptable
  - assesses defenders using their attack, defense and health instead of a simplified weapon estimate
  - strongly prefers strategically useful enemy camps
  - has no cooldown after capturing an enemy camp
- economy
  - [cheat] values offered goods at 150% when trading for needed equipment
