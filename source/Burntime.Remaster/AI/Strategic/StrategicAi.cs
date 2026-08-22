using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class StrategicAi
{
    public static void RunTurn(ClassicAiState state)
    {
        Stopwatch totalTimer = Stopwatch.StartNew();
        DefenseIntelligence.ObserveWorld(state);
        Location? bestTradeCity = TradeTask.FindBestTradeCity(state);
        LocalOpportunities.Apply(state, bestTradeCity);
        long localMilliseconds = totalTimer.ElapsedMilliseconds;
        List<string> actionTimings = new();

        const int maximumStrategicActions = 10;
        for (int action = 0; action < maximumStrategicActions; action++)
        {
            Stopwatch actionTimer = Stopwatch.StartNew();
            AiDecision decision = Choose(state, bestTradeCity);
            long chooseMilliseconds = actionTimer.ElapsedMilliseconds;
            AiDecisionExecutor.Execute(state, decision, bestTradeCity);
            actionTimings.Add($"{decision.Action} choose {chooseMilliseconds} ms, " +
                $"execute {actionTimer.ElapsedMilliseconds - chooseMilliseconds} ms");

            // Waiting and travelling deliberately consume the rest of the world
            // turn. Other local strategic actions may expose the next useful step
            // immediately, so continue choosing until one of these boundaries.
            if (decision.Action == AiAction.Wait || state.Player.IsTraveling ||
                state.Player.IsDead)
                break;

            if (action == maximumStrategicActions - 1)
                AiTelemetry.Report(state.Player,
                    $"stopped local strategy after {maximumStrategicActions} actions to avoid an infinite loop");
        }

        if (totalTimer.ElapsedMilliseconds >= 1000)
            AiTelemetry.Report(state.Player,
                $"slow AI turn {totalTimer.ElapsedMilliseconds} ms: local opportunities " +
                $"{localMilliseconds} ms; {string.Join("; ", actionTimings)}");
    }

    static AiDecision Choose(ClassicAiState state, Location? bestTradeCity)
    {
        Stopwatch timer = Stopwatch.StartNew();
        Player player = state.Player;
        AiPolicy policy = AiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        AiContext observation = AiContext.Create(state, policy);
        long contextMilliseconds = timer.ElapsedMilliseconds;
        List<AiDecision> candidates = new();

        ExpansionTask.CancelSettlementAtHostileWaypoint(state, observation);

        if (AttackTask.TryAddImmediateResponse(state, observation, policy, candidates))
            return SelectAndReport(state, candidates);

        if (observation.CriticalSupplies && !CanFinishCommittedSettlementJourney(state))
        {
            if (RecruitmentTask.TryAddCriticalRouteRecruitContinuation(
                state, observation, policy, candidates))
                return SelectAndReport(state, candidates);

            Location? reachableRecovery = RecoveryServices.FindDestination(
                state, requireReachable: true);
            Location? recovery = reachableRecovery ??
                RecoveryServices.FindDestination(state, requireReachable: false);
            AddTravelCandidate(state, candidates, recovery, 1100,
                "seek real food, water, or medical services",
                allowSurvivableRecoveryRisk: true);
            if (reachableRecovery == null && player.Group.Count > 1 &&
                !RecoveryServices.CanRecoverLocallyForTravel(state))
            {
                candidates.Add(new AiDecision(
                    AiAction.ReleaseFollower,
                    1110,
                    observation.Current,
                    Reason: "release followers when the enlarged group has no survivable recovery option"));
            }
            candidates.Add(new AiDecision(
                AiAction.Wait,
                recovery == null ? 1090 : 900,
                observation.Current,
                Reason: recovery == null
                    ? "no affordable local recovery and no useful destination"
                    : "cannot yet reach a provisioned recovery destination safely"));

            // Survival remains a hard constraint, but it now consumes real goods
            // at real facilities. If none are usable, the faction may fail.
            return SelectAndReport(state, candidates);
        }

        TerritorialPlan territory = ExpansionTask.CreatePlan(state, observation, policy);
        long territoryMilliseconds = timer.ElapsedMilliseconds;
        Location? target = territory.Target;
        bool preparingAttack = territory.PreparingAttack;
        ExpansionTask.AddImmediateClaimCandidate(observation, territory, candidates);
        bool shouldVisitTrader = TradeTask.ShouldVisitTrader(state, bestTradeCity) ||
            preparingAttack && TradeTask.NeedsAttackWaterPreparation(state);
        long tradePlanMilliseconds = timer.ElapsedMilliseconds;

        RecruitmentNeeds recruitment = RecruitmentTask.AddCandidates(
            state, observation, policy, target, preparingAttack,
            shouldVisitTrader, candidates);
        long recruitmentMilliseconds = timer.ElapsedMilliseconds;
        ReinforcementTask.AddCandidates(
            state, observation, policy, recruitment, candidates);
        long reinforcementMilliseconds = timer.ElapsedMilliseconds;

        if (state.NeedsCampImprovement() &&
            !ExpansionTask.ShouldReserveProductionTool(state))
        {
            float economicGain = System.Math.Max(0,
                EconomicReturn.MarginalCampImprovement(state, observation.Current));
            candidates.Add(new AiDecision(
                AiAction.ImproveCamp,
                650 + economicGain * 180,
                observation.Current,
                Reason: $"improve sustainable camp income by about {economicGain:0.0} value/day"));
        }

        TradeTask.AddCandidates(
            state, observation, policy, target, preparingAttack,
            shouldVisitTrader, bestTradeCity, candidates);
        long tradeMilliseconds = timer.ElapsedMilliseconds;

        ExpansionTask.AddCandidates(state, observation, policy, territory, candidates);
        long expansionMilliseconds = timer.ElapsedMilliseconds;

        if (!preparingAttack && player.Group.Count > 1 &&
            player.Group.Count < observation.TravelGroupSize && !state.HasHireableNpc())
        {
            Location? preparationCamp = TradeTask.FindBestCampForCityPreparation(state);
            AddTravelCandidate(state, candidates, preparationCamp ?? FindNearestCity(state), 560,
                preparationCamp == null ? "look for recruits" : "collect trade cargo before looking for recruits");
        }

        string idleReason = ExpansionTask.NeedsExpansionTool(state)
            ? "expansion blocked: no portable production tool and no affordable or collectible route"
            : !AttackTask.HasGroupWeapon(player)
                ? "expansion blocked: group has no weapon and no equipment route is available"
                : "no reachable expansion target with current supplies";
        candidates.Add(new AiDecision(AiAction.Wait, 0, Reason: idleReason));
        if (timer.ElapsedMilliseconds >= 500)
            AiTelemetry.Report(player,
                $"slow decision planning {timer.ElapsedMilliseconds} ms: context " +
                $"{contextMilliseconds} ms, territory " +
                $"{territoryMilliseconds - contextMilliseconds} ms, recruitment " +
                $"{recruitmentMilliseconds - tradePlanMilliseconds} ms, trade plan " +
                $"{tradePlanMilliseconds - territoryMilliseconds} ms, reinforcement " +
                $"{reinforcementMilliseconds - recruitmentMilliseconds} ms, trade " +
                $"{tradeMilliseconds - reinforcementMilliseconds} ms, expansion " +
                $"{expansionMilliseconds - tradeMilliseconds} ms");
        return SelectAndReport(state, candidates);
    }

    static bool CanFinishCommittedSettlementJourney(ClassicAiState state)
    {
        Location? target = state.StrategicTarget;
        if (!state.HasSettlementPlan || target == null || target.Player != null ||
            state.Player.Group.Any(character => character.Health <= 40))
            return false;
        RouteFinder.Route? route = RouteFinder.Find(state.Player, state.Current, target);
        return route != null && SupplyCalculator.HasRouteSupplies(
            state.Player, route, hostileTarget: false);
    }

    static AiDecision SelectAndReport(ClassicAiState state, List<AiDecision> candidates)
    {
        Player player = state.Player;
        AiDecision selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(_ => Burntime.Platform.Math.Random.Next())
            .First();
        selected = ForceCityDeparture(state, candidates, selected);
        string target = selected.Target == null ? "none" : selected.Target.Title;
        AiTelemetry.Report(player,
            $"decision {selected.Action}, target {target}, score {selected.Score:0}: {selected.Reason}");
        return selected;
    }

    static AiDecision ForceCityDeparture(
        ClassicAiState state,
        List<AiDecision> candidates,
        AiDecision selected)
    {
        if (!state.Current.IsCity || selected.Action != AiAction.Wait)
            return selected;

        AiDecision? plannedDeparture = candidates
            .Where(candidate => candidate.Action == AiAction.Travel &&
                candidate.NextStep != null &&
                state.Player.CanTravel(state.Current, candidate.NextStep))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
        if (plannedDeparture != null)
            return plannedDeparture;

        // A city may be used for its immediate recruit, barter, doctor and 3/3
        // waypoint effects, but never as an indefinite refuge. Prefer returning
        // to the faction's real economy. This final fallback deliberately ignores
        // survival projections; the human travel rules still determine which
        // direct departure is legal.
        Location? destination = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player &&
                CampEconomy.CanProvisionFood(location) &&
                CampEconomy.CanProvisionTravelGroupWater(
                    location, state.Player.Group.Count))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location)
            })
            .Where(candidate => candidate.Route?.NextStep != null &&
                state.Player.CanTravel(state.Current, candidate.Route.NextStep))
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
        destination ??= state.StrategicTarget != state.Current
            ? state.StrategicTarget
            : null;
        RouteFinder.Route? route = destination == null
            ? null
            : RouteFinder.Find(state.Player, state.Current, destination);
        Location? nextStep = route?.NextStep;
        if (nextStep == null || !state.Player.CanTravel(state.Current, nextStep))
        {
            nextStep = Enumerable.Range(0, state.Current.Neighbors.Count)
                .Where(index => state.Current.WayLengths[index] > 0)
                .Select(index => state.Current.Neighbors[index])
                .FirstOrDefault(neighbor => state.Player.CanTravel(state.Current, neighbor));
            destination = nextStep;
        }
        return nextStep == null
            ? selected
            : new AiDecision(
                AiAction.Travel,
                selected.Score,
                destination,
                nextStep,
                "leave the city instead of depending on it for survival");
    }

    internal static void AddTravelCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        Location? target,
        float score,
        string reason,
        bool allowSurvivableRecoveryRisk = false)
    {
        if (target == null)
            return;
        RouteFinder.Route? route = RouteFinder.Find(state.Player, state.Current, target);
        if (route?.NextStep == null)
            return;
        bool normallySupplied = SupplyCalculator.HasTerritorialRouteSupplies(
            state.Player, state.Current, route, hostileTarget: false);
        bool dehydrationEscape = allowSurvivableRecoveryRisk &&
            SupplyCalculator.IsDehydrationTravelNoWorseThanWaiting(
                state.Player, state.Current, route);
        if (!normallySupplied && (!allowSurvivableRecoveryRisk ||
            !SupplyCalculator.CanSurviveRecoveryRoute(state.Player, route)) &&
            !dehydrationEscape)
            return;
        candidates.Add(new AiDecision(
            AiAction.Travel,
            score - route.Days,
            target,
            route.NextStep,
            reason));
    }

    internal static Location? FindNearestLogistics(ClassicAiState state, bool requireReachable = false)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity || location.Player == state.Player)
            .Select(location => (Location: location, Route: RouteFinder.Find(state.Player, state.Current, location)))
            .Where(candidate => candidate.Route != null && candidate.Location != state.Current &&
                (!requireReachable || SupplyCalculator.HasRouteSupplies(
                    state.Player, candidate.Route, hostileTarget: false)))
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static Location? FindNearestCity(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity && location != state.Current)
            .Select(location => (Location: location, Route: RouteFinder.Find(state.Player, state.Current, location)))
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

}
