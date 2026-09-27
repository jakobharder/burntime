using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AttackPlanning
{
    public static bool TryAddImmediateResponse(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        List<AiDecision> candidates)
    {
        Player player = context.Player;
        if (!IsHostile(context.Current, player))
            return false;

        DefenseIntelligence.UpdateKnowledgeFromEncounter(state, context.Current,
            CombatStrength.Defenders(context.Current));

        if (!IsSuitable(state, player, context.Current, policy))
        {
            if (state.HasAttackPlan && state.StrategicTarget == context.Current)
            {
                AiTelemetry.Report(player,
                    $"abandoned attack plan for {context.Current.Title}: observed defense is unsuitable");
                state.DeferAttackPlan(context.Current, policy);
            }
            Location? suppliedRetreat = AiTurnController.FindNearestLogistics(state, requireReachable: true);
            Location? ownedRetreat = suppliedRetreat ??
                AiTurnController.FindNearestLogistics(state);
            Location? previousRetreat = player.PreviousLocation is Location previous &&
                !IsHostile(previous, player) &&
                player.CanTravel(context.Current, previous)
                ? previous
                : null;
            Location? retreat = ownedRetreat ?? previousRetreat;
            if (retreat != null)
            {
                bool returningToPreviousLocation = ownedRetreat == null;
                candidates.Add(new AiDecision(
                    AiAction.Travel,
                    1250,
                    retreat,
                    RouteFinder.Find(player, context.Current, retreat)?.NextStep,
                    returningToPreviousLocation
                        ? $"retreat to the previous location {retreat.Title} because no owned logistics remain"
                        : suppliedRetreat != null
                        ? "retreat toward the nearest reachable safe location"
                        : "make an emergency retreat despite insufficient route supplies"));
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
        DecisionContext context,
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
        bool currentReady = context.Player.Party.Count >= requiredGroupSize &&
            IsSuitable(state, context.Player, target, policy) &&
            TravelSupplies.HasRouteSupplies(context.Player, route, hostileTarget: true);
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
        if (player.Party.Count < RequiredAttackGroupSize(state, target, policy) ||
            !HasGroupWeapon(player) ||
            !IsTerritorialFrontierTarget(state, target))
            return false;
        RouteFinder.Route? route = RouteFinder.Find(player, state.Current, target);
        if (route == null)
            return false;

        if (route.NextStep == target)
            return TravelSupplies.HasRouteSupplies(player, route, hostileTarget: true) &&
                IsSuitable(state, player, target, policy);

        if (route.NextStep.Player == player || route.NextStep.IsCity)
        {
            if (route.NextStep.Player == player &&
                !CampEconomy.CanProvisionGroupWater(
                    route.NextStep, player.Party.Count))
                return false;
            RouteFinder.Route? safeLeg = RouteFinder.Find(player, state.Current, route.NextStep);
            return safeLeg != null && TravelSupplies.HasRouteSupplies(
                player, safeLeg, hostileTarget: false);
        }

        return TravelSupplies.HasRouteSupplies(player, route, hostileTarget: true) &&
            IsSuitable(state, player, target, policy);
    }

    public static string AdvanceBlockingReason(
        ClassicAiState state,
        Location target,
        AiPolicy policy)
    {
        Player player = state.Player;
        int requiredGroupSize = RequiredAttackGroupSize(state, target, policy);
        if (player.Party.Count < requiredGroupSize)
            return $"attack plan for {target.Title} is waiting for {requiredGroupSize} attackers";
        if (!HasGroupWeapon(player))
            return $"attack plan for {target.Title} is waiting for a weapon";
        if (!IsTerritorialFrontierTarget(state, target))
            return $"attack plan for {target.Title} is no longer on the territorial frontier";

        RouteFinder.Route? route = RouteFinder.Find(player, state.Current, target);
        if (route == null)
            return $"attack plan for {target.Title} has no permitted route";

        if (route.NextStep.Player == player &&
            !CampEconomy.CanProvisionGroupWater(route.NextStep, player.Party.Count))
        {
            return $"attack plan for {target.Title} is waiting for camp water capacity";
        }

        bool supplied = route.NextStep == target
            ? TravelSupplies.HasRouteSupplies(player, route, hostileTarget: true)
            : route.NextStep.Player == player || route.NextStep.IsCity
                ? RouteFinder.Find(player, state.Current, route.NextStep) is { } safeLeg &&
                    TravelSupplies.HasRouteSupplies(player, safeLeg, hostileTarget: false)
                : TravelSupplies.HasRouteSupplies(player, route, hostileTarget: true);
        if (!supplied)
            return $"attack plan for {target.Title} is waiting for route supplies";
        if (!IsSuitable(state, player, target, policy))
            return $"attack plan for {target.Title} is waiting for safe combat readiness";

        return $"attack plan for {target.Title} is waiting for a legal advance";
    }

    public static bool IsSuitable(
        ClassicAiState state,
        Player player,
        Location target,
        AiPolicy policy)
    {
        Character[] followers = player.Party
            .Where(character => character != player.Character && !character.IsDead)
            .ToArray();
        if (followers.Length == 0 || !HasGroupWeapon(player) ||
            !IsTargetAllowed(state, target, policy))
            return false;

        float defenders = DefenseIntelligence.Estimate(state, target).EstimatedStrength;
        if (defenders <= 0)
            return true;
        float attackersStrength = CombatStrength.Attacker(player);
        return state.HasImprovedSinceFailedAttack(
                target, followers.Length + (player.Character.IsDead ? 0 : 1), attackersStrength, defenders) &&
            attackersStrength / defenders >= policy.MinimumAttackRatio;
    }

    public static bool IsTargetAllowed(
        ClassicAiState state,
        Location target,
        AiPolicy policy)
    {
        if (!IsHostile(target, state.Player) ||
            !AttackRetryMemory.For(state.Player).CanRetry(state.Player, target))
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
        // Hard AI already uses the detailed combat-strength estimate below.
        // Requiring an additional numerical advantage as well made it spend most
        // campaigns assembling a third or fourth body even when the current
        // armed group was strong enough to attack safely.
        int numericalAdvantage = policy.UseDetailedCombatEstimate ? 0 : 1;
        int desired = System.Math.Clamp(
            expectedDefenders + numericalAdvantage, 2, policy.AttackGroupSize);
        // Numerical superiority is a preparation goal, not a reason to reject
        // an existing armed party that already meets the safety margin. Easy
        // still requires two; Hard retains its detailed-estimate policy.
        int available = state.Player.Party.Count;
        if (!policy.UseDetailedCombatEstimate && available >= 2 && available < desired &&
            IsSuitable(state, state.Player, target, policy))
            return available;
        return desired;
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

    public static bool HasGroupWeapon(Player player) => player.Party.Any(character =>
        !character.IsDead &&
        (character.Items.FindBestWeapon()?.DamageValue ?? 0) > 0);

    public static bool IsHostile(Location? location, Player player) =>
        location != null && !location.IsCity &&
        location.Player != null && location.Player != player;

    static Location? FindReadyAlternativeTarget(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location currentTarget)
    {
        return state.RootGame.World.Locations
            .Where(location => location != currentTarget && location != context.Current &&
                !location.IsCity && !TerritorialTargetDeferrals.IsDeferred(state, location) &&
                IsHostile(location, context.Player) &&
                IsTerritorialFrontierTarget(state, location) &&
                context.Player.Party.Count >= RequiredAttackGroupSize(state, location, policy) &&
                IsTargetAllowed(state, location, policy) &&
                IsSuitable(state, context.Player, location, policy))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(context.Player, context.Current, location)
            })
            .Where(candidate => candidate.Route != null &&
                TravelSupplies.HasRouteSupplies(
                    context.Player, candidate.Route, hostileTarget: true))
            .OrderBy(candidate => DefenseIntelligence.Estimate(state, candidate.Location).EstimatedStrength)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
