using System.Collections.Generic;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    public static void AddCandidates(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location? territorialTarget,
        bool preparingAttack,
        List<AiDecision> candidates)
    {
        bool earlyEconomy = state.OwnedCampCount < 3;
        bool expansionNeedsEquipment = ExpansionTask.NeedsExpansionTool(state);
        // Economy work prepares the next expansion push. Once a claim or attack is
        // already safe and reachable, gathering and trading must not postpone it.
        bool economyGrowthNeeded = ExpansionTask.ShouldPrioritizeEconomicGrowth(state);
        float improvementReturn = EconomicReturn.BestEmpireImprovement(state);
        // Missing productive capacity matters, but an uncertain trader assortment
        // must not outrank a strong reachable camp by itself. Finished equipment
        // and complete recipes receive the larger, actionable delivery bonus.
        float returnBonus = System.Math.Min(250, improvementReturn * 100);
        float preparedEconomyScore = preparingAttack
            ? 1000
            : territorialTarget == null || economyGrowthNeeded
            ? float.PositiveInfinity
            : 300;

        if (TradeTask.ShouldContinueTrading(state))
        {
            candidates.Add(new AiDecision(
                AiAction.Wait,
                System.Math.Min(preparedEconomyScore,
                    expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 900 : 740) +
                    returnBonus,
                context.Current,
                Reason: "continue trading surplus goods for needed equipment"));
        }
        else if (TradeTask.ShouldVisitTrader(state))
        {
            Location? tradeCity = TradeTask.FindBestTradeCity(state) ??
                StrategicAi.FindNearestCity(state);
            float tradeScore = System.Math.Min(preparedEconomyScore,
                expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 880 : 720) +
                returnBonus;
            if (TradeTask.ShouldReduceTradeCaravan(state))
            {
                candidates.Add(new AiDecision(
                    AiAction.StationTradeFollower, tradeScore + 20, context.Current,
                    Reason: "leave an extra follower at camp for an efficient city caravan"));
            }
            else if (TradeTask.ShouldFillCityCaravanBeforeDeparture(state))
            {
                candidates.Add(new AiDecision(
                    AiAction.Wait, tradeScore, context.Current,
                    Reason: "wait for camp production to fill the city caravan"));
            }
            else
            {
                RouteOpportunities.AddCityTradeCandidate(
                    state, candidates, tradeCity, tradeScore);
            }
        }

        RegionalOpportunities.AddDeliveryCandidate(
            state, candidates,
            System.Math.Min(preparedEconomyScore,
                expansionNeedsEquipment ? policy.ExpansionEconomyScore - 20 : earlyEconomy ? 840 : 690) +
                returnBonus);
    }
}
