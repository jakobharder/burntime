using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Resolves the exceptional case where hostile territory has trapped an AI
/// group and normal recovery or expansion planning cannot provide an escape.
/// </summary>
internal static class LastChanceCombat
{
    internal static bool TryExecute(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        Location? target = state.LastChanceAttackTarget;
        if (AttackPlanning.IsHostile(target, player))
            return ExecuteAssault(state, target!);

        state.LastChanceAttackTarget = null;
        TerritorialEscape escape = FindTerritorialEscape(state, maximumHops: 2);
        if (escape.HasEscape || escape.BlockingHostiles.Count == 0)
            return false;

        if (AiPolicy.ForDifficulty(state.Difficulty).DieWhenTrapped)
        {
            player.Character.Health = 0;
            AiTelemetry.Report(player,
                $"was trapped at {current.Title} with no viable destination and dies on easy");
            return true;
        }

        target = escape.BlockingHostiles
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(player, current, location),
                Strength = DefenseIntelligence.Estimate(state, location).EstimatedStrength
            })
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Strength)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
        state.LastChanceAttackTarget = target;

        if (target == null)
        {
            player.Character.Health = 0;
            AiTelemetry.Report(player,
                $"was trapped at {current.Title} with no reachable hostile camp and dies");
            return true;
        }

        return ExecuteAssault(state, target);
    }

    static bool ExecuteAssault(ClassicAiState state, Location target)
    {
        Player player = state.Player;
        Location current = state.Current;
        RouteFinder.Route? route = RouteFinder.Find(player, current, target);
        if (current == target)
        {
            AiTelemetry.Report(player,
                $"last-chance assault on {target.Title}: committing every survivor until victory or death");
            CombatResolver.Resolve(state, fightToDeath: true);
        }
        else if (route?.NextStep != null && player.CanTravel(current, route.NextStep))
        {
            GroupManagement.PrepareCampWaterReservesForDeparture(state);
            player.Travel(route.NextStep);
            AiTelemetry.Report(player,
                $"last-chance assault advances on weakest reachable camp {target.Title} via {route.NextStep.Title}");
        }
        else
        {
            player.Character.Health = 0;
            AiTelemetry.Report(player,
                $"could not advance the last-chance assault on {target.Title} and dies");
        }
        return true;
    }

    static TerritorialEscape FindTerritorialEscape(
        ClassicAiState state,
        int maximumHops)
    {
        Player player = state.Player;
        Location current = state.Current;
        Queue<(Location Location, int Hops)> frontier = new();
        HashSet<Location> visited = new() { current };
        HashSet<Location> blockingHostiles = new();
        bool hasOpenFrontier = false;
        frontier.Enqueue((current, 0));

        while (frontier.TryDequeue(out var candidate))
        {
            Location location = candidate.Location;
            if (IsTerritorialEscape(state, location, candidate.Hops == 0))
                return new TerritorialEscape(true, blockingHostiles.ToArray());

            for (int index = 0; index < location.Neighbors.Count; index++)
            {
                if (location.WayLengths[index] <= 0)
                    continue;

                Location neighbor = location.Neighbors[index];
                // Only the first edge is subject to the player's current travel
                // restriction (notably retreating from a hostile camp). Beyond
                // it this is a territorial graph check, not a supply projection.
                if (candidate.Hops == 0 && !player.CanTravel(current, neighbor))
                    continue;
                if (AttackPlanning.IsHostile(neighbor, player))
                {
                    blockingHostiles.Add(neighbor);
                    continue;
                }
                if (visited.Add(neighbor))
                {
                    if (candidate.Hops < maximumHops)
                        frontier.Enqueue((neighbor, candidate.Hops + 1));
                    else
                        // The bounded search found a non-hostile continuation.
                        // It cannot prove that enemy ownership cuts the player
                        // off, so normal strategy must remain in control.
                        hasOpenFrontier = true;
                }
            }
        }

        return new TerritorialEscape(hasOpenFrontier, blockingHostiles.ToArray());
    }

    static bool IsTerritorialEscape(
        ClassicAiState state,
        Location location,
        bool isCurrent)
    {
        // Reaching any friendly holding reconnects the group with its empire;
        // normal recovery and expansion logic can take over from there.
        if (location.Player == state.Player)
            return true;
        if (location.Player != null || location.IsCity ||
            !CampEconomy.CanSustainCamp(location) || !state.CanClaim(location))
            return false;

        // A remote sustainable site is a territorial escape even when a settler
        // must still be recruited en route. At the current site the group must be
        // able to station somebody immediately.
        return !isCurrent || state.CanStationCamp();
    }

    readonly record struct TerritorialEscape(
        bool HasEscape,
        IReadOnlyCollection<Location> BlockingHostiles);
}
