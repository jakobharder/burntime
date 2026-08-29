using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class ReinforcementPlanning
{
    public static void AddCandidates(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        RecruitmentNeeds recruitment,
        List<AiDecision> candidates)
    {
        Location? camp = recruitment.ReinforcementCamp;
        int activeGroupLimit = 2;
        if (state.HasAttackPlan && state.StrategicTarget != null)
        {
            activeGroupLimit = System.Math.Max(activeGroupLimit,
                AttackPlanning.RequiredAttackGroupSize(
                    state, state.StrategicTarget, policy));
        }
        bool demobilizingSurplus = state.Player.Group.Count > activeGroupLimit;
        if (camp != null && !CanSupportAdditionalGuard(
            state, camp, CampEconomy.LivingGuardCount(camp, state.Player)))
            camp = null;
        if (camp != null && state.Player.Group.Count > 1)
        {
            // Deliver the normal second traveller, then recruit or recall its
            // replacement only if another concrete personnel task needs one.
            // Garrison logistics must never create a third roaming traveller.
            float priority = demobilizingSurplus || recruitment.IsAttackStaging
                ? 2000
                : 830;
            if (camp == context.Current)
            {
                candidates.Add(new AiDecision(
                    AiAction.StationFollower,
                    priority,
                    context.Current,
                    Reason: demobilizingSurplus
                        ? "demobilize a surplus attack follower into a strategically needed garrison"
                        : recruitment.IsAttackStaging
                        ? $"stage a recruited guard at frontier camp {camp.Title}"
                        : $"raise critical garrison toward {recruitment.ReinforcementTarget} guards"));
            }
            else
            {
                AiTurnController.AddTravelCandidate(state, candidates, camp, priority,
                    demobilizingSurplus
                        ? $"demobilize a surplus attack follower into the needed garrison at {camp.Title}"
                        : recruitment.IsAttackStaging
                        ? $"deliver a recruited guard to frontier camp {camp.Title}"
                        : $"reinforce critical camp toward {recruitment.ReinforcementTarget} guards");
            }
        }
        else if (demobilizingSurplus)
        {
            candidates.Add(new AiDecision(
                AiAction.DismissFollower,
                2000,
                context.Current,
                Reason: "dismiss a surplus attack follower because no strategic garrison needs another guard"));
        }
    }

    public static Location FindBestCampForReinforcement(ClassicAiState state, int garrisonTarget)
    {
        if (garrisonTarget <= 1)
            return null;

        return state.RootGame.World.Locations
            .Where(location => WantsAdditionalGuard(state, location, garrisonTarget))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                Guards = CampEconomy.LivingGuardCount(location, state.Player)
            })
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Guards)
            .ThenBy(candidate => candidate.Route.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static bool WantsAdditionalGuard(
        ClassicAiState state,
        Location camp,
        int garrisonTarget)
    {
        if (garrisonTarget <= 1 || camp.Player != state.Player ||
            !IsCriticalCamp(state, camp))
            return false;

        // A young faction must finish forming a useful local network before it
        // spends every available recruit filling its first camps to the maximum.
        // One additional guard is enough early security; mature empires use the
        // full difficulty-specific target below.
        int effectiveTarget = state.OwnedCampCount < 4
            ? System.Math.Min(garrisonTarget, 2)
            : garrisonTarget;
        int guards = CampEconomy.LivingGuardCount(camp, state.Player);
        int target = SustainableGarrisonTarget(camp, effectiveTarget);
        return guards < target && CanSupportAdditionalGuard(state, camp, guards);
    }

    public static Location? FindAttackStagingCamp(
        ClassicAiState state,
        Location target,
        int stagedGuardTarget)
    {
        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != target)
            .Select(location => new
            {
                Location = location,
                CurrentRoute = RouteFinder.Find(state.Player, state.Current, location),
                AttackRoute = RouteFinder.Find(state.Player, location, target),
                Sustainable = CanSupportGarrison(state, location, stagedGuardTarget)
            })
            .Where(candidate => candidate.CurrentRoute != null && candidate.AttackRoute != null &&
                candidate.Sustainable)
            .OrderBy(candidate => candidate.AttackRoute!.Days)
            .ThenByDescending(candidate => candidate.Location.Source?.Water ?? 0)
            .ThenBy(candidate => candidate.CurrentRoute!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool IsCriticalCamp(ClassicAiState state, Location location)
    {
        if (IsStrategicLocation(state, location) || IsThreatened(state, location))
            return true;

        return false;
    }

    public static bool IsStrategicLocation(ClassicAiState state, Location location)
    {
        int openRoutes = Enumerable.Range(0, location.Neighbors.Count)
            .Count(index => location.WayLengths[index] > 0);
        if (openRoutes <= 2 || location.Source?.Water > 0)
            return true;

        Production.Rate production = location.GetFoodProductionRate();
        return production.FoodPerDay >= 4;
    }

    internal static bool IsThreatened(ClassicAiState state, Location origin)
    {
        if (AiTurnContext.For(state).Camps.TryGet(origin, out CampAssessment assessment))
            return assessment.Threatened;

        return CalculateIsThreatened(state, origin);
    }

    internal static bool CalculateIsThreatened(ClassicAiState state, Location origin)
    {
        int radius = AiPolicy.ForDifficulty(state.Difficulty).ThreatRadius;
        HashSet<Location> threats = state.RootGame.World.Locations
            .Where(location => location.Player != null && location.Player != state.Player)
            .ToHashSet();
        foreach (Player opponent in state.RootGame.World.Players.Where(player =>
            player != state.Player && !player.IsDead && player.Location != null))
            threats.Add(opponent.Location);
        if (threats.Contains(origin))
            return true;

        Queue<(Location Location, int Distance)> queue = new();
        HashSet<Location> visited = new() { origin };
        queue.Enqueue((origin, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Distance >= radius)
                continue;
            for (int index = 0; index < current.Location.Neighbors.Count; index++)
            {
                if (current.Location.WayLengths[index] <= 0)
                    continue;
                Location neighbor = current.Location.Neighbors[index];
                if (!visited.Add(neighbor))
                    continue;
                if (threats.Contains(neighbor))
                    return true;
                queue.Enqueue((neighbor, current.Distance + 1));
            }
        }
        return false;
    }

    internal static bool CanSupportAdditionalGuard(ClassicAiState state, Location camp, int currentGuards)
    {
        int garrisonLimit = AiPolicy.ForDifficulty(state.Difficulty).CriticalGarrisonTarget;
        if (currentGuards >= garrisonLimit)
            return false;
        int projectedGuards = currentGuards + 1;
        int wantedSurplus = CampEconomy.WantedSupplySurplus(state);
        if (camp.Production == null ||
            (camp.Source?.Water ?? 0) - projectedGuards < wantedSurplus)
            return false;

        int toolCount = camp.Rooms.Sum(room => room.Items.Count(item =>
                item.Type.Production == camp.Production)) +
            camp.CampNPC.Where(npc => npc.Player == state.Player && !npc.IsDead)
                .Sum(npc => npc.Items.Count(item => item.Type.Production == camp.Production));
        Production.Rate projected = camp.Production.GetRate(toolCount, projectedGuards);
        return !projected.IsCampStarving &&
            projected.FoodPerDay - projectedGuards >= wantedSurplus;
    }

    internal static int SustainableGarrisonTarget(Location camp, int policyTarget) =>
        System.Math.Max(1, System.Math.Min(policyTarget, camp.Source?.Water ?? 0));

    static bool CanSupportGarrison(ClassicAiState state, Location camp, int guardTarget)
    {
        int wantedSurplus = CampEconomy.WantedSupplySurplus(state);
        if (camp.Production == null ||
            (camp.Source?.Water ?? 0) - guardTarget < wantedSurplus)
            return false;

        int toolCount = camp.Rooms.Sum(room => room.Items.Count(item =>
                item.Type.Production == camp.Production)) +
            camp.CampNPC.Where(npc => npc.Player == state.Player && !npc.IsDead)
                .Sum(npc => npc.Items.Count(item => item.Type.Production == camp.Production));
        Production.Rate projected = camp.Production.GetRate(toolCount, guardTarget);
        return !projected.IsCampStarving &&
            projected.FoodPerDay - guardTarget >= wantedSurplus;
    }
}
