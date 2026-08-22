using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class StrategicAi
{
    public static void RunTurn(ClassicAiState state)
    {
        DefenseIntelligence.ObserveWorld(state);
        LocalOpportunities.Apply(state);

        AiDecision decision = Choose(state);
        AiDecisionExecutor.Execute(state, decision);
    }

    static AiDecision Choose(ClassicAiState state)
    {
        Player player = state.Player;
        AiPolicy policy = AiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        AiContext observation = AiContext.Create(state, policy);
        List<AiDecision> candidates = new();

        ExpansionTask.CancelSettlementAtHostileWaypoint(state, observation);

        if (AttackTask.TryAddImmediateResponse(state, observation, policy, candidates))
            return SelectAndReport(state, candidates);

        if (observation.CriticalSupplies && !CanFinishCommittedSettlementJourney(state))
        {
            Location? reachableRecovery = RecoveryServices.FindDestination(
                state, requireReachable: true);
            Location? recovery = reachableRecovery ??
                RecoveryServices.FindDestination(state, requireReachable: false);
            AddTravelCandidate(state, candidates, recovery, 1100,
                "seek real food, water, or medical services",
                allowSurvivableRecoveryRisk: true);
            if (reachableRecovery == null && player.Group.Count > 1 &&
                !RecoveryServices.CanWaitForLocalRecovery(state))
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
        Location? target = territory.Target;
        bool preparingAttack = territory.PreparingAttack;
        ExpansionTask.AddImmediateClaimCandidate(observation, territory, candidates);

        RecruitmentNeeds recruitment = RecruitmentTask.AddCandidates(
            state, observation, policy, target, preparingAttack, candidates);
        ReinforcementTask.AddCandidates(
            state, observation, policy, recruitment, candidates);

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
            state, observation, policy, target, preparingAttack, candidates);

        ExpansionTask.AddCandidates(state, observation, policy, territory, candidates);

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
        if (!normallySupplied && (!allowSurvivableRecoveryRisk ||
            !SupplyCalculator.CanSurviveRecoveryRoute(state.Player, route)))
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
