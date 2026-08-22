using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct TerritorialPlan(
    bool CanClaimCurrent,
    float CurrentClaimScore,
    Location? Target,
    float TargetScore,
    bool PreparingAttack);

internal static partial class ExpansionTask
{
    public static void CancelSettlementAtHostileWaypoint(
        ClassicAiState state,
        AiContext context)
    {
        Location? target = state.StrategicTarget;
        if (!state.HasSettlementPlan || target == null ||
            !AttackTask.IsHostile(context.Current, context.Player))
            return;

        AiTelemetry.Report(context.Player,
            target == context.Current
                ? $"released settlement target {target.Title}: another player claimed it before arrival"
                : $"abandoned settlement target {target.Title}: waypoint " +
                    $"{context.Current.Title} became hostile");
        state.StrategicTarget = null;
    }

    public static TerritorialPlan CreatePlan(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy)
    {
        bool suitableCurrentClaim = IsSuitableCurrentClaim(state, context);
        bool currentCanBecomeCamp = state.CanClaim(context.Current) && suitableCurrentClaim;
        bool canClaimCurrent = currentCanBecomeCamp && state.CanStationCamp();
        Location? target = ValidatePersistentTarget(state, context, policy);
        if (target == null && !(canClaimCurrent && state.StrategicTarget == null))
        {
            target = SelectTerritorialTarget(state, context, policy);
            if (AttackTask.IsHostile(target, context.Player))
            {
                int eliminationBonus = TerritorialEliminationBonus(state, target!);
                if (eliminationBonus > 0)
                {
                    int remainingCamps = CountOwnedCamps(state, target!.Player!);
                    AiTelemetry.Report(context.Player,
                        $"prioritized territorial elimination of {target.Player!.Name}: " +
                        $"{remainingCamps} nearby camp{(remainingCamps == 1 ? "" : "s")} remain");
                }
                state.StartAttackPlan(target!, policy);
            }
            else
                state.SetSettlementTarget(target);
        }

        return new TerritorialPlan(
            canClaimCurrent,
            CurrentClaimScore(state, context.Current),
            target,
            TargetScore(state, policy, target),
            AttackTask.IsHostile(target, context.Player));
    }

    public static bool TryClaimCurrentAsLocalOpportunity(ClassicAiState state)
    {
        AiContext context = AiContext.Create(
            state, AiPolicy.ForDifficulty(state.RootGame.World.Difficulty));
        Location current = context.Current;
        if (!state.CanClaim(current) || !state.CanStationCamp() ||
            !IsSuitableCurrentClaim(state, context))
            return false;

        bool selectedDestination = state.StrategicTarget == current;
        bool firstCamp = !HasOwnedCamp(state);
        bool securesRoute = CampEconomy.ConnectsOwnedCamps(current, state.Player);
        bool onwardWaypoint = state.HasSettlementPlan &&
            state.StrategicTarget != null && state.StrategicTarget != current;
        if (!selectedDestination && !firstCamp && !securesRoute && !onwardWaypoint)
            return false;

        Character? settler = state.SelectCampNpc();
        if (settler == null)
            return false;

        Location? onwardSettlement = state.HasSettlementPlan &&
            state.StrategicTarget != current
            ? state.StrategicTarget
            : null;
        state.CreateCamp(settler);
        if (onwardSettlement != null)
            state.SetSettlementTarget(onwardSettlement);
        LocalOpportunities.ProvisionGroupFromCampSurplus(state, current);
        AiTelemetry.Report(state.Player,
            onwardSettlement == null
                ? $"claimed {current.Title} as a local opportunity using {settler.Name}"
                : $"claimed waypoint {current.Title} using {settler.Name} while continuing toward " +
                    onwardSettlement.Title);
        return true;
    }

