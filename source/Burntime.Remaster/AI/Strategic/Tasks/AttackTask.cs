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
        bool currentReady = context.Player.Group.Count >= policy.AttackGroupSize &&
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
        if (player.Group.Count < policy.AttackGroupSize || !HasGroupWeapon(player))
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

        Character[] livingDefenders = target.CampNPC
            .Where(character => !character.IsDead && character.Player == target.Player)
            .ToArray();

        float defenders = policy.UseDetailedCombatEstimate
            ? livingDefenders.Sum(character =>
                character.AttackValue + character.DefenseValue + character.Health / 10f)
            : livingDefenders.Sum(character =>
                (character.Items.FindBestWeapon()?.DamageValue ?? character.BaseAttackValue) + 10f);
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

        Character[] defenders = target.CampNPC
            .Where(character => !character.IsDead && character.Player == target.Player)
            .ToArray();
        int visibleDefenders = defenders.Length;
        Item? loneGuardWeapon = visibleDefenders == 1 ? defenders[0].Items.FindBestWeapon() : null;
        if (policy.TreatLoneKnifeGuardAsUndefended && visibleDefenders == 1 &&
            (loneGuardWeapon == null || loneGuardWeapon.ID == "item_knife"))
            visibleDefenders = 0;
        return visibleDefenders <= policy.MaxHumanCampDefendersToAttack;
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
        if (context.Player.Group.Count < policy.AttackGroupSize)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location != currentTarget && location != context.Current &&
                !location.IsCity && !state.IsAttackTargetDeferred(location) &&
                IsHostile(location, context.Player) &&
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
            .OrderBy(candidate => CombatStrength.AssessedDefenders(candidate.Location, policy))
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
