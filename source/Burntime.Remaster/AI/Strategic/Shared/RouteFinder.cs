using System.Collections.Generic;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class RouteFinder
{
    public static Route? Find(Player player, Location start, Location target)
    {
        if (start == target)
            return new Route(target, 0);

        bool retreatOnly = IsHostile(start, player);
        Location? retreat = retreatOnly ? player.PreviousLocation : null;
        Dictionary<Location, int> distance = new() { [start] = 0 };
        Dictionary<Location, Location> previous = new();
        HashSet<Location> visited = new();
        PriorityQueue<Location, (int Distance, int Sequence)> frontier = new();
        int sequence = 0;
        frontier.Enqueue(start, (0, sequence++));

        while (frontier.TryDequeue(out Location? current, out var priority))
        {
            if (visited.Contains(current) || priority.Distance != distance[current])
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
                if (!distance.TryGetValue(neighbor, out int known) || candidate < known)
                {
                    distance[neighbor] = candidate;
                    previous[neighbor] = current;
                    frontier.Enqueue(neighbor, (candidate, sequence++));
                }
            }
        }

        if (!distance.ContainsKey(target))
            return null;

        Location step = target;
        while (previous.TryGetValue(step, out Location? parent) && parent != start)
            step = parent;
        return previous.ContainsKey(target) ? new Route(step, distance[target]) : null;
    }

    static bool IsHostile(Location location, Player player) =>
        !location.IsCity && location.Player != null && location.Player != player;

    internal sealed record Route(Location NextStep, int Days);
}
