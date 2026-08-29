using System.Collections.Generic;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class RouteFinder
{
    public static Route? Find(Player player, Location start, Location target)
        => Find(player, start, target, _ => 0);

    // City trade routes may take a substantial detour through friendly territory.
    // Ten effective days per exposed intermediate waypoint makes two or three
    // ordinary owned legs preferable to an equally useful unsupported corridor.
    public static Route? FindSupportedTradeRoute(
        Player player,
        Location start,
        Location target) =>
        Find(player, start, target, location =>
            location != target && location.Player != player ? 10 : 0);

    static Route? Find(
        Player player,
        Location start,
        Location target,
        System.Func<Location, int> waypointPenalty)
    {
        if (start == target)
            return new Route(target, 0, System.Array.Empty<Location>(), 0);

        bool retreatOnly = IsHostile(start, player);
        Location? retreat = retreatOnly ? player.PreviousLocation : null;
        Dictionary<Location, int> distance = new() { [start] = 0 };
        Dictionary<Location, int> effectiveCost = new() { [start] = 0 };
        Dictionary<Location, Location> previous = new();
        HashSet<Location> visited = new();
        PriorityQueue<Location, (int Cost, int Distance, int Sequence)> frontier = new();
        int sequence = 0;
        frontier.Enqueue(start, (0, 0, sequence++));

        while (frontier.TryDequeue(out Location? current, out var priority))
        {
            if (visited.Contains(current) || priority.Distance != distance[current] ||
                priority.Cost != effectiveCost[current])
                continue;
            if (current == target)
                break;
            visited.Add(current);

            for (int index = 0; index < current.Neighbors.Count; index++)
            {
                Location neighbor = current.Neighbors[index];
                int length = current.WayLengths[index];
                bool forcedRetreat = current == start && retreatOnly && neighbor == retreat;
                if (current == start && retreatOnly && !forcedRetreat)
                    continue;
                if (length <= 0 || IsHostile(neighbor, player) && neighbor != target &&
                    !forcedRetreat)
                    continue;
                // Returning into a second hostile camp is legal, but that camp
                // would again restrict the human player to going back. Do not
                // invent an onward route through it.
                if (forcedRetreat && IsHostile(neighbor, player) && neighbor != target)
                    continue;

                int candidate = distance[current] + length;
                int candidateCost = effectiveCost[current] + length + waypointPenalty(neighbor);
                if (!effectiveCost.TryGetValue(neighbor, out int knownCost) ||
                    candidateCost < knownCost ||
                    candidateCost == knownCost && candidate < distance[neighbor])
                {
                    distance[neighbor] = candidate;
                    effectiveCost[neighbor] = candidateCost;
                    previous[neighbor] = current;
                    frontier.Enqueue(neighbor, (candidateCost, candidate, sequence++));
                }
            }
        }

        if (!distance.ContainsKey(target))
            return null;

        if (!previous.ContainsKey(target))
            return null;

        List<Location> path = new();
        Location step = target;
        path.Add(step);
        while (previous.TryGetValue(step, out Location? parent) && parent != start)
        {
            step = parent;
            path.Add(step);
        }
        path.Reverse();
        return new Route(path[0], distance[target], path, effectiveCost[target]);
    }

    static bool IsHostile(Location location, Player player) =>
        !location.IsCity && location.Player != null && location.Player != player;

    internal sealed record Route(
        Location NextStep,
        int Days,
        IReadOnlyList<Location>? Path = null,
        int EffectiveCost = 0);
}
