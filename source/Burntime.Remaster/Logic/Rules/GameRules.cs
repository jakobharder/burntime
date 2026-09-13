using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Classic.Logic.Generation;
using Burntime.Data.BurnGfx.Save;
using Burntime.Framework;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal sealed class GameRules
{
    public RuleSet Id { get; }

    GameSettings? settings;
    public GameSettings Settings => settings ??= new(GameDefinitions.Get(Id).SettingsPath);

    public GameRules(RuleSet id)
    {
        Id = id;
    }

    public void InitializeHumanPlayer(Player player, int playerIndex, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        if (settings.PlayerSetupRules.Equals("remaster_settings", StringComparison.OrdinalIgnoreCase))
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
            return;
        }

        var character = source.Characters[playerIndex];
        player.Character.Health = character.Health;
        player.Character.Experience = character.Experience;
        player.Character.Food = character.Food;
        player.Character.Water = character.Water;
        player.BaseExperience = character.Experience;

        player.Character.Items.Clear();
        foreach (var info in source.Items.Where(info =>
            info.OwnerType == ItemOwnerType.Character && info.OwnerId == playerIndex))
        {
            Item item = ((ClassicGame)player.Container.Root).ItemTypes[info.SpriteId].Generate();
            player.Character.Items.Add(item);
        }
    }

    public void SetStartLocations(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source) =>
        StartLocationPlacement.Apply(game, settings);

    public void PopulateInitialItems(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        if (settings.InitialItemRules.Equals("remaster_spawning", StringComparison.OrdinalIgnoreCase))
        {
            var spawner = new ItemSpawner(game, source, settings);
            spawner.SpawnAtPlayerLocation();
            spawner.SpawnInAllLocations();
            spawner.SpawnRegionItems();
            return;
        }

        foreach (var info in source.Items)
        {
            if (info.OwnerType == ItemOwnerType.Pool)
                continue;
            if (info.OwnerType == ItemOwnerType.Character && info.OwnerId is >= 0 and < 4)
                continue;

            Item item = game.ItemTypes[info.SpriteId].Generate();
            if (info.OwnerType == ItemOwnerType.Character && info.OwnerId >= 4 &&
                info.OwnerId < game.World.AllCharacters.Count)
            {
                game.World.AllCharacters[info.OwnerId].Items.Add(item);
            }
            else if (info.OwnerType == ItemOwnerType.Room && info.LocationId >= 0 &&
                info.LocationId < game.World.Locations.Count)
            {
                Location location = game.World.Locations[info.LocationId];
                if (info.RoomId < location.Rooms.Count)
                    location.Rooms[info.RoomId].Items.Add(item);
            }
            else if (info.OwnerType == ItemOwnerType.Dropped && info.DroppedLocationId >= 0 &&
                info.DroppedLocationId < game.World.Locations.Count)
            {
                game.World.Locations[info.DroppedLocationId].Items.DropAt(
                    item, info.DroppedPosition);
            }
        }
    }

    public void InitializeRecruit(Character recruit, Player boss, Item? payment)
    {
        if (!Settings.RecruitSupplyRules.Equals(
            "remaster_difficulty", StringComparison.OrdinalIgnoreCase))
        {
            recruit.Food = 9;
            recruit.Water = 5;
            return;
        }

        ClassicGame game = (ClassicGame)recruit.Container.Root;
        int difficulty = 2 - game.World.Difficulty;
        if (boss.Type == PlayerType.Ai)
        {
            recruit.Food = 5;
            recruit.Water = 5;
            return;
        }

        if (payment?.FoodValue != 0)
            recruit.Food = Math.Min(recruit.MaxFood, recruit.Food + payment.FoodValue);
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

    public void InitializeTraderInventory(Trader trader)
    {
        if (Settings.TraderInventoryRules.Equals(
            "remaster_random", StringComparison.OrdinalIgnoreCase))
            trader.RandomizeInventory();
    }

    public void TurnTrader(Trader trader)
    {
        if (Settings.TraderRefreshRules.Equals("remaster_random", StringComparison.OrdinalIgnoreCase))
            trader.TurnExtendedTrader();
        else
            OriginalTraderRefresh.Turn(trader, Settings.TraderRefreshRules);
    }

    public void TurnTraders(IEnumerable<Trader> traders) =>
        OriginalTraderRefresh.Turn(traders, Settings.TraderRefreshRules);

    public void ProcessFoodProduction(Location location, Production.Rate production)
    {
        if (Settings.FoodProductionRules.Equals(
            "amiga_store_all", StringComparison.OrdinalIgnoreCase))
        {
            location.ProduceFood(production.FoodPerDay);
            return;
        }

        int available = production.FoodPerDay;
        Character[] employees = location.CampNPC
            .Where(character => character.Player == location.Player && !character.IsDead)
            .ToArray();
        foreach (Character employee in employees)
        {
            if (available == 0)
                break;
            if (employee.Food < employee.MaxFood)
            {
                employee.Food++;
                available--;
            }
        }
        location.ProduceFood(available);
    }

    public int CalculateBossExperience(Player player, ClassicGame game) =>
        RuleFormulas.BossExperience(Settings.BossExperienceRules, player, game);

    public bool MeetsRecruitmentExperience(Character boss, Character recruit) =>
        RuleFormulas.CanRecruit(Settings.RecruitmentRules, boss.Experience, recruit.Experience);

    public void TurnEmployedCharacter(Character character, int? naturalHealingThreshold = null)
    {
        if (Settings.SurvivalRules.Equals(
            "remaster_supply_pool", StringComparison.OrdinalIgnoreCase))
        {
            character.TurnExtendedEmployed(naturalHealingThreshold);
            HazardRules.ApplyDaily(character);
            return;
        }

        TurnOriginalEmployed(character, naturalHealingThreshold);
    }

    public bool PassesDailyHazardCheck(Character character) => HazardRules.PassesDailyCheck(character);

    public bool ApplyContinuousHazard(Character character, float elapsed) =>
        HazardRules.ApplyContinuous(character, elapsed);

    public int CalculateDoctorResult(int health, IItemCollection payment) =>
        RuleFormulas.DoctorResult(health, payment.GetHealValue(),
            Settings.DoctorHealingFactor, Settings.DoctorHealthCap);

    public int CalculateRestaurantValue(IItemCollection payment) =>
        Settings.ServiceValueRules.Equals("remaster_nutrition", StringComparison.OrdinalIgnoreCase)
            ? payment.GetEatValue()
            : OriginalServiceValue(payment);

    public int CalculatePubValue(IItemCollection payment) =>
        Settings.ServiceValueRules.Equals("remaster_nutrition", StringComparison.OrdinalIgnoreCase)
            ? payment.GetDrinkValue()
            : OriginalServiceValue(payment);

    public bool AcceptTrade(IItemCollection playerOffer, IItemCollection traderOffer, int difficulty) =>
        RuleFormulas.AcceptTrade(playerOffer, traderOffer, Settings.GetBarterFactor(difficulty));

    public int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump) =>
        RuleFormulas.WaterOutput(
            Settings.WaterOutputRules, baseOutput, handPump, industrialPump);

    public void DealAttackDamage(Character attacker, Character defender, bool useAmmo) =>
        defender.Health -= RollAttackDamage(attacker, defender, useAmmo);

    public int GetExperienceTier(int experience) =>
        RuleFormulas.ExperienceTier(experience, Settings.CombatTierWidth);

    public CombatPreview GetCombatPreview(Character character) =>
        TableCombat.Preview(character, Settings, UsesArmour);

    public float CalculateStrategicStrength(Character character, bool detailed) =>
        TableCombat.Strength(character, Settings, detailed, UsesArmour);

    public int RollStrategicDamage(
        ClassicGame game,
        Player attackerOwner,
        Character attacker,
        Character defender) =>
        RollAttackDamage(attacker, defender, useAmmo: true);

    bool UsesArmour => Settings.CombatRules.Equals(
        "remaster_armour", StringComparison.OrdinalIgnoreCase);

    int RollAttackDamage(Character attacker, Character defender, bool useAmmo) =>
        TableCombat.Roll(attacker, UsesArmour ? defender : null,
            Settings, useAmmo, UsesArmour);

    static int OriginalServiceValue(IItemCollection payment) =>
        RuleFormulas.OriginalServiceValue(payment.Select(item => item.TradeValue));

    static void TurnOriginalEmployed(Character character, int? naturalHealingThreshold)
    {
        bool doctorAvailable = character.HasLocalDoctor;

        int threshold = RuleFormulas.NaturalHealingThreshold(
            doctorAvailable, naturalHealingThreshold);
        if (character.Health >= threshold)
            character.Health += doctorAvailable ? 4 : 2;

        if (!ConsumeWater(character))
        {
            character.Water = 0;
            character.Health -= 25;
            if (character.IsDead)
                return;
        }

        if (!ConsumeFood(character))
        {
            character.Food = 0;
            character.Health -= 25;
        }

        HazardRules.ApplyDaily(character);
    }

    static bool ConsumeWater(Character character)
    {
        character.Water--;
        if (character.Water >= 0)
            return true;

        if (!character.IsWithBoss && character.Location?.Player == character.Player &&
            character.Location.Source.Reserve > 0)
        {
            character.Location.Source.Reserve--;
            character.Water = 0;
            return true;
        }

        (IItemCollection? owner, Item? item) = AccessibleItems(character)
            .Where(entry => entry.Item.WaterValue > 0)
            .OrderBy(entry => entry.Item.WaterValue)
            .ThenBy(entry => entry.Item.ID)
            .Select(entry => ((IItemCollection?)entry.Owner, (Item?)entry.Item))
            .FirstOrDefault();
        if (item == null || owner == null)
            return false;
        character.Water = Math.Min(5, character.Water + item.WaterValue);
        item.MakeEmpty();
        return character.Water >= 0;
    }

    static bool ConsumeFood(Character character)
    {
        character.Food--;
        if (character.Food >= 0)
            return true;

        Item? item = character.FindAccessibleFood(out IItemCollection? owner);
        if (item == null || owner == null)
            return false;
        character.Food = Math.Min(character.MaxFood, character.Food + item.FoodValue);
        owner.Remove(item);
        return character.Food >= 0;
    }

    static IEnumerable<(IItemCollection Owner, Item Item)> AccessibleItems(Character character)
    {
        if (character.IsWithBoss)
        {
            foreach (Character member in character.Player.Group)
                foreach (Item item in member.Items)
                    yield return (member.Items, item);
            yield break;
        }

        if (character.Location != null)
        {
            foreach (Room room in character.Location.Rooms)
                foreach (Item item in room.Items)
                    yield return (room.Items, item);
            foreach (Character member in character.Location.CampNPC.Where(member =>
                member.Player == character.Player))
                foreach (Item item in member.Items)
                    yield return (member.Items, item);
        }
    }
}
