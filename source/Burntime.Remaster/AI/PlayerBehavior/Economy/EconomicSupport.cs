using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Runtime-only timing and trader-exposure memory. The material grant count is
/// kept separately on ClassicAiState because its configured lifetime allowance
/// must survive saving and reloading.
/// </summary>
internal static class EconomicSupport
{
    internal const int AdvancedTrapSlumpTurns = 25;

    static readonly HashSet<string> AdvancedTrapIds = new()
    {
        "item_rat_trap", "item_trap", "item_snake_trap"
    };

    static readonly ConditionalWeakTable<Player, ProgressState> Progress = new();

    internal static void GrantSlumpSupportIfNeeded(ClassicAiState state)
    {
        ProgressState progress = Progress.GetOrCreateValue(state.Player);
        int day = state.RootGame.World.Day;
        int trapCount = AdvancedTrapCount(state);
        int grantLimit = AiPolicy.ForDifficulty(state.Difficulty).SlumpMaterialGrantLimit;

        if (!progress.Initialized)
        {
            progress.Initialized = true;
            progress.LastAdvancedTrapDay = day;
            progress.AdvancedTrapCount = trapCount;
            return;
        }

        if (trapCount > progress.AdvancedTrapCount)
            progress.LastAdvancedTrapDay = day;
        progress.AdvancedTrapCount = trapCount;

        // This remains a bounded escape hatch: difficulty controls the lifetime
        // grant budget, while the existing timer still requires a 25-turn slump.
        if (state.SlumpMaterialGrantsUsed >= grantLimit)
            return;

        if (day - progress.LastAdvancedTrapDay < AdvancedTrapSlumpTurns)
            return;

        Trading.ConstructionOpportunity opportunity = Trading.UsefulConstructionOpportunities(state)
            .Where(candidate => candidate.Result is "item_trap" or "item_rat_trap")
            .Select(candidate => new
            {
                Opportunity = candidate,
                Missing = candidate.Materials
                    .Where(component => !Trading.HasConstructionComponent(state, component))
                    .ToArray()
            })
            .Where(candidate => candidate.Missing.Length > 0)
            // Complete a nearly finished rat-trap recipe immediately. Otherwise
            // keep the higher-return meat trap as the anti-stall priority.
            .OrderBy(candidate => candidate.Opportunity.Result == "item_rat_trap" &&
                candidate.Missing.Length == 1
                    ? 0
                    : candidate.Opportunity.Result == "item_trap" ? 1 : 2)
            .ThenBy(candidate => candidate.Missing.Length)
            .ThenByDescending(candidate => candidate.Opportunity.EconomicValue)
            .Select(candidate => candidate.Opportunity)
            .FirstOrDefault();
        if (opportunity == null)
            return;

        string component = opportunity.Materials
            .Where(itemId => !Trading.HasConstructionComponent(state, itemId))
            .OrderByDescending(itemId => state.RootGame.ItemTypes[itemId].TradeValue)
            .FirstOrDefault();
        if (component == null)
            return;

        Item generated = state.RootGame.ItemTypes[component].Generate();
        if (!state.Reserve.TryReserveConstructionMaterial(generated))
            return;

        int stalledTurns = day - progress.LastAdvancedTrapDay;
        progress.LastAdvancedTrapDay = day;
        state.SlumpMaterialGrantsUsed++;
        AiTelemetry.Report(state.Player,
            $"economic slump support generated {component} for {opportunity.Result} " +
            $"after {stalledTurns} turns without a new advanced trap " +
            $"(grant {state.SlumpMaterialGrantsUsed}/{grantLimit})");
    }

    internal static bool HasAdvancedTrap(ClassicAiState state) => AdvancedTrapCount(state) > 0;

