using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Runtime-only memory for economic progress and trader exposure. Keeping this
/// outside ClassicAiState deliberately leaves every legacy serialization field
/// unchanged.
/// </summary>
internal static class EconomicSupport
{
    internal const int AdvancedTrapSlumpTurns = 25;

    static readonly HashSet<string> AdvancedTrapIds = new()
    {
        "item_rat_trap", "item_trap", "item_snake_trap"
    };

    static readonly ConditionalWeakTable<Player, ProgressState> Progress = new();

    internal static void ApplySlumpSupport(ClassicAiState state)
    {
        ProgressState progress = Progress.GetOrCreateValue(state.Player);
        int day = state.RootGame.World.Day;
        int trapCount = AdvancedTrapCount(state);

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

        if (day - progress.LastAdvancedTrapDay < AdvancedTrapSlumpTurns)
            return;

        TradeTask.ConstructionOpportunity opportunity = TradeTask.UsefulConstructionOpportunities(state)
            .Where(candidate => candidate.Result is "item_trap" or "item_rat_trap")
            .Select(candidate => new
            {
                Opportunity = candidate,
                Missing = candidate.Materials
                    .Where(component => !TradeTask.HasConstructionComponent(state, component))
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
            .Where(itemId => !TradeTask.HasConstructionComponent(state, itemId))
            .OrderByDescending(itemId => state.RootGame.ItemTypes[itemId].TradeValue)
            .FirstOrDefault();
        if (component == null)
            return;

        Item generated = state.RootGame.ItemTypes[component].Generate();
        if (!state.Pool.TryReserveConstructionMaterial(generated))
            return;

        int stalledTurns = day - progress.LastAdvancedTrapDay;
        progress.LastAdvancedTrapDay = day;
        AiTelemetry.Report(state.Player,
            $"economic slump support generated {component} for {opportunity.Result} " +
            $"after {stalledTurns} turns without a new advanced trap");
    }

    internal static bool HasAdvancedTrap(ClassicAiState state) => AdvancedTrapCount(state) > 0;

    internal static bool IsSavingForSnakeTrap(ClassicAiState state) =>
        Progress.GetOrCreateValue(state.Player).SavingForSnakeTrap &&
        TradeTask.HasStrategicSnakeTrapNeed(state);

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

    static int AdvancedTrapCount(ClassicAiState state)
    {
        int pooled = state.Pool.GetContents()
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
        public readonly Dictionary<int, int> TraderExposureDay = new();
    }
}
