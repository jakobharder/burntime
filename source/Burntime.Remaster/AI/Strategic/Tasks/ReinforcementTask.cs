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
        if (camp != null && state.Player.Group.Count > recruitment.DesiredGroupSize)
        {
            if (camp == context.Current)
            {
                candidates.Add(new AiDecision(
                    AiAction.StationFollower,
                    1080,
                    context.Current,
                    Reason: $"raise critical garrison toward {policy.CriticalGarrisonTarget} guards"));
            }
            else
            {
                StrategicAi.AddTravelCandidate(state, candidates, camp, 1080,
                    $"reinforce critical camp toward {policy.CriticalGarrisonTarget} guards");
            }
        }
        else if (camp != null && state.Player.Group.Count >= recruitment.DesiredGroupSize &&
            !state.HasHireableNpc())
        {
            StrategicAi.AddTravelCandidate(
                state, candidates, StrategicAi.FindNearestCity(state), 800,
                "find a recruit for a critical camp garrison");
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
                Guards = CampEconomy.LivingGuardCount(location, state.Player)
            })
            .Where(candidate => candidate.Route != null && candidate.Guards < garrisonTarget &&
                CanSupportAdditionalGuard(state, candidate.Location, candidate.Guards))
            .OrderBy(candidate => candidate.Guards)
            .ThenBy(candidate => candidate.Route.Days)
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
        if (camp.Production == null)
            return false;

        int toolCount = camp.Rooms.Sum(room => room.Items.Count(item =>
                item.Type.Production == camp.Production)) +
            camp.CampNPC.Where(npc => npc.Player == state.Player && !npc.IsDead)
                .Sum(npc => npc.Items.Count(item => item.Type.Production == camp.Production));
        Production.Rate projected = camp.Production.GetRate(toolCount, currentGuards + 1);
        return !projected.IsCampStarving && projected.FoodPerDay >= currentGuards + 1;
    }
}
