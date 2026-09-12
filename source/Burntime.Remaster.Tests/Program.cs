using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

static partial class Program
{
    static int passed;
    static int failed;

    static int Main()
    {
        Burntime.Platform.IO.FileSystem.AddPackage("classic", System.IO.Path.GetDirectoryName(ResourceFile("rules/dos/items.txt")) + "/../..");
        Run("AI preparation and Amiga recovery", AiPreparationRecoveryCases());
        Run("economy observations", EconomyObservationCases());
        Run("DOS conflict attrition", DosConflictAttritionCases());
        Run("recovery and frontier equipment", RecoveryAndEquipmentCases());
        Run("strategic encounters", StrategicEncounterCases());
        Run("local combat lifecycle", LocalCombatCases());
        Run("doctor locality", DoctorLocalityCases());
        Run("combat experience", CombatExperienceCases());
        Run("experience tiers", ExperienceTierCases());
        Run("boss experience", BossExperienceCases());
        Run("recruitment", RecruitmentCases());
        Run("doctor healing", DoctorCases());
        Run("configured rules", ConfiguredRuleCases());
        Run("restaurant and pub value", ServiceValueCases());
        Run("original barter", BarterCases());
        Run("original defenders", DefenderCases());
        Run("water output", WaterOutputCases());
        Run("original regression cases", OriginalRegressionCases());
        Run("original stock and cleanup", OriginalStockCases());
        Run("production policy and compatibility", ProductionPolicyCases());
        Run("configured ammunition lifecycle", AmmunitionLifecycleCases());
        Run("continuous hazards", ContinuousHazardCases());
        Run("original combat damage", OriginalCombatCases());
        Run("inventory combat preview", CombatPreviewCases());
        Run("strategic combat", StrategicCombatCases());
        Run("extended strategic damage", ExtendedStrategicDamageCases());
        Run("item generation ranges", ItemGenerationCases());
        Run("profile parsing", ProfileParsingCases());
        Run("rule registry", RuleRegistryCases());
        Run("resolution scaling", ResolutionCases());

        Console.WriteLine($"Rule formulas: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    static void Run<T>(string group, IEnumerable<Case<T>> cases)
    {
        foreach (Case<T> test in cases)
        {
            try
            {
                T actual = test.Actual();
                if (!EqualityComparer<T>.Default.Equals(test.Expected, actual))
                    throw new InvalidOperationException(
                        $"expected {test.Expected}, got {actual}");
                passed++;
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {group} / {test.Name}: {exception}");
            }
        }
    }

    static IEnumerable<Case<bool>> EconomyObservationCases()
    {
        yield return new("hunting does not exempt economic camps", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 0, false, false));
        yield return new("new recruit combat damage receives grace", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 60, false, true));
        yield return new("dead guard still receives combat grace", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 0, true, false));
        yield return new("ordinary daily starvation damage", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 77, 0, 4));
        yield return new("terminal small health drop counts", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(14, 0, 6, 0));
        yield return new("small nonlethal damage is not a supply penalty", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 98, 0, 4));
        yield return new("supplied damage is not starvation", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 77, 4, 4));
        yield return new("dead characters cannot repeatedly count", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(0, 0, 0, 0));
    }

    static IEnumerable<Case<bool>> DosConflictAttritionCases()
    {
        yield return new("blocked maintenance permits daily supply death", true,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfileId.Dos, true, true, true));
        yield return new("successful refill clears the exemption", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfileId.Dos, true, true, false));
        yield return new("unrelated DOS daily death remains unexpected", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfileId.Dos, true, false, true));
        yield return new("Extended AI never receives the exemption", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfileId.Extended, true, true, true));
    }

    static IEnumerable<Case<int>> RecoveryAndEquipmentCases()
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

    static IEnumerable<Case<int>> StrategicEncounterCases()
    {
        foreach (int startingHealth in new[] { 30, 35, 36 })
            yield return Int($"follower starting at {startingHealth} health withdraws only after new injury", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(RuleSetId.Extended);
                var guard = EncounterFighter(manager, defender, 100, 1);
                var follower = EncounterFighter(manager, attacker, startingHealth, 10);
                attacker.Group.Add(follower);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender, new[] { guard }, false);
                Equal(89, guard.Health, "injured follower gets its attack; boss exchange alone does not cause withdrawal");
                Equal(startingHealth - 1, follower.Health, "new retaliation damage triggers safety rule");
                Equal(true, result.AttackerWithdrew, "withdraw after fresh injury at or below threshold");
                return 0;
            });
        yield return Int("raid still withdraws after eliminating a defender with an injured follower along", 0, () =>
        {
            var (game, attacker, defender, manager) = EncounterPlayers(RuleSetId.Extended);
            var weakGuard = EncounterFighter(manager, defender, 2, 1);
            var otherGuard = EncounterFighter(manager, defender, 100, 1);
            var follower = EncounterFighter(manager, attacker, 30, 10);
            attacker.Group.Add(follower);
            var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                new[] { weakGuard, otherGuard }, false);
            Equal(true, weakGuard.IsDead, "raid can make progress despite old injury");
            Equal(100, otherGuard.Health, "successful raid still disengages before another round");
            Equal(30, follower.Health, "old injury alone does not abort the round");
            Equal(true, result.AttackerWithdrew, "raid-and-return behavior preserved");
            return 0;
        });
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        {
            yield return Int($"{rule} immediate retaliation protects attacking boss", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                var guard = EncounterFighter(manager, defender, 100, 60);
                var follower = EncounterFighter(manager, attacker, 100, 1);
                attacker.Group.Add(follower);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard }, false);
                Equal(40, attacker.Character.Health, "boss receives immediate retaliation");
                Equal(99, guard.Health, "retreat happens before follower's attack");
                Equal(100, follower.Health, "follower not used as an invulnerable boss's shield");
                Equal(true, result.AttackerWithdrew, "boss orders retreat at safety threshold");
                return 0;
            });
            yield return Int($"{rule} defending party disengages before serious injury", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                attacker.Character.Items.Clear();
                attacker.Character.Items.Add(TestItem(manager, "item_knife", damage: 40, damageValues: new[] { 40 }));
                attacker.Group.Add(EncounterFighter(manager, attacker, 100, 1));
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { defender.Character }, false);
                Equal(true, result.DefendingPartyDisengaged, "party disengages before a hit could cross 65");
                Equal(100, defender.Character.Health, "no serious injury");
                Equal(false, defender.IsTraveling, "disengagement alone does not move the party");
                return 0;
            });
            yield return Int($"{rule} last-chance boss can die", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                var guard = EncounterFighter(manager, defender, 100, 100);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard }, true);
                Equal(true, attacker.Character.IsDead, "last-chance boss accepts lethal retaliation");
                Equal(false, result.AttackerWithdrew, "last chance does not retreat");
                return 0;
            });
            yield return Int($"{rule} disengaged defending party stays when guards hold", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                defender.Character.Health = 65;
                var guard = EncounterFighter(manager, defender, 100, 60);
                attacker.Group.Add(EncounterFighter(manager, attacker, 100, 1));
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard, defender.Character }, false);
                Equal(true, result.DefendingPartyDisengaged, "low-health boss disengages");
                Equal(true, result.AttackerWithdrew, "guards repel attack");
                Equal(65, defender.Character.Health, "disengaged boss takes no further damage");
                Equal(false, defender.IsTraveling, "party stays while camp holds");
                return 0;
            });
        }
    }

    static (ClassicGame Game, Burntime.Remaster.Logic.Player Attacker, Burntime.Remaster.Logic.Player Defender,
        Burntime.Framework.States.StateManager Manager) EncounterPlayers(RuleSetId rule)
    {
        var manager = new Burntime.Framework.States.StateManager(null!);
        var game = manager.Create(() => new ClassicGame());
        manager.Root = game;
        game.SetProfiles(rule, AiProfileId.Extended, WorldId.Original);
        var attacker = manager.Create<Burntime.Remaster.Logic.Player>(new object[] { 0 });
        var defender = manager.Create<Burntime.Remaster.Logic.Player>(new object[] { 1 });
        attacker.Character = EncounterFighter(manager, attacker, 100, 1);
        defender.Character = EncounterFighter(manager, defender, 100, 1);
        return (game, attacker, defender, manager);
    }

    static HazardCharacter EncounterFighter(Burntime.Framework.States.StateManager manager,
        Burntime.Remaster.Logic.Player owner, int health, int damage)
    {
        var fighter = manager.Create(() => new HazardCharacter());
        fighter.Player = owner;
        fighter.Class = CharClass.Mercenary;
        fighter.Health = health;
        fighter.Items = manager.Create<ItemList>();
        fighter.Items.Add(TestItem(manager, "item_knife", damage: damage, damageValues: new[] { damage }));
        return fighter;
    }

    static IEnumerable<Case<int>> LocalCombatCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        foreach (bool lethal in new[] { false, true })
            yield return Int($"{rule} local retaliation, lethal={lethal}", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var game = manager.Create(() => new ClassicGame());
                manager.Root = game;
                game.SetProfiles(rule, AiProfileId.Extended, WorldId.Original);
                var attacker = manager.Create(() => new HazardCharacter());
                var defender = manager.Create(() => new HazardCharacter());
                attacker.Class = defender.Class = CharClass.Dog;
                attacker.Items = manager.Create<ItemList>();
                defender.Items = manager.Create<ItemList>();
                attacker.Health = 100;
                defender.Health = lethal ? 1 : 100;
                attacker.Items.Add(TestItem(manager, "item_knife", damage: 7, damageValues: new[] { 7 }));
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 9,
                    damageValues: new[] { 9 }, ammo: 6);
                defender.Items.Add(rifle);
                attacker.Attack(defender);
                Equal(lethal, attacker.Health == 100, "only living defenders retaliate");
                Equal(6, rifle.AmmoValue, "creature retaliation ignores inventory weapons");
                Equal(lethal ? 1 : 0, defender.DeathCalls, "lethal hit invokes death once");
                if (lethal)
                {
                    attacker.Attack(defender);
                    Equal(1, defender.DeathCalls, "dead targets cannot be attacked again");
                }
                return 0;
            });
    }

    static IEnumerable<Case<int>> DoctorLocalityCases()
    {
        yield return Int("doctor benefits stay with the patient's group", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var owner = manager.Create<HazardPlayer>(new object[] { 0 });
            var otherOwner = manager.Create<HazardPlayer>(new object[] { 1 });
            var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
            camp.Characters = manager.CreateLinkList<Burntime.Remaster.Logic.Character>();
            var patient = manager.Create(() => new HazardCharacter());
            patient.Player = owner;
            patient.Health = 70;
            patient.Place(camp);
            camp.Characters.Add(patient);
            var doctor = manager.Create(() => new HazardCharacter());
            doctor.Player = owner;
            doctor.Health = 100;
            doctor.Class = CharClass.Doctor;
            owner.Group.Add(doctor);
            Equal(false, patient.HasLocalDoctor, "boss's distant doctor cannot heal camp employees");
            owner.Group.Add(patient);
            Equal(true, patient.HasLocalDoctor, "travelling party shares its doctor");
            owner.Group.Remove(patient);
            owner.Group.Remove(doctor);
            camp.Characters.Add(doctor);
            Equal(true, patient.HasLocalDoctor, "camp doctor heals same-owner employees");
            doctor.Player = otherOwner;
            Equal(false, patient.HasLocalDoctor, "enemy doctor does not heal patient");
            doctor.Player = owner;
            doctor.Health = 0;
            Equal(false, patient.HasLocalDoctor, "dead doctor does not heal patient");
            doctor.Health = 100;
            owner.Group.Add(patient);
            Equal(false, patient.HasLocalDoctor, "camp doctor does not join visiting party treatment");
            return 0;
        });
    }

    static IEnumerable<Case<int>> CombatExperienceCases()
    {
        yield return Int("fight class uses full XP", 99,
            () => RuleFormulas.CombatExperience(true, 99));
        yield return Int("other class uses half XP", 49,
            () => RuleFormulas.CombatExperience(false, 99));
        yield return Int("odd XP rounds down", 18,
            () => RuleFormulas.CombatExperience(false, 37));
    }

    static IEnumerable<Case<int>> ExperienceTierCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        {
            var rules = GameRulesRegistry.Get(rule);
            int width = 25;
            foreach (int xp in new[] { -1, 0, 24, 25, 26, 49, 50, 51, 52, 74, 75, 77, 78, 99, 100, 500 })
            {
                int expected = xp < width ? 0 : xp < width * 2 ? 1 : xp < width * 3 ? 2 : 3;
                yield return Int($"{rule} XP {xp}", expected, () => rules.GetExperienceTier(xp));
            }
            foreach (CharClass characterClass in new[] { CharClass.Boss, CharClass.Doctor, CharClass.Technician, CharClass.Mercenary })
            {
                var character = new Burntime.Remaster.Logic.Character { Class = characterClass, Experience = 50 };
                int dots = 3;
                yield return Int($"{rule} {characterClass} map dots at 50 XP", dots,
                    () => rules.GetExperienceTier(character.Experience) + 1);
            }
        }
    }

    static IEnumerable<Case<int>> BossExperienceCases()
    {
        yield return Int("DOS initial", 37, () => RuleFormulas.DosBossExperience(0));
        yield return Int("DOS camps", 97, () => RuleFormulas.DosBossExperience(20));
        yield return Int("DOS cap", 99, () => RuleFormulas.DosBossExperience(40));
        yield return Int("Amiga initial", 38,
            () => RuleFormulas.AmigaBossExperience(0, 0, 0));
        yield return Int("Amiga economy", 45,
            () => RuleFormulas.AmigaBossExperience(8, 8, 4));
        yield return Int("Amiga integer truncation", 39,
            () => RuleFormulas.AmigaBossExperience(1, 1, 1));
        yield return Int("Amiga cap", 99,
            () => RuleFormulas.AmigaBossExperience(100, 100, 20));
        yield return Int("Extended base and camps", 67,
            () => RuleFormulas.ExtendedBossExperience(37, 10));
        yield return Int("Extended cap", 99,
            () => RuleFormulas.ExtendedBossExperience(90, 10));
    }

    static bool ExtendedCanRecruit(int bossExperience, int recruitExperience) =>
        GameRulesRegistry.Get(RuleSetId.Extended).MeetsRecruitmentExperience(
            new Burntime.Remaster.Logic.Character { Experience = bossExperience },
            new Burntime.Remaster.Logic.Character { Experience = recruitExperience });

    static IEnumerable<Case<bool>> RecruitmentCases()
    {
        yield return Bool("DOS boundary allowed", true,
            () => RuleFormulas.DosCanRecruit(40, 60));
        yield return Bool("DOS boundary denied", false,
            () => RuleFormulas.DosCanRecruit(40, 61));
        yield return Bool("DOS integer truncation allowed", true,
            () => RuleFormulas.DosCanRecruit(39, 58));
        yield return Bool("DOS integer truncation denied", false,
            () => RuleFormulas.DosCanRecruit(39, 59));
        yield return Bool("Amiga four-point allowance", true,
            () => RuleFormulas.AmigaCanRecruit(40, 44));
        yield return Bool("Amiga beyond allowance", false,
            () => RuleFormulas.AmigaCanRecruit(40, 45));
        yield return Bool("Extended no longer rounds two-thirds down to 66 percent", false,
            () => ExtendedCanRecruit(33, 50));
        yield return Bool("Extended exact two-thirds ceiling", true,
            () => ExtendedCanRecruit(34, 50));
        yield return Bool("Extended boundary allowed", true,
            () => ExtendedCanRecruit(40, 60));
        yield return Bool("Extended boundary denied", false,
            () => ExtendedCanRecruit(39, 60));
        yield return Bool("Extended high boundary allowed", true,
            () => ExtendedCanRecruit(66, 99));
        yield return Bool("Extended high boundary denied", false,
            () => ExtendedCanRecruit(65, 99));
    }

    static IEnumerable<Case<int>> DoctorCases()
    {
        yield return Int("DOS four points per food", 94,
            () => RuleFormulas.DoctorResult(90, 1, 4, 95));
        yield return Int("DOS caps at 95", 95,
            () => RuleFormulas.DoctorResult(91, 1, 4, 95));
        yield return Int("DOS empty payment preserves high health", 99,
            () => RuleFormulas.DoctorResult(99, 0, 4, 95));
        yield return Int("Amiga three points per food", 93,
            () => RuleFormulas.DoctorResult(90, 1, 3, 95));
        yield return Int("Amiga caps at 95", 95,
            () => RuleFormulas.DoctorResult(93, 1, 3, 95));
        yield return Int("Amiga empty payment preserves high health", 99,
            () => RuleFormulas.DoctorResult(99, 0, 3, 95));
        yield return Int("Extended healing", 92,
            () => RuleFormulas.DoctorResult(80, 12, 1, 95));
        yield return Int("Extended doctor cap", 95,
            () => RuleFormulas.DoctorResult(95, 12, 1, 95));
        foreach (float factor in new[] { 1f, 0.75f })
        foreach (int health in new[] { 94, 95, 96, 100 })
        {
            yield return Int($"doctor empty payment at {health}, factor {factor}", health,
                () => RuleFormulas.DoctorResult(health, 0, factor, 95));
            yield return Int($"doctor paid treatment at {health}, factor {factor}", Math.Max(health, 95),
                () => RuleFormulas.DoctorResult(health, 12, factor, 95));
        }
    }

    static IEnumerable<Case<int>> ConfiguredRuleCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
            yield return Int($"{rule} configured doctor, barter and damage", 0, () =>
            {
                var rules = GameRulesRegistry.Get(rule);
                Equal(37, rules.Settings.StartExperience, "shared boss starting XP");
                if (rule == RuleSetId.Amiga)
                {
                    int[][] regions = { new[] { 26, 34, 19, 20 }, new[] { 31, 28, 33, 23 },
                        new[] { 11, 12, 17, 10 }, new[] { 5, 3, 9, 2 } };
                    for (int difficulty = 0; difficulty < 3; difficulty++)
                    {
                        rules.Settings.SetDifficulty(difficulty);
                        Equal(4, rules.Settings.StartRegionCount, "Amiga regional placement group count");
                        for (int region = 1; region <= 4; region++)
                            Equal(true, regions[region - 1].SequenceEqual(rules.Settings.GetStartLocation(region)),
                                "Amiga regional placement preserves original locations");
                    }
                }

                var config = new Burntime.Platform.IO.ConfigFile();
                config.Open(System.IO.File.OpenRead(ResourceFile(
                    new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File)));
                var manager = new Burntime.Framework.States.StateManager(null!);
                var payment = manager.Create<ItemList>();
                var meat = TestItem(manager, "item_meat", food: 9,
                    heal: config["item_meat"].GetInt("heal"), trade: 27);
                payment.Add(meat);
                Equal(rule == RuleSetId.Amiga ? 37 : 46,
                    rules.CalculateDoctorResult(10, payment), "meat healing from item and settings");
                Equal(95,
                    rules.CalculateDoctorResult(90, payment), "configured health cap");
                Equal(rule == RuleSetId.Dos ? 80 : 95,
                    rules.Settings.GetBarterFactor(1), "normal barter factor");
                Equal(25,
                    rules.Settings.CombatTierWidth, "combat XP tier width");
                Equal(true, rules.Settings.FightClasses.Contains("fighter"),
                    "fighters use full combat XP");
                Equal(rule != RuleSetId.Amiga, rules.Settings.FightClasses.Contains("trader"),
                    "trader combat XP follows the ruleset");
                var demand = manager.Create<ItemList>();
                demand.Add(TestItem(manager, "payment", trade: 27));
                Equal(false, rules.AcceptTrade(payment, demand, 1),
                    "normal trade uses the configured factor");
                Equal(100, rules.Settings.GetBarterFactor(0), "easy barter factor");
                Equal(rule == RuleSetId.Dos ? 60 : 90,
                    rules.Settings.GetBarterFactor(2), "hard barter factor");
                rules.Settings.SetDifficulty(0);
                Equal(rule == RuleSetId.Dos ? RespawnMethod.PlayerCycle : RespawnMethod.LocationCycle,
                    rules.Settings.Respawn.Method, "configured NPC spawn method");
                Equal(rule switch
                    {
                        RuleSetId.Amiga => 8,
                        RuleSetId.Dos => 4,
                        _ => 4,
                    },
                    rules.Settings.Respawn.NPC, "configured easy NPC spawn interval");
                Equal(rule == RuleSetId.Extended ? 12 : 0,
                    rules.Settings.Respawn.CitySpawnThreshold,
                    "configured city spawn threshold");
                Equal(true, rules.AcceptTrade(payment, demand, 0), "equal offer accepted on easy");
                Equal(false, rules.AcceptTrade(payment, demand, 2), "equal offer rejected on hard");
                Equal(16,
                    config["item_knife"].GetInts("damage").Length, "damage format");
                Equal(true, RuleFormulas.OriginalDamage(new[] { 7 }, 99, 25, 3) == 7,
                    "scalar damage supports every tier and roll");
                int[] traderDamage = rule == RuleSetId.Amiga
                    ? new[] { 9, 11, 13, 15 }
                    : new[] { 8, 16, 25, 40 };
                Equal(true, traderDamage.SequenceEqual(rules.Settings.GetTraderAttack(0)),
                    "configured trader damage rolls");
                Equal(traderDamage[3],
                    RuleFormulas.OriginalDamage(traderDamage, 99, 25, 3),
                    "trader damage is a fixed four-roll table");
                for (int difficulty = 0; difficulty < 3; difficulty++)
                {
                    int bonus = rule == RuleSetId.Extended ? difficulty * 2 : 0;
                    int[] creatureDamage = rule == RuleSetId.Dos
                        ? new[] { 2, 2, 2, 2 }
                        : new[] { 2 + bonus, 3 + bonus, 4 + bonus, 6 + bonus };
                    Equal(true, creatureDamage.SequenceEqual(
                        rules.Settings.GetMutantAttack(difficulty)),
                        "configured mutant damage rolls");
                    Equal(true, creatureDamage.SequenceEqual(
                        rules.Settings.GetDogAttack(difficulty)),
                        "configured dog damage rolls");
                }

                if (rule != RuleSetId.Dos)
                {
                    int interval = rules.Settings.Respawn.NPC;
                    for (int location = 0; location < 3; location++)
                    {
                        int dueDays = Enumerable.Range(1, interval * 2)
                            .Count(day => CharacterRespawn.IsLocationCycleDue(day, location, interval));
                        Equal(2, dueDays, "location cycle runs once per interval");
                    }
                    Equal(false,
                        CharacterRespawn.IsLocationCycleDue(1, 0, 0),
                        "disabled location cycle never runs");
                }
                return 0;
            });
    }

    static IEnumerable<Case<int>> ServiceValueCases()
    {
        yield return Int("meat", 6,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f }));
        yield return Int("bible", 11,
            () => RuleFormulas.OriginalServiceValue(new[] { 11.25f }));
        yield return Int("two meats truncate after summing", 13,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f, 6.75f }));
        yield return Int("mixed payment", 18,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f, 11.25f }));
        yield return Int("empty payment", 0,
            () => RuleFormulas.OriginalServiceValue(Array.Empty<float>()));
    }

    static IEnumerable<Case<bool>> BarterCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
            yield return Bool($"{rule} preserves fractional barter value", rule != RuleSetId.Dos, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var offer = manager.Create<ItemList>();
                var demand = manager.Create<ItemList>();
                offer.Add(TestItem(manager, "offer", trade: 6.75f));
                demand.Add(TestItem(manager, "demand", trade: 6f));
                return GameRulesRegistry.Get(rule).AcceptTrade(offer, demand, 1);
            });
        yield return Bool("empty offers still rejected", false, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            return GameRulesRegistry.Get(RuleSetId.Extended).AcceptTrade(
                manager.Create<ItemList>(), manager.Create<ItemList>(), 0);
        });
        yield return Bool("reserve quote preserves quarter values", true,
            () => RuleFormulas.AcceptTrade(new[] { 6.75f }, new[] { 6f }, 95));
        yield return Bool("reserve quote checks entire basket", false,
            () => RuleFormulas.AcceptTrade(new[] { 6.75f }, new[] { 6f, 0.5f }, 95));
        yield return Bool("reserve quote rejects empty offers", false,
            () => RuleFormulas.AcceptTrade(Array.Empty<float>(), Array.Empty<float>(), 100));
        yield return Bool("profile trade factor composes with ruleset", true,
            () => RuleFormulas.AcceptTrade(new[] { 88f }, new[] { 100f }, 95, 1.2f));
        yield return Bool("profile trade factor still observes ruleset", false,
            () => RuleFormulas.AcceptTrade(new[] { 87f }, new[] { 100f }, 95, 1.2f));
        yield return Bool("default profile factor is neutral", false,
            () => RuleFormulas.AcceptTrade(new[] { 100f }, new[] { 100f }, 95));
        yield return Bool("easy equality", true,
            () => RuleFormulas.OriginalAcceptTrade(100, 100, 100));
        yield return Bool("easy short offer", false,
            () => RuleFormulas.OriginalAcceptTrade(99, 100, 100));
        yield return Bool("DOS normal boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(125, 100, 80));
        yield return Bool("DOS normal below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(124, 100, 80));
        yield return Bool("DOS hard boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(167, 100, 60));
        yield return Bool("DOS hard below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(166, 100, 60));
        yield return Bool("Amiga normal boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(106, 100, 95));
        yield return Bool("Amiga normal below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(105, 100, 95));
        yield return Bool("Amiga hard boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(112, 100, 90));
        yield return Bool("Amiga hard below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(111, 100, 90));
        yield return Bool("empty-value trade equality", true,
            () => RuleFormulas.OriginalAcceptTrade(0, 0, 100));

        yield return Bool("payment scaling preserves meat value", true,
            () => RuleFormulas.OriginalPaymentValue(new[] { 6.75f }) == 27);
        yield return Bool("payment scaling preserves bible value", true,
            () => RuleFormulas.OriginalPaymentValue(new[] { 11.25f }) == 45);
    }

    static IEnumerable<Case<int>> DefenderCases()
    {
        yield return Int("DOS strategic damage zero boundary", 0, () =>
        {
            var (game, attacker, defender, manager) = EncounterPlayers(RuleSetId.Dos);
            // This damage calculation reads only Day; skip the world's UI-singleton initializer.
            game.World = manager.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(Burntime.Framework.States.StateObject).GetField(field,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.Day = 1;
            defender.Character.Experience = 37; // Knife basis 20: strength 57.
            var ai = manager.Create<Burntime.Remaster.AI.DosAiState>(new object[]
            {
                attacker, new Burntime.Remaster.AI.AiSettings { Difficulty = 2, Profile = AiProfileId.Dos }
            });
            var damage = typeof(Burntime.Remaster.AI.DosAiState).GetMethod("StrategicDamage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (var (xp, expected) in new[] { (36, -1), (37, 1), (38, 1), (40, 3) })
            {
                attacker.Character.Experience = xp;
                Equal(expected, (int)damage.Invoke(ai, new object[] { attacker.Character, defender.Character })!,
                    $"damage at boss XP {xp}");
            }
            return 0;
        });
        foreach (string position in new[] { "present", "approaching", "departing", "absent" })
        {
            yield return Int($"DOS owner safeguard: {position}", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
                var away = manager.Create(() => new Burntime.Remaster.Logic.Location());
                camp.Characters = manager.CreateLinkList<Burntime.Remaster.Logic.Character>();
                var attacker = manager.Create<HazardPlayer>(new object[] { 0 });
                var owner = manager.Create<HazardPlayer>(new object[] { 1 });
                attacker.Location = camp;
                camp.Player = owner;
                owner.Location = position is "present" or "departing" ? camp : away;
                owner.SetDestination(position == "approaching" ? camp : away);
                owner.Traveling = position is "approaching" or "departing";
                var guard = manager.Create(() => new HazardCharacter());
                guard.Player = owner;
                guard.Health = 100;
                if (position != "absent")
                    camp.Characters.Add(guard);
                var ai = manager.Create<Burntime.Remaster.AI.DosAiState>(new object[]
                {
                    attacker, new Burntime.Remaster.AI.AiSettings { Difficulty = 2, Profile = AiProfileId.Dos }
                });
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var resolve = typeof(Burntime.Remaster.AI.DosAiState).GetMethod("ResolveCurrentOpposition", flags)!;
                Equal(false, (bool)resolve.Invoke(ai, null)!, "continues to routing");
                Equal(position != "absent", camp.Player == owner, "protected camp retains ownership, absent owner loses empty camp");
                if (position != "absent")
                {
                    Equal(100, guard.Health, "protected guard takes no strategic damage");
                    Equal(6, (int)typeof(Burntime.Remaster.AI.OriginalAiState).GetField("wait", flags)!.GetValue(ai)!, "resets conflict budget");
                }
                return 0;
            });
        }

        yield return Int("DOS owner-only and Amiga opposing parties", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
            camp.Characters = manager.CreateLinkList<Burntime.Remaster.Logic.Character>();
            var attacker = manager.Create<HazardPlayer>(new object[] { 0 });
            var owner = manager.Create<HazardPlayer>(new object[] { 1 });
            var visitor = manager.Create<HazardPlayer>(new object[] { 2 });
            camp.Player = owner;
            owner.Location = camp;
            visitor.Location = camp;
            var guard = manager.Create(() => new HazardCharacter());
            guard.Player = owner; guard.Health = 100;
            camp.Characters.Add(guard);
            var boss = manager.Create(() => new HazardCharacter());
            boss.Player = owner; boss.Health = 100;
            owner.Group.Add(boss);
            var visitingBoss = manager.Create(() => new HazardCharacter());
            visitingBoss.Player = visitor; visitingBoss.Health = 100;
            visitor.Group.Add(visitingBoss);
            var dead = manager.Create(() => new HazardCharacter());
            dead.Player = owner; dead.Health = 0;
            owner.Group.Add(dead);
            var dos = Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner }).ToArray();
            Equal(true, dos.SequenceEqual(new[] { guard, boss }), "DOS includes stationed guard and present owner, excludes dead members");
            Equal(true, Burntime.Remaster.AI.CombatStrength.Defenders(camp).SequenceEqual(dos),
                "Extended roster includes the owner party, not unrelated visitors");
            var amiga = Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { attacker, owner, visitor }).ToArray();
            Equal(true, amiga.SequenceEqual(new[] { guard, boss, visitingBoss }), "Amiga includes other opposing parties");
            owner.Traveling = true;
            Equal(true, Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner })
                .SequenceEqual(new[] { guard }), "travelling party excluded, guard remains");
            Equal(true, Burntime.Remaster.AI.CombatStrength.Defenders(camp).SequenceEqual(new[] { guard }),
                "Extended excludes parties already travelling");
            owner.Traveling = false;
            owner.Location = manager.Create(() => new Burntime.Remaster.Logic.Location());
            Equal(1, Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner }).Count(), "absent party excluded");
            return 0;
        });
    }

    static IEnumerable<Case<int>> WaterOutputCases()
    {
        foreach (var (source, hand, industrial) in new[]
        {
            (0, 1, 2), (1, 2, 3), (2, 3, 4), (3, 4, 5),
            (4, 5, 6), (5, 6, 7), (6, 7, 9), (7, 8, 10),
            (8, 10, 12), (9, 11, 13), (10, 12, 15), (12, 15, 18)
        })
        {
            yield return Int($"corrected DOS base {source}, no pump", source,
                () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(source, false, false));
            yield return Int($"corrected DOS base {source}, hand", hand,
                () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(source, true, false));
            yield return Int($"corrected DOS base {source}, industrial", industrial,
                () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(source, false, true));
            yield return Int($"corrected DOS base {source}, both", industrial,
                () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(source, true, true));
        }
        (int Base, bool Hand, bool Industrial, int Original, int Extended)[] cases =
        {
            (1, false, false, 1, 1),
            (1, true, false, 3, 3),
            (1, false, true, 6, 6),
            (4, true, false, 5, 6),
            (4, false, true, 6, 9),
            (10, true, false, 12, 12),
            (10, false, true, 15, 15),
            (10, true, true, 15, 15),
            (12, false, false, 12, 12),
            (12, true, false, 15, 14),
            (12, false, true, 18, 17),
            (12, true, true, 18, 17)
        };
        foreach (var entry in cases)
        {
            string pumps = entry.Industrial ? "industrial" : entry.Hand ? "hand" : "none";
            yield return Int($"original base {entry.Base}, {pumps}", entry.Original,
                () => RuleFormulas.OriginalWaterOutput(entry.Base, entry.Hand, entry.Industrial));
            yield return Int($"extended base {entry.Base}, {pumps}", entry.Extended,
                () => RuleFormulas.ExtendedWaterOutput(entry.Base, entry.Hand, entry.Industrial));
        }
    }

    static IEnumerable<Case<int>> OriginalRegressionCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        {
            yield return Int($"{rule} production comes from its config", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var food = TestItem(manager, "item_snake", food: 7).Type;
                var config = new Burntime.Platform.IO.ConfigFile();
                config.Open(System.IO.File.OpenRead(ResourceFile(GameDefinitions.Get(rule).ProductionPath)));
                var section = config["item_snake_trap"];
                var snakes = manager.Create(() => new Burntime.Remaster.Logic.Production(
                    section.GetInt("maxcombination"), section.GetInts("amount"),
                    section.GetInts("amount2"), food, 2));
                Equal(5, snakes.GetRate(2, 1).FoodPerDay, "two traps, one employee");
                Equal(rule == RuleSetId.Extended ? section.GetInts("amount")[1] : rule == RuleSetId.Amiga ? 4 : 3,
                    snakes.GetRate(1, 1).FoodPerDay, "single snake trap");
                Equal(5, snakes.GetRate(2, 0).FoodPerDay, "no rules-specific staffing check");
                snakes.ApplySettings(2, new[] { 0, 8, 9 }, Array.Empty<int>());
                Equal(8, snakes.GetRate(1, 1).FoodPerDay, "no hardcoded edition override");
                return 0;
            });
        }
        yield return Int("legacy item paths still resolve", 0, () =>
        {
            Equal("rules/dos/items.txt", GameDefinitions.ResolveItemsPath("items_original.txt"), "classic save");
            Equal("rules/extended/items.txt", GameDefinitions.ResolveItemsPath("items.txt"), "extended save");
            foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
            {
                var path = new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File;
                Equal(true, System.IO.File.Exists(ResourceFile(GameDefinitions.ResolveItemsPath(path))), "item file exists");
            }
            return 0;
        });
        yield return Int("DOS base zero industrial minimum boost", 2,
            () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(0, false, true));
        yield return Int("DOS base 3 industrial", 5,
            () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(3, false, true));
        yield return Int("Amiga base 3 industrial", 6,
            () => GameRulesRegistry.Get(RuleSetId.Amiga).CalculateWaterOutput(3, false, true));
        yield return Int("DOS base 1 industrial precedence", 3,
            () => GameRulesRegistry.Get(RuleSetId.Dos).CalculateWaterOutput(1, true, true));
        yield return Int("Amiga stock removal residue classes", 0, () =>
        {
            for (int day = 0; day < 8; day++)
                for (int residue = 0; residue < 8; residue++)
                    Equal(residue == day || residue == (day + 5) % 8,
                        RuleFormulas.AmigaTraderRemovalCandidate(0x40 + residue, day),
                        $"day {day}, residue {residue}");
            Equal(true, RuleFormulas.AmigaTraderRemovalCandidate(0x36, 1), "food cleared");
            return 0;
        });
        yield return Int("unloaded rifle retained only for Amiga strategic combat", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
            fighter.Items = manager.Create<ItemList>();
            Item rifle = TestItem(manager, "item_unloaded_rifle");
            fighter.Items.Add(rifle);
            Equal(rifle, fighter.SelectOriginalWeapon(allowUnloadedRifle: true), "Amiga rifle");
            Equal<Item?>(null, fighter.SelectOriginalWeapon(), "on-map unarmed");
            Item knife = TestItem(manager, "item_knife", damage: 25);
            fighter.Items.Add(knife);
            Equal(knife, fighter.SelectOriginalWeapon(allowUnloadedRifle: true), "prefer knife");
            return 0;
        });
        yield return Int("food cap follows the selected product across changes", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
            camp.Rooms = manager.CreateLinkList<Burntime.Remaster.Logic.Room>();
            var room = manager.Create(() => new Burntime.Remaster.Logic.Room());
            room.Items = manager.Create<ItemList>();
            camp.Rooms.Add(room);
            for (int n = 0; n < 3; n++)
            {
                room.Items.Add(TestItem(manager, "item_maggots", food: 3));
                room.Items.Add(TestItem(manager, "item_meat", food: 9));
            }
            room.Items.Add(TestItem(manager, "item_knife", damage: 25));
            Equal(0, camp.GetCurrentProductionStockCount(), "no selected product");
            var foodType = room.Items[0].Type;
            camp.Production = manager.Create(() => new Burntime.Remaster.Logic.Production(
                1, new[] { 1, 2 }, new[] { 1, 3 }, foodType, 0));
            Equal(3, camp.GetCurrentProductionStockCount(), "meat does not count against maggots");
            for (int n = 0; n < 3; n++)
                room.Items.Add(TestItem(manager, "item_maggots", food: 3));
            Equal(6, camp.GetCurrentProductionStockCount(), "six maggots fill the cap");
            var meatType = room.Items[1].Type;
            camp.Production = manager.Create(() => new Burntime.Remaster.Logic.Production(
                2, new[] { 0, 5, 7 }, Array.Empty<int>(), meatType, 3));
            Equal(3, camp.GetCurrentProductionStockCount(), "maggots do not count against meat");
            return 0;
        });
    }

    static string ResourceFile(string relativePath)
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, "resources/game/classic", relativePath);
            if (System.IO.File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new System.IO.FileNotFoundException(relativePath);
    }

    sealed class HazardPlayer : Burntime.Remaster.Logic.Player
    {
        public bool Traveling;
        public void SetPrevious(Burntime.Remaster.Logic.Location value) => previousLocation = value;
        public void SetDestination(Burntime.Remaster.Logic.Location value) => destination = value;
        public override bool IsTraveling => Traveling;
    }

    sealed class HazardCharacter : Burntime.Remaster.Logic.Character
    {
        public int DeathCalls;
        public float ExactHealth => health;
        public void Place(Burntime.Remaster.Logic.Location value) => location = value;
        public override void Die() { DeathCalls++; health = 0; }
    }

    sealed class HazardProtectionType : Burntime.Remaster.Logic.ItemType
    {
        public HazardProtectionType(string hazard)
        {
            data = new Burntime.Remaster.Logic.Data.ItemTypeData
            {
                DataName = "test_protection", Class = Array.Empty<string>()
            };
            protection = new Burntime.Platform.Resource.DataID<Burntime.Remaster.Logic.Interaction.DangerProtection>[]
            {
                new Burntime.Remaster.Logic.Interaction.DangerProtection(hazard, 1)
                { DataName = "test_hazard_protection" }
            };
        }
    }

    static IEnumerable<Case<int>> ContinuousHazardCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        foreach (string hazard in new[] { "gas", "radiation" })
        {
            yield return Int($"{rule} {hazard} exposure, equipment and death", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var game = manager.Create(() => new ClassicGame());
                manager.Root = game;
                game.SetProfiles(rule, AiProfileId.Extended, WorldId.Original);
                var owner = manager.Create<HazardPlayer>(new object[] { 0 });
                owner.Type = Burntime.Remaster.Logic.PlayerType.Human;
                var character = manager.Create(() => new HazardCharacter());
                character.Player = owner;
                character.Items = manager.Create<ItemList>();
                character.Health = 100;
                var location = manager.Create(() => new Burntime.Remaster.Logic.Location());
                location.Characters = manager.CreateLinkList<Burntime.Remaster.Logic.Character>();
                location.Danger = new Burntime.Remaster.Logic.Interaction.Danger(hazard, 95, "", null!)
                    { DataName = "test_hazard" };
                character.Place(location);
                IGameRules rules = GameRulesRegistry.Get(rule);
                for (int frame = 0; frame < 20; frame++)
                    Equal(false, rules.ApplyContinuousHazard(character, 0.1f), "survives exposure");
                float expected = 100 - (hazard == "gas" ? 0.5f : 1.35f) * 2;
                Equal(true, Math.Abs(expected - character.ExactHealth) < 0.001f, "elapsed-time damage");
                var type = manager.Create(() => new HazardProtectionType(hazard));
                var protection = manager.Create<Item>(type);
                character.Items.Add(protection);
                character.Health = 100;
                Equal(false, rules.ApplyContinuousHazard(character, 10), "protected");
                Equal(100, character.Health, "auto-equips protection");
                Equal(true, rules.PassesDailyHazardCheck(character), "protection also passes daily check");
                character.Items.Remove(protection);
                character.Protection = null;
                character.FaceID = 10;
                rules.ApplyContinuousHazard(character, 2);
                Equal(hazard == "gas", character.Health == 100, "configured gas immunity");
                Equal(hazard == "gas", rules.PassesDailyHazardCheck(character), "daily immunity");
                Equal(1f, character.GetHazardProtectionRate("gas"), "inventory shows innate 100% gas protection without equipment");
                Equal(0f, character.GetHazardProtectionRate("radiation"), "innate immunity gives no radiation protection");
                Equal(hazard == "gas" ? 0f : 1f, character.GetDangerRate(), "danger assessment includes innate immunity");
                character.FaceID = 0;
                Equal(0f, character.GetHazardProtectionRate("gas"), "ordinary face has no innate protection");
                character.Health = 1;
                Equal(true, rules.ApplyContinuousHazard(character, 4), "lethal exposure");
                Equal(1, character.DeathCalls, "normal death handling invoked");

                character.Health = 100;
                character.DeathCalls = 0;
                owner.Type = Burntime.Remaster.Logic.PlayerType.Ai;
                Equal(false, rules.ApplyContinuousHazard(character, 20), "AI ignores on-map timer");
                Equal(100, character.Health, "AI timed immunity");
                HazardRules.ApplyDaily(character);
                Equal(1, character.DeathCalls, "stationed AI dies at daily check");
                character.Health = 100;
                character.DeathCalls = 0;
                owner.Group.Add(character);
                HazardRules.ApplyDaily(character);
                Equal(100, character.Health, "active AI party skips daily check");

                owner.Type = Burntime.Remaster.Logic.PlayerType.Human;
                owner.Traveling = true;
                HazardRules.ApplyDaily(character);
                Equal(100, character.Health, "departing human party skips daily check");
                rules.ApplyContinuousHazard(character, 2);
                Equal(true, character.Health < 100, "departing humans still take timed damage");

                owner.Traveling = false;
                character.Health = 100;
                character.Food = 9;
                character.Water = 5;
                rules.TurnEmployedCharacter(character);
                Equal(1, character.DeathCalls, "normal daily turn kills an exposed human party");
                return 0;
            });
        }
    }

    static Item TestItem(Burntime.Framework.States.StateManager manager,
        string id, int food = 0, int damage = 0, int heal = 0, float trade = 0, int[]? damageValues = null, int ammo = 0, int defense = 0)
    {
        var data = new Burntime.Remaster.Logic.Data.ItemTypeData
        {
            DataName = id, FoodValue = food, DamageValue = damage, HealValue = heal, TradeValue = trade,
            DamageValues = damageValues ?? Array.Empty<int>(), AmmoValue = ammo, DefenseValue = defense,
            Class = Array.Empty<string>(), Protection = Array.Empty<string>()
        };
        var type = manager.Create<Burntime.Remaster.Logic.ItemType>(data);
        return manager.Create<Item>(type);
    }

    static int[] ReadDamage(string? weapon, RuleSetId rule = RuleSetId.Dos)
    {
        var config = new Burntime.Platform.IO.ConfigFile();
        config.Open(System.IO.File.OpenRead(ResourceFile(new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File)));
        return config[weapon ?? ""].GetInts("damage");
    }

    static IEnumerable<Case<int>> OriginalCombatCases()
    {
        (string? Weapon, int[][] Tiers)[] tables =
        {
            (null, new[]
            {
                new[] { 2, 3, 4, 6 }, new[] { 6, 8, 10, 11 },
                new[] { 8, 10, 12, 15 }, new[] { 9, 11, 13, 15 }
            }),
            ("item_knife", new[]
            {
                new[] { 9, 11, 14, 16 }, new[] { 10, 13, 16, 18 },
                new[] { 12, 14, 17, 20 }, new[] { 14, 16, 18, 20 }
            }),
            ("item_axe", new[]
            {
                new[] { 9, 11, 15, 20 }, new[] { 9, 12, 17, 22 },
                new[] { 10, 14, 19, 25 }, new[] { 13, 17, 21, 25 }
            }),
            ("item_pitchfork", new[]
            {
                new[] { 4, 7, 10, 20 }, new[] { 5, 8, 11, 21 },
                new[] { 6, 9, 12, 25 }, new[] { 9, 12, 16, 25 }
            }),
            ("item_loaded_rifle", new[]
            {
                new[] { 0, 6, 14, 15 }, new[] { 0, 10, 17, 20 },
                new[] { 5, 13, 19, 25 }, new[] { 8, 16, 25, 40 }
            })
        };

        foreach ((string? weapon, int[][] tiers) in tables)
        foreach (int tierWidth in new[] { 25, 26 })
        for (int tier = 0; tier < tiers.Length; tier++)
        for (int roll = 0; roll < tiers[tier].Length; roll++)
        {
            string weaponName = weapon ?? "unarmed";
            int capturedTier = tier;
            int capturedRoll = roll;
            yield return Int(
                $"{weaponName}, width {tierWidth}, tier {tier}, roll {roll}",
                tiers[tier][roll],
                () => RuleFormulas.OriginalDamage(
                    ReadDamage(weapon, tierWidth == 25 ? RuleSetId.Dos : RuleSetId.Amiga), capturedTier * tierWidth, tierWidth, capturedRoll));
        }

        yield return Int("DOS tier clamps above 99", 20,
            () => RuleFormulas.OriginalDamage(ReadDamage("item_knife"), 500, 25, 3));
        yield return Int("unknown weapon is unarmed", 2,
            () => RuleFormulas.OriginalDamage(ReadDamage(null), 0, 25, 0));
    }

    static IEnumerable<Case<int>> CombatPreviewCases()
    {
        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
            yield return Int($"{rule} trader uses configured attack instead of stock", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var trader = manager.Create(() => new Burntime.Remaster.Logic.Character());
                trader.Class = CharClass.Trader;
                trader.Experience = 99;
                trader.Items = manager.Create<ItemList>();
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                trader.Items.Add(rifle);
                trader.Weapon = rifle;
                var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
                defender.Items = manager.Create<ItemList>();
                defender.Health = 100;
                var rules = GameRulesRegistry.Get(rule);
                var preview = rules.GetCombatPreview(trader);
                Equal(rule == RuleSetId.Amiga ? 9 : 8, preview.Minimum,
                    "configured trader minimum");
                Equal(rule == RuleSetId.Amiga ? 15 : 40, preview.Maximum,
                    "configured trader maximum");
                rules.DealAttackDamage(trader, defender, true);
                Equal(6, rifle.AmmoValue, "trader stock is not used as a weapon");
                Equal(rifle, trader.Weapon, "trader weapon selection is unchanged");
                return 0;
            });

        foreach (RuleSetId rule in Enum.GetValues<RuleSetId>())
        foreach (CharClass creatureClass in new[] { CharClass.Mutant, CharClass.Dog })
            yield return Int($"{rule} {creatureClass} uses configured attack", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var creature = manager.Create(() => new Burntime.Remaster.Logic.Character());
                creature.Class = creatureClass;
                creature.Experience = 99;
                creature.Items = manager.Create<ItemList>();
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                creature.Items.Add(rifle);
                creature.Weapon = rifle;
                var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
                defender.Items = manager.Create<ItemList>();
                defender.Health = 100;
                var preview = GameRulesRegistry.Get(rule).GetCombatPreview(creature);
                Equal(rule == RuleSetId.Extended ? 4 : 2, preview.Minimum,
                    "configured creature minimum at normal difficulty");
                Equal(rule switch
                    {
                        RuleSetId.Dos => 2,
                        RuleSetId.Amiga => 6,
                        _ => 8,
                    }, preview.Maximum, "configured creature maximum at normal difficulty");
                GameRulesRegistry.Get(rule).DealAttackDamage(creature, defender, true);
                Equal(6, rifle.AmmoValue, "creature inventory is not used as a weapon");
                Equal(rifle, creature.Weapon, "creature weapon selection is unchanged");
                return 0;
            });

        foreach (RuleSetId rule in new[] { RuleSetId.Dos, RuleSetId.Amiga })
            yield return Int($"{rule} preview follows weapon and XP without changing equipment", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
                fighter.Class = CharClass.Boss;
                fighter.Experience = 50;
                fighter.Items = manager.Create<ItemList>();
                var knife = TestItem(manager, "item_knife", damage: 25,
                    damageValues: ReadDamage("item_knife", rule));
                fighter.Items.Add(knife);
                var rules = GameRulesRegistry.Get(rule);
                var preview = rules.GetCombatPreview(fighter);
                Equal(10, preview.Minimum, "boss combat uses half XP");
                Equal(18, preview.Maximum, "weapon range");
                Equal<int?>(null, preview.Defense, "original combat has no defense rating");
                Equal<Item?>(null, fighter.Weapon, "preview does not equip a weapon");
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                fighter.Items.Add(rifle);
                fighter.Weapon = rifle;
                fighter.Experience = 0;
                preview = rules.GetCombatPreview(fighter);
                Equal(0, preview.Minimum, "rifle can roll zero");
                Equal(15, preview.Maximum, "selected rifle range");
                Equal(6, rifle.AmmoValue, "preview does not consume ammunition");
                Equal(rifle, fighter.Weapon, "preview preserves selected weapon");
                var unloaded = TestItem(manager, "item_unloaded_rifle");
                fighter.Items.Remove(rifle);
                fighter.Items.Add(unloaded);
                fighter.Weapon = unloaded;
                preview = rules.GetCombatPreview(fighter);
                Equal(9, preview.Minimum, "unloaded rifle falls back to knife");
                Equal(16, preview.Maximum, "fallback knife range");
                Equal(unloaded, fighter.Weapon, "fallback preview does not change selection");
                return 0;
            });
        yield return Int("unarmed and scalar previews", 0, () =>
        {
            Equal(new CombatPreview(2, 6, null),
                RuleFormulas.OriginalCombatPreview(ReadDamage(null), 0, 25), "unarmed table");
            Equal(new CombatPreview(7, 7, null),
                RuleFormulas.OriginalCombatPreview(new[] { 7 }, 99, 25), "single damage value");
            return 0;
        });
        yield return Int("Extended damage preview and percentage armour", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
            fighter.Items = manager.Create<ItemList>();
            fighter.Class = CharClass.Mercenary;
            fighter.Experience = 50;
            fighter.Items.Add(TestItem(manager, "item_knife", damage: 25,
                damageValues: ReadDamage("item_knife", RuleSetId.Extended)));
            var rules = GameRulesRegistry.Get(RuleSetId.Extended);
            var preview = rules.GetCombatPreview(fighter);
            Equal(new CombatPreview(12, 20, null), preview, "damage range; zero defence hidden");
            var sweater = TestItem(manager, "sweater", defense: 5);
            var jacket = TestItem(manager, "jacket", defense: 20);
            fighter.Items.Add(sweater);
            fighter.Items.Add(jacket);
            fighter.Protection = sweater;
            preview = rules.GetCombatPreview(fighter);
            Equal<int?>(20, preview.Defense, "strongest clothing, without stacking");
            Equal(sweater, fighter.Protection, "preview does not change equipment");
            fighter.Experience = 0;
            Equal<int?>(20, rules.GetCombatPreview(fighter).Defense, "armour independent of XP");
            return 0;
        });
        yield return Int("Extended local and off-screen damage share rolls and armour", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var attacker = manager.Create(() => new Burntime.Remaster.Logic.Character());
            attacker.Items = manager.Create<ItemList>();
            attacker.Class = CharClass.Mercenary;
            attacker.Experience = 50;
            var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                damageValues: ReadDamage("item_loaded_rifle", RuleSetId.Extended));
            attacker.Items.Add(rifle);
            var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
            defender.Items = manager.Create<ItemList>();
            defender.Class = CharClass.Boss;
            defender.Experience = 99;
            defender.Health = 100;
            defender.Items.Add(TestItem(manager, "jacket", defense: 20));
            var rules = GameRulesRegistry.Get(RuleSetId.Extended);
            Burntime.Platform.Math.SetRandomSeed(42);
            rules.DealAttackDamage(attacker, defender, true);
            int local = 100 - defender.Health;
            Equal(5, rifle.AmmoValue, "local shot consumes ammunition");
            Burntime.Platform.Math.SetRandomSeed(42);
            int strategic = rules.RollStrategicDamage(null!, null!, attacker, defender);
            Equal(local, strategic, "same seeded local and strategic damage");
            Equal(4, rifle.AmmoValue, "off-screen shot consumes ammunition");
            defender.Experience = 0;
            Burntime.Platform.Math.SetRandomSeed(42);
            Equal(local, rules.RollStrategicDamage(null!, null!, attacker, defender), "defender XP grants no armour");
            return 0;
        });
    }

    static IEnumerable<Case<float>> StrategicCombatCases()
    {
        yield return Float("original detailed strength", 19.25f,
            () => RuleFormulas.OriginalStrategicStrength(
                ReadDamage("item_knife"), 25, 25, 50, detailed: true));
        yield return Float("original coarse strength", 24.25f,
            () => RuleFormulas.OriginalStrategicStrength(
                ReadDamage("item_knife"), 25, 25, 50, detailed: false));
    }

    static IEnumerable<Case<int>> ExtendedStrategicDamageCases()
    {
        foreach (var (raw, expected) in new[] { (12, 10), (14, 11), (17, 14), (20, 16) })
            yield return Int($"20 percent armour against {raw}", expected,
                () => RuleFormulas.ApplyArmour(raw, 20));
        yield return Int("no armour", 20, () => RuleFormulas.ApplyArmour(20, 0));
        yield return Int("rifle miss stays zero", 0, () => RuleFormulas.ApplyArmour(0, 25));
        yield return Int("weak hit remains meaningful", 2, () => RuleFormulas.ApplyArmour(2, 25));
        yield return Int("successful hits retain minimum one", 1, () => RuleFormulas.ApplyArmour(1, 25));
    }

    static IEnumerable<Case<int>> ItemGenerationCases()
    {
        yield return Generation("default rate", "", 1, 1, new[] { "food" }, new[] { "rare" });
        yield return Generation("single count", "4", 4, 4, new[] { "food" }, new[] { "rare" });
        yield return Generation("range", "1 3", 1, 3, new[] { "food" }, new[] { "rare" });
        yield return Generation("larger range", "4 9", 4, 9, new[] { "food" }, new[] { "rare" });
        yield return Generation("inverted range", "9 4", 9, 9, new[] { "food" }, new[] { "rare" });
        yield return Generation("negative values", "-2 -1", 0, 0, new[] { "food" }, new[] { "rare" });
        yield return Generation("invalid minimum", "x 3", 1, 3, new[] { "food" }, new[] { "rare" });
        yield return Generation("invalid maximum", "3 x", 3, 3, new[] { "food" }, new[] { "rare" });
    }

    static Case<int> Generation(
        string name,
        string rate,
        int minimum,
        int maximum,
        string[] include,
        string[] exclude) => Int(name, 0, () =>
        {
            GameSettings.ItemGeneration generation = GameSettings.ItemGeneration.FromString(
                "food -rare", rate);
            Equal(minimum, generation.Minimum, "minimum");
            Equal(maximum, generation.Maximum, "maximum");
            Equal(string.Join(',', include), string.Join(',', generation.Include), "include");
            Equal(string.Join(',', exclude), string.Join(',', generation.Exclude), "exclude");
            return 0;
        });

    static IEnumerable<Case<int>> ProfileParsingCases()
    {
        yield return Int("DOS rules case-insensitive", (int)RuleSetId.Dos,
            () => (int)GameDefinitions.ParseRules("dOs"));
        yield return Int("invalid rules fallback", (int)RuleSetId.Amiga,
            () => (int)GameDefinitions.ParseRules("invalid", RuleSetId.Amiga));
        yield return Int("numeric undefined rules fallback", (int)RuleSetId.Extended,
            () => (int)GameDefinitions.ParseRules("99"));
        yield return Int("Amiga AI case-insensitive", (int)AiProfileId.Amiga,
            () => (int)GameDefinitions.ParseAi("aMiGa"));
        yield return Int("missing AI defaults Extended", (int)AiProfileId.Extended,
            () => (int)GameDefinitions.ParseAi(null));
        yield return Int("numeric undefined AI fallback", (int)AiProfileId.Extended,
            () => (int)GameDefinitions.ParseAi("99"));
    }

    static IEnumerable<Case<int>> RuleRegistryCases()
    {
        yield return Int("DOS registry", (int)RuleSetId.Dos,
            () => (int)GameRulesRegistry.Get(RuleSetId.Dos).Id);
        yield return Int("Amiga registry", (int)RuleSetId.Amiga,
            () => (int)GameRulesRegistry.Get(RuleSetId.Amiga).Id);
        yield return Int("Extended registry", (int)RuleSetId.Extended,
            () => (int)GameRulesRegistry.Get(RuleSetId.Extended).Id);
    }

    static Case<int> Int(string name, int expected, Func<int> actual) => new(name, expected, actual);
    static Case<bool> Bool(string name, bool expected, Func<bool> actual) => new(name, expected, actual);
    static Case<float> Float(string name, float expected, Func<float> actual) => new(name, expected, actual);

    static void Equal<T>(T expected, T actual, string field)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{field}: expected {expected}, got {actual}");
    }

    readonly record struct Case<T>(string Name, T Expected, Func<T> Actual);
}
