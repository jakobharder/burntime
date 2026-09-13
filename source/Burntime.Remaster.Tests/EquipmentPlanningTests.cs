using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class EquipmentPlanningTests
{
    internal static IEnumerable<Case<int>> RecoveryAndEquipmentCases()
    {
        for (int difficulty = 0; difficulty < 3; difficulty++)
        {
            int level = difficulty;
            yield return Int($"difficulty {level} equipment preferences", 0, () =>
            {
                var policy = Burntime.Remaster.AI.AiPolicy.ForDifficulty(level);
                Equal(new[] { 5, 10, 25 }[level], policy.ArmourLimit, "difficulty armour limit");
                Equal(new[] { 1, 1, -1 }[level], policy.FirearmLimit, "difficulty firearm purchasing limit");
                Equal(level + 2, policy.RecoveryWaterDays, "harder AI plans recovery earlier");
                Equal(new[] { 1f, 1.2f, 1.5f }[level], policy.TradeBenefit,
                    "difficulty AI trade benefit");
                var manager = new Burntime.Framework.States.StateManager(null!);
                var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
                camp.Rooms = manager.CreateLinkList<Burntime.Remaster.Logic.Room>();
                var room = manager.Create(() => new Burntime.Remaster.Logic.Room());
                room.Items = manager.Create<ItemList>();
                camp.Rooms.Add(room);
                var guard = manager.Create(() => new HazardCharacter());
                guard.Place(camp);
                guard.Items = manager.Create<ItemList>();
                var knife = TestItem(manager, "item_knife", damage: 25);
                var axe = TestItem(manager, "item_axe", damage: 33);
                var pitchfork = TestItem(manager, "item_pitchfork", damage: 38);
                guard.Items.Add(knife);
                while (!guard.Items.IsFull)
                    guard.Items.Add(TestItem(manager, "cargo"));
                room.Items.Add(axe);
                room.Items.Add(pitchfork);
                int before = guard.Items.Count + room.Items.Count;
                Burntime.Remaster.AI.WeaponLoadout.EquipStoredCampWeapon(guard,
                    type => true);
                Equal(pitchfork, guard.Items.FindBestWeapon(),
                    "all difficulties use owned weapons without confiscation");
                Equal(before, guard.Items.Count + room.Items.Count, "full-inventory swap preserves items");
                Equal(true, room.Items.Concat(guard.Items).Contains(knife), "old production tool stays in camp");
                Equal(true, room.Items.Concat(guard.Items).Contains(axe), "axe preserved");
                Equal(true, room.Items.Concat(guard.Items).Contains(pitchfork), "pitchfork preserved");
                if (level == 0)
                {
                    Equal(1, policy.CriticalGarrisonTarget, "easy retains one-guard target");
                    Equal(0, policy.MaxHumanCampDefendersToAttack, "easy avoids defended human camps");
                    Equal(100, policy.ProgressingAttackRetryTurns, "easy retains long retry delay");
                }
                return 0;
            });
        }
        yield return Int("physical firearm transfer preserves shots and melee", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var carrier = manager.Create(() => new HazardCharacter());
            var follower = manager.Create(() => new HazardCharacter());
            carrier.Items = manager.Create<ItemList>();
            follower.Items = manager.Create<ItemList>();
            var first = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6);
            var spare = TestItem(manager, "item_loaded_rifle_1", damage: 55, ammo: 6);
            spare.Use();
            var knife = TestItem(manager, "item_knife", damage: 25);
            carrier.Items.Add(first);
            carrier.Items.Add(spare);
            follower.Items.Add(knife);
            while (!follower.Items.IsFull) follower.Items.Add(TestItem(manager, "cargo"));
            var party = new Burntime.Remaster.Logic.Character[] { carrier, follower };
            Burntime.Remaster.AI.EquipmentPlanning.DistributeFirearms(party,
                Burntime.Remaster.AI.AiPolicy.ForDifficulty(2));
            Equal(spare, follower.Items.FindBestWeapon(), "spare gun assigned even with full inventory");
            Equal(5, spare.AmmoValue, "transfer preserves spent ammunition");
            Equal(true, carrier.Items.Contains(knife), "replaced melee weapon passed back");
            Burntime.Remaster.AI.WeaponLoadout.RefreshWeapons(party);
            Equal(5, spare.AmmoValue, "maintenance does not refill gun");
            var pool = manager.Create<Burntime.Remaster.AI.AiItemPool>();
            Equal(false, pool.Insert(spare), "partly used gun cannot become type-only stock");
            Equal(5, Burntime.Remaster.AI.EquipmentPlanning.LoadedShots(spare.Type, spare.AmmoValue), "actual available shots");
            return 0;
        });
        yield return Int("easy retains acquired firearms and pitchforks", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var owner = manager.Create(() => new HazardCharacter());
            owner.Items = manager.Create<ItemList>();
            var gun = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 2);
            var fork = TestItem(manager, "item_pitchfork", damage: 38);
            owner.Items.Add(gun);
            owner.Items.Add(fork);
            Burntime.Remaster.AI.WeaponLoadout.RefreshWeapons(new Burntime.Remaster.Logic.Character[] { owner });
            Equal(2, owner.Items.Count, "neither weapon confiscated");
            Equal(gun, owner.Weapon, "best owned weapon selected");
            return 0;
        });
        yield return Int("equipment opportunities are stable and bounded", 0, () =>
        {
            for (int day = 0; day < 100; day++)
            {
                Equal(false, Burntime.Remaster.AI.EquipmentNeeds.Opportunity(day, 0, "item_axe", 0), "zero disables optional buying");
                Equal(true, Burntime.Remaster.AI.EquipmentNeeds.Opportunity(day, 0, "item_axe", 100), "hard considers every useful upgrade");
                bool first = Burntime.Remaster.AI.EquipmentNeeds.Opportunity(day, 0, "item_axe", 25);
                Equal(first, Burntime.Remaster.AI.EquipmentNeeds.Opportunity(day, 0, "item_axe", 25), "planning retry does not reroll");
            }
            return 0;
        });
        yield return Int("clothing reserve can pass equipment down without generation", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var pool = manager.Create<Burntime.Remaster.AI.AiItemPool>();
            var sweater = TestItem(manager, "item_sweater", defense: 5);
            Equal(true, pool.Insert(sweater), "clothing accepted");
            Equal(5, pool.TakeForTrade(sweater.Type)!.DefenseValue, "armour data retained");
            Equal<Item?>(null, pool.TakeForTrade(sweater.Type), "cannot take the same stock twice");
            return 0;
        });
        yield return Int("clothing goes to boss first and passes down", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var boss = manager.Create(() => new HazardCharacter());
            var follower = manager.Create(() => new HazardCharacter());
            boss.Items = manager.Create<ItemList>();
            follower.Items = manager.Create<ItemList>();
            var sweater = TestItem(manager, "item_sweater", defense: 5);
            var jacket = TestItem(manager, "item_leather_jacket", defense: 20);
            boss.Items.Add(sweater);
            follower.Items.Add(jacket);
            follower.Protection = jacket;
            while (!boss.Items.IsFull) boss.Items.Add(TestItem(manager, "cargo"));
            var party = new Burntime.Remaster.Logic.Character[] { boss, follower };
            Burntime.Remaster.AI.EquipmentPlanning.DistributeClothing(party, 10);
            Equal(sweater, boss.Items.FindBestDefense(), "normal does not assign 20 percent clothing");
            Burntime.Remaster.AI.EquipmentPlanning.DistributeClothing(party, 25);
            Equal(jacket, boss.Items.FindBestDefense(), "hard gives best clothing to boss");
            Equal(sweater, follower.Items.FindBestDefense(), "old clothing passed down even with full boss inventory");
            Equal(sweater, follower.Protection, "old owner protection link updated");
            return 0;
        });
        yield return Int("recovery counts carried supplies and difficulty horizon", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var player = manager.Create<HazardPlayer>(new object[] { 0 });
            var boss = manager.Create(() => new HazardCharacter());
            boss.Items = manager.Create<ItemList>();
            boss.Health = 100;
            boss.Food = 9;
            boss.Water = 3;
            player.Character = boss;
            Equal(false, Burntime.Remaster.AI.RecoveryServices.NeedsRecovery(player, 2), "easy keeps old water horizon");
            Equal(true, Burntime.Remaster.AI.RecoveryServices.NeedsRecovery(player, 4), "hard acts before dehydration");
            var type = manager.Create<Burntime.Remaster.Logic.ItemType>(new Burntime.Remaster.Logic.Data.ItemTypeData
            {
                DataName = "water", WaterValue = 5, Class = Array.Empty<string>(), Protection = Array.Empty<string>()
            });
            boss.Items.Add(manager.Create<Item>(type));
            Equal(false, Burntime.Remaster.AI.RecoveryServices.NeedsRecovery(player, 4), "carried water avoids unnecessary recovery trips");
            boss.Health = 30;
            Equal(true, Burntime.Remaster.AI.RecoveryServices.NeedsRecovery(player, 4), "supplies do not hide urgent healing needs");
            return 0;
        });
    }
}
