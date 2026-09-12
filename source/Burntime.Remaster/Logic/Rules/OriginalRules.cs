using System;
using System.Linq;
using Burntime.Framework;
using Burntime.Remaster.Logic.Generation;
using Burntime.Data.BurnGfx.Save;

namespace Burntime.Remaster.Logic.Rules;

internal abstract class OriginalRules : IGameRules
{
    const int OriginalItemRecordCount = 1199;
    public abstract RuleSet Id { get; }

    GameSettings? settings;
    public GameSettings Settings => settings ??= new(GameDefinitions.Get(Id).SettingsPath);
    protected abstract void SetOriginalStartLocations(ClassicGame game, GameSettings settings);

    public abstract int CalculateBossExperience(Player player, ClassicGame game);
    public abstract bool MeetsRecruitmentExperience(Character boss, Character recruit);
    public int RollStrategicDamage(
        ClassicGame game,
        Player attackerOwner,
        Character attacker,
        Character defender) => RollAttackDamage(attacker, useAmmo: true);

    public void InitializeHumanPlayer(Player player, int playerIndex, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        var character = source.Characters[playerIndex];
        player.Character.Health = character.Health;
        player.Character.Experience = character.Experience;
        player.Character.Food = character.Food;
        player.Character.Water = character.Water;
        player.BaseExperience = character.Experience;

        player.Character.Items.Clear();
        foreach (var (info, index) in source.Items.Select((info, index) => (info, index)).Where(entry =>
            entry.info.OwnerType == ItemOwnerType.Character && entry.info.OwnerId == playerIndex))
        {
            Item item = ((ClassicGame)player.Container.Root).ItemTypes[info.SpriteId].Generate();
            item.OriginalRecordSlot = index + 1;
            player.Character.Items.Add(item);
        }
    }

    public void SetStartLocations(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
        => SetOriginalStartLocations(game, settings);

    public void PopulateInitialItems(ClassicGame game, GameSettings settings,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        foreach (var (info, index) in source.Items.Select((info, index) => (info, index)))
        {
            if (info.OwnerType == ItemOwnerType.Pool)
                continue;
            if (info.OwnerType == ItemOwnerType.Character && info.OwnerId is >= 0 and < 4)
                continue;

            Item item = game.ItemTypes[info.SpriteId].Generate();
            item.OriginalRecordSlot = index + 1;
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
        recruit.Food = 9;
        recruit.Water = 5;
    }

    public void InitializeTraderInventory(Trader trader)
    {
        // The trader's character-owned GAM.DAT records are its initial stock.
    }

    public abstract void TurnTrader(Trader trader);
    public virtual void TurnTraders(System.Collections.Generic.IEnumerable<Trader> traders)
    {
        foreach (Trader trader in traders) trader.Turn();
    }

    public void ProcessFoodProduction(Location location, Production.Rate production)
    {
        int available = production.FoodPerDay;
        Character[] employees = location.CampNPC
            .Where(character => character.Player == location.Player && !character.IsDead)
            .ToArray();
        while (available > 0)
        {
            Character? hungry = employees
                .Where(character => character.Food < character.MaxFood)
                .OrderBy(character => character.Food)
                .FirstOrDefault();
            if (hungry == null)
                break;
            hungry.Food++;
            available--;
        }
        location.AccumulateOriginalFood(available);
    }

    public bool CanCreateItem(ClassicGame game)
    {
        int characterItems = game.World.AllCharacters.Sum(character => character.Items.Count);
        int roomItems = game.World.Locations.Sum(location =>
            location.Rooms.Sum(room => room.Items.Count));
        int droppedItems = game.World.Locations.Sum(location => location.Items.Count);
        return characterItems + roomItems + droppedItems < OriginalItemRecordCount;
    }

    public void TurnEmployedCharacter(Character character)
    {
        ClassicGame game = (ClassicGame)character.Container.Root;
        bool amigaAi = game.UsesAiProfile(character.Player, AiProfile.Amiga);
        bool amigaActiveParty = amigaAi && character.IsWithBoss;
        bool doctorAvailable = character.HasLocalDoctor;

        int threshold = doctorAvailable || amigaAi ? 50 : 70;
        if (character.Health >= threshold)
            character.Health += doctorAvailable ? 4 : 2;

        if (!amigaActiveParty && !ConsumeWater(character))
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

    public bool PassesDailyHazardCheck(Character character) => HazardRules.PassesDailyCheck(character);

    public bool ApplyContinuousHazard(Character character, float elapsed) =>
        HazardRules.ApplyContinuous(character, elapsed);

    public int CalculateDoctorResult(int health, IItemCollection payment) =>
        RuleFormulas.DoctorResult(health, payment.GetHealValue(),
            Settings.DoctorHealingFactor, Settings.DoctorHealthCap);

    public int CalculateRestaurantValue(IItemCollection payment) => ServiceValue(payment);
    public int CalculatePubValue(IItemCollection payment) => ServiceValue(payment);

    public bool AcceptTrade(IItemCollection playerOffer, IItemCollection traderOffer, int difficulty) =>
        RuleFormulas.AcceptTrade(playerOffer, traderOffer, Settings.GetBarterFactor(difficulty));

    public virtual int CalculateWaterOutput(int baseOutput, bool handPump, bool industrialPump) =>
        RuleFormulas.OriginalWaterOutput(baseOutput, handPump, industrialPump);

    public void DealAttackDamage(
        Character attacker,
        Character defender,
        bool useAmmo)
    {
        defender.Health -= RollAttackDamage(attacker, useAmmo);
    }

    public int GetExperienceTier(int experience) =>
        RuleFormulas.ExperienceTier(experience, Settings.CombatTierWidth);

    public CombatPreview GetCombatPreview(Character character) =>
        TableCombat.Preview(character, Settings, armour: false);

    public float CalculateStrategicStrength(Character character, bool detailed) =>
        TableCombat.Strength(character, Settings, detailed, armour: false);

    int RollAttackDamage(Character attacker, bool useAmmo) =>
        TableCombat.Roll(attacker, null, Settings, useAmmo, armour: false);

    static int ServiceValue(IItemCollection payment) =>
        RuleFormulas.OriginalServiceValue(payment.Select(item => item.TradeValue));

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
        character.Water = System.Math.Min(5, character.Water + item.WaterValue);
        item.MakeEmpty();
        return character.Water >= 0;
    }

    static bool ConsumeFood(Character character)
    {
        character.Food--;
        if (character.Food >= 0)
            return true;

        (IItemCollection? owner, Item? item) = AccessibleItems(character)
            .Where(entry => entry.Item.FoodValue > 0)
            .OrderBy(entry => entry.Item.FoodValue)
            .ThenBy(entry => entry.Item.ID)
            .Select(entry => ((IItemCollection?)entry.Owner, (Item?)entry.Item))
            .FirstOrDefault();
        if (item == null || owner == null)
            return false;
        character.Food = System.Math.Min(character.MaxFood, character.Food + item.FoodValue);
        owner.Remove(item);
        return character.Food >= 0;
    }

    static System.Collections.Generic.IEnumerable<(IItemCollection Owner, Item Item)> AccessibleItems(
        Character character)
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
