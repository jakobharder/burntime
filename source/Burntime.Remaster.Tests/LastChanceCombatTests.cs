using System.Collections.Generic;
using System.Linq;
using Burntime.Framework.States;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Tests;

using static Program;

static class LastChanceCombatTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        foreach (int difficulty in new[] { 0, 1, 2 })
        foreach (int distance in new[] { 1, 4 })
            yield return Int($"difficulty {difficulty}: camps beyond hostile territory do not provide an escape at distance {distance}", 0, () =>
            {
                var (ai, enemy, manager) = Fixture(difficulty);
                Location edge = ai.Current;
                for (int i = 0; i < distance; i++)
                {
                    Location barren = manager.Create<Location>();
                    Link(edge, barren);
                    edge = barren;
                }
                Location blocker = manager.Create<Location>(); blocker.Player = enemy;
                Location remoteCamp = manager.Create<Location>(); remoteCamp.Player = ai.Player;
                Link(edge, blocker);
                Link(blocker, remoteCamp);

                Equal(null, RouteFinder.Find(ai.Player, ai.Current, remoteCamp), "owned camp is unreachable");
                var escape = LastChanceCombat.FindTerritorialEscape(ai);
                Equal(false, escape.HasEscape, "enclosed neutral pocket");
                Equal(true, escape.BlockingHostiles.Contains(blocker), "hostile boundary found");
                return 0;
            });

        yield return Int("a cycle of cities and barren locations is not an escape", 0, () =>
        {
            var (ai, enemy, manager) = Fixture(1);
            Location edge = ai.Current;
            for (int i = 0; i < 6; i++)
            {
                Location next = manager.Create<Location>(); next.IsCity = i % 2 == 0;
                Link(edge, next);
                edge = next;
            }
            Link(edge, ai.Current);
            Location blocker = manager.Create<Location>(); blocker.Player = enemy;
            Link(edge, blocker);
            Equal(false, LastChanceCombat.FindTerritorialEscape(ai).HasEscape, "closed cycle");
            return 0;
        });

        yield return Int("a friendly camp beyond two hops still provides an escape", 0, () =>
        {
            var (ai, enemy, manager) = Fixture(1);
            Location blocker = manager.Create<Location>(); blocker.Player = enemy;
            Link(ai.Current, blocker);
            Location edge = ai.Current;
            for (int i = 0; i < 4; i++)
            {
                Location next = manager.Create<Location>();
                Link(edge, next);
                edge = next;
            }
            edge.Player = ai.Player;
            Equal(true, LastChanceCombat.FindTerritorialEscape(ai).HasEscape, "reachable friendly holding");
            return 0;
        });

        yield return Int("an impassable link to a friendly camp is not an escape", 0, () =>
        {
            var (ai, enemy, manager) = Fixture(1);
            Location camp = manager.Create<Location>(); camp.Player = ai.Player;
            Location blocker = manager.Create<Location>(); blocker.Player = enemy;
            Link(ai.Current, camp, days: 0);
            Link(ai.Current, blocker);
            Equal(false, LastChanceCombat.FindTerritorialEscape(ai).HasEscape, "zero-length link is closed");
            return 0;
        });

        yield return Int("an isolated location without hostile borders does not trigger last resort", 0, () =>
        {
            var (ai, _, _) = Fixture(0);
            Equal(false, LastChanceCombat.TryExecute(ai), "not trapped by enemies");
            Equal(100, ai.Player.Character.Health, "leader survives");
            return 0;
        });
    }

    static (ClassicAiState Ai, Player Enemy, StateManager Manager) Fixture(int difficulty)
    {
        var (_, player, enemy, manager) = EncounterPlayers(RuleSet.Extended);
        Location city = manager.Create<Location>(); city.IsCity = true;
        player.Location = city;
        var ai = manager.Create<ClassicAiState>(new object[]
            { player, new AiSettings { Difficulty = difficulty } });
        return (ai, enemy, manager);
    }

    static void Link(Location first, Location second, int days = 1)
    {
        first.Neighbors.Add(second);
        first.WayLengths = [.. first.WayLengths, days];
        second.Neighbors.Add(first);
        second.WayLengths = [.. second.WayLengths, days];
    }
}
