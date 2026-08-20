using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AttackTask
{
    public static bool TryAddImmediateResponse(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        List<AiDecision> candidates)
    {
        Player player = context.Player;
        if (!IsHostile(context.Current, player))
            return false;

        DefenseIntelligence.ObserveEncounter(state, context.Current,
            context.Current.CampNPC.Where(character =>
                character.Player == context.Current.Player && !character.IsDead));

        if (!IsSuitable(state, player, context.Current, policy))
        {
            Location? retreat = StrategicAi.FindNearestLogistics(state, requireReachable: true);
            if (retreat != null)
            {
                candidates.Add(new AiDecision(
                    AiAction.Travel,
                    1250,
                    retreat,
                    RouteFinder.Find(player, context.Current, retreat)?.NextStep,
                    "retreat toward the nearest reachable safe location"));
            }
            else
            {
                candidates.Add(new AiDecision(
                    AiAction.Wait,
                    1250,
                    context.Current,
                    Reason: "will not initiate an unsuitable attack without a retreat route"));
            }
        }
        else
        {
            candidates.Add(new AiDecision(
                AiAction.AttackHostile,
                1200,
                context.Current,
                Reason: "hostile camp blocks the current route"));
        }
        return true;
    }

    public static Location? ValidatePersistentTarget(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location target,
        RouteFinder.Route route)
    {
        if (!IsTerritorialFrontierTarget(state, target))
        {
            AiTelemetry.Report(context.Player,
                $"released attack plan for {target.Title}: viable neutral territory still blocks the frontier");
            state.StrategicTarget = null;
            return null;
        }
        if (!HasGroupWeapon(context.Player))
        {
            AiTelemetry.Report(context.Player,
                $"released attack plan for {target.Title}: waiting for an opportunity weapon");
            state.StrategicTarget = null;
            return null;
        }
        if (!IsTargetAllowed(state, target, policy))
        {
            AiTelemetry.Report(context.Player,
                $"abandoned attack plan for {target.Title}: target is no longer permitted");
            state.StrategicTarget = null;
            return null;
        }
        int requiredGroupSize = RequiredAttackGroupSize(state, target, policy);
        bool currentReady = context.Player.Group.Count >= requiredGroupSize &&
            IsSuitable(state, context.Player, target, policy) &&
            SupplyCalculator.HasRouteSupplies(context.Player, route, hostileTarget: true);
        if (!currentReady)
        {
            Location? alternative = FindReadyAlternativeTarget(
                state, context, policy, target);
            if (alternative != null)
            {
                AiTelemetry.Report(context.Player,
                    $"switched attack plan from {target.Title} to ready target {alternative.Title}");
                state.StartAttackPlan(alternative, policy);
                state.MarkAttackPlanReady(alternative);
                return alternative;
            }
        }
        if (currentReady)
            state.MarkAttackPlanReady(target);
        if (state.IsAttackPlanExpired)
        {
            AiTelemetry.Report(context.Player,
                $"abandoned attack plan for {target.Title}: preparation time expired");
            state.DeferExpiredAttackPlan(target, policy);
            return null;
        }
        return target;
    }

    public static bool CanAdvancePlan(
        ClassicAiState state,
        Location target,
        AiPolicy policy)
    {
        Player player = state.Player;
        if (player.Group.Count < RequiredAttackGroupSize(state, target, policy) ||
            !HasGroupWeapon(player) ||
            !IsTerritorialFrontierTarget(state, target))
            return false;
        RouteFinder.Route? route = RouteFinder.Find(player, state.Current, target);
        if (route == null)
            return false;

        if (route.NextStep == target)
            return SupplyCalculator.HasRouteSupplies(player, route, hostileTarget: true) &&
                IsSuitable(state, player, target, policy);

        if (route.NextStep.Player == player || route.NextStep.IsCity)
        {
            RouteFinder.Route? safeLeg = RouteFinder.Find(player, state.Current, route.NextStep);
            return safeLeg != null && SupplyCalculator.HasRouteSupplies(
                player, safeLeg, hostileTarget: false);
        }

        return SupplyCalculator.HasRouteSupplies(player, route, hostileTarget: true) &&
            IsSuitable(state, player, target, policy);
    }

    public static bool IsSuitable(
        ClassicAiState state,
        Player player,
        Location target,
        AiPolicy policy)
    {
        Character[] followers = player.Group
            .Where(character => character != player.Character && !character.IsDead)
            .ToArray();
        if (followers.Length == 0 || !HasGroupWeapon(player) ||
            !IsTargetAllowed(state, target, policy))
            return false;

        Character[] attackers = player.Group.Where(character => !character.IsDead).ToArray();
        if (attackers.Any(character =>
                AiItemPool.IsFirearm(character.Items.FindBestWeapon()?.Type)) ||
            attackers.Count(character => character.Items.FindBestWeapon()?.ID == "item_pitchfork") >
                (state.RootGame.World.Difficulty == 0 ? 0 : 1))
            return false;

        float defenders = DefenseIntelligence.Estimate(state, target).EstimatedStrength;
        if (defenders <= 0)
            return true;
        float attackersStrength = CombatStrength.Attacker(player);
        return state.HasImprovedSinceFailedAttack(
                target, attackers.Length, attackersStrength, defenders) &&
            attackersStrength / defenders >= policy.MinimumAttackRatio;
    }

    public static bool IsTargetAllowed(
        ClassicAiState state,
        Location target,
        AiPolicy policy)
    {
        if (!IsHostile(target, state.Player))
            return false;
        if (target.Player?.Type != PlayerType.Human || state.IsRetaliatingAgainst(target.Player))
            return true;

        int estimatedDefenders = DefenseIntelligence.Estimate(state, target).ExpectedDefenders;
        if (policy.TreatLoneKnifeGuardAsUndefended && estimatedDefenders == 1)
            estimatedDefenders = 0;
        return estimatedDefenders <= policy.MaxHumanCampDefendersToAttack;
    }

    public static int RequiredAttackGroupSize(
        ClassicAiState state,
        Location target,
        AiPolicy policy)
    {
        int expectedDefenders = DefenseIntelligence.Estimate(state, target).ExpectedDefenders;
        return System.Math.Clamp(expectedDefenders + 1, 2, policy.AttackGroupSize);
    }

    public static bool IsTerritorialFrontierTarget(ClassicAiState state, Location target)
    {
        if (!IsHostile(target, state.Player))
            return false;

        Queue<Location> frontier = new();
        HashSet<Location> visited = new();
        foreach (Location camp in state.RootGame.World.Locations.Where(location =>
            location.Player == state.Player))
        {
            frontier.Enqueue(camp);
            visited.Add(camp);
        }

        while (frontier.Count > 0)
        {
            Location current = frontier.Dequeue();
            for (int index = 0; index < current.Neighbors.Count; index++)
            {
                if (current.WayLengths[index] <= 0)
                    continue;
                Location neighbor = current.Neighbors[index];
                if (neighbor == target)
                    return true;
                if (!visited.Add(neighbor))
                    continue;

                // Cities cannot be owned, and a site incapable of sustaining even
                // one guard would only create a liability. Both may be skipped when
                // determining whether hostile territory is on the real frontier.
                if (neighbor.IsCity ||
                    neighbor.Player == null && !CampEconomy.CanSustainCamp(neighbor))
                    frontier.Enqueue(neighbor);
            }
        }
        return false;
    }

    public static bool HasGroupWeapon(Player player) => player.Group.Any(character =>
        !character.IsDead &&
        (character.Items.FindBestWeapon()?.DamageValue ?? 0) > 0);

    public static bool IsHostile(Location? location, Player player) =>
        location != null && !location.IsCity &&
        location.Player != null && location.Player != player;

    static Location? FindReadyAlternativeTarget(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location currentTarget)
    {
        return state.RootGame.World.Locations
            .Where(location => location != currentTarget && location != context.Current &&
                !location.IsCity && !state.IsAttackTargetDeferred(location) &&
                IsHostile(location, context.Player) &&
                IsTerritorialFrontierTarget(state, location) &&
                context.Player.Group.Count >= RequiredAttackGroupSize(state, location, policy) &&
                IsTargetAllowed(state, location, policy) &&
                IsSuitable(state, context.Player, location, policy))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(context.Player, context.Current, location)
            })
            .Where(candidate => candidate.Route != null &&
                SupplyCalculator.HasRouteSupplies(
                    context.Player, candidate.Route, hostileTarget: true))
            .OrderBy(candidate => DefenseIntelligence.Estimate(state, candidate.Location).EstimatedStrength)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
