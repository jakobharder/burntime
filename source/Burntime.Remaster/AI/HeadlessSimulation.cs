using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

public sealed class HeadlessSimulationOptions
{
    public int Turns { get; init; } = 100;
    public int Difficulty { get; init; } = 2;
    public int Seed { get; init; } = 1;
    public bool ExtendedGame { get; init; }
}

/// <summary>
/// Runs a complete game synchronously without starting the server, AI, or render threads.
/// No fields are added to serialized game-state types.
/// </summary>
public static class HeadlessSimulation
{
    public static string Run(BurntimeClassic app, HeadlessSimulationOptions options)
    {
        if (options.Turns < 1)
            throw new ArgumentOutOfRangeException(nameof(options.Turns), "Turn count must be positive.");
        if (options.Difficulty is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(options.Difficulty), "Difficulty must be easy, normal, or hard.");

        Platform.Math.SetRandomSeed(options.Seed);

        GameCreation creation = new(app);
        NewGameInfo info = new()
        {
            NameOne = null,
            NameTwo = null,
            FaceOne = -1,
            FaceTwo = -1,
            Difficulty = options.Difficulty,
            ColorOne = BurntimePlayerColor.Green,
            ColorTwo = BurntimePlayerColor.Red,
            DisableAI = false,
            ExtendedGame = options.ExtendedGame
        };

        creation.CreateNewGame(info, startServer: false);
        ClassicGame game = (ClassicGame)app.Server.World;
        List<string> events = new();
        Dictionary<int, int?> ownership = CaptureOwnership(game);
        int completedTurns = 0;
        Player? winner = null;
        int activeTurn = 0;
        EconomyMetrics economy = new(game);
        AiTelemetry.Sink = (eventPlayer, message) =>
        {
            events.Add($"Turn {activeTurn}: {PlayerLabel(eventPlayer)} {message}.");
            economy.Observe(eventPlayer, activeTurn, message);
        };

        try
        {
            for (int turn = 1; turn <= options.Turns; turn++)
            {
                activeTurn = turn;
                foreach (Player player in game.World.Players)
                {
                    if (player.IsDead || player.IsTraveling)
                        continue;

                    DecisionSnapshot before = DecisionSnapshot.Capture(player);
                    Location beforeLocation = player.Location;
                    Location? beforeDestination = player.Destination;

                    if (player.AiState is ClassicAiState ai)
                        ai.Turn();

                    RecordDecisionChanges(player, before, turn, events);

                    if (beforeDestination != player.Destination && player.Destination is not null)
                    {
                        events.Add($"Turn {turn}: {PlayerLabel(player)} travels from {LocationLabel(beforeLocation)} " +
                            $"toward {LocationLabel(player.Destination)} ({player.RemainingDays} days).");
                    }
                }

                economy.RecordCappedCampTurn();
                game.Turn();

                foreach (Player player in game.World.Players)
                    player.Turn();

                RecordOwnershipChanges(game, ownership, turn, events);
                foreach (Player player in game.World.Players.Where(player => !player.IsDead))
                    events.Add($"Turn {turn}: {FormatGroupState(player)}");
                completedTurns = turn;
                winner = game.CheckWinner() as Player;
                if (winner is not null)
                    break;
            }
        }
        finally
        {
            AiTelemetry.Sink = null;
        }

        return BuildReport(game, options, completedTurns, winner, events, economy);
    }

    static Dictionary<int, int?> CaptureOwnership(ClassicGame game)
    {
        return game.World.Locations.ToDictionary(location => location.Id, location => location.Player?.Index);
    }

    static void RecordOwnershipChanges(
        ClassicGame game,
        Dictionary<int, int?> ownership,
        int turn,
        List<string> events)
    {
        foreach (Location location in game.World.Locations)
        {
            int? currentOwner = location.Player?.Index;
            if (ownership[location.Id] == currentOwner)
                continue;

            string owner = currentOwner.HasValue ? PlayerLabel(game.World.Players[currentOwner.Value]) : "neutral";
            events.Add($"Turn {turn}: {LocationLabel(location)} is now held by {owner}.");
            ownership[location.Id] = currentOwner;
        }
    }

