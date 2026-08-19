using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct TerritorialPlan(
    bool CanClaimCurrent,
    Location? Target,
    bool PreparingAttack);

internal static partial class ExpansionTask
{
    public static TerritorialPlan CreatePlan(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy)
    {
        bool canClaimCurrent = state.CanClaim(context.Current) && state.CanStationCamp();
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
            target,
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
                1250,
                context.Current,
                Reason: "neutral sustainable camp at current location"));
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
            StrategicAi.AddTravelCandidate(
                state, candidates, plan.Target, policy.NeutralTargetScore,
                "advance toward neutral territory");
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
                SupplyCalculator.HasTerritorialRouteSupplies(
                    context.Player, context.Current, route, hostileTarget: false))
            {
                targets.Add((location, policy.NeutralTargetScore - route.Days * 8 + Jitter()));
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

    static int Jitter() => Burntime.Platform.Math.Random.Next(-20, 21);
}
