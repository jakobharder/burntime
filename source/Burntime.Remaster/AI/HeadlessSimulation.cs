using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Burntime.Data.BurnGfx;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

public sealed class HeadlessSimulationOptions
{
    public int Turns { get; init; } = 100;
    public bool WeakFrontierTest { get; init; }
    public int Difficulty { get; init; } = 2;
    public int[]? AiDifficulties { get; init; }
    public AiProfile[]? AiProfiles { get; init; }
    public int Seed { get; init; } = 1;
    public RuleSet Rules { get; init; } = RuleSet.Dos;
    public AiProfile AI { get; init; } = AiProfile.Modern;
    public string? LoadGamePath { get; init; }
    public string? SaveGamePath { get; init; }
    public bool AssertSmokeInvariants { get; init; }
    public int EarlyDeathTurn { get; init; } = 60;
    public string? EconomyReportPath { get; init; }
}

/// <summary>
/// Runs a complete game synchronously without starting the server, AI, or render threads.
/// </summary>
public static class HeadlessSimulation
{
    public static string Run(BurntimeClassic app, HeadlessSimulationOptions options)
    {
        if (options.WeakFrontierTest && (options.LoadGamePath != null ||
            options.Rules != RuleSet.Extended || options.AI != AiProfile.Modern ||
            options.Difficulty < 1 || options.AiProfiles != null || options.AiDifficulties != null))
            throw new ArgumentException("Weak frontier requires a new Extended/Modern game on Normal or Hard without slot overrides.");
        if (options.Turns < 1)
            throw new ArgumentOutOfRangeException(nameof(options.Turns), "Turn count must be positive.");
        if (options.Difficulty is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(options.Difficulty), "Difficulty must be easy, normal, or hard.");
        if (options.AiDifficulties != null &&
            (options.AiDifficulties.Length != 4 ||
                options.AiDifficulties.Any(difficulty => difficulty is < 0 or > 2)))
            throw new ArgumentOutOfRangeException(nameof(options.AiDifficulties),
                "AI difficulties must contain four easy, normal, or hard values.");
        if (options.AiProfiles != null && options.AiProfiles.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(options.AiProfiles),
                "AI profiles must contain four values.");
        if (options.EarlyDeathTurn < 1)
            throw new ArgumentOutOfRangeException(nameof(options.EarlyDeathTurn),
                "Early-death cutoff must be positive.");

        Platform.Math.SetRandomSeed(options.Seed);

        GameCreation creation = new(app);
        if (options.LoadGamePath is null)
        {
            NewGameInfo info = new()
            {
                NameOne = null,
                NameTwo = null,
                FaceOne = -1,
                FaceTwo = -1,
                Difficulty = options.Difficulty,
                AiDifficulties = options.AiDifficulties,
                AiProfiles = options.AiProfiles,
                ColorOne = BurntimePlayerColor.Green,
                ColorTwo = BurntimePlayerColor.Red,
                Rules = options.Rules,
                AI = options.AI,
            };
            creation.CreateNewGame(info, startServer: false);
        }
        else if (!creation.LoadGame(options.LoadGamePath, startServer: false))
        {
            throw new InvalidDataException($"Could not load save game '{options.LoadGamePath}'.");
        }

        ClassicGame game = (ClassicGame)app.Server.World;
        WeakFrontierScenario? frontier = options.WeakFrontierTest ? new(game, options.Difficulty) : null;
        if (options.AssertSmokeInvariants)
            AssertConfiguration(game, options);
        List<string> events = new();
        AttackPlanObservation plans = new();
        List<DeathObservation> deaths = new();
        bool[] initialCharacterDeaths = game.World.Players
            .Select(player => player.Character.IsDead)
            .ToArray();
        bool[] knownCharacterDeaths = initialCharacterDeaths.ToArray();
        bool[] dosMaintenanceBlocked = new bool[game.World.Players.Count];
        Dictionary<int, int?> ownership = CaptureOwnership(game);
        int completedTurns = 0;
        Player? winner = null;
        int activeTurn = 0;
        EconomyMetrics economy = new(game);
        EconomyObservation? observation = options.EconomyReportPath == null ? null : new(game);
        List<(int Turn, long AiMilliseconds, long WorldMilliseconds, long TotalMilliseconds,
            string PlayerMilliseconds)> timings = new();
        AiTelemetry.Sink = (eventPlayer, message) =>
        {
            events.Add($"Turn {activeTurn}: {PlayerLabel(eventPlayer)} {message}.");
            economy.Observe(eventPlayer, activeTurn, message);
        };
        AiTelemetry.AttackPlanStarted = (player, target) =>
            plans.Record(player.Index, target.Id, game.World.Day);
        AiTelemetry.EventSink = (eventPlayer, telemetryEvent) =>
        {
            if (telemetryEvent == AiTelemetryEvent.DosMaintenanceBlockedByConflict)
                dosMaintenanceBlocked[eventPlayer.Index] = true;
            else if (telemetryEvent == AiTelemetryEvent.DosMaintenanceCompleted)
                dosMaintenanceBlocked[eventPlayer.Index] = false;
        };

        try
        {
            for (int turn = 1; turn <= options.Turns; turn++)
            {
                int campaignTurn = game.World.Day + 1;
                Stopwatch turnTimer = Stopwatch.StartNew();
                List<string> playerMilliseconds = new();
                activeTurn = turn;
                foreach (Player player in game.World.Players)
                {
                    if (player.IsDead || player.IsTraveling)
                        continue;

                    DecisionSnapshot before = DecisionSnapshot.Capture(player);
                    Location beforeLocation = player.Location;
                    Location? beforeDestination = player.Destination;
                    int eventCountBeforeTurn = events.Count;
                    bool hostileContext = IsHostileCombatContext(player, beforeLocation);

                    Stopwatch playerTimer = Stopwatch.StartNew();
                    observation?.BeforeAction();
                    frontier?.BeforeAction();
                    AiStateOperations.Turn(player.AiState);
                    frontier?.AfterAction(player);
                    observation?.AfterAction(turn);
                    observation?.AfterFoodCollection();
                    playerMilliseconds.Add(
                        $"{PlayerLabel(player)} {playerTimer.ElapsedMilliseconds} ms");

                    RecordDecisionChanges(player, before, turn, events);
                    string[] turnEvents = events.Skip(eventCountBeforeTurn).ToArray();
                    bool lastChance = turnEvents.Any(message =>
                        message.Contains("last-chance", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("trapped", StringComparison.OrdinalIgnoreCase));
                    bool reportedCombat = turnEvents.Any(message =>
                        message.Contains(" attacks ", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("opposition", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("contested", StringComparison.OrdinalIgnoreCase));
                    RecordDeaths(
                        game,
                        knownCharacterDeaths,
                        deaths,
                        campaignTurn,
                        lastChance ? DeathCause.LastChanceCombat
                            : hostileContext || reportedCombat
                                ? DeathCause.StrategicCombat
                                : DeathCause.AiAction,
                        dosMaintenanceBlocked);

                    if (beforeDestination != player.Destination && player.Destination is not null)
                    {
                        events.Add($"Turn {turn}: {PlayerLabel(player)} travels from {LocationLabel(beforeLocation)} " +
                            $"toward {LocationLabel(player.Destination)} ({player.RemainingDays} days).");
                    }
                }

                long aiMilliseconds = turnTimer.ElapsedMilliseconds;

                economy.RecordCappedCampTurn();
                observation?.BeforeDaily();
                bool[] foodExhaustedBeforeDaily = game.World.Players
                    .Select(player => player.Character.Food == 0)
                    .ToArray();
                bool[] waterExhaustedBeforeDaily = game.World.Players
                    .Select(player => player.Character.Water == 0)
                    .ToArray();
                game.Turn();
                RecordDeaths(game, knownCharacterDeaths, deaths, campaignTurn,
                    DeathCause.DailyProcessing, dosMaintenanceBlocked,
                    foodExhaustedBeforeDaily, waterExhaustedBeforeDaily);

                // Advance every player, including human-controlled slots from
                // loaded games. This makes the simulation a save compatibility
                // smoke test rather than only an AI decision test.
                foreach (Player player in game.World.Players)
                    player.Turn();
                RecordDeaths(game, knownCharacterDeaths, deaths, campaignTurn,
                    DeathCause.PlayerTurn, dosMaintenanceBlocked);
                long worldMilliseconds = turnTimer.ElapsedMilliseconds - aiMilliseconds;

                RecordOwnershipChanges(game, ownership, turn, events, economy);
                economy.RecordTurn(turn);
                observation?.RecordTurn(turn);
                foreach (Player player in game.World.Players.Where(player => !player.IsDead))
                    events.Add($"Turn {turn}: {FormatGroupState(player)}");
                timings.Add((turn, aiMilliseconds, worldMilliseconds,
                    turnTimer.ElapsedMilliseconds, string.Join(", ", playerMilliseconds)));
                completedTurns = turn;
                if (frontier?.HasProgress == true)
                    break;
                winner = game.CheckWinner() as Player;
                if (winner is not null && observation == null && frontier == null)
                    break;

                Player[] survivors = game.World.Players
                    .Where(player => !player.IsDead)
                    .ToArray();
                if (survivors.Length == 1)
                    winner = survivors[0];
                if (observation == null ? survivors.Length <= 1 :
                    !survivors.Any(player => AiStateOperations.GetProfile(player) == AiProfile.Modern))
                    break;
            }
        }
        finally
        {
            AiTelemetry.Sink = null;
            AiTelemetry.EventSink = null;
            AiTelemetry.AttackPlanStarted = null;
        }

        if (options.EconomyReportPath is not null)
            observation!.Write(options.EconomyReportPath, options);

        if (options.SaveGamePath is not null && !creation.SaveGame(options.SaveGamePath))
            throw new InvalidOperationException($"Could not save simulation to '{options.SaveGamePath}'.");

        if (options.AssertSmokeInvariants)
            AssertSmokeInvariants(game, options, completedTurns, deaths,
                initialCharacterDeaths);

        string report = BuildReport(game, options, completedTurns, winner, events, economy, timings)
            + "\nRepeated attack plans (review only; restarts can be legitimate)\n"
            + plans.Describe();
        if (frontier != null && !frontier.HasProgress)
            throw new InvalidOperationException($"Weak frontier: no damage or capture after {completedTurns} turns.\n{report}");
        return report;
    }

    static void AssertConfiguration(ClassicGame game, HeadlessSimulationOptions options)
    {
        if (game.Rules != options.Rules)
            throw new InvalidDataException(
                $"Smoke invariant failed: expected {options.Rules} rules, loaded {game.Rules}.");

        AiProfile[] expected = options.AiProfiles ??
            Enumerable.Repeat(options.AI, game.World.Players.Count).ToArray();
        AiProfile[] actual = game.World.Players.Select(AiStateOperations.GetProfile).ToArray();
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidDataException(
                "Smoke invariant failed: expected AI profiles " +
                $"[{string.Join(", ", expected)}], loaded [{string.Join(", ", actual)}].");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] == AiProfile.None && !game.World.Players[index].IsDead)
            {
                throw new InvalidDataException(
                    $"Smoke invariant failed: disabled P{index + 1} is alive.");
            }
        }
    }