    static void RecordDecisionChanges(Player player, DecisionSnapshot before, int turn, List<string> events)
    {
        string prefix = $"Turn {turn}: {PlayerLabel(player)}";

        if (before.GroundItems.Count > 0)
        {
            Dictionary<string, int> currentGround = CountItems(before.Location.Items);
            Dictionary<string, int> removed = ItemDifference(before.GroundItems, currentGround);
            Dictionary<string, int> collected = removed
                .Where(pair => before.GroundItemTypes[pair.Key])
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<string, int> retained = removed
                .Where(pair => !before.GroundItemTypes[pair.Key])
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<string, int> leftBehind = currentGround
                .Where(pair => before.GroundItems.ContainsKey(pair.Key) && !before.GroundItemTypes[pair.Key])
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            events.Add($"{prefix} found at {LocationLabel(before.Location)}: {FormatItems(before.GroundItems)}.");
            if (collected.Count > 0)
                events.Add($"{prefix} moved to AI item pool: {FormatItems(collected)}.");
            if (retained.Count > 0)
                events.Add($"{prefix} retained for inventory or camp storage: {FormatItems(retained)}.");
            if (leftBehind.Count > 0)
                events.Add($"{prefix} left ground items due to unavailable storage: {FormatItems(leftBehind)}.");
        }

        Character[] currentGroup = player.Group.ToArray();
        Character[] hired = currentGroup.Where(character => !before.Group.Contains(character)).ToArray();
        foreach (Character character in hired)
        {
            events.Add($"{prefix} hired {CharacterLabel(character)} at {LocationLabel(player.Location)}; " +
                $"inventory: {FormatItems(character.Items)}.");
        }

        Character[] removedFromGroup = before.Group
            .Where(character => !currentGroup.Contains(character) && character.IsStationed)
            .ToArray();
        Character[] newCampMembers = before.Location.CampNPC
            .Where(character => character.Player == player && !before.CampMembers.Contains(character))
            .ToArray();
        Character[] stationed = removedFromGroup.Union(newCampMembers).Distinct().ToArray();
        foreach (Character character in stationed)
        {
            events.Add($"{prefix} created a camp at {LocationLabel(character.Location)} using " +
                $"{CharacterLabel(character)}; NPC inventory: {FormatItems(character.Items)}; " +
                $"camp room items: {FormatItems(character.Location.Rooms.SelectMany(room => room.Items))}.");
        }

        foreach (Character character in before.Group.Union(currentGroup).Union(stationed))
        {
            Dictionary<string, int> oldItems = before.Inventory.TryGetValue(character, out Dictionary<string, int>? items)
                ? items
                : new Dictionary<string, int>();
            Dictionary<string, int> newItems = CountItems(character.Items);
            Dictionary<string, int> added = ItemDifference(newItems, oldItems);
            Dictionary<string, int> removed = ItemDifference(oldItems, newItems);

            if (added.Count > 0)
                events.Add($"{prefix} {character.Name}'s inventory gained: {FormatItems(added)}.");
            if (removed.Count > 0 && !stationed.Contains(character))
                events.Add($"{prefix} {character.Name}'s inventory lost: {FormatItems(removed)}.");
        }

        Dictionary<string, int> currentRoomItems = CountItems(before.Location.Rooms.SelectMany(room => room.Items));
        Dictionary<string, int> campAdded = ItemDifference(currentRoomItems, before.RoomItems);
        if (campAdded.Count > 0)
            events.Add($"{prefix} room storage at {LocationLabel(before.Location)} gained: {FormatItems(campAdded)}.");
    }