    internal static float AdvancedTrapCoverage(ClassicAiState state)
    {
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .ToArray();
        if (camps.Length == 0)
            return 0;
        int covered = camps.Count(camp => camp.Rooms.SelectMany(room => room.Items)
            .Concat(camp.CampNPC
                .Where(npc => npc.Player == state.Player && !npc.IsDead)
                .SelectMany(npc => npc.Items))
            .Any(item => AdvancedTrapIds.Contains(item.ID)));
        return covered / (float)camps.Length;
    }

    internal static bool HasPooledAdvancedTrap(ClassicAiState state) => state.Reserve.GetContents()
        .Any(entry => entry.Count > 0 && AdvancedTrapIds.Contains(entry.Type.ID));

    internal static bool IsSavingForSnakeTrap(ClassicAiState state) =>
        Progress.GetOrCreateValue(state.Player).SavingForSnakeTrap &&
        Trading.HasStrategicSnakeTrapNeed(state);

    internal static void StartSnakeTrapCampaign(ClassicAiState state) =>
        Progress.GetOrCreateValue(state.Player).SavingForSnakeTrap = true;

    internal static void CompleteSnakeTrapCampaign(ClassicAiState state) =>
        Progress.GetOrCreateValue(state.Player).SavingForSnakeTrap = false;

    internal static int TraderNoveltyScore(ClassicAiState state, Trader trader)
    {
        ProgressState progress = Progress.GetOrCreateValue(state.Player);
        if (!progress.TraderExposureDay.TryGetValue(trader.TraderId, out int lastDay))
            return 100;
        int daysSinceExposure = state.RootGame.World.Day - lastDay;
        return Math.Clamp(daysSinceExposure * 5, 0, 80);
    }

    internal static bool RecordTraderExposure(ClassicAiState state, Trader trader)
    {
        ProgressState progress = Progress.GetOrCreateValue(state.Player);
        int day = state.RootGame.World.Day;
        bool firstExposureToday = !progress.TraderExposureDay.TryGetValue(trader.TraderId, out int lastDay) ||
            lastDay != day;
        progress.TraderExposureDay[trader.TraderId] = day;
        return firstExposureToday;
    }

    internal static bool HasBeenStrategicallyStalled(ClassicAiState state, int turns)
    {
        ProgressState progress = Progress.GetOrCreateValue(state.Player);
        int day = state.RootGame.World.Day;
        int campCount = state.RootGame.World.Locations.Count(location =>
            location.Player == state.Player);
        Location current = state.Current;

        if (!progress.StrategicProgressInitialized || progress.LastStrategicLocation != current ||
            progress.LastCampCount != campCount)
        {
            progress.StrategicProgressInitialized = true;
            progress.LastStrategicLocation = current;
            progress.LastCampCount = campCount;
            progress.LastStrategicProgressDay = day;
            return false;
        }

        return day - progress.LastStrategicProgressDay >= turns;
    }

    static int AdvancedTrapCount(ClassicAiState state)
    {
        int pooled = state.Reserve.GetContents()
            .Where(entry => AdvancedTrapIds.Contains(entry.Type.ID))
            .Sum(entry => entry.Count);
        int carried = state.Player.Group.SelectMany(character => character.Items)
            .Count(item => AdvancedTrapIds.Contains(item.ID));
        int established = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .SelectMany(location => location.Rooms.SelectMany(room => room.Items)
                .Concat(location.CampNPC
                    .Where(npc => npc.Player == state.Player)
                    .SelectMany(npc => npc.Items)))
            .Count(item => AdvancedTrapIds.Contains(item.ID));
        return pooled + carried + established;
    }

    sealed class ProgressState
    {
        public bool Initialized;
        public int LastAdvancedTrapDay;
        public int AdvancedTrapCount;
        public bool SavingForSnakeTrap;
        public bool StrategicProgressInitialized;
        public Location LastStrategicLocation;
        public int LastCampCount;
        public int LastStrategicProgressDay;
        public readonly Dictionary<int, int> TraderExposureDay = new();
    }
}
