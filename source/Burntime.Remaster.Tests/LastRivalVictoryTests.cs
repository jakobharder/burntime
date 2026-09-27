using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Burntime.Framework.Network;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.Tests;

using static Program;

static class LastRivalVictoryTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("sole survival alone never grants victory", 0, () =>
        {
            var (game, human, rival, _) = Fixture();
            rival.IsDead = true;
            Equal(null, game.CheckWinner(), "no automatic win in a no-rival game");
            game.World.Day = 100;
            Equal(null, game.CheckWinner(), "continuing retains the city goal");
            return 0;
        });
        yield return Int("living rivals prevent shortcut acceptance", 0, () =>
        {
            var (game, human, _, _) = Fixture();
            var condition = game.World.VictoryCondition.Object;
            Equal(false, condition.CanOfferLastRivalVictory(human), "rival alive");
            condition.AcceptLastRivalVictory(human);
            Equal(null, game.CheckWinner(), "invalid acceptance ignored");
            return 0;
        });
        yield return Int("other humans also count as rivals", 0, () =>
        {
            var (game, human, rival, manager) = Fixture();
            Player other = AddPlayer(game, manager, 2, PlayerType.Human);
            rival.IsDead = true;
            Equal(false, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(human), "human alive");
            other.Character.Health = 0;
            Equal(true, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(human), "all rivals dead");
            return 0;
        });
        yield return Int("a dead human cannot claim victory", 0, () =>
        {
            var (game, human, rival, _) = Fixture();
            human.Character.Health = rival.Character.Health = 0;
            game.World.Locations[1].Player = human;
            game.World.VictoryCondition.Object.AcceptLastRivalVictory(human);
            Equal(false, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(human), "no surviving human");
            Equal(null, game.CheckWinner(), "dead player cannot win");
            return 0;
        });
        yield return Int("AI survivors use the city goal", 0, () =>
        {
            var (game, human, rival, _) = Fixture();
            human.IsDead = true;
            game.World.VictoryCondition.Object.AcceptLastRivalVictory(rival);
            Equal(false, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(rival), "no AI offer");
            Equal(null, game.CheckWinner(), "no AI shortcut");
            return 0;
        });
        yield return Int("offering the choice preserves the city goal", 0, () =>
        {
            var (game, human, rival, _) = Fixture();
            rival.IsDead = true;
            Equal(true, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(human), "eligible survivor");
            Equal(null, game.CheckWinner(), "choice has not been accepted");
            game.World.Locations[1].Player = human;
            Equal(human, game.CheckWinner(), "city victory still works");
            return 0;
        });
        yield return Int("accepted shortcut uses the normal server victory news", 0, () =>
        {
            var (game, human, rival, manager) = Fixture();
            rival.IsDead = true;
            GameServer server = new();
            server.Create(game, manager);
            Equal(false, server.CheckVictory(), "not yet accepted");
            game.World.VictoryCondition.Object.AcceptLastRivalVictory(human);
            Equal(human, game.CheckWinner(), "standard winner check");
            Equal(true, server.CheckVictory(), "server recognizes winner");
            Equal(human.Name, ((VictoryNews)server.PopNews()).Name, "normal victory announcement");
            Equal(true, server.CheckVictory(), "rechecking is harmless");
            Equal(null, server.PopNews(), "no duplicate announcement");
            return 0;
        });
        yield return Int("ordinary city victory takes precedence", 0, () =>
        {
            var (game, human, rival, manager) = Fixture();
            rival.IsDead = true;
            game.World.Locations[1].Player = human;
            Equal(false, game.World.VictoryCondition.Object.CanOfferLastRivalVictory(human), "no redundant question");
            GameServer server = new(); server.Create(game, manager);
            Equal(true, server.CheckVictory(), "normal city win");
            Equal(human.Name, ((VictoryNews)server.PopNews()).Name, "same victory announcement");
            return 0;
        });
    }

    static (ClassicGame Game, Player Human, Player Rival, StateManager Manager) Fixture()
    {
        StateManager manager = new(null!);
        ClassicGame game = manager.Create<ClassicGame>(); manager.Root = game;
        game.World = manager.Create(() =>
        {
            var world = (ClassicWorld)RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
            foreach (string field in new[] { "ID", "localID" })
                typeof(StateObject).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(world, -1);
            return world;
        });
        game.World.Players = manager.CreateLinkList<Player>();
        game.World.Locations = manager.CreateLinkList<Location>();
        game.World.VictoryCondition = manager.Create<VictoryCondition>();
        Player human = AddPlayer(game, manager, 0, PlayerType.Human);
        Player rival = AddPlayer(game, manager, 1, PlayerType.Ai);
        Location city = manager.Create<Location>(); city.IsCity = true;
        Location camp = manager.Create<Location>();
        city.Neighbors.Add(camp); camp.Neighbors.Add(city);
        game.World.Locations.Add(city); game.World.Locations.Add(camp);
        return (game, human, rival, manager);
    }

    static Player AddPlayer(ClassicGame game, StateManager manager, int index, PlayerType type)
    {
        Player player = manager.Create<Player>(new object[] { index }); player.Type = type;
        Character boss = manager.Create<TestCharacter>(); boss.Health = 100;
        boss.Player = player; player.Character = boss;
        game.World.Players.Add(player);
        return player;
    }

    sealed class TestCharacter : Character
    {
        public override string Name { get; set; } = "Survivor";
        public override void Die() => health = 0;
    }
}