    static string BuildReport(
        ClassicGame game,
        HeadlessSimulationOptions options,
        int completedTurns,
        Player? winner,
        IReadOnlyCollection<string> events,
        EconomyMetrics economy)
    {
        StringBuilder report = new();
        report.AppendLine("Burntime headless all-AI simulation");
        report.AppendLine($"Seed: {options.Seed}");
        report.AppendLine($"Difficulty: {DifficultyLabel(options.Difficulty)}");
        report.AppendLine($"Requested turns: {options.Turns}");
        report.AppendLine($"Completed turns: {completedTurns}");
        report.AppendLine($"Final world day: {game.World.Day}");
        report.AppendLine($"Rules: {(options.ExtendedGame ? "extended" : "1993")}");
        report.AppendLine($"Winner: {(winner is null ? "none" : PlayerLabel(winner))}");
        report.AppendLine();

        report.AppendLine("Players");
        foreach (Player player in game.World.Players)
        {
            int camps = game.World.Locations.Count(location => location.Player == player);
            int defenders = game.World.Locations.Sum(location => location.CampNPC.Count(character => character.Player == player));
            string state = player.IsDead || player.Character.IsDead ? "dead" : "alive";
            string travel = player.IsTraveling
                ? $"traveling to {LocationLabel(player.Destination)} ({player.RemainingDays} days left)"
                : $"at {LocationLabel(player.Location)}";

            report.AppendLine($"- {PlayerLabel(player)}: {state}, {travel}, {camps} camps, " +
                $"group {player.Group.Count}, defenders {defenders}, " +
                $"health {player.Character.Health}, food {player.Character.Food}, water {player.Character.Water}");
            foreach (Character member in player.Group)
            {
                report.AppendLine($"  - {CharacterLabel(member)}: health {member.Health}, food {member.Food}, " +
                    $"water {member.Water}, inventory {FormatItems(member.Items)}");
            }
        }

        report.AppendLine();
        report.AppendLine("Owned locations and stationed NPCs");
        foreach (Location location in game.World.Locations.Where(location => location.Player is not null).OrderBy(location => location.Id))
        {
            Character[] defenders = location.CampNPC.Where(character => character.Player == location.Player).ToArray();
            Item[] campItems = location.Rooms.SelectMany(room => room.Items)
                .Concat(defenders.SelectMany(character => character.Items))
                .ToArray();
            Production? bestProduction = location.ValidProductions
                .OrderByDescending(production => production.Produce.TradeValue)
                .ThenByDescending(production => production.Produce.FoodValue)
                .FirstOrDefault();
            Item[] usedTraps = location.Production == null
                ? Array.Empty<Item>()
                : campItems.Where(item => item.Type.Production == location.Production).ToArray();
            string bestTrap = bestProduction == null
                ? "none"
                : $"{TrapTypeLabel(game, bestProduction)} -> {bestProduction.Produce.ID}";
            string usedTrap = location.Production == null
                ? "none"
                : $"{FormatItems(usedTraps)} -> {location.Production.Produce.ID}";
            report.AppendLine($"- {LocationLabel(location)}: {PlayerLabel(location.Player!)}, {defenders.Length} NPC(s); " +
                $"items {FormatItems(campItems)}; highest possible trap {bestTrap}; used trap {usedTrap}");
            foreach (Character defender in defenders)
            {
                string weapon = defender.Weapon?.Type.ID ?? "none";
                report.AppendLine($"  - {defender.Name}: {defender.Class}, health {defender.Health}, " +
                    $"food {defender.Food}, water {defender.Water}, weapon {weapon}");
            }
        }

        report.AppendLine();
        report.AppendLine("Combined camp inventory (room storage and stationed NPCs)");
        foreach (Player player in game.World.Players)
        {
            Item[] campItems = game.World.Locations
                .Where(location => location.Player == player)
                .SelectMany(location => location.Rooms.SelectMany(room => room.Items)
                    .Concat(location.CampNPC
                        .Where(character => character.Player == player)
                        .SelectMany(character => character.Items)))
                .ToArray();
            float tradeValue = campItems.Sum(item => item.TradeValue);
            report.AppendLine($"- {PlayerLabel(player)}: {campItems.Length} items, " +
                $"trade value {tradeValue:0}; {FormatItems(campItems)}");
        }

        report.AppendLine();
        report.AppendLine("AI item pools (shared, slotless inventory)");
        foreach (Player player in game.World.Players)
        {
            var contents = (player.AiState as ClassicAiState)?.Pool.GetContents().ToArray() ??
                Array.Empty<(ItemType Type, int Count)>();
            float tradeValue = contents.Sum(entry => entry.Type.TradeValue * entry.Count);
            Dictionary<string, int> counts = contents.ToDictionary(entry => entry.Type.ID, entry => entry.Count);
            report.AppendLine($"- {PlayerLabel(player)}: {counts.Values.Sum()} items, " +
                $"trade value {tradeValue:0}; {FormatItems(counts)}");
        }

        report.AppendLine();
        report.AppendLine("AI economy results");
        foreach (Player player in game.World.Players)
        {
            PlayerEconomyMetrics result = economy[player];
            string firstTrap = result.FirstAdvancedTrapTurn?.ToString() ?? "none";
            string preparedCityCargo = FormatCargoFill(result.PreparedCityCargo, result.PreparedCityCapacity);
            string incidentalCityCargo = FormatCargoFill(result.IncidentalCityCargo, result.IncidentalCityCapacity);
            string roamingCargo = FormatCargoFill(result.RoamingCargo, result.RoamingCapacity);
            report.AppendLine($"- {PlayerLabel(player)}: first advanced trap turn {firstTrap}; " +
                $"prepared city barter arrivals {result.PreparedCityVisits} at {preparedCityCargo} cargo; " +
                $"incidental city barter visits {result.IncidentalCityVisits} at {incidentalCityCargo} cargo; " +
                $"roaming barter encounters {result.RoamingVisits} at {roamingCargo} cargo; " +
                $"camp goods collected value {result.CollectedTradeValue:0}; " +
                $"barter value offered/acquired {result.OfferedTradeValue:0}/{result.AcquiredTradeValue:0}; " +
                $"value consolidations {result.Consolidations}; capped-production camp-turns {result.CappedCampTurns}");
        }

        report.AppendLine();
        report.AppendLine("Timeline");
        if (events.Count == 0)
            report.AppendLine("- No strategic actions recorded.");
        else
            foreach (string entry in events)
                report.AppendLine("- " + entry);

        return report.ToString();
    }