    public static void AddImmediateClaimCandidate(
        AiContext context,
        TerritorialPlan plan,
        List<AiDecision> candidates)
    {
        if (!plan.PreparingAttack && plan.CanClaimCurrent)
        {
            bool routeCamp = CampEconomy.ConnectsOwnedCamps(context.Current, context.Player);
            candidates.Add(new AiDecision(
                AiAction.ClaimNeutral,
                plan.CurrentClaimScore,
                context.Current,
                Reason: routeCamp
                    ? "claim neutral camp connecting owned territory"
                    : $"claim {CampEconomy.StrategicRole(context.Current)} at current location"));
        }
    }

    public static void AddCandidates(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        TerritorialPlan plan,
        List<AiDecision> candidates)
    {
        if (state.WaitTurns > 0 && !context.CriticalSupplies && !plan.PreparingAttack)
        {
            state.WaitTurns--;
            candidates.Add(new AiDecision(
                AiAction.Wait,
                900,
                Reason: "expansion cooldown"));
        }

        if (plan.Target == null)
            return;

        if (!plan.PreparingAttack)
        {
            if (plan.Target == context.Current)
                return;

            // A neutral destination is not a useful journey by itself. Keep the
            // target so recruitment and equipment planning can prepare it, but
            // do not send a solo leader there when nobody can establish the camp
            // on arrival. This also prevents unsupported loot/recovery dead ends.
            if (context.Player.Group.Count <= 1)
                return;
            RouteFinder.Route? route = RouteFinder.Find(
                context.Player, context.Current, plan.Target);
            if (route == null)
                return;
            if (!HasSettlementRouteSupplies(state, context, plan.Target, route))
            {
                candidates.Add(new AiDecision(
                    AiAction.Wait,
                    10,
                    plan.Target,
                    Reason: $"settlement route to {plan.Target.Title} is waiting for travel reserves"));
                return;
            }

            // HasSettlementRouteSupplies already approves either the complete
            // expedition or a supplied first leg to an owned camp/city. Preserve
            // that staged approval instead of recalculating the full journey in
            // AddTravelCandidate and silently dropping the movement.
            candidates.Add(new AiDecision(
                AiAction.Travel,
                plan.TargetScore - route.Days,
                plan.Target,
                route.NextStep,
                $"advance toward {CampEconomy.StrategicRole(plan.Target)}"));
        }
        else if (AttackTask.CanAdvancePlan(state, plan.Target, policy))
        {
            StrategicAi.AddTravelCandidate(
                state, candidates, plan.Target, 1010,
                $"attack plan ready: advance toward {plan.Target.Title}");
        }
    }

