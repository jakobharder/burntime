using System.Collections.Generic;
using System.Diagnostics;
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
        bool shouldVisitTrader,
        Location? bestTradeCity,
        List<AiDecision> candidates)
    {
        // Once the second traveller has been recruited for a concrete neutral
        // settlement, that follower is committed cargo. Provision and finish the
        // camp before considering ordinary trader visits; otherwise a harmless
        // low-score round trip can repeatedly replace the next settlement leg.
        if (territorialTarget is { IsCity: false, Player: null } &&
            state.HasSettlementPlan && state.Player.Group.Count > 1)
            return;

        // Once attack preparation has started, personnel assembly and movement
        // must complete without re-running or selecting ordinary trade errands
        // between each local strategic action. Required weapons are acquired
        // before an attack plan is accepted.
        if (preparingAttack)
            return;

        Stopwatch timer = Stopwatch.StartNew();
        bool earlyEconomy = state.OwnedCampCount < 3;
        bool expansionNeedsEquipment = ExpansionTask.NeedsExpansionTool(state);
        long expansionEquipmentMilliseconds = timer.ElapsedMilliseconds;
        // Economy work prepares the next expansion push. Once a claim or attack is
        // already safe and reachable, gathering and trading must not postpone it.
        float improvementReturn = EconomicReturn.BestEmpireImprovement(state);
        long empireImprovementMilliseconds = timer.ElapsedMilliseconds;
        bool economyGrowthNeeded = improvementReturn > 0.01f;
        bool fundedSnakeTrapCampaign = EconomicSupport.IsSavingForSnakeTrap(state) &&
            TradeTask.HasAffordableHighReturnTradeCargo(state);
        long campaignMilliseconds = timer.ElapsedMilliseconds;
        // Missing productive capacity matters, but an uncertain trader assortment
        // must not outrank a strong reachable camp by itself. Finished equipment
        // and complete recipes receive the larger, actionable delivery bonus.
        float returnBonus = System.Math.Min(250, improvementReturn * 100);
        float preparedEconomyScore = preparingAttack
            ? 1000
            : territorialTarget == null || economyGrowthNeeded
            ? float.PositiveInfinity
            : 300;

        bool shouldContinueTrading = TradeTask.ShouldContinueTrading(state);
        long continueTradingMilliseconds = timer.ElapsedMilliseconds;
        if (shouldContinueTrading)
        {
            candidates.Add(new AiDecision(
                AiAction.Wait,
                System.Math.Min(preparedEconomyScore,
                    expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 900 : 740) +
                    returnBonus,
                context.Current,
                Reason: "continue trading surplus goods for needed equipment"));
        }
        else if (shouldVisitTrader)
        {
            Location? tradeCity = bestTradeCity ??
                StrategicAi.FindNearestCity(state);
            float tradeScore = System.Math.Min(preparedEconomyScore,
                expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 880 : 720) +
                returnBonus;
            if (fundedSnakeTrapCampaign)
                tradeScore = System.Math.Max(tradeScore, 1850);
            if (!preparingAttack && TradeTask.ShouldReduceTradeCaravan(state))
            {
                candidates.Add(new AiDecision(
                    AiAction.StationTradeFollower, tradeScore + 20, context.Current,
                    Reason: "leave an extra follower at camp for an efficient city caravan"));
            }
            else if (TradeTask.ShouldFillCityCaravanBeforeDeparture(state, bestTradeCity))
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
        long tradeCandidateMilliseconds = timer.ElapsedMilliseconds;

        RegionalOpportunities.AddDeliveryCandidate(
            state, candidates,
            System.Math.Min(preparedEconomyScore,
                expansionNeedsEquipment ? policy.ExpansionEconomyScore - 20 : earlyEconomy ? 840 : 690) +
                returnBonus);
        long deliveryMilliseconds = timer.ElapsedMilliseconds;
        if (deliveryMilliseconds >= 50)
            AiTelemetry.Report(state.Player,
                $"TradeTask.AddCandidates took {deliveryMilliseconds} ms: expansion equipment " +
                $"{expansionEquipmentMilliseconds} ms, empire improvement " +
                $"{empireImprovementMilliseconds - expansionEquipmentMilliseconds} ms, campaign " +
                $"{campaignMilliseconds - empireImprovementMilliseconds} ms, continue trading " +
                $"{continueTradingMilliseconds - campaignMilliseconds} ms, trade candidate " +
                $"{tradeCandidateMilliseconds - continueTradingMilliseconds} ms, delivery " +
                $"{deliveryMilliseconds - tradeCandidateMilliseconds} ms");
    }
}
