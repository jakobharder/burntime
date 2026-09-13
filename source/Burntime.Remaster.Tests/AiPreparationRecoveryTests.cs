using System;
using System.Collections.Generic;
using Burntime.Framework.States;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Rules;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Tests;

static partial class Program
{
    static IEnumerable<Case<int>> AiPreparationRecoveryCases()
    {
        yield return Int("Amiga offsets exactly one daily water consumption for its living party", 0, () =>
        {
            var m = new StateManager(null!);
            var player = m.Create<HazardPlayer>(new object[] { 0 });
            var boss = m.Create<HazardCharacter>(); boss.Player = player; player.Character = boss;
            var follower = m.Create<HazardCharacter>(); follower.Player = player; player.Group.Add(follower);
            var deadFollower = m.Create<HazardCharacter>(); deadFollower.Player = player;
            deadFollower.Health = 0; player.Group.Add(deadFollower);
            boss.Water = 0;
            follower.Water = follower.MaxWater;
            deadFollower.Water = 0;
            var ai = m.Create<AmigaAiState>(new object[]
                { player, new AiSettings { Difficulty = 0 } });

            AmigaAiState.OffsetDailyWaterConsumption(player);

            Equal(50, AiStateOperations.GetNaturalHealingThreshold(ai),
                "Amiga healing policy");
            Equal(null, AiStateOperations.GetNaturalHealingThreshold(null),
                "default healing policy");
            Equal(1, boss.Water, "empty bottle avoidance");
            Equal(follower.MaxWater, follower.Water, "maximum respected");
            Equal(0, deadFollower.Water, "dead follower ignored");
            return 0;
        });

        foreach (bool city in new[] { false, true })
        foreach (bool hostileOrigin in new[] { false, true })
        foreach (bool hostileDestination in new[] { false, true })
            yield return Int($"Amiga arrival city={city}, hostile origin={hostileOrigin}, hostile destination={hostileDestination}", 0, () =>
            {
                var m = new StateManager(null!);
                var player = m.Create<HazardPlayer>(new object[] { 0 });
                var enemy = m.Create<HazardPlayer>(new object[] { 1 });
                var from = m.Create<Location>(); from.Player = hostileOrigin ? enemy : player;
                var to = m.Create<Location>(); to.IsCity = city;
                to.Player = hostileDestination ? enemy : city ? null : player;
                player.Location = to; player.SetPrevious(from);
                var boss = m.Create<HazardCharacter>(); boss.Player = player; player.Character = boss;
                var follower = m.Create<HazardCharacter>(); follower.Player = player; player.Group.Add(follower);
                foreach (var member in player.Group) { member.Health = 48; member.Food = 0; member.Water = 5; }
                AmigaAiState.ApplyArrivalRecovery(player);
                foreach (var member in player.Group)
                {
                    Equal(hostileDestination ? 0 : city && hostileOrigin ? 3 : 9, member.Food, "food branch");
                    Equal(city ? hostileOrigin ? 68 : 78 : 48, member.Health, "healing is city-only");
                    Equal(5, member.Water, "no water grant");
                }
                // Repeated grant remains capped, including on followers.
                AmigaAiState.ApplyArrivalRecovery(player);
                foreach (var member in player.Group)
                    Equal(hostileDestination ? 0 : city && hostileOrigin ? 6 : 9, member.Food, "food cap");
                return 0;
            });

        yield return Int("Amiga pending local maintenance survives expansion suppression, then runs once", 0, () =>
        {
            var m = new StateManager(null!); var game = m.Create<ClassicGame>(); m.Root = game;
            game.SetRules(RuleSet.Extended);
            game.World = m.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(StateObject).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.Locations = m.CreateLinkList<Location>();
            game.World.AllCharacters = m.CreateLinkList<Character>();
            game.World.Players = m.CreateLinkList<Player>();
            var player = m.Create<HazardPlayer>(new object[] { 0 }); player.Type = PlayerType.Ai;
            var enemy = m.Create<HazardPlayer>(new object[] { 1 }); enemy.Type = PlayerType.Human;
            foreach (var owner in new Player[] { player, enemy })
            {
                var boss = m.Create<HazardCharacter>(); boss.Items = m.Create<ItemList>();
                boss.Player = owner; boss.Health = 60; owner.Character = boss;
                game.World.Players.Add(owner); game.World.AllCharacters.Add(boss);
            }
            var city = m.Create<Location>(); city.IsCity = true;
            city.Rooms = m.CreateLinkList<Room>();
            player.Location = city;
            game.World.Locations.Add(city);
            for (int i = 0; i < 4; i++) { var camp = m.Create<Location>(); camp.Player = player; camp.Rooms = m.CreateLinkList<Room>(); game.World.Locations.Add(camp); }
            var ai = m.Create<AmigaAiState>(new object[] { player, new AiSettings { Difficulty = 0 } });
            ai.Turn(); Equal(9, player.Character.Food, "first maintenance");
            player.Character.Food = 0; game.World.Day++;
            ai.Turn(); Equal(9, player.Character.Food, "suppressed local work retains maintenance");
            game.World.Players.Remove(enemy); // No relative cap: local work completes.
            game.World.Day++; ai.Turn();
            player.Character.Food = 2; game.World.Day++;
            ai.Turn(); Equal(2, player.Character.Food, "routing-only update grants nothing");
            return 0;
        });

        yield return Int("medium uses sufficient existing fighters but retains safety and retry gates", 0, () =>
        {
            var (game, player, enemy, m) = EncounterPlayers(RuleSet.Extended);
            enemy.Type = PlayerType.Ai;
            game.World = m.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(StateObject).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.Locations = m.CreateLinkList<Location>();
            game.World.Day = 200;
            var camp = m.Create<Location>(); camp.Player = enemy;
            camp.AvailableProducts = new[] { 0 };
            camp.Source.Water = 5;
            var food = TestItem(m, "item_meat").Type;
            game.Productions = m.CreateLinkList<Production>();
            game.Productions.Add(m.Create(() => new Production(1, new[] { 4, 4 }, Array.Empty<int>(), food, 0)));
            game.World.Locations.Add(camp);
            var ai = m.Create<ClassicAiState>(new object[] { player, new AiSettings { Difficulty = 1 } });
            var policy = AiPolicy.ForDifficulty(1);
            DefenseIntelligence.UpdateKnowledge(ai);
            game.World.Day = 300;
            player.Group.Add(EncounterFighter(m, player, 100, 100));
            Equal(2, AttackPlanning.RequiredAttackGroupSize(ai, camp, policy), "strong pair can prepare without a third body");
            var follower = player.Group[1];
            var strongWeapon = follower.Items[0];
            follower.Items.Clear();
            follower.Items.Add(TestItem(m, "item_knife", damage: 1, damageValues: new[] { 1 }));
            follower.Health = 1;
            Equal(3, AttackPlanning.RequiredAttackGroupSize(ai, camp, policy), "weak pair still requires reinforcement");
            follower.Health = 100;
            follower.Items.Clear(); follower.Items.Add(strongWeapon);
            enemy.Type = PlayerType.Human;
            Equal(3, AttackPlanning.RequiredAttackGroupSize(ai, camp, policy), "strong pair cannot bypass defended-human restriction");
            enemy.Type = PlayerType.Ai;
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ClassicAiState).GetField("failedAttackCamp", fields)!.SetValue(ai, (StateLink<Location>)camp);
            typeof(ClassicAiState).GetField("failedAttackUntilDay", fields)!.SetValue(ai, 320);
            typeof(ClassicAiState).GetField("failedAttackGroupSize", fields)!.SetValue(ai, 2);
            typeof(ClassicAiState).GetField("failedAttackerStrength", fields)!.SetValue(ai, CombatStrength.Attacker(player));
            typeof(ClassicAiState).GetField("failedDefenderStrength", fields)!.SetValue(ai, DefenseIntelligence.Estimate(ai, camp).EstimatedStrength);
            Equal(3, AttackPlanning.RequiredAttackGroupSize(ai, camp, policy), "unchanged failed force cannot bypass retry memory");
            return 0;
        });
    }
}