    public static bool NeedsExpansionTool(ClassicAiState state)
    {
        Location[] neutralCamps = state.RootGame.World.Locations
            .Where(location => !location.IsCity && location.Player == null)
            .ToArray();
        if (neutralCamps.Length == 0)
            return false;

        bool carried = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production != null && neutralCamps.Any(location =>
                location.ValidProductions.Contains(item.Type.Production)));
        bool pooled = neutralCamps.Any(location => state.Pool.HasTrap(state.AvailableProducts(location)));
        return !carried && !pooled;
    }

    public static bool CanBootstrapCamp(ClassicAiState state, Location location)
    {
        // Stored supplies and neighboring camps may help establish a productive
        // site, but must never turn a permanently barren location into a camp.
        if (!CampEconomy.HasFoodProductionPotential(location))
            return false;
        if (ReinforcementTask.IsThreatened(state, location))
            return false;
        bool hasLocalSurplus = location.ValidProductions.Any(production =>
            production.GetRate(toolCount: 0, npcCount: 1).FoodPerDay > 1);
        return hasLocalSurplus || HasNeighboringFoodSupport(state, location);
    }

    static bool HasNeighboringFoodSupport(ClassicAiState state, Location location)
    {
        // Establishing the camp stations one group member. A directly connected
        // owned camp may support the remaining travellers while the new holding
        // acquires its own compatible production tool.
        int travellers = System.Math.Max(1, state.Player.Group.Count - 1);
        for (int index = 0; index < location.Neighbors.Count; index++)
        {
            if (location.WayLengths[index] <= 0)
                continue;
            Location supplier = location.Neighbors[index];
            if (supplier.Player != state.Player)
                continue;

            RouteFinder.Route? route = RouteFinder.Find(state.Player, location, supplier);
            if (route == null || !SupplyCalculator.CanSurviveRecoveryRoute(state.Player, route))
                continue;

            if (CampEconomy.FoodSurplusPerDay(supplier) >= travellers)
                return true;
        }
        return false;
    }

    public static bool ShouldPrioritizeEconomicGrowth(ClassicAiState state)
        => EconomicReturn.BestEmpireImprovement(state) > 0.01f;

    public static bool ShouldReserveProductionTool(ClassicAiState state)
    {
        if (!TradeTask.HasNeutralExpansionOpportunity(state))
            return false;
        if (EconomicSupport.AdvancedTrapCoverage(state) < 0.5f &&
            EconomicSupport.HasPooledAdvancedTrap(state))
            return false;
        int portableTools = state.Pool.ProductionToolCount + state.Player.Group
            .SelectMany(character => character.Items)
            .Count(item => item.Type.Production != null);
        return portableTools <= 1;
    }

    static Location? ValidatePersistentTarget(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy)
    {
        Location? target = state.StrategicTarget;
        if (target == null || target.IsCity)
            return null;
        if (target == context.Current)
        {
            if (target.Player == null && state.CanClaim(target))
            {
                if (!state.CanStationCamp())
                    return target;
            }
            else if (target.Player == null && state.HasSettlementPlan)
            {
                AiTelemetry.Report(context.Player,
                    $"abandoned settlement at {target.Title}: it can no longer be bootstrapped");
            }
            state.StrategicTarget = null;
            return null;
        }
        RouteFinder.Route? route = RouteFinder.Find(context.Player, context.Current, target);
        if (route == null)
        {
            AiTelemetry.Report(context.Player,
                $"abandoned target {target.Title}: no permitted route remains");
            state.StrategicTarget = null;
            return null;
        }
        if (target.Player == null)
        {
            if (context.NeutralExpansionAllowed && state.CanClaim(target) &&
                (HasSettlementRouteSupplies(state, context, target, route) ||
                    CanWaitForSettlementFood(state, context, target, route)))
                return target;

            if (state.HasSettlementPlan)
            {
                string reason = !state.CanClaim(target)
                    ? "it can no longer be bootstrapped"
                    : "its route is no longer supportable";
                AiTelemetry.Report(context.Player,
                    $"abandoned settlement target {target.Title}: {reason}");
            }
            state.StrategicTarget = null;
            return null;
        }
        if (target.Player == context.Player)
        {
            state.StrategicTarget = null;
            return null;
        }
        if (state.HasSettlementPlan)
        {
            AiTelemetry.Report(context.Player,
                $"released settlement target {target.Title}: another player claimed it first");
            state.StrategicTarget = null;
            return null;
        }
        return AttackTask.ValidatePersistentTarget(state, context, policy, target, route);
    }

    static Location? SelectTerritorialTarget(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy)
    {
        List<(Location Location, float Score)> targets = new();
        bool needsFirstCamp = !HasOwnedCamp(state);
        bool hasAcceptableFirstCamp = needsFirstCamp &&
            HasReachableAcceptableFirstCamp(state, context);
        foreach (Location location in state.RootGame.World.Locations)
        {
            if (location.IsCity || state.IsAttackTargetDeferred(location))
                continue;

            if (location == context.Current)
            {
                if (location.Player == null && context.NeutralExpansionAllowed &&
                    state.CanClaim(location) &&
                    (!hasAcceptableFirstCamp || CampEconomy.IsAcceptableFirstCamp(location)))
                {
                    // Standing at a viable site does not make it free when a
                    // settler still has to be recruited elsewhere. Compare it
                    // with every other candidate instead of promising to return.
                    targets.Add((location, NeutralTargetScore(state, policy, location) +
                        NeutralFrontierScore(state, location) + Jitter()));
                }
                continue;
            }

            RouteFinder.Route? route = RouteFinder.Find(context.Player, context.Current, location);
            if (route == null)
                continue;

            if (location.Player == null && context.NeutralExpansionAllowed &&
                state.CanClaim(location) &&
                (!hasAcceptableFirstCamp || CampEconomy.IsAcceptableFirstCamp(location)) &&
                (HasSettlementRouteSupplies(state, context, location, route) ||
                    CanWaitForSettlementFood(state, context, location, route)))
            {
                targets.Add((location, NeutralTargetScore(state, policy, location) +
                    NeutralFrontierScore(state, location) - route.Days * 24 + Jitter()));
            }
            else if (AttackTask.HasGroupWeapon(context.Player) &&
                AttackTask.IsHostile(location, context.Player) &&
                AttackTask.IsTerritorialFrontierTarget(state, location) &&
                AttackTask.IsTargetAllowed(state, location, policy))
            {
                DefenseEstimate defense = DefenseIntelligence.Estimate(state, location);
                float weakness = System.Math.Max(
                    -100, 100 - defense.EstimatedStrength);
                float strategicBonus = (ReinforcementTask.IsStrategicLocation(state, location) ||
                    state.WasRecentlyContested(location))
                    ? policy.StrategicHostileTargetBonus
                    : 0;
                float eliminationBonus = TerritorialEliminationBonus(state, location);
                float proactiveConflict = state.RootGame.World.Day >= policy.ProactiveConflictDay
                    ? policy.ProactiveConflictBonus
                    : 0;
                // A human player would remember leaving a contacted defender near
                // death and finish that fight after recovering instead of starting
                // a fresh campaign elsewhere.
                float finishingBonus = defense.BasedOnContact && defense.ExpectedDefenders == 1
                    ? 700
                    : 0;
                targets.Add((location, policy.HostileTargetScore + weakness + strategicBonus +
                    eliminationBonus + proactiveConflict + finishingBonus - route.Days * 6 + Jitter()));
            }
        }

        return targets.OrderByDescending(target => target.Score).FirstOrDefault().Location;
    }

    static int TerritorialEliminationBonus(ClassicAiState state, Location target)
    {
        Player? opponent = target.Player;
        if (opponent == null || opponent == state.Player || opponent.IsDead)
            return 0;

        int remainingCamps = CountOwnedCamps(state, opponent);
        if (remainingCamps is < 1 or > 2)
            return 0;

        int frontierDistance = DistanceFromOwnedTerritory(state, target, maximum: 2);
        return (remainingCamps, frontierDistance) switch
        {
            (1, 1) => 650,
            (1, 2) => 400,
            (2, 1) => 300,
            (2, 2) => 150,
            _ => 0
        };
    }

    static int CountOwnedCamps(ClassicAiState state, Player player) =>
        state.RootGame.World.Locations.Count(location => location.Player == player);

    static int DistanceFromOwnedTerritory(
        ClassicAiState state,
        Location target,
        int maximum)
    {
        Queue<(Location Location, int Distance)> queue = new();
        HashSet<Location> visited = new();
        foreach (Location camp in state.RootGame.World.Locations.Where(location =>
            location.Player == state.Player))
        {
            queue.Enqueue((camp, 0));
            visited.Add(camp);
        }

        while (queue.Count > 0)
        {
            (Location location, int distance) = queue.Dequeue();
            if (distance >= maximum)
                continue;
            for (int index = 0; index < location.Neighbors.Count; index++)
            {
                if (location.WayLengths[index] <= 0)
                    continue;
                Location neighbor = location.Neighbors[index];
                int neighborDistance = distance + 1;
                if (neighbor == target)
                    return neighborDistance;
                if (visited.Add(neighbor))
                    queue.Enqueue((neighbor, neighborDistance));
            }
        }
        return int.MaxValue;
    }

    static bool IsSuitableCurrentClaim(ClassicAiState state, AiContext context)
    {
        if (!CampEconomy.HasFoodProductionPotential(context.Current))
            return false;

        // Do not consume a committed settler at a barren intermediate stop. It
        // must remain with the travelling group unless the waypoint can support
        // the new guard as a real camp while the expedition continues onward.
        if (state.HasSettlementPlan && state.StrategicTarget != context.Current &&
            !CampEconomy.CanSustainCamp(context.Current))
            return false;

        if (HasOwnedCamp(state) || CampEconomy.IsAcceptableFirstCamp(context.Current))
            return true;

        // A maggot-only site remains a last-resort first camp, but never wins while
        // a reachable rat, meat, or snake site can bootstrap the territory.
        return !HasReachableAcceptableFirstCamp(state, context);
    }

    static bool HasReachableAcceptableFirstCamp(ClassicAiState state, AiContext context) =>
        state.RootGame.World.Locations.Any(location =>
        {
            if (location == context.Current || location.IsCity || location.Player != null ||
                !CampEconomy.IsAcceptableFirstCamp(location) || !state.CanClaim(location))
                return false;
            RouteFinder.Route? route = RouteFinder.Find(context.Player, context.Current, location);
            return route != null && HasSettlementRouteSupplies(
                state, context, location, route);
        });

    static bool HasSettlementRouteSupplies(
        ClassicAiState state,
        AiContext context,
        Location target,
        RouteFinder.Route route)
    {
        if (context.Player.Group.Count == 1)
        {
            // Target selection comes before recruitment planning. Do not invent a
            // zero-reserve local recruit here: keeping the target lets recruitment
            // evaluate the actual alternatives, such as a recruit waiting at the
            // destination or a city recruit. Every actual AI recruit starts at 5/5.
            // The solo leader cannot advance on a neutral target via ExpansionTask.
            return true;
        }
        if (!HasExpansionRouteSupplies(state, context, route))
            return false;

        // Reassess at owned waypoints and cities. The check becomes binding at
        // the boundary where the group actually leaves its supply network.
        if (!HasOwnedCamp(state) || route.NextStep.Player == context.Player ||
            route.NextStep.IsCity)
            return true;

        // A viable neutral waypoint will be established before the farther plan
        // continues, so it is the destination of this expedition. Otherwise the
        // group must be able to cover the complete trip to the selected camp.
        Location expeditionTarget = state.CanClaim(route.NextStep)
            ? route.NextStep
            : target;
        RouteFinder.Route? outbound = RouteFinder.Find(
            context.Player, context.Current, expeditionTarget);
        if (outbound == null)
            return false;

        RouteFinder.Route? returnRoute = FindSettlementReturnRoute(
            state, context, expeditionTarget);
        return returnRoute != null && SupplyCalculator.HasSettlementRoundTripFood(
            context.Player, outbound, returnRoute);
    }

    static bool CanWaitForSettlementFood(
        ClassicAiState state,
        AiContext context,
        Location target,
        RouteFinder.Route route)
    {
        // Only hold the plan when waiting here can actually improve the reserve.
        // A city or non-producing camp must seek supplies elsewhere instead of
        // becoming stuck on an unaffordable expedition.
        if (context.Current.Player != context.Player || context.Current.Production == null ||
            CampEconomy.FoodSurplusPerDay(context.Current) <= 0 ||
            route.NextStep.Player == context.Player || route.NextStep.IsCity)
            return false;

        Location expeditionTarget = state.CanClaim(route.NextStep)
            ? route.NextStep
            : target;
        return FindSettlementReturnRoute(state, context, expeditionTarget) != null;
    }

    static RouteFinder.Route? FindSettlementReturnRoute(
        ClassicAiState state,
        AiContext context,
        Location expeditionTarget) => state.RootGame.World.Locations
            .Where(location => location.Player == context.Player &&
                CampEconomy.CanProvisionFood(location))
            .Select(location => RouteFinder.Find(
                context.Player, expeditionTarget, location))
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate!.Days)
            .FirstOrDefault();

    static bool HasOwnedCamp(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => location.Player == state.Player);

    static float CurrentClaimScore(ClassicAiState state, Location location)
    {
        if (CampEconomy.ConnectsOwnedCamps(location, state.Player) ||
            state.StrategicTarget != null && state.StrategicTarget != location)
            return 2300;
        return 1050 + CampEconomy.TerritorialValue(location) * 0.5f;
    }

    static float NeutralTargetScore(ClassicAiState state, AiPolicy policy, Location location) =>
        policy.NeutralTargetScore + CampEconomy.TerritorialValue(location) +
        (CampEconomy.ConnectsOwnedCamps(location, state.Player) ? 900 : 0);

    static int NeutralFrontierScore(ClassicAiState state, Location location)
    {
        if (!HasOwnedCamp(state))
            return 0;

        // Expansion should normally grow outward from established territory.
        // Rich but remote camps remain future objectives instead of pulling a
        // young faction across the map before its local network is useful.
        int distance = DistanceFromOwnedTerritory(state, location, maximum: 4);
        int locality = distance switch
        {
            1 => 500,
            2 => 300,
            3 => 100,
            4 => 0,
            _ => -300
        };
        int cityAccess = CampEconomy.OpensCityAccess(location) ? 250 : 0;
        int earlyAdvancedFood = state.OwnedCampCount < 2 &&
            CampEconomy.HasAdvancedFoodPotential(location)
            ? 300
            : 0;
        // The second starting knife is seed capital, not merely a weapon. Once
        // the first camp has taken one production tool, deploy another compatible
        // portable tool into a neighboring camp before chasing distant mature
        // potential. Two producing camps start accumulating food and trade value
        // while the faction builds the rest of its network.
        bool deploysEarlyProduction = state.OwnedCampCount == 1 && distance == 1 &&
            HasPortableCompatibleProduction(state, location);
        int earlyProductionDeployment = deploysEarlyProduction ? 900 : 0;
        int earlyNetworkOpening = deploysEarlyProduction
            ? EarlyNetworkOpeningScore(location)
            : 0;
        return locality + cityAccess + earlyAdvancedFood + earlyProductionDeployment +
            earlyNetworkOpening;
    }

    static bool HasPortableCompatibleProduction(ClassicAiState state, Location location) =>
        state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production != null &&
                location.ValidProductions.Contains(item.Type.Production)) ||
        state.Pool.GetContents().Any(entry => entry.Count > 0 &&
            entry.Type.Production != null &&
            location.ValidProductions.Contains(entry.Type.Production));

    static int EarlyNetworkOpeningScore(Location location)
    {
        // A safe city-side camp remains useful, but a neighboring camp that opens
        // another sustainable neutral frontier is the stronger network seed. Keep
        // this below the city-access bonus so it acts as a close economic tie-breaker.
        int outwardFrontiers = Enumerable.Range(0, location.Neighbors.Count)
            .Count(index => location.WayLengths[index] > 0 &&
                !location.Neighbors[index].IsCity &&
                location.Neighbors[index].Player == null &&
                CampEconomy.CanSustainCamp(location.Neighbors[index]));
        return System.Math.Min(outwardFrontiers, 2) * 180;
    }

    static float TargetScore(ClassicAiState state, AiPolicy policy, Location? target)
    {
        if (target == null)
            return 0;
        if (target.Player == null)
            return NeutralTargetScore(state, policy, target);
        return 1010;
    }

    static bool HasExpansionRouteSupplies(
        ClassicAiState state,
        AiContext context,
        RouteFinder.Route route)
    {
        if (SupplyCalculator.HasTerritorialRouteSupplies(
            context.Player, context.Current, route, hostileTarget: false))
            return true;

        Location step = route.NextStep;
        if (step.Player != null || step.IsCity || context.Player.Group.Count <= 1 ||
            !state.CanClaim(step))
            return false;
        RouteFinder.Route? leg = RouteFinder.Find(context.Player, context.Current, step);
        return leg != null && SupplyCalculator.HasRouteSupplies(
            context.Player, leg, hostileTarget: false);
    }

    static int Jitter() => Burntime.Platform.Math.Random.Next(-20, 21);
}