    static bool IsHostileCombatContext(Player actor, Location location)
    {
        if (location.Player != null && location.Player != actor)
            return true;
        ClassicGame game = (ClassicGame)actor.Container.Root;
        return game.World.Players.Any(opponent =>
            opponent != actor && !opponent.IsDead && !opponent.IsTraveling &&
            opponent.Location == location);
    }

    static void RecordDeaths(
        ClassicGame game,
        bool[] knownDeaths,
        ICollection<DeathObservation> deaths,
        int turn,
        DeathCause cause,
        IReadOnlyList<bool> dosMaintenanceBlocked,
        IReadOnlyList<bool>? foodExhausted = null,
        IReadOnlyList<bool>? waterExhausted = null)
    {
        foreach (Player player in game.World.Players)
        {
            bool dead = player.Character.IsDead;
            if (!knownDeaths[player.Index] && dead)
                deaths.Add(new DeathObservation(player.Index, turn, cause,
                    foodExhausted?[player.Index] == true,
                    waterExhausted?[player.Index] == true,
                    dosMaintenanceBlocked[player.Index]));
            knownDeaths[player.Index] = dead;
        }
    }

    static void AssertSmokeInvariants(
        ClassicGame game,
        HeadlessSimulationOptions options,
        int completedTurns,
        IReadOnlyCollection<DeathObservation> deaths,
        IReadOnlyList<bool> initiallyDead)
    {
        AssertConfiguration(game, options);

        DeathObservation[] unexpected = deaths.Where(death =>
                death.Turn <= options.EarlyDeathTurn && death.Cause is not
                    (DeathCause.StrategicCombat or DeathCause.LastChanceCombat) &&
                !IsExpectedAmigaFoodAttrition(
                    AiStateOperations.GetProfile(game.World.Players[death.Player]),
                    death.Cause == DeathCause.DailyProcessing,
                    death.FoodExhausted,
                    death.WaterExhausted) &&
                !IsExpectedDosConflictAttrition(
                    AiStateOperations.GetProfile(game.World.Players[death.Player]),
                    death.Cause == DeathCause.DailyProcessing,
                    death.FoodExhausted || death.WaterExhausted,
                    death.DosMaintenanceBlocked))
            .Take(1)
            .ToArray();
        if (unexpected.Length > 0)
        {
            DeathObservation death = unexpected[0];
            throw new InvalidDataException(
                $"Smoke invariant failed: P{death.Player + 1} died on turn {death.Turn} " +
                $"during {death.Cause}, not conquest-related combat.");
        }

        if (completedTurns >= options.Turns)
            return;

        bool gameVictory = game.CheckWinner() is Player;
        Player[] enabledPlayers = game.World.Players.Where(player =>
            AiStateOperations.GetProfile(player) != AiProfile.None).ToArray();
        bool soleSurvivor = enabledPlayers.Count(player => !player.IsDead) <= 1;
        if (!gameVictory && !soleSurvivor)
        {
            throw new InvalidDataException(
                $"Smoke invariant failed: stopped after {completedTurns} of {options.Turns} turns " +
                "without game victory or a sole survivor.");
        }

        if (soleSurvivor)
        {
            int[] unexplained = enabledPlayers
                .Where(player => player.IsDead && !initiallyDead[player.Index] &&
                    !deaths.Any(death =>
                        death.Player == player.Index &&
                        (death.Cause is DeathCause.StrategicCombat or DeathCause.LastChanceCombat ||
                            IsExpectedAmigaFoodAttrition(
                                AiStateOperations.GetProfile(player),
                                death.Cause == DeathCause.DailyProcessing,
                                death.FoodExhausted,
                                death.WaterExhausted))))
                .Select(player => player.Index + 1)
                .ToArray();
            if (unexplained.Length > 0)
            {
                throw new InvalidDataException(
                    "Smoke invariant failed: sole-survivor termination includes " +
                    $"non-combat deaths for P{string.Join(", P", unexplained)}.");
            }
        }
    }

