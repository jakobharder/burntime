using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class ReinforcementTask
{
    public static void AddCandidates(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        RecruitmentNeeds recruitment,
        List<AiDecision> candidates)
    {
        Location? camp = recruitment.ReinforcementCamp;
        if (camp != null && state.Player.Group.Count > recruitment.TravelGroupSize)
        {
            // A third or fourth traveller is either a guard in transit or a
            // survivor of a temporary attack force. Deposit that surplus before
            // ordinary expansion or trade turns it back into a roaming party.
            const float priority = 2000;
            if (camp == context.Current)
            {
                candidates.Add(new AiDecision(
                    AiAction.StationFollower,
                    priority,
                    context.Current,
                    Reason: recruitment.IsAttackStaging
                        ? $"stage a recruited guard at frontier camp {camp.Title}"
                        : $"raise critical garrison toward {recruitment.ReinforcementTarget} guards"));
            }
            else
            {
                StrategicAi.AddTravelCandidate(state, candidates, camp, priority,
                    recruitment.IsAttackStaging
                        ? $"deliver a recruited guard to frontier camp {camp.Title}"
                        : $"reinforce critical camp toward {recruitment.ReinforcementTarget} guards");
            }
        }
        else if (camp != null && state.Player.Group.Count >= recruitment.TravelGroupSize &&
            !state.HasHireableNpc())
        {
            StrategicAi.AddTravelCandidate(
                state, candidates, StrategicAi.FindNearestCity(state), 800,
                recruitment.IsAttackStaging
                    ? $"find another guard for frontier camp {camp.Title}"
                    : "find a recruit for a critical camp garrison");
        }
    }

    public static Location FindBestCampForReinforcement(ClassicAiState state, int garrisonTarget)
    {
        if (garrisonTarget <= 1)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && IsCriticalCamp(state, location))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                Guards = CampEconomy.LivingGuardCount(location, state.Player),
                Target = SustainableGarrisonTarget(location, garrisonTarget)
            })
            .Where(candidate => candidate.Route != null && candidate.Guards < candidate.Target &&
                CanSupportAdditionalGuard(state, candidate.Location, candidate.Guards))
            .OrderBy(candidate => candidate.Guards)
            .ThenBy(candidate => candidate.Route.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
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
        int radius = state.RootGame.World.Difficulty == 0 ? 1 : 2;
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
        if (camp.Production == null || (camp.Source?.Water ?? 0) < currentGuards + 1)
            return false;

        int toolCount = camp.Rooms.Sum(room => room.Items.Count(item =>
                item.Type.Production == camp.Production)) +
            camp.CampNPC.Where(npc => npc.Player == state.Player && !npc.IsDead)
                .Sum(npc => npc.Items.Count(item => item.Type.Production == camp.Production));
        Production.Rate projected = camp.Production.GetRate(toolCount, currentGuards + 1);
        return !projected.IsCampStarving && projected.FoodPerDay >= currentGuards + 1;
    }

    internal static int SustainableGarrisonTarget(Location camp, int policyTarget) =>
        System.Math.Max(1, System.Math.Min(policyTarget, camp.Source?.Water ?? 0));

    static bool CanSupportGarrison(ClassicAiState state, Location camp, int guardTarget)
    {
        if (camp.Production == null || (camp.Source?.Water ?? 0) < guardTarget)
            return false;

        int toolCount = camp.Rooms.Sum(room => room.Items.Count(item =>
                item.Type.Production == camp.Production)) +
            camp.CampNPC.Where(npc => npc.Player == state.Player && !npc.IsDead)
                .Sum(npc => npc.Items.Count(item => item.Type.Production == camp.Production));
        Production.Rate projected = camp.Production.GetRate(toolCount, guardTarget);
        return !projected.IsCampStarving && projected.FoodPerDay >= guardTarget;
    }
}