    static string PlayerLabel(Player player) => $"P{player.Index + 1} {player.Name}";

    static string CharacterLabel(Character character) => $"{character.Name} ({character.Class})";

    static string LocationLabel(Location? location) => location is null ? "nowhere" : $"{location.Title} [#{location.Id}]";

    static string DifficultyLabel(int difficulty) => difficulty switch
    {
        0 => "easy",
        1 => "normal",
        _ => "hard"
    };

    static string FormatCargoFill(int cargo, int capacity) => capacity == 0
        ? "n/a"
        : $"{cargo * 100f / capacity:0}%";

    static string TrapTypeLabel(ClassicGame game, Production production)
    {
        string[] preferred = { "item_trap", "item_snake_trap", "item_rat_trap", "item_knife", "item_axe", "item_pitchfork" };
        return preferred.FirstOrDefault(id =>
            game.ItemTypes.Contains(id) && game.ItemTypes[id].Production == production) ?? "base production";
    }

    static string FormatGroupState(Player player)
    {
        string members = string.Join("; ", player.Group.Select(character =>
            $"{character.Name}: H{character.Health}/F{character.Food}/W{character.Water}, " +
            $"items [{FormatItems(character.Items)}]"));
        return $"{PlayerLabel(player)} group at {LocationLabel(player.Location)}: {members}.";
    }

