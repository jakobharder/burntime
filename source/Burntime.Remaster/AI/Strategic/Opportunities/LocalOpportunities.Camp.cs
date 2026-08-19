using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class LocalOpportunities
{
    internal const int CampWeaponReserve = 2;

    public static bool ShouldPreferProductionAtCamp(ClassicAiState state, Location location) =>
        location.Danger == null && !ReinforcementTask.IsThreatened(state, location);

    internal static void MaintainCurrentCamp(ClassicAiState state)
    {
        Location camp = state.Current;
        camp.AutoSelectFoodProduction(onlyIfStarving: false);
        TradeTask.CollectRedundantProductionTools(state, camp);
        InstallProductionFromPool(state, camp);
        camp.AutoSelectFoodProduction(onlyIfStarving: false);
        InstallLoosePump(state, camp);
        ConstructForCamp(state, camp);
        CollectProducedSurplus(state, camp);
        CollectStoredTradeGoods(state, camp);
    }

    internal static void ProvisionGroupFromCampSurplus(ClassicAiState state, Location camp)
    {
        int consumedItems = 0;
        int consumedFood = 0;
        while (state.Player.Group.Any(character => character.Food < character.MaxFood))
        {
            int storedFood = camp.Rooms.Sum(room => room.Items.Count(item => item.FoodValue > 0));
            int campReserve = System.Math.Max(2,
                CampEconomy.LivingGuardCount(camp, state.Player));

            List<(IItemCollection Owner, Item Item)> candidates = state.Player.Group
                .SelectMany(character => character.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => ((IItemCollection)character.Items, item)))
                .ToList();
            if (storedFood > campReserve)
                candidates.AddRange(camp.Rooms.SelectMany(room => room.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => ((IItemCollection)room.Items, item))));
            (IItemCollection Owner, Item Item) candidate = candidates
                .OrderBy(entry => entry.Item.FoodValue)
                .ThenBy(entry => entry.Item.TradeValue)
                .FirstOrDefault();
            if (candidate.Item == null)
                break;

            Item food = candidate.Item;
            IItemCollection owner = candidate.Owner;
            int capacity = state.Player.Group.Sum(character => character.MaxFood - character.Food);
            if (food.FoodValue > capacity)
                break;

            state.Player.Group.Eat(null, food.FoodValue);
            owner.Remove(food);
            consumedItems++;
            consumedFood += food.FoodValue;
        }

        if (consumedItems > 0)
            AiTelemetry.Report(state.Player,
                $"provisioned group at {camp.Title} before collecting exports " +
                $"({consumedItems} food items, {consumedFood} food value)");
    }

    internal static void InstallProductionFromPool(ClassicAiState state, Location camp)
    {
        if (state.HasSettlementPlan && ExpansionTask.ShouldReserveProductionTool(state))
            return;

        string[] products = camp.ValidProductions
            .Where(production => TradeTask.NeedsProductionResult(state, camp, production.Produce.ID))
            .OrderByDescending(TradeTask.ProductionTradePriority)
            .Select(production => production.Produce.ID)
            .ToArray();
        if (products.Length == 0 || !state.Pool.HasTrap(products))
            return;
        Item tool = state.Pool.GetBestTrap(products);
        if (tool == null)
            return;
        camp.StoreItemRandom(tool);
        camp.AutoSelectFoodProduction(onlyIfStarving: false);
        AiTelemetry.Report(state.Player, $"installed {tool.ID} for food production at {camp.Title}");
    }

    internal static bool HasPortableBestProduction(ClassicAiState state, Location camp)
    {
        Production best = camp.ValidProductions
            .OrderByDescending(TradeTask.ProductionTradePriority)
            .ThenByDescending(production => production.Produce.FoodValue)
            .FirstOrDefault();
        if (best == null || CampEconomy.ProductionToolCount(camp, best) >=
            CampEconomy.DesiredProductionToolCount(state, camp, best))
            return false;

        bool carried = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production == best);
        bool pooled = state.Pool.GetContents()
            .Any(entry => entry.Count > 0 && entry.Type.Production == best);
        return carried || pooled;
    }

    internal static void ConstructForCamp(ClassicAiState state, Location camp)
    {
        List<string> wanted = new();
        Production meat = camp.ValidProductions.FirstOrDefault(production => production.Produce.ID == "item_meat");
        Production rats = camp.ValidProductions.FirstOrDefault(production => production.Produce.ID == "item_rats");
        if (meat != null && CampEconomy.ProductionToolCount(camp, meat) <
            CampEconomy.DesiredProductionToolCount(state, camp, meat))
            wanted.Add("item_trap");
        if (rats != null && CampEconomy.ProductionToolCount(camp, rats) <
            CampEconomy.DesiredProductionToolCount(state, camp, rats))
            wanted.Add("item_rat_trap");

        if (TradeTask.NeedsPump(camp))
        {
            wanted.Add("item_industrial_pump");
            wanted.Add("item_hand_pump");
        }
        if (camp.Danger != null)
            wanted.Add("item_protective_suit");

        if (wanted.Count == 0)
            return;

        List<IItemCollection> sources = TradeTask.GetLocalItemSources(state, camp).ToList();
        IEnumerable<Character> builders = state.Player.Group
            .Concat(camp.CampNPC.Where(npc => npc.Player == state.Player))
            .Where(character => !character.IsDead);
        foreach (Character builder in builders)
        {
            Item result = state.RootGame.Constructions.TryConstructAny(
                sources, state.Pool, state.RootGame, wanted.ToArray());
            if (result == null)
                continue;

            InstallConstructedItem(state, camp, result);
            AiTelemetry.Report(state.Player,
                $"{builder.Name} constructed {result.ID} at {camp.Title}");
            return;
        }
    }

    internal static void ConstructPortableWeapon(ClassicAiState state)
    {
        if (!TradeTask.NeedsTravelOrDefenseWeapons(state))
            return;

        List<IItemCollection> sources = state.Player.Group.Select(character => (IItemCollection)character.Items).ToList();
        foreach (Character builder in state.Player.Group.Where(character => !character.IsDead))
        {
            Item weapon = state.RootGame.Constructions.TryConstructAny(
                sources, state.Pool, state.RootGame, "item_loaded_rifle", "item_loaded_pistol");
            if (weapon == null)
                continue;
            state.Pool.Insert(weapon);
            AiTelemetry.Report(state.Player, $"{builder.Name} assembled {weapon.ID}");
            return;
        }
    }

    internal static void InstallConstructedItem(ClassicAiState state, Location camp, Item item)
    {
        if (item.Type.IsClass("pump") || item.ID == "item_industrial_pump")
        {
            Room source = camp.GetSourceRoom();
            if (source != null && !source.Items.IsFull)
                source.Items.Add(item);
            else
                camp.StoreItemRandom(item);
            return;
        }

        if (item.Type.Production != null)
        {
            camp.StoreItemRandom(item);
            camp.AutoSelectFoodProduction(onlyIfStarving: false);
            return;
        }

        state.Pool.Insert(item);
    }

    internal static void InstallLoosePump(ClassicAiState state, Location camp)
    {
        if (!TradeTask.NeedsPump(camp))
            return;
        Room source = camp.GetSourceRoom();
        if (source == null || source.Items.IsFull)
            return;

        Item pump = TradeTask.GetLocalItemSources(state, camp)
            .Select(collection => new { Collection = collection, Item = collection.FirstOrDefault(TradeTask.IsPump) })
            .Where(entry => entry.Item != null)
            .OrderByDescending(entry => entry.Item.ID == "item_industrial_pump")
            .FirstOrDefault()?.Item;
        if (pump == null)
            return;

        IItemCollection owner = TradeTask.GetLocalItemSources(state, camp).First(collection => collection.Contains(pump));
        owner.Remove(pump);
        source.Items.Add(pump);
        AiTelemetry.Report(state.Player, $"installed {pump.ID} at {camp.Title}'s water source");
    }

    internal static void CollectProducedSurplus(ClassicAiState state, Location camp)
    {
        if (camp.Production == null)
            return;

        int stock = camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce));
        int reserve = System.Math.Max(2, camp.CampNPC.Count(npc => npc.Player == state.Player));
        int collected = 0;
        foreach (Room room in camp.Rooms)
        {
            foreach (Item item in room.Items.Where(item => item.Type == camp.Production.Produce).ToArray())
            {
                if (stock <= reserve)
                    break;
                if (state.Player.Group.GetFreeSlotCount() <= TradeTask.CargoSpaceReserve(state))
                {
                    if (!TryReplaceCargo(
                        state, item, out Item replaced, out Character replacementCarrier))
                        break;
                    room.Items.Remove(item);
                    replacementCarrier.Items.Add(item);
                    room.Items.Add(replaced);
                    stock--;
                    collected++;
                    continue;
                }
                Character carrier = state.Player.Group.FirstOrDefault(character => !character.Items.IsFull);
                if (carrier == null)
                    break;
                room.Items.Remove(item);
                carrier.Items.Add(item);
                stock--;
                collected++;
            }
        }
        if (collected > 0)
            AiTelemetry.Report(state.Player,
                $"collected surplus {camp.Production.Produce.ID} x{collected} from {camp.Title} for trade " +
                $"(trade value {collected * camp.Production.Produce.TradeValue:0})");
    }

    internal static void CollectStoredTradeGoods(ClassicAiState state, Location camp)
    {
        List<Item> collected = new();
        HashSet<Item> reservedWeapons = CampStoredWeaponReserveItems(camp);
        var candidates = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => !reservedWeapons.Contains(entry.Item))
            .Where(entry => camp.Production == null || entry.Item.Type != camp.Production.Produce)
            .Where(entry => entry.Item.Type.Production == null ||
                !camp.ValidProductions.Contains(entry.Item.Type.Production))
            .Where(entry => !TradeTask.IsPump(entry.Item) && CanCollectForTrade(state, entry.Item))
            .OrderByDescending(entry => TradeTask.ConstructionMaterialPriority(state, entry.Item.ID))
            .ThenByDescending(entry => entry.Item.TradeValue)
            .ToArray();
        foreach (var candidate in candidates)
        {
            if (state.Player.Group.GetFreeSlotCount() <= TradeTask.CargoSpaceReserve(state))
            {
                if (!TryReplaceCargo(
                    state, candidate.Item, out Item replaced, out Character replacementCarrier))
                    continue;
                candidate.Room.Items.Remove(candidate.Item);
                replacementCarrier.Items.Add(candidate.Item);
                candidate.Room.Items.Add(replaced);
                collected.Add(candidate.Item);
                continue;
            }
            Character carrier = state.Player.Group.FirstOrDefault(character => !character.Items.IsFull);
            if (carrier == null)
                break;
            candidate.Room.Items.Remove(candidate.Item);
            carrier.Items.Add(candidate.Item);
            collected.Add(candidate.Item);
        }

        if (collected.Count > 0)
            AiTelemetry.Report(state.Player,
                $"collected stored trade goods from {camp.Title}: " +
                string.Join(", ", collected.GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)) +
                $" (trade value {collected.Sum(item => item.TradeValue):0})");
    }

}
