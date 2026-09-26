using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.AI;

// Headless diagnostics only: count real plan starts, not prose or repeated
// evaluations of the same active plan. No save state or gameplay decisions.
internal sealed class AttackPlanObservation
{
    readonly Dictionary<(int Player, int Target), List<int>> starts = new();

    internal void Record(int player, int target, int day)
    {
        var key = (player, target);
        if (!starts.TryGetValue(key, out var days))
            starts[key] = days = new();
        days.Add(day);
    }

    internal string Describe()
    {
        List<string> lines = new();
        foreach (var (key, days) in starts.OrderBy(pair => pair.Key.Player).ThenBy(pair => pair.Key.Target))
        {
            int first = 0, bestFirst = 0, bestLast = -1;
            for (int last = 0; last < days.Count; last++)
            {
                while (days[last] - days[first] > 60) first++;
                if (last - first > bestLast - bestFirst)
                    (bestFirst, bestLast) = (first, last);
            }
            int count = bestLast - bestFirst + 1;
            if (count >= 3)
                lines.Add($"- P{key.Player + 1}, location {key.Target}: {count} attack-plan starts " +
                    $"on days {days[bestFirst]}–{days[bestLast]}; inspect the timeline for preparation, cancellations and combat.");
        }
        return lines.Count == 0 ? "None.\n" : string.Join('\n', lines) + "\n";
    }
}
