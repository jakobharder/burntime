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
        if (LocalOpportunities.ConsumeAvailableSupplies(state))
            AiTelemetry.Report(state.Player,
                "consumed carried or surplus camp supplies before emergency recovery");

        AiDecision decision = Choose(state);
        AiDecisionExecutor.Execute(state, decision);
    }

    static AiDecision Choose(ClassicAiState state)
    {
        Player player = state.Player;
        AiPolicy policy = AiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        AiContext observation = AiContext.Create(state, policy);
        List<AiDecision> candidates = new();

        if (AttackTask.TryAddImmediateResponse(state, observation, policy, candidates))
            return SelectAndReport(player, candidates);

        if (observation.CriticalSupplies)
        {
            if (observation.SafeLocation)
            {
                candidates.Add(new AiDecision(
                    AiAction.Recover,
                    1100,
                    observation.Current,
                    Reason: "critical food, water, or health"));
            }
            else
            {
                AddTravelCandidate(state, candidates,
                    FindNearestLogistics(state, requireReachable: true) ?? FindNearestLogistics(state),
                    1050, "seek emergency supplies");
            }

            // Survival is a hard constraint. Do not allow a lucrative trade,
            // settlement, or attack score to override recovery or retreat.
            if (candidates.Count > 0)
                return SelectAndReport(player, candidates);
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
        return SelectAndReport(player, candidates);
    }

    static AiDecision SelectAndReport(Player player, List<AiDecision> candidates)
    {
        AiDecision selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(_ => Burntime.Platform.Math.Random.Next())
            .First();
        string target = selected.Target == null ? "none" : selected.Target.Title;
        AiTelemetry.Report(player,
            $"decision {selected.Action}, target {target}, score {selected.Score:0}: {selected.Reason}");
        return selected;
    }

    internal static void AddTravelCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        Location? target,
        float score,
        string reason)
    {
        if (target == null)
            return;
        RouteFinder.Route? route = RouteFinder.Find(state.Player, state.Current, target);
        if (route?.NextStep == null)
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
