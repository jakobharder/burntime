using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AiTurnController
{
    public static void RunTurn(ClassicAiState state)
    {
        Stopwatch totalTimer = Stopwatch.StartNew();
        AiTurnContext context = AiTurnContext.Begin(state);
        DefenseIntelligence.UpdateKnowledge(state);
        if (LastChanceCombat.TryExecute(state))
            return;
        PrepareTurn(state, context);
        context.Camps.Refresh();
        long localMilliseconds = totalTimer.ElapsedMilliseconds;
        List<string> actionTimings = new();

        const int maximumStrategicActions = 10;
        for (int action = 0; action < maximumStrategicActions; action++)
        {
            Stopwatch actionTimer = Stopwatch.StartNew();
            AiDecision decision = Choose(state, context);
            long chooseMilliseconds = actionTimer.ElapsedMilliseconds;
            AiActionResult result = decision.Execute(state);
            actionTimings.Add($"{decision.Action} choose {chooseMilliseconds} ms, " +
                $"execute {actionTimer.ElapsedMilliseconds - chooseMilliseconds} ms");

            // Waiting and travelling deliberately consume the rest of the world
            // turn. Other local strategic actions may expose the next useful step
            // immediately, so continue choosing until one of these boundaries.
            if (result != AiActionResult.ContinuePlanning ||
                state.Player.IsTraveling || state.Player.IsDead)
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

    static void PrepareTurn(ClassicAiState state, AiTurnContext turn)
    {
        CampManagement.MaintainCampProductionPolicy(state);
        ExecuteImmediateRecovery(state);
        EconomicSupport.GrantSlumpSupportIfNeeded(state);
        Construction.RefillConstructionReserve(state);
        Construction.ConstructPortableEconomicUpgrade(state);
        GroupManagement.MaintainGroupEquipment(state, allowCampTransfers: true);
        CampManagement.MaintainCampNetwork(state);
        ExpansionPlanning.TryClaimCurrentAsLocalOpportunity(state);

        if (state.Current.Player == state.Player)
        {
            CampManagement.MaintainCurrentCamp(state);
            bool tradeBlockedByPlan = !state.Current.IsCity &&
                (state.HasSettlementPlan || state.HasAttackPlan);
            if (!tradeBlockedByPlan && Trading.ShouldVisitTrader(state, turn.BestTradeCity))
                CargoManagement.FillCityCaravan(state, state.Current);
        }

        bool completedTrade = TradeLocally(state);
        RecoveryServices.UseDoctor(state);
        if (completedTrade)
        {
            GroupManagement.MaintainGroupEquipment(state);
            GroupInventory.MaintainLeaderRoleSlots(state);
        }
    }

    static void ExecuteImmediateRecovery(ClassicAiState state)
    {
        RecoveryServices.ProvideCityWaterMinimum(state);
        UseLocalWaterSource(state);
        GroupInventory.RemoveAdviceItems(state);
        if (state.Current.Player == state.Player)
            GroupManagement.ProvisionGroupFromCampSurplus(state, state.Current);
        if (GroupInventory.ConsumeAvailableSupplies(state))
            AiTelemetry.Report(state.Player,
                "consumed carried or stored supplies before seeking paid recovery");
        RecoveryServices.ProvideCityFoodMinimum(state);
        GroupInventory.MaintainLeaderRoleSlots(state);
        RecoveryServices.UseDoctor(state);
    }

    static bool TradeLocally(ClassicAiState state)
    {
        bool completedTrade = false;
        if (!state.ShouldReserveSettlerPayment || RecoveryServices.NeedsDoctorPayment(state))
        {
            foreach (Trader trader in Trading.EncounteredTraders(state))
            {
                completedTrade |= Trading.TradeWithTrader(state, trader);
                Construction.RefillConstructionReserve(state);
                Construction.ConstructPortableEconomicUpgrade(state);
            }
        }
        return completedTrade;
    }

    static void UseLocalWaterSource(ClassicAiState state)
    {
        Player player = state.Player;
        if (player.Location.Source == null)
            return;
        player.Location.Source.Reserve = player.Group.Drink(
            player.Character, player.Location.Source.Reserve);
        foreach (Item item in player.Group.GetEmptyWaterItems())
            player.Location.Source.RefillItem(item);
    }

    static AiDecision Choose(ClassicAiState state, AiTurnContext turn)
    {
        Stopwatch timer = Stopwatch.StartNew();
        AiPolicy policy = turn.Policy;
        DecisionContext observation = turn.RefreshDecisionContext();
        long contextMilliseconds = timer.ElapsedMilliseconds;

        ExpansionPlanning.CancelSettlementAtHostileWaypoint(state, observation);
        AiDecision? emergency = PlanEmergency(state, observation, policy);
        if (emergency != null)
            return emergency;

        AiDecision? journey = ContinueCommittedJourney(state);
        if (journey != null)
            return journey;

        return PlanNormalActions(
            state, observation, policy, turn, timer, contextMilliseconds);
    }

    static AiDecision? ContinueCommittedJourney(ClassicAiState state)
    {
        Location? destination = state.CommittedJourneyDestination;
        if (destination == null)
            return null;
        if (state.Current == destination)
        {
            AiTelemetry.Report(state.Player,
                $"completed committed journey to {destination.Title}: " +
                state.CommittedJourneyReason);
            state.ClearCommittedJourney();
            return null;
        }
        if (destination.IsCity || destination.Player != state.Player ||
            !CampEconomy.CanProvisionFood(destination) ||
            !CampEconomy.CanProvisionGroupWater(destination, state.Player.Group.Count))
        {
            AiTelemetry.Report(state.Player,
                $"cancelled committed journey to {destination.Title}: destination is no longer a sustainable owned camp");
            state.ClearCommittedJourney();
            return null;
        }

        RouteFinder.Route? route = RouteFinder.Find(
            state.Player, state.Current, destination);
        if (route?.NextStep == null ||
            !state.Player.CanTravel(state.Current, route.NextStep) ||
            !TravelSupplies.HasRouteSupplies(
                state.Player, route, hostileTarget: false))
        {
            AiTelemetry.Report(state.Player,
                $"cancelled committed journey to {destination.Title}: remaining route is unavailable or no longer supplied");
            state.ClearCommittedJourney();
            return null;
        }

        return new AiDecision(
            AiAction.Travel,
            1500 - route.Days,
            destination,
            route.NextStep,
            $"continue committed journey to {destination.Title}: " +
            state.CommittedJourneyReason);
    }

    static AiDecision? PlanEmergency(
        ClassicAiState state,
        DecisionContext observation,
        AiPolicy policy)
    {
        Player player = state.Player;
        List<AiDecision> candidates = new();

        if (AttackPlanning.TryAddImmediateResponse(state, observation, policy, candidates))
            return SelectAndReport(state, candidates);

        if (observation.CriticalSupplies && !CanFinishCommittedSettlementJourney(state))
        {
            if (Recruitment.TryAddCriticalRouteRecruitContinuation(
                state, observation, policy, candidates))
                return SelectAndReport(state, candidates);

            Location? reachableRecovery = RecoveryServices.FindDestination(
                state, requireReachable: true);
            bool lastChance = reachableRecovery == null &&
                RecoveryServices.WaitingForSuppliesWillBeFatal(state);
            Location? recovery = reachableRecovery ?? (lastChance
                ? RecoveryServices.FindLastChanceDestination(state)
                : RecoveryServices.FindDestination(state, requireReachable: false));
            AddTravelCandidate(state, candidates, recovery, 1100,
                lastChance
                    ? "take the least-bad route toward emergency supplies instead of waiting to die"
                    : "seek real food, water, or medical services",
                allowSurvivableRecoveryRisk: true,
                allowFatalRecoveryRisk: lastChance);
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

        return null;
    }

    static AiDecision PlanNormalActions(
        ClassicAiState state,
        DecisionContext observation,
        AiPolicy policy,
        AiTurnContext turn,
        Stopwatch timer,
        long contextMilliseconds)
    {
        Player player = state.Player;
        List<AiDecision> candidates = new();

        TerritorialPlan territory = ExpansionPlanning.CreatePlan(state, observation, policy);
        long territoryMilliseconds = timer.ElapsedMilliseconds;
        Location? target = territory.Target;
        bool preparingAttack = territory.PreparingAttack;
        ExpansionPlanning.AddImmediateClaimCandidate(observation, territory, candidates);
        bool needsAttackWater = preparingAttack && Trading.NeedsAttackWaterPreparation(state);
        bool tradeBlockedByPlan = !state.Current.IsCity &&
            (state.HasSettlementPlan || state.HasAttackPlan);
        Location? bestTradeCity = tradeBlockedByPlan && !needsAttackWater
            ? null
            : turn.BestTradeCity;
        bool shouldVisitTrader = needsAttackWater ||
            !tradeBlockedByPlan && Trading.ShouldVisitTrader(state, bestTradeCity);
        long tradePlanMilliseconds = timer.ElapsedMilliseconds;

        RecruitmentNeeds recruitment = Recruitment.AddCandidates(
            state, observation, policy, target, preparingAttack,
            shouldVisitTrader, candidates);
        long recruitmentMilliseconds = timer.ElapsedMilliseconds;
        ReinforcementPlanning.AddCandidates(
            state, observation, policy, recruitment, candidates);
        long reinforcementMilliseconds = timer.ElapsedMilliseconds;

        if (state.NeedsCampImprovement() &&
            !ExpansionPlanning.ShouldReserveProductionTool(state))
        {
            float economicGain = System.Math.Max(0,
                EconomicReturn.MarginalCampImprovement(state, observation.Current));
            candidates.Add(new AiDecision(
                AiAction.ImproveCamp,
                650 + economicGain * 180,
                observation.Current,
                Reason: $"improve sustainable camp income by about {economicGain:0.0} value/day"));
        }

        Trading.AddCandidates(
            state, observation, policy, target, preparingAttack,
            shouldVisitTrader, bestTradeCity, candidates);
        long tradeMilliseconds = timer.ElapsedMilliseconds;

        ExpansionPlanning.AddCandidates(state, observation, policy, territory, candidates);
        long expansionMilliseconds = timer.ElapsedMilliseconds;

        if (!preparingAttack && player.Group.Count > 1 &&
            player.Group.Count < observation.DesiredGroupSize && !state.HasHireableNpc())
        {
            Location? preparationCamp = Trading.FindBestCampForCityPreparation(state);
            Location? recruitmentCity = Recruitment.FindNearestRecruitmentCity(state, policy);
            AddTravelCandidate(state, candidates, preparationCamp ?? recruitmentCity, 560,
                preparationCamp == null ? "look for recruits" : "collect trade cargo before looking for recruits");
        }

        string idleReason = preparingAttack && target != null
            ? AttackPlanning.AdvanceBlockingReason(state, target, policy)
            : ExpansionPlanning.NeedsExpansionTool(state)
                ? "expansion blocked: no portable production tool and no affordable or collectible route"
                : !AttackPlanning.HasGroupWeapon(player)
                    ? "expansion blocked: group has no weapon and no equipment route is available"
                    : "no eligible neutral or hostile frontier target with current supplies";
        candidates.Add(new AiDecision(
            AiAction.Wait,
            0,
            preparingAttack ? target : null,
            Reason: idleReason));
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
        return route != null && TravelSupplies.HasRouteSupplies(
            state.Player, route, hostileTarget: false);
    }

    static AiDecision SelectAndReport(ClassicAiState state, List<AiDecision> candidates)
    {
        Player player = state.Player;
        AiDecision selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(_ => Burntime.Platform.Math.Random.Next())
            .First();
        Location? recruitmentFailure = state.ConsumeRecruitmentFailureTheaterAnchor();
        if (recruitmentFailure != null)
        {
            AiPolicy policy = AiPolicy.ForDifficulty(state.Difficulty);
            AiDecision? relocation = FindStrategicTheaterRelocation(
                state, policy, recruitmentFailure, allowSmallTerritory: true);
            if (relocation != null)
            {
                selected = relocation with
                {
                    Reason = $"recruitment unavailable for {recruitmentFailure.Title} at " +
                        $"{state.Current.Title}; {relocation.Reason}"
                };
                AiTelemetry.Report(player,
                    $"recruitment failure for {recruitmentFailure.Title} triggered an " +
                    $"immediate strategic-theater change toward {relocation.Target?.Title}");
            }
            else
            {
                AiTelemetry.Report(player,
                    $"recruitment failure for {recruitmentFailure.Title} found no alternative " +
                    "owned frontier with an eligible expansion opportunity; continuing ordinary planning");
            }
        }
        AiDecision blockedDecision = selected;
        int relocationTier = state.StationaryRelocationTier(
            selected, out int stationaryTurns, out int strategicTurns);
        if (relocationTier > 0)
        {
            AiPolicy policy = AiPolicy.ForDifficulty(state.Difficulty);
            AiDecision? relocation = relocationTier == 1
                ? FindExploratoryRelocation(state, allowUnowned: false)
                : FindPurposefulRelocation(
                    state, policy, blockedDecision, strategicTurns);
            string tier = relocationTier == 1 ? "adjacent-owned exploration" : "reachable logistics relocation";
            if (relocation == null)
            {
                Location? emergencyTarget = relocationTier == 2 && strategicTurns >= 12
                    ? FindEmergencyEscapeTarget(state)
                    : null;
                if (emergencyTarget != null)
                {
                    RouteFinder.Route? route = RouteFinder.Find(
                        player, state.Current, emergencyTarget);
                    selected = new AiDecision(
                        AiAction.EmergencyEscape,
                        2000,
                        emergencyTarget,
                        route?.NextStep,
                        $"emergency watchdog bailout after {stationaryTurns} stationary waits " +
                        $"and {strategicTurns} strategic waits; normal relocation failed: " +
                        blockedDecision.Reason);
                    AiTelemetry.Report(player,
                        $"non-progress watchdog escalated to emergency escape from " +
                        $"{state.Current.Title} toward {emergencyTarget.Title} after " +
                        $"{strategicTurns} strategic waits; ordinary relocation and one-way " +
                        $"escape checks found no sufficiently supplied route");
                }
                else
                {
                    AiTelemetry.Report(player,
                        $"non-progress watchdog could not find {tier} after {stationaryTurns} " +
                        $"stationary waits and {strategicTurns} strategic waits at " +
                        $"{state.Current.Title}; underlying decision: " +
                        $"{blockedDecision.Reason}");
                }
            }
            else
            {
                AiTelemetry.Report(player,
                    $"non-progress watchdog selected {tier} after {stationaryTurns} " +
                    $"stationary waits and {strategicTurns} strategic waits at " +
                    $"{state.Current.Title}; underlying decision: " +
                    $"{blockedDecision.Reason}; moving toward " +
                    $"{relocation.Target?.Title ?? relocation.NextStep?.Title}");
                selected = relocation with
                {
                    Reason = $"{relocation.Reason}; non-progress fallback for: {blockedDecision.Reason}"
                };
                state.ResetStationaryWaits();
            }
        }
        string target = selected.Target == null ? "none" : selected.Target.Title;
        Location? stalledTarget = state.StrategicTarget == selected.Target
            ? selected.Target
            : null;
        if (state.CancelStalledStrategicWait(selected, out int waitTurns))
        {
            int retryDay = 0;
            bool deferred = stalledTarget != null && TerritorialTargetDeferrals.TryDefer(
                state, stalledTarget, selected.Reason, out retryDay);
            AiTelemetry.Report(player,
                $"abandoned strategic target {target}: waited {waitTurns} consecutive turns " +
                $"without progress ({selected.Reason})" +
                (deferred ? $"; deferred until day {retryDay} or relevant readiness improves" : ""));
        }
        AiTelemetry.Report(player,
            $"decision {selected.Action}, target {target}, score {selected.Score:0}: {selected.Reason}");
        return selected;
    }

    static AiDecision? FindExploratoryRelocation(
        ClassicAiState state,
        bool allowUnowned)
    {
        Location current = state.Current;
        var reachable = Enumerable.Range(0, current.Neighbors.Count)
            .Where(index => current.WayLengths[index] > 0)
            .Select(index => new
            {
                Location = current.Neighbors[index],
                Days = current.WayLengths[index]
            })
            .Where(candidate =>
                (allowUnowned || candidate.Location.Player == state.Player) &&
                state.Player.CanTravel(current, candidate.Location))
            .Where(candidate => TravelSupplies.HasTerritorialRouteSupplies(
                state.Player,
                current,
                new RouteFinder.Route(candidate.Location, candidate.Days),
                hostileTarget: false))
            .ToArray();
        if (reachable.Length == 0)
            return null;

        // Continue through the owned network where possible instead of bouncing
        // straight back. A low owned-neighbor count gently favors its corners.
        var alternatives = reachable
            .Where(candidate => candidate.Location != state.PreviousExploratoryCamp)
            .ToArray();
        var destination = (alternatives.Length > 0 ? alternatives : reachable)
            .OrderByDescending(candidate => candidate.Location.Player == state.Player)
            .ThenBy(candidate => candidate.Location.Neighbors.Count(neighbor =>
                neighbor.Player == state.Player))
            .ThenByDescending(candidate => candidate.Days)
            .ThenBy(candidate => candidate.Location.Title)
            .First();

        state.MarkExploratoryRelocation(current);
        return new AiDecision(
            AiAction.Travel,
            0,
            NextStep: destination.Location,
            Reason: $"explore the owned territory at {destination.Location.Title} after three idle turns");
    }

    static AiDecision? FindPurposefulRelocation(
        ClassicAiState state,
        AiPolicy policy,
        AiDecision blockedDecision,
        int strategicTurns)
    {
        Location current = state.Current;
        Location? blockedAttack = state.StrategicTarget is { Player: not null } target &&
            target.Player != state.Player &&
            blockedDecision.Reason.StartsWith("attack plan for ", StringComparison.Ordinal)
            ? target
            : null;
        AiDecision? theaterChange = blockedAttack != null
            ? FindStrategicTheaterRelocation(state, policy, blockedAttack)
            : null;
        if (theaterChange == null && strategicTurns >= 12 &&
            blockedDecision.Reason.StartsWith(
                "no eligible neutral or hostile frontier target",
                StringComparison.Ordinal))
        {
            theaterChange = FindStrategicTheaterRelocation(
                state, policy, blockedTarget: null);
        }
        if (theaterChange != null)
            return theaterChange;

        Location? escape = RecoveryServices.FindDestination(
            state,
            requireReachable: true,
            RecoveryServices.TripMode.Escape);
        if (escape != null)
        {
            RouteFinder.Route? escapeRoute = RouteFinder.Find(
                state.Player, current, escape);
            if (escapeRoute?.NextStep != null &&
                state.Player.CanTravel(current, escapeRoute.NextStep))
            {
                return new AiDecision(
                    AiAction.Travel,
                    0,
                    escape,
                    escapeRoute.NextStep,
                    $"take a one-way escape to sustainable owned camp {escape.Title}; " +
                    "return-route and reserve-building requirements waived",
                    CommitJourney: true);
            }
        }

        bool needsRecruit = blockedDecision.Reason.Contains("attackers", StringComparison.Ordinal);
        var reachable = state.RootGame.World.Locations
            .Where(location => location != current &&
                (location.IsCity || location.Player == state.Player))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, current, location)
            })
            .Where(candidate => candidate.Route?.NextStep != null &&
                state.Player.CanTravel(current, candidate.Route.NextStep) &&
                TravelSupplies.HasTerritorialRouteSupplies(
                    state.Player, current, candidate.Route, hostileTarget: false))
            .ToArray();
        if (reachable.Length == 0)
            return null;

        var alternatives = reachable
            .Where(candidate => candidate.Location != state.PreviousExploratoryCamp)
            .ToArray();
        var destination = (alternatives.Length > 0 ? alternatives : reachable)
            .OrderByDescending(candidate => needsRecruit && candidate.Location.IsCity)
            .ThenByDescending(candidate => candidate.Location.IsCity)
            .ThenBy(candidate => candidate.Route!.Days)
            .ThenBy(candidate => candidate.Location.Title)
            .First();

        state.MarkExploratoryRelocation(current);
        return new AiDecision(
            AiAction.Travel,
            0,
            destination.Location,
            destination.Route!.NextStep,
            $"seek useful logistics at {destination.Location.Title}");
    }

    static AiDecision? FindStrategicTheaterRelocation(
        ClassicAiState state,
        AiPolicy policy,
        Location? blockedTarget,
        bool allowSmallTerritory = false)
    {
        int campCount = state.OwnedCampCount;
        if (campCount < (blockedTarget == null || allowSmallTerritory ? 2 : 6))
            return null;

        Location theaterAnchor = blockedTarget ?? state.Current;
        int minimumDepth = System.Math.Clamp(2 + campCount / 5, 2, 6);
        var candidates = state.RootGame.World.Locations
            .Where(location => location != state.Current && !location.IsCity &&
                location.Player == state.Player)
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                TheaterDepth = LinkDistance(theaterAnchor, location),
                Opportunities = FrontierOpportunityScore(state, policy, location)
            })
            .Where(candidate => candidate.Route?.NextStep != null &&
                candidate.TheaterDepth != int.MaxValue &&
                candidate.Opportunities > 0 &&
                state.Player.CanTravel(state.Current, candidate.Route.NextStep))
            .ToArray();
        if (candidates.Length == 0)
            return null;

        // A theater change is deliberately exploratory. It should not inherit
        // the food, water, or combat-readiness constraints that blocked the old
        // campaign: the boss is crossing owned territory, and normal emergency
        // handling can dismiss followers if conditions deteriorate en route.
        // Prefer a materially different border, but relax the depth until there
        // are several genuinely different frontiers to randomize across.
        int selectedDepth = minimumDepth;
        var frontierPool = candidates
            .Where(candidate => candidate.TheaterDepth >= selectedDepth)
            .ToArray();
        while (selectedDepth > 1 && frontierPool.Length < 3)
        {
            selectedDepth--;
            frontierPool = candidates
                .Where(candidate => candidate.TheaterDepth >= selectedDepth)
                .ToArray();
        }
        if (frontierPool.Length == 0)
            frontierPool = candidates;

        var destination = frontierPool[
            Burntime.Platform.Math.Random.Next(frontierPool.Length)];

        if (blockedTarget != null)
            state.DeferAttackPlan(blockedTarget, policy);
        state.MarkExploratoryRelocation(state.Current);
        string blockedFrontier = blockedTarget == null
            ? $"unproductive {state.Current.Title} region"
            : $"blocked {blockedTarget.Title} frontier";
        return new AiDecision(
            AiAction.Travel,
            0,
            destination.Location,
            destination.Route!.NextStep,
            $"change strategic theater from {blockedFrontier} to " +
                $"{destination.Location.Title} ({destination.TheaterDepth} links away, " +
                $"{destination.Opportunities} frontier opportunity score; uniformly selected " +
                $"from {frontierPool.Length} frontiers at minimum depth {selectedDepth}; " +
                "route-supply constraints waived)",
            CommitJourney: true);
    }

    static int FrontierOpportunityScore(
        ClassicAiState state,
        AiPolicy policy,
        Location camp)
    {
        int score = 0;
        for (int index = 0; index < camp.Neighbors.Count; index++)
        {
            if (camp.WayLengths[index] <= 0)
                continue;
            Location neighbor = camp.Neighbors[index];
            if (neighbor.IsCity || neighbor.Player == state.Player)
                continue;
            if (neighbor.Player == null)
            {
                if (state.CanClaim(neighbor) &&
                    CampEconomy.HasFoodProductionPotential(neighbor))
                    score += CampEconomy.IsAcceptableFirstCamp(neighbor) ? 5 : 2;
            }
            else if (AttackPlanning.IsTargetAllowed(state, neighbor, policy))
            {
                int defenders = DefenseIntelligence.Estimate(state, neighbor)
                    .ExpectedDefenders;
                score += System.Math.Max(2, 8 - defenders * 2);
            }
        }
        return score;
    }

    static int LinkDistance(Location start, Location destination)
    {
        if (start == destination)
            return 0;
        Queue<(Location Location, int Distance)> queue = new();
        HashSet<Location> visited = new() { start };
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            (Location location, int distance) = queue.Dequeue();
            for (int index = 0; index < location.Neighbors.Count; index++)
            {
                if (location.WayLengths[index] <= 0)
                    continue;
                Location neighbor = location.Neighbors[index];
                if (!visited.Add(neighbor))
                    continue;
                if (neighbor == destination)
                    return distance + 1;
                queue.Enqueue((neighbor, distance + 1));
            }
        }
        return int.MaxValue;
    }

    static Location? FindEmergencyEscapeTarget(ClassicAiState state) =>
        state.RootGame.World.Locations
            .Where(location => location != state.Current && !location.IsCity &&
                location.Player == state.Player &&
                CampEconomy.CanProvisionFood(location) &&
                CampEconomy.WaterSurplusPerDay(location) > 0)
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

    internal static void AddTravelCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        Location? target,
        float score,
        string reason,
        bool allowSurvivableRecoveryRisk = false,
        RouteFinder.Route? knownRoute = null,
        bool allowFatalRecoveryRisk = false)
    {
        if (target == null)
            return;
        RouteFinder.Route? route = knownRoute ??
            RouteFinder.Find(state.Player, state.Current, target);
        if (route?.NextStep == null)
            return;
        bool normallySupplied = TravelSupplies.HasTerritorialRouteSupplies(
            state.Player, state.Current, route, hostileTarget: false);
        bool dehydrationEscape = allowSurvivableRecoveryRisk &&
            TravelSupplies.IsDehydrationTravelNoWorseThanWaiting(
                state.Player, state.Current, route);
        if (!normallySupplied && !allowFatalRecoveryRisk &&
            (!allowSurvivableRecoveryRisk ||
            !TravelSupplies.CanSurviveRecoveryRoute(state.Player, route)) &&
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
            // A city may be traversed as a route waypoint, but it cannot sustain
            // an emergency retreat. The retreat destination must be a real camp
            // owned by the faction.
            .Where(location => !location.IsCity && location.Player == state.Player)
            .Select(location => (Location: location, Route: RouteFinder.Find(state.Player, state.Current, location)))
            .Where(candidate => candidate.Route != null && candidate.Location != state.Current &&
                (!requireReachable || TravelSupplies.HasRouteSupplies(
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