    static Dictionary<string, int> CountItems(IEnumerable<Item> items)
    {
        return items
            .GroupBy(item => item.Type.ID)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    static Dictionary<string, int> ItemDifference(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        return left
            .Select(pair => new KeyValuePair<string, int>(
                pair.Key,
                pair.Value - (right.TryGetValue(pair.Key, out int count) ? count : 0)))
            .Where(pair => pair.Value > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    static string FormatItems(IEnumerable<Item> items) => FormatItems(CountItems(items));

    static string FormatItems(IReadOnlyDictionary<string, int> items)
    {
        if (items.Count == 0)
            return "empty";
        return string.Join(", ", items.OrderBy(pair => pair.Key).Select(pair =>
            pair.Value == 1 ? pair.Key : $"{pair.Key} x{pair.Value}"));
    }

    sealed class DecisionSnapshot
    {
        public required Location Location { get; init; }
        public required Character[] Group { get; init; }
        public required Dictionary<Character, Dictionary<string, int>> Inventory { get; init; }
        public required Character[] CampMembers { get; init; }
        public required Dictionary<string, int> GroundItems { get; init; }
        public required Dictionary<string, bool> GroundItemTypes { get; init; }
        public required Dictionary<string, int> RoomItems { get; init; }

        public static DecisionSnapshot Capture(Player player)
        {
            Item[] groundItems = player.Location.Items.ToArray();
            Character[] group = player.Group.ToArray();
            return new DecisionSnapshot
            {
                Location = player.Location,
                Group = group,
                Inventory = group.ToDictionary(character => character, character => CountItems(character.Items)),
                CampMembers = player.Location.CampNPC.Where(character => character.Player == player).ToArray(),
                GroundItems = CountItems(groundItems),
                GroundItemTypes = groundItems
                    .GroupBy(item => item.Type.ID)
                    .ToDictionary(itemGroup => itemGroup.Key, itemGroup =>
                        AiItemPool.Accepts(itemGroup.First().Type) ||
                        AiItemPool.IsConstructionMaterial(itemGroup.Key)),
                RoomItems = CountItems(player.Location.Rooms.SelectMany(room => room.Items))
            };
        }
    }

    sealed class EconomyMetrics
    {
        static readonly Regex CargoPattern = new(
            @"^(prepared city|incidental city|roaming) barter .*: cargo (\d+)/(\d+) slots", RegexOptions.Compiled);
        static readonly Regex TradePattern = new(
            @"^(traded|consolidated).*\(value (\d+) -> (\d+),", RegexOptions.Compiled);
        static readonly Regex CollectionPattern = new(
            @"^collected .*\(trade value (\d+)\)$", RegexOptions.Compiled);

        readonly ClassicGame game;
        readonly Dictionary<Player, PlayerEconomyMetrics> players;

        public EconomyMetrics(ClassicGame game)
        {
            this.game = game;
            players = game.World.Players.ToDictionary(player => player, _ => new PlayerEconomyMetrics());
        }

        public PlayerEconomyMetrics this[Player player] => players[player];

        public void Observe(Player player, int turn, string message)
        {
            PlayerEconomyMetrics result = players[player];
            if (result.FirstAdvancedTrapTurn == null &&
                (message.Contains("item_trap") || message.Contains("item_rat_trap") ||
                    message.Contains("item_snake_trap")) &&
                (message.StartsWith("assembled") || message.StartsWith("installed") ||
                    message.StartsWith("traded")))
                result.FirstAdvancedTrapTurn = turn;

            Match cargo = CargoPattern.Match(message);
            if (cargo.Success)
            {
                int carried = int.Parse(cargo.Groups[2].Value);
                int capacity = int.Parse(cargo.Groups[3].Value);
                if (cargo.Groups[1].Value == "prepared city")
                {
                    result.PreparedCityVisits++;
                    result.PreparedCityCargo += carried;
                    result.PreparedCityCapacity += capacity;
                }
                else if (cargo.Groups[1].Value == "incidental city")
                {
                    result.IncidentalCityVisits++;
                    result.IncidentalCityCargo += carried;
                    result.IncidentalCityCapacity += capacity;
                }
                else
                {
                    result.RoamingVisits++;
                    result.RoamingCargo += carried;
                    result.RoamingCapacity += capacity;
                }
            }

            Match trade = TradePattern.Match(message);
            if (trade.Success)
            {
                result.OfferedTradeValue += int.Parse(trade.Groups[2].Value);
                result.AcquiredTradeValue += int.Parse(trade.Groups[3].Value);
                if (trade.Groups[1].Value == "consolidated")
                    result.Consolidations++;
            }

            Match collection = CollectionPattern.Match(message);
            if (collection.Success)
                result.CollectedTradeValue += int.Parse(collection.Groups[1].Value);
        }

        public void RecordCappedCampTurn()
        {
            foreach (Location camp in game.World.Locations.Where(location =>
                location.Player != null && CampEconomy.IsFoodStockCapped(location)))
                players[camp.Player].CappedCampTurns++;
        }
    }

    sealed class PlayerEconomyMetrics
    {
        public int? FirstAdvancedTrapTurn;
        public int PreparedCityVisits;
        public int PreparedCityCargo;
        public int PreparedCityCapacity;
        public int IncidentalCityVisits;
        public int IncidentalCityCargo;
        public int IncidentalCityCapacity;
        public int RoamingVisits;
        public int RoamingCargo;
        public int RoamingCapacity;
        public int CollectedTradeValue;
        public int OfferedTradeValue;
        public int AcquiredTradeValue;
        public int Consolidations;
        public int CappedCampTurns;
    }
}