    enum DeathCause
    {
        AiAction,
        StrategicCombat,
        LastChanceCombat,
        DailyProcessing,
        PlayerTurn
    }

    internal static bool IsExpectedDosConflictAttrition(
        AiProfile profile,
        bool diedDuringDailyProcessing,
        bool supplyExhausted,
        bool maintenanceBlockedSinceLastRefill) =>
        profile == AiProfile.Dos && diedDuringDailyProcessing &&
        supplyExhausted && maintenanceBlockedSinceLastRefill;

    internal static bool IsExpectedAmigaFoodAttrition(
        AiProfile profile,
        bool diedDuringDailyProcessing,
        bool foodExhausted,
        bool waterExhausted) =>
        profile == AiProfile.Amiga && diedDuringDailyProcessing &&
        foodExhausted && !waterExhausted;

    readonly record struct DeathObservation(
        int Player,
        int Turn,
        DeathCause Cause,
        bool FoodExhausted,
        bool WaterExhausted,
        bool DosMaintenanceBlocked);

    static Dictionary<int, int?> CaptureOwnership(ClassicGame game)
    {
        return game.World.Locations.ToDictionary(location => location.Id, location => location.Player?.Index);
    }

    static void RecordOwnershipChanges(
        ClassicGame game,
        Dictionary<int, int?> ownership,
        int turn,
        List<string> events,
        EconomyMetrics economy)
    {
        foreach (Location location in game.World.Locations)
        {
            int? currentOwner = location.Player?.Index;
            int? previousOwner = ownership[location.Id];
            if (previousOwner == currentOwner)
                continue;

            if (previousOwner.HasValue && currentOwner.HasValue)
                economy.RecordConquest(game.World.Players[currentOwner.Value]);

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
                events.Add($"{prefix} moved to strategic reserve: {FormatItems(collected)}.");
            if (retained.Count > 0)
                events.Add($"{prefix} retained for inventory or camp storage: {FormatItems(retained)}.");
            if (leftBehind.Count > 0)
                events.Add($"{prefix} left ground items due to unavailable storage: {FormatItems(leftBehind)}.");
        }

        Character[] currentGroup = player.Party.ToArray();
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
            bool firstCampMember = before.CampMembers.Length == 0 &&
                before.CampOwner != player;
            string action = firstCampMember
                ? $"created a camp at {LocationLabel(character.Location)} using {CharacterLabel(character)}"
                : $"stationed {CharacterLabel(character)} at {LocationLabel(character.Location)}";
            events.Add($"{prefix} {action}; NPC inventory: {FormatItems(character.Items)}; " +
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
        EconomyMetrics economy,
        IReadOnlyCollection<(int Turn, long AiMilliseconds, long WorldMilliseconds,
            long TotalMilliseconds, string PlayerMilliseconds)> timings)
    {
        StringBuilder report = new();
        report.AppendLine("Burntime headless simulation");
        report.AppendLine($"Seed: {options.Seed}");
        report.AppendLine($"Source: {(options.LoadGamePath is null ? "new game" : "save game")}");
        report.AppendLine($"Difficulty: {DifficultyLabel(game.World.Difficulty)}");
        report.AppendLine("AI difficulties: " + string.Join(", ",
            game.World.Players.Select(player =>
                AiStateOperations.GetProfile(player) == AiProfile.None
                    ? $"P{player.Index + 1} disabled"
                    : AiStateOperations.TryGetDifficulty(player.AiState, out int difficulty)
                    ? $"P{player.Index + 1} {DifficultyLabel(difficulty)}"
                    : $"P{player.Index + 1} human")));
        AiProfile[] profiles = game.World.Players
            .Select(AiStateOperations.GetProfile)
            .ToArray();
        if (profiles.Distinct().Count() > 1)
            report.AppendLine("AI profiles: " + string.Join(", ", profiles.Select(
                (profile, index) => $"P{index + 1} {profile.ToString().ToLowerInvariant()}")));
        report.AppendLine($"Requested turns: {options.Turns}");
        report.AppendLine($"Completed turns: {completedTurns}");
        report.AppendLine($"Final world day: {game.World.Day}");
        report.AppendLine($"Rules: {game.Rules.ToString().ToLowerInvariant()}");
        report.AppendLine($"Winner: {(winner is null ? "none" : PlayerLabel(winner))}");
        report.AppendLine();

        report.AppendLine("Turn processing performance");
        var slowTurns = timings
            .Where(timing => timing.TotalMilliseconds >= 1000)
            .OrderByDescending(timing => timing.TotalMilliseconds)
            .ToArray();
        report.AppendLine($"- Slow turns (>=1000 ms): {slowTurns.Length}/{timings.Count}");
        var longestTurn = timings.OrderByDescending(timing => timing.TotalMilliseconds).FirstOrDefault();
        if (timings.Count > 0)
            report.AppendLine($"- Longest turn: {longestTurn.Turn} at " +
                $"{longestTurn.TotalMilliseconds} ms (AI {longestTurn.AiMilliseconds} ms, " +
                $"world {longestTurn.WorldMilliseconds} ms; {longestTurn.PlayerMilliseconds})");
        foreach (var timing in slowTurns)
            report.AppendLine($"- Turn {timing.Turn}: {timing.TotalMilliseconds} ms " +
                $"(AI {timing.AiMilliseconds} ms, world {timing.WorldMilliseconds} ms; " +
                $"{timing.PlayerMilliseconds})");
        report.AppendLine();

        report.AppendLine("Recovery service locations");
        foreach (Location location in game.World.Locations.Where(location =>
            location.Map?.Entrances?.Any(entrance => entrance.RoomType is
                RoomType.Restaurant or RoomType.Pub or RoomType.Doctor) == true))
        {
            string services = string.Join(", ", location.Map.Entrances
                .Where(entrance => entrance.RoomType is
                    RoomType.Restaurant or RoomType.Pub or RoomType.Doctor)
                .Select(entrance => entrance.RoomType.ToString().ToLowerInvariant())
                .Distinct());
            report.AppendLine($"- {LocationLabel(location)}: {services}");
        }
        report.AppendLine();

        report.AppendLine("Players");
        foreach (Player player in game.World.Players)
        {
            int camps = game.World.Locations.Count(location => location.Player == player);
            int establishedCamps = game.World.Locations.Count(location =>
                location.Player == player && CampEconomy.IsWellEstablished(location));
            int defenders = game.World.Locations.Sum(location => location.CampNPC.Count(character => character.Player == player));
            string state = player.IsDead || player.Character.IsDead ? "dead" : "alive";
            string travel = player.IsTraveling
                ? $"traveling to {LocationLabel(player.Destination)} ({player.RemainingDays} days left)"
                : $"at {LocationLabel(player.Location)}";

            report.AppendLine($"- {PlayerLabel(player)}: {state}, {travel}, {camps} camps " +
                $"({establishedCamps} well established), " +
                $"group {player.Party.Count}, defenders {defenders}, " +
                $"health {player.Character.Health}, food {player.Character.Food}, water {player.Character.Water}");
            foreach (Character member in player.Party)
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
                $"food surplus {CampEconomy.FoodSurplusPerDay(location)}/day; " +
                $"water {location.Source.Water}/day; role {CampEconomy.StrategicRole(location)}; " +
                $"items {FormatItems(campItems)}; highest possible trap {bestTrap}; used trap {usedTrap}");
            foreach (Character defender in defenders)
            {
                string weapon = defender.Weapon?.Type.ID ?? "none";
                string protection = defender.Protection?.Type.ID ?? "none";
                report.AppendLine($"  - {defender.Name}: {defender.Class}, health {defender.Health}, " +
                    $"food {defender.Food}, water {defender.Water}, weapon {weapon}, " +
                    $"protection {protection}, inventory {FormatItems(defender.Items)}");
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
        report.AppendLine("Strategic reserves (shared, slotless inventory)");
        foreach (Player player in game.World.Players)
        {
            var contents = (player.AiState as ClassicAiState)?.Reserve.GetContents().ToArray() ??
                Array.Empty<(ItemType Type, int Count)>();
            float tradeValue = contents.Sum(entry => entry.Type.TradeValue * entry.Count);
            Dictionary<string, int> counts = contents
                .GroupBy(entry => entry.Type.ID)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Count));
            report.AppendLine($"- {PlayerLabel(player)}: {counts.Values.Sum()} items, " +
                $"trade value {tradeValue:0}; {FormatItems(counts)}");
        }

        report.AppendLine();
        report.AppendLine("AI economy results");
        foreach (Player player in game.World.Players)
        {
            PlayerEconomyMetrics result = economy[player];
            float sustainableIncome = player.AiState is ClassicAiState ai
                ? EconomicReturn.SustainableEmpireIncome(ai)
                : 0;
            string firstTrap = result.FirstAdvancedTrapTurn?.ToString() ?? "none";
            string preparedCityCargo = FormatCargoFill(result.PreparedCityCargo, result.PreparedCityCapacity);
            string incidentalCityCargo = FormatCargoFill(result.IncidentalCityCargo, result.IncidentalCityCapacity);
            string roamingCargo = FormatCargoFill(result.RoamingCargo, result.RoamingCapacity);
            string slumpComponents = result.SlumpComponents.Count == 0
                ? "none"
                : string.Join(", ", result.SlumpComponents.OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key} x{entry.Value}"));
            report.AppendLine($"- {PlayerLabel(player)}: sustainable camp income " +
                $"{sustainableIncome:0.0} trade value/day; first advanced trap turn {firstTrap}; " +
                $"snake-trap sightings/purchases {result.SnakeTrapEncounters}/{result.SnakeTrapPurchases}; " +
                $"25-turn slump components {slumpComponents}; " +
                $"prepared city barter arrivals {result.PreparedCityVisits} at {preparedCityCargo} cargo; " +
                $"incidental city barter visits {result.IncidentalCityVisits} at {incidentalCityCargo} cargo; " +
                $"roaming barter encounters {result.RoamingVisits} at {roamingCargo} cargo; " +
                $"camp goods collected value {result.CollectedTradeValue:0}; " +
                $"barter value offered/acquired {result.OfferedTradeValue:0}/{result.AcquiredTradeValue:0}; " +
                $"value consolidations {result.Consolidations}; capped-production camp-turns {result.CappedCampTurns}");
        }

        report.AppendLine();
        report.AppendLine("Requirement indicators");
        foreach (Player player in game.World.Players)
        {
            PlayerEconomyMetrics result = economy[player];
            Location[] camps = game.World.Locations
                .Where(location => location.Player == player)
                .ToArray();
            int advancedCamps = camps.Count(HasAdvancedTrapAtCamp);
            int containers = camps.Sum(camp => camp.Rooms.SelectMany(room => room.Items)
                .Concat(camp.CampNPC
                    .Where(npc => npc.Player == player && !npc.IsDead)
                    .SelectMany(npc => npc.Items))
                .Count(item => AiItemPool.IsWaterContainer(item.Type)));
            int pumps = camps.Count(camp => camp.Rooms.SelectMany(room => room.Items)
                .Any(Trading.IsPump));
            int unmetPumpNeeds = camps.Count(Trading.NeedsPump);
            string campsAt30 = result.CampsAtTurn30?.ToString() ?? "n/a";
            float advancedCoverage = camps.Length == 0 ? 0 : advancedCamps * 100f / camps.Length;
            float containersPerCamp = camps.Length == 0 ? 0 : containers / (float)camps.Length;
            report.AppendLine($"- {PlayerLabel(player)}: camps at turn 30 {campsAt30}; " +
                $"conquests {result.Conquests}; advanced-trap coverage {advancedCamps}/{camps.Length} " +
                $"({advancedCoverage:0}%); camp water containers {containers}/{camps.Length} " +
                $"({containersPerCamp:0.0}/camp); camps with pumps {pumps}; " +
                $"unmet pump needs {unmetPumpNeeds}; " +
                $"city minimum top-ups {result.CityMinimumTopUps}; " +
                $"paid doctor visits {result.DoctorVisits}; " +
                $"generated slump components {result.SlumpComponents.Values.Sum()}");
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

    static bool HasAdvancedTrapAtCamp(Location camp) => camp.Rooms
        .SelectMany(room => room.Items)
        .Concat(camp.CampNPC.SelectMany(npc => npc.Items))
        .Any(item => item.ID is "item_rat_trap" or "item_snake_trap" or "item_trap");

    static string FormatGroupState(Player player)
    {
        string members = string.Join("; ", player.Party.Select(character =>
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
        public required Player? CampOwner { get; init; }
        public required Dictionary<string, int> GroundItems { get; init; }
        public required Dictionary<string, bool> GroundItemTypes { get; init; }
        public required Dictionary<string, int> RoomItems { get; init; }

        public static DecisionSnapshot Capture(Player player)
        {
            Item[] groundItems = player.Location.Items.ToArray();
            Character[] group = player.Party.ToArray();
            return new DecisionSnapshot
            {
                Location = player.Location,
                Group = group,
                Inventory = group.ToDictionary(character => character, character => CountItems(character.Items)),
                CampMembers = player.Location.CampNPC.Where(character => character.Player == player).ToArray(),
                CampOwner = player.Location.Player,
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
        static readonly Regex SlumpSupportPattern = new(
            @"^economic slump support generated (\S+) ", RegexOptions.Compiled);

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
            if (message.StartsWith("encountered item_snake_trap"))
                result.SnakeTrapEncounters++;
            if (message.StartsWith("paid doctor"))
                result.DoctorVisits++;
            if (message.StartsWith("received city minimum"))
                result.CityMinimumTopUps++;
            if (message.StartsWith("traded") && message.Contains(" for item_snake_trap "))
                result.SnakeTrapPurchases++;

            Match slumpSupport = SlumpSupportPattern.Match(message);
            if (slumpSupport.Success)
            {
                string component = slumpSupport.Groups[1].Value;
                result.SlumpComponents[component] = result.SlumpComponents.GetValueOrDefault(component) + 1;
            }

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

        public void RecordConquest(Player player) => players[player].Conquests++;

        public void RecordTurn(int turn)
        {
            if (turn != 30)
                return;
            foreach (Player player in game.World.Players)
                players[player].CampsAtTurn30 = game.World.Locations.Count(location =>
                    location.Player == player);
        }
    }

    sealed class PlayerEconomyMetrics
    {
        public int? FirstAdvancedTrapTurn;
        public int SnakeTrapEncounters;
        public int SnakeTrapPurchases;
        public readonly Dictionary<string, int> SlumpComponents = new();
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
        public int? CampsAtTurn30;
        public int Conquests;
        public int DoctorVisits;
        public int CityMinimumTopUps;
    }
}
