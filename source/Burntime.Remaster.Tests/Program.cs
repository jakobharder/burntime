using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

static class Program
{
    static int passed;
    static int failed;

    static int Main()
    {
        Burntime.Platform.IO.FileSystem.AddPackage("classic", System.IO.Path.GetDirectoryName(ResourceFile("rules/dos/items.txt")) + "/../..");
        Run("AI preparation and Amiga recovery", AiPreparationRecoveryTests.AiPreparationRecoveryCases());
        Run("economy observations", EconomyObservationTests.EconomyObservationCases());
        Run("DOS conflict attrition", HeadlessSimulationTests.DosConflictAttritionCases());
        Run("Amiga food attrition", HeadlessSimulationTests.AmigaFoodAttritionCases());
        Run("recovery and frontier equipment", EquipmentPlanningTests.RecoveryAndEquipmentCases());
        Run("strategic encounters", StrategicEncounterTests.StrategicEncounterCases());
        Run("local combat lifecycle", CombatResolverTests.LocalCombatCases());
        Run("local combat encounters", CombatResolverTests.EncounterCases());
        Run("doctor locality", CharacterTests.DoctorLocalityCases());
        Run("combat experience", RuleFormulasTests.CombatExperienceCases());
        Run("experience tiers", RuleFormulasTests.ExperienceTierCases());
        Run("boss experience", RuleFormulasTests.BossExperienceCases());
        Run("recruitment", RuleFormulasTests.RecruitmentCases());
        Run("doctor healing", RuleFormulasTests.DoctorCases());
        Run("configured rules", GameRulesTests.ConfiguredRuleCases());
        Run("radio intelligence", RadioIntelTests.RadioCases());
        Run("item functions", ItemFunctionTests.FunctionCases());
        Run("restaurant and pub value", RuleFormulasTests.ServiceValueCases());
        Run("original barter", RuleFormulasTests.BarterCases());
        Run("original defenders", AiStateOperationsTests.DefenderCases());
        Run("water output", RuleFormulasTests.WaterOutputCases());
        Run("original regression cases", OriginalRulesRegressionTests.OriginalRegressionCases());
        Run("original stock and cleanup", OriginalStockTests.OriginalStockCases());
        Run("locations", LocationTests.LocationCases());
        Run("production policy and compatibility", ProductionPolicyTests.ProductionPolicyCases());
        Run("configured ammunition lifecycle", ProductionPolicyTests.AmmunitionLifecycleCases());
        Run("continuous hazards", HazardRulesTests.ContinuousHazardCases());
        Run("original combat damage", TableCombatTests.OriginalCombatCases());
        Run("inventory combat preview", TableCombatTests.CombatPreviewCases());
        Run("strategic combat", TableCombatTests.StrategicCombatCases());
        Run("extended strategic damage", TableCombatTests.ExtendedStrategicDamageCases());
        Run("item generation ranges", GameSettingsTests.ItemGenerationCases());
        Run("profile parsing", GameDefinitionsTests.ProfileParsingCases());
        Run("rule registry", GameDefinitionsTests.RuleRegistryCases());
        Run("resolution scaling", ResolutionTests.ResolutionCases());
        Run("font indicators", FontIndicatorTests.IndicatorCases());
        Run("tooltip text substitution", TooltipTextTests.SubstitutionCases());
        Run("construction feedback", ConstructionFeedbackTests.AvailabilityCases());

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

    internal static (ClassicGame Game, Burntime.Remaster.Logic.Player Attacker, Burntime.Remaster.Logic.Player Defender,
        Burntime.Framework.States.StateManager Manager) EncounterPlayers(RuleSet rule)
    {
        var manager = new Burntime.Framework.States.StateManager(null!);
        var game = manager.Create(() => new ClassicGame());
        manager.Root = game;
        game.SetRules(rule);
        var attacker = manager.Create<Burntime.Remaster.Logic.Player>(new object[] { 0 });
        var defender = manager.Create<Burntime.Remaster.Logic.Player>(new object[] { 1 });
        attacker.Character = EncounterFighter(manager, attacker, 100, 1);
        defender.Character = EncounterFighter(manager, defender, 100, 1);
        return (game, attacker, defender, manager);
    }

    internal static HazardCharacter EncounterFighter(Burntime.Framework.States.StateManager manager,
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

    internal static string ResourceFile(string relativePath)
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

    internal sealed class HazardPlayer : Burntime.Remaster.Logic.Player
    {
        public bool Traveling;
        public void SetPrevious(Burntime.Remaster.Logic.Location value) => previousLocation = value;
        public void SetDestination(Burntime.Remaster.Logic.Location value) => destination = value;
        public override bool IsTraveling => Traveling;
    }

    internal sealed class HazardCharacter : Burntime.Remaster.Logic.Character
    {
        public int DeathCalls;
        public float ExactHealth => health;
        public void Place(Burntime.Remaster.Logic.Location value) => location = value;
        public override void Die() { DeathCalls++; health = 0; }
    }

    internal sealed class HazardProtectionType : Burntime.Remaster.Logic.ItemType
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

    internal static Item TestItem(Burntime.Framework.States.StateManager manager,
        string id, int food = 0, int damage = 0, int heal = 0, float trade = 0,
        int[]? damageValues = null, int ammo = 0, int defense = 0, int attackRange = 0,
        ItemFunction functions = ItemFunction.None)
    {
        var data = new Burntime.Remaster.Logic.Data.ItemTypeData
        {
            DataName = id, FoodValue = food, DamageValue = damage, HealValue = heal, TradeValue = trade,
            DamageValues = damageValues ?? Array.Empty<int>(), AmmoValue = ammo, DefenseValue = defense,
            AttackRange = attackRange, Functions = functions,
            Class = Array.Empty<string>(), Protection = Array.Empty<string>()
        };
        var type = manager.Create<Burntime.Remaster.Logic.ItemType>(data);
        return manager.Create<Item>(type);
    }

    internal static Case<int> Int(string name, int expected, Func<int> actual) => new(name, expected, actual);
    internal static Case<bool> Bool(string name, bool expected, Func<bool> actual) => new(name, expected, actual);
    internal static Case<float> Float(string name, float expected, Func<float> actual) => new(name, expected, actual);

    internal static void Equal<T>(T expected, T actual, string field)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{field}: expected {expected}, got {actual}");
    }

    internal readonly record struct Case<T>(string Name, T Expected, Func<T> Actual);
}
