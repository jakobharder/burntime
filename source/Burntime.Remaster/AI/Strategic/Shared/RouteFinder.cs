using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class RouteFinder
{
    public static Route? Find(Player player, Location start, Location target)
    {
        if (start == target)
            return new Route(target, 0);

        Dictionary<Location, int> distance = new() { [start] = 0 };
        Dictionary<Location, Location> previous = new();
        HashSet<Location> visited = new();

        while (true)
        {
            Location? current = distance
                .Where(pair => !visited.Contains(pair.Key))
                .OrderBy(pair => pair.Value)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (current == null)
                return null;
            if (current == target)
                break;
            visited.Add(current);

            for (int index = 0; index < current.Neighbors.Count; index++)
            {
                Location neighbor = current.Neighbors[index];
                int length = current.WayLengths[index];
                if (length <= 0 || (IsHostile(neighbor, player) && neighbor != target))
                    continue;

                int candidate = distance[current] + length;
                if (!distance.TryGetValue(neighbor, out int known) || candidate < known)
                {
                    distance[neighbor] = candidate;
                    previous[neighbor] = current;
                }
            }
        }

        Location step = target;
        while (previous.TryGetValue(step, out Location? parent) && parent != start)
            step = parent;
        return previous.ContainsKey(target) ? new Route(step, distance[target]) : null;
    }

    static bool IsHostile(Location location, Player player) =>
        !location.IsCity && location.Player != null && location.Player != player;

    internal sealed record Route(Location NextStep, int Days);
}
