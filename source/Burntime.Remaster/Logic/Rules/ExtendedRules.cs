using Burntime.Framework;
using Burntime.Remaster.Logic.Generation;
using Burntime.Classic.Logic.Generation;
using Burntime.Data.BurnGfx.Save;
using System.Linq;

namespace Burntime.Remaster.Logic.Rules;

internal sealed class ExtendedRules : IGameRules
{
    public RuleSetId Id => RuleSetId.Extended;

    GameSettings? settings;
    public GameSettings Settings => settings ??= new(GameDefinitions.Get(Id).SettingsPath);

    public void InitializeHumanPlayer(Player player, int playerIndex, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        player.Character.Health = settings.StartHealth;
        player.Character.Experience = settings.StartExperience;
        player.Character.Food = settings.StartFood;
        player.Character.Water = settings.StartWater;
        player.BaseExperience = settings.StartExperience;
        player.Character.Items.Clear();
        foreach (string item in settings.StartItems)
            player.Character.Items.Add(((ClassicGame)player.Container.Root)
                .ItemTypes[item].Generate());
    }

    public void SetStartLocations(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
        => StartLocationPlacement.ApplyRegional(game, settings);

    public void PopulateInitialItems(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        var spawner = new ItemSpawner(game, source, settings);
        spawner.SpawnAtPlayerLocation();
        spawner.SpawnInAllLocations();
        spawner.SpawnRegionItems();
    }

    public void InitializeRecruit(Character recruit, Player boss, Item? payment)
    {
        ClassicGame game = (ClassicGame)recruit.Container.Root;
        int difficulty = 2 - game.World.Difficulty;
        if (boss.Type == PlayerType.Ai)
        {
            recruit.Food = 5;
            recruit.Water = 5;
            return;
        }

        if (payment?.FoodValue != 0)
            recruit.Food = System.Math.Min(recruit.MaxFood, recruit.Food + payment.FoodValue);
        if (recruit.Food < difficulty)
            recruit.Food = difficulty;

        (int minimum, int maximum) = game.World.Difficulty switch
        {
            0 => (3, 5),
            1 => (1, 4),
            _ => (0, 2)
        };
        recruit.Water = Burntime.Platform.Math.Random.Next(minimum, maximum + 1);
    }

    public void InitializeTraderInventory(Trader trader) => trader.RandomizeInventory();
    public void TurnTrader(Trader trader) => trader.TurnExtendedTrader();
    public void ProcessFoodProduction(Location location, Production.Rate production) =>
        location.ProcessExtendedFoodProduction(production);
    public bool CanCreateItem(ClassicGame game) => true;

    public int CalculateBossExperience(Player player, ClassicGame game) =>
        RuleFormulas.ExtendedBossExperience(
            player.BaseExperience, player.GetOwnedLocationCount(game.World));

    public bool MeetsRecruitmentExperience(Character boss, Character recruit) =>
        RuleFormulas.DosCanRecruit(boss.Experience, recruit.Experience);

    public void TurnEmployedCharacter(Character character)
    {
        character.TurnExtendedEmployed(UsesAmigaSurvivalBehavior(character));
        HazardRules.ApplyDaily(character);
    }

    public bool PassesDailyHazardCheck(Character character) => HazardRules.PassesDailyCheck(character);

    public bool ApplyContinuousHazard(Character character, float elapsed) =>
        HazardRules.ApplyContinuous(character, elapsed);

    static bool UsesAmigaSurvivalBehavior(Character character) =>
        ((ClassicGame)character.Container.Root).UsesAiProfile(
            character.Player, AiProfileId.Amiga);

    public int CalculateDoctorResult(int health, IItemCollection payment) =>
        RuleFormulas.DoctorResult(health, payment.GetHealValue(),
            Settings.DoctorHealingFactor, Settings.DoctorHealthCap);

    public int CalculateRestaurantValue(IItemCollection payment) => payment.GetEatValue();
    public int CalculatePubValue(IItemCollection payment) => payment.GetDrinkValue();

    public bool AcceptTrade(IItemCollection playerOffer, IItemCollection traderOffer, int difficulty) =>
        RuleFormulas.AcceptTrade(playerOffer, traderOffer, Settings.GetBarterFactor(difficulty));

    public int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump)
        => RuleFormulas.ExtendedWaterOutput(baseOutput, handPump, industrialPump);

    public void DealAttackDamage(
        Character attacker,
        Character defender,
        bool useAmmo) =>
        defender.Health -= TableCombat.Roll(attacker, defender, Settings, useAmmo, armour: true);

    public int GetExperienceTier(int experience) =>
        RuleFormulas.ExperienceTier(experience, Settings.CombatTierWidth);

    public CombatPreview GetCombatPreview(Character character) =>
        TableCombat.Preview(character, Settings, armour: true);

    public float CalculateStrategicStrength(Character character, bool detailed) =>
        TableCombat.Strength(character, Settings, detailed, armour: true);

    public int RollStrategicDamage(
        ClassicGame game,
        Player attackerOwner,
        Character attacker,
        Character defender) =>
        TableCombat.Roll(attacker, defender, Settings, useAmmo: true, armour: true);
}
