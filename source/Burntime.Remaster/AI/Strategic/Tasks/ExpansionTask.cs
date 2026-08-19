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
    public static TerritorialPlan CreatePlan(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy)
    {
        bool suitableCurrentClaim = IsSuitableCurrentClaim(state, context);
        bool currentCanBecomeCamp = state.CanClaim(context.Current) && suitableCurrentClaim;
        bool canClaimCurrent = currentCanBecomeCamp && state.CanStationCamp();
        if (currentCanBecomeCamp && !state.CanStationCamp())
            state.StrategicTarget = context.Current;
        Location? target = ValidatePersistentTarget(state, context, policy);
        if (target == null && !(canClaimCurrent && state.StrategicTarget == null))
        {
            target = SelectTerritorialTarget(state, context, policy);
            if (AttackTask.IsHostile(target, context.Player))
                state.StartAttackPlan(target!, policy);
            else
                state.StrategicTarget = target;
        }

        return new TerritorialPlan(
            canClaimCurrent,
            CurrentClaimScore(context.Current),
            target,
            TargetScore(policy, target),
            AttackTask.IsHostile(target, context.Player));
    }

    public static void AddImmediateClaimCandidate(
        AiContext context,
        TerritorialPlan plan,
        List<AiDecision> candidates)
    {
        if (!plan.PreparingAttack && plan.CanClaimCurrent)
        {
            candidates.Add(new AiDecision(
                AiAction.ClaimNeutral,
                plan.CurrentClaimScore,
                context.Current,
                Reason: $"claim {CampEconomy.StrategicRole(context.Current)} at current location"));
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
            StrategicAi.AddTravelCandidate(
                state, candidates, plan.Target, plan.TargetScore,
                $"advance toward {CampEconomy.StrategicRole(plan.Target)}");
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
        if (ReinforcementTask.IsThreatened(state, location))
            return false;
        return location.ValidProductions.Any(production =>
            !production.GetRate(toolCount: 0, npcCount: 1).IsCampStarving);
    }

    public static bool ShouldPrioritizeEconomicGrowth(ClassicAiState state)
    {
        Location[] productiveCamps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player &&
                LocalOpportunities.ShouldPreferProductionAtCamp(state, location))
            .ToArray();
        if (productiveCamps.Length == 0)
            return false;

        return productiveCamps.Any(camp =>
        {
            Production best = camp.ValidProductions
                .Where(production => production.Produce.ID is "item_meat" or "item_rats" or "item_snake")
                .OrderByDescending(TradeTask.ProductionTradePriority)
                .FirstOrDefault();
            return best != null && CampEconomy.ProductionToolCount(camp, best) <
                CampEconomy.DesiredProductionToolCount(state, camp, best);
        });
    }

    public static bool ShouldReserveProductionTool(ClassicAiState state)
    {
        if (!TradeTask.HasNeutralExpansionOpportunity(state))
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
            if (target.Player == null && state.CanClaim(target) && !state.CanStationCamp())
                return target;
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
                SupplyCalculator.HasTerritorialRouteSupplies(
                    context.Player, context.Current, route, hostileTarget: false))
                return target;
            state.StrategicTarget = null;
            return null;
        }
        if (target.Player == context.Player)
        {
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
            if (location == context.Current || location.IsCity ||
                state.IsAttackTargetDeferred(location))
                continue;

            RouteFinder.Route? route = RouteFinder.Find(context.Player, context.Current, location);
            if (route == null)
                continue;

            if (location.Player == null && context.NeutralExpansionAllowed &&
                state.CanClaim(location) &&
                (!hasAcceptableFirstCamp || CampEconomy.IsAcceptableFirstCamp(location)) &&
                SupplyCalculator.HasTerritorialRouteSupplies(
                    context.Player, context.Current, route, hostileTarget: false))
            {
                targets.Add((location, NeutralTargetScore(policy, location) - route.Days * 8 + Jitter()));
            }
            else if (AttackTask.HasGroupWeapon(context.Player) &&
                AttackTask.IsHostile(location, context.Player) &&
                AttackTask.IsTargetAllowed(state, location, policy))
            {
                float weakness = System.Math.Max(
                    -100, 100 - CombatStrength.AssessedDefenders(location, policy));
                float strategicBonus = (ReinforcementTask.IsStrategicLocation(state, location) ||
                    state.WasRecentlyContested(location))
                    ? policy.StrategicHostileTargetBonus
                    : 0;
                targets.Add((location, policy.HostileTargetScore + weakness + strategicBonus -
                    route.Days * 6 + Jitter()));
            }
        }

        return targets.OrderByDescending(target => target.Score).FirstOrDefault().Location;
    }

    static bool IsSuitableCurrentClaim(ClassicAiState state, AiContext context)
    {
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
            return route != null && SupplyCalculator.HasTerritorialRouteSupplies(
                context.Player, context.Current, route, hostileTarget: false);
        });

    static bool HasOwnedCamp(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => location.Player == state.Player);

    static float CurrentClaimScore(Location location) =>
        1050 + CampEconomy.TerritorialValue(location) * 0.5f;

    static float NeutralTargetScore(AiPolicy policy, Location location) =>
        policy.NeutralTargetScore + CampEconomy.TerritorialValue(location);

    static float TargetScore(AiPolicy policy, Location? target)
    {
        if (target == null)
            return 0;
        if (target.Player == null)
            return NeutralTargetScore(policy, target);
        return 1010;
    }

    static int Jitter() => Burntime.Platform.Math.Random.Next(-20, 21);
}
