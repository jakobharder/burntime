using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CampManagement
{
    internal static void MaintainCampProductionPolicy(ClassicAiState state)
    {
        foreach (Location camp in state.RootGame.World.Locations.Where(location =>
            location.Player == state.Player && !location.IsCity))
        {
            camp.AutoSelectFoodProduction(onlyIfCurrentProducesNothing: false);
            camp.ConsumeExcessFoodStock(Location.MaxStockFood);
        }
    }

    internal const int CampWeaponReserve = 2;
    // Stationed NPCs consume the camp's daily production directly. Physical food
    // items are therefore available for provisioning, export, and other spending.
    internal const int CampFoodItemReserve = 0;

    internal static bool CanStoreItemInCamp(Location camp, ItemType type)
    {
        if (!camp.Rooms.Any(room => !room.Items.IsFull))
            return false;
        if (camp.Player?.Type != PlayerType.Ai || camp.Production == null ||
            type == camp.Production.Produce || type.Production != null)
            return true;

        int produced = camp.GetCurrentProductionStockCount();
        int missingProductionSlots = System.Math.Max(0, Location.MaxStockFood - produced);
        int freeSlots = camp.Rooms.Sum(room => room.Items.MaxCount == ItemList.Infinite
            ? Location.MaxStockFood
            : System.Math.Max(0, room.Items.MaxCount - room.Items.Count));
        return freeSlots > missingProductionSlots;
    }

    internal static bool StoreItemInCamp(
        Location camp,
        Item item,
        bool reserveProductionCapacity = true)
    {
        if (reserveProductionCapacity
                ? !CanStoreItemInCamp(camp, item.Type)
                : !camp.Rooms.Any(room => !room.Items.IsFull))
            return false;

        bool waterContainer = AiItemPool.IsWaterContainer(item.Type);
        Room room = camp.Rooms
            .Where(candidate => !candidate.Items.IsFull)
            .OrderBy(candidate => waterContainer ? !candidate.IsWaterSource : candidate.IsWaterSource)
            .FirstOrDefault();
        if (room == null)
            return false;

        if (room.IsWaterSource && item.Type.Full != null &&
            camp.Source.Reserve >= item.Type.Full.WaterValue)
        {
            item.MakeFull();
            camp.Source.Reserve -= item.WaterValue;
        }
        room.Items.Add(item);
        return true;
    }

    internal static int UnloadGarrisonBelongings(
        ClassicAiState state,
        Location camp,
        Character npc)
    {
        if (camp.Player != state.Player || npc.Player != state.Player || npc.IsDead)
            return 0;

        Item[] belongings = npc.Items
            .Where(item => item.DamageValue <= 0 &&
                !item.Type.IsClass("weapon") &&
                !item.Type.IsClass("protection"))
            .ToArray();
        int unloaded = 0;
        foreach (Item item in belongings)
        {
            npc.Items.Remove(item);
            if (StoreItemInCamp(camp, item, reserveProductionCapacity: false))
            {
                unloaded++;
                continue;
            }

            // A genuinely full camp cannot accept another physical item. Keep
            // it on the guard instead of silently destroying or ground-dropping it.
            npc.Items.Add(item);
        }

        if (unloaded > 0)
            AiTelemetry.Report(state.Player,
                $"unloaded {unloaded} belongings from {npc.Name} into {camp.Title} storage");
        return unloaded;
    }

    internal static void NormalizeLoadedGarrisonBelongings(ClassicAiState state)
    {
        foreach (Location camp in state.RootGame.World.Locations
            .Where(location => location.Player == state.Player))
        {
            foreach (Character guard in camp.CampNPC
                .Where(npc => npc.Player == state.Player && !npc.IsDead))
                UnloadGarrisonBelongings(state, camp, guard);
        }
    }

    internal static void StoreInReserveOrAtLocation(
        ClassicAiState state,
        Item item,
        Location? location = null)
    {
        if (state.Reserve.Insert(item))
            return;
        location ??= state.Current;
        if (location.Player == state.Player && StoreItemInCamp(location, item))
            return;
        location.Items.Add(item);
    }

    internal static int EnsureAiProductionStorage(ClassicAiState state, Location camp)
    {
        if (camp.Player != state.Player || camp.Player.Type != PlayerType.Ai ||
            camp.Production == null)
            return 0;

        ItemType product = camp.Production.Produce;
        int produced = camp.GetCurrentProductionStockCount();
        int requiredFreeSlots = System.Math.Max(0, Location.MaxStockFood - produced);
        if (camp.Rooms.Any(room => room.Items.MaxCount == ItemList.Infinite))
            return 0;

        int freeSlots = camp.Rooms.Sum(room =>
            System.Math.Max(0, room.Items.MaxCount - room.Items.Count));
        if (freeSlots >= requiredFreeSlots)
            return 0;

        var removable = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            // Food from another production remains physical until the camp's
            // end-of-turn AI consumption pass applies the combined food limit.
            .Where(entry => entry.Item.FoodValue == 0 &&
                entry.Item.Type != product &&
                entry.Item.Type.Production != camp.Production)
            .OrderBy(entry => entry.Item.TradeValue)
            .ThenBy(entry => entry.Item.FoodValue)
            .ThenBy(entry => entry.Item.ID)
            .ToArray();
        int removed = 0;
        foreach (var entry in removable)
        {
            if (freeSlots >= requiredFreeSlots)
                break;
            entry.Room.Items.Remove(entry.Item);
            freeSlots++;
            removed++;
        }
        if (removed > 0)
            AiTelemetry.Report(state.Player,
                $"discarded {removed} low-value stored items at {camp.Title} to reserve " +
                $"{Location.MaxStockFood} slots for {product.ID} production");
        return removed;
    }

    static bool MakeRoomForProductionChange(
        ClassicAiState state,
        Location camp,
        Production? nextProduction)
    {
        if (camp.Rooms.Any(room => !room.Items.IsFull))
            return true;
        if (camp.Player != state.Player || camp.Player.Type != PlayerType.Ai ||
            nextProduction == null || nextProduction == camp.Production)
            return false;

        var removable = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => camp.Production == null ||
                entry.Item.Type.Production != camp.Production)
            .OrderBy(entry => entry.Item.TradeValue)
            .ThenBy(entry => entry.Item.FoodValue)
            .ThenBy(entry => entry.Item.ID)
            .FirstOrDefault();
        if (removable == null)
            return false;
        removable.Room.Items.Remove(removable.Item);
        AiTelemetry.Report(state.Player,
            $"discarded low-value {removable.Item.ID} at {camp.Title} to install " +
            $"{nextProduction.Produce.ID} production");
        return true;
    }

    public static bool ShouldPreferProductionAtCamp(ClassicAiState state, Location location) =>
        location.Danger == null && !ReinforcementPlanning.IsThreatened(state, location);

    internal static void MaintainCampNetwork(ClassicAiState state)
    {
        Player player = state.Player;
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == player)
            .ToArray();

        foreach (Location camp in camps)
            WeaponLoadout.NormalizeWeaponLimits(state,
                camp.CampNPC.Where(npc => npc.Player == player && !npc.IsDead).ToArray());

        Character[] frontierGuards = camps
            .Where(location => ReinforcementPlanning.IsThreatened(state, location))
            .SelectMany(location => location.CampNPC
                .Where(npc => npc.Player == player && !npc.IsDead))
            .ToArray();
        foreach (Character guard in frontierGuards)
            WeaponLoadout.EquipWeapon(state, guard,
                guard.Location.CampNPC
                    .Where(npc => npc.Player == player && !npc.IsDead)
                    .ToArray(),
                upgradeWeakWeapon: true, "frontier guard");

        StockCurrentCampWeaponReserve(state);
        foreach (Location camp in camps.Where(location => location.Danger != null))
        {
            foreach (Character guard in camp.CampNPC
                .Where(npc => npc.Player == player && !npc.IsDead))
                EquipDangerProtection(state, guard, camp);
        }

        RedistributeCampProductionTools(state, camps);
        StockCampWaterContainers(state, camps);
    }

    static void RedistributeCampProductionTools(
        ClassicAiState state,
        IReadOnlyCollection<Location> camps)
    {
        Location[] productionPriority = camps
            .OrderByDescending(location => ShouldPreferProductionAtCamp(state, location))
            .ThenBy(location => location.GetFoodProductionRate().FoodPerDay)
            .ToArray();

        // A second cycle lets a better tool displace a lower tier and cascade
        // the reclaimed tool into another compatible camp.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Location camp in productionPriority)
                Trading.CollectRedundantProductionTools(state, camp);
            foreach (Location camp in productionPriority)
                InstallProductionFromPool(state, camp);
        }

        foreach (Location camp in productionPriority)
        {
            camp.AutoSelectFoodProduction(onlyIfCurrentProducesNothing: false);
            EnsureAiProductionStorage(state, camp);
        }
    }

    static void StockCampWaterContainers(
        ClassicAiState state,
        IEnumerable<Location> camps)
    {
        foreach (Location camp in camps
            .OrderByDescending(camp => CampEconomy.IsTravelWaterBottleneck(camp))
            .ThenBy(camp => camp.Source?.Water ?? 0))
        {
            int stock = Trading.CampWaterContainerCount(camp);
            while (stock < Trading.DesiredCampWaterContainerCount(camp) &&
                state.Reserve.HasWaterContainer())
            {
                if (!camp.Rooms.Any(room => !room.Items.IsFull))
                    break;
                Item container = state.Reserve.GetBestWaterContainer();
                if (!StoreItemInCamp(camp, container))
                {
                    state.Reserve.Insert(container);
                    break;
                }
                stock++;
                AiTelemetry.Report(state.Player,
                    $"stocked {container.ID} as water reserve at {camp.Title}");
            }
        }
    }

    internal static void StockCurrentCampWeaponReserve(ClassicAiState state)
    {
        Location camp = state.Current;
        if (camp.Player != state.Player)
            return;

        while (CampStoredWeaponCount(camp) < CampWeaponReserve &&
            state.Reserve.HasWeapon() && camp.Rooms.Any(room => !room.Items.IsFull))
        {
            Item weapon = state.Reserve.GetBestWeapon(type => !AiItemPool.IsFirearm(type));
            if (weapon == null)
                return;
            if (!StoreItemInCamp(camp, weapon))
            {
                state.Reserve.Insert(weapon);
                return;
            }
            AiTelemetry.Report(state.Player,
                $"stored reserve weapon {weapon.ID} at {camp.Title}");
        }
    }

    internal static int CampStoredWeaponCount(Location camp) => camp.Rooms
        .SelectMany(room => room.Items)
        .Count(WeaponLoadout.IsMeleeWeapon);

    internal static HashSet<Item> CampStoredWeaponReserveItems(Location camp) => camp.Rooms
        .SelectMany(room => room.Items)
        .Where(WeaponLoadout.IsMeleeWeapon)
        .OrderByDescending(item => item.DamageValue)
        .Take(CampWeaponReserve)
        .ToHashSet();

    internal static void EquipDangerProtection(
        ClassicAiState state,
        Character guard,
        Location camp)
    {
        if (guard.Items.FindBestProtection(null, camp.Danger.Type) != null ||
            guard.Items.IsFull)
            return;

        Item protection = camp.Danger.Type == "radiation"
            ? state.Reserve.GetProtectionSuit()
            : state.Reserve.GetGasMask();
        if (protection == null)
            return;

        guard.Items.Add(protection);
        guard.Protection = protection;
        AiTelemetry.Report(state.Player,
            $"equipped {guard.Name} at {camp.Title} against {camp.Danger.Type} with {protection.ID}");
    }

    internal static void MaintainCurrentCamp(ClassicAiState state)
    {
        Location camp = state.Current;
        GroupInventory.MaintainLeaderRoleSlots(state);

        // construct
        ConstructForCamp(state, camp);

        // install
        InstallProductionFromPool(state, camp);
        InstallLoosePump(state, camp);
        camp.AutoSelectFoodProduction(onlyIfCurrentProducesNothing: false);
        EnsureAiProductionStorage(state, camp);

        // collect
        Trading.CollectRedundantProductionTools(state, camp);

        Item? depositedWaterReserve = StockCriticalWaterContainerFromGroup(state, camp);
        CollectFutureRecruitmentPayment(state, camp);
        CollectProducedSurplus(state, camp);
        CollectStoredTradeGoods(state, camp, depositedWaterReserve);
    }

    static Item? StockCriticalWaterContainerFromGroup(ClassicAiState state, Location camp)
    {
        if (!CampEconomy.IsTravelWaterBottleneck(camp))
            return null;

        int portableCapacity = Trading.PortableWaterCapacity(state);
        bool reusableReserve = CampEconomy.NeedsReusableWaterReserve(camp);
        var container = state.Player.Group
            .SelectMany(character => character.Items
                .Where(item => AiItemPool.IsWaterContainer(item.Type))
                .Select(item => new { Character = character, Item = item }))
            // An empty container is infrastructure only when the camp has some
            // water left after its residents drink. At a dry camp, only a
            // genuinely filled emergency cache helps.
            .Where(entry => reusableReserve || entry.Item.WaterValue > 0)
            .Where(entry => portableCapacity -
                AiItemPool.WaterContainerCapacity(entry.Item.Type) >=
                Trading.DesiredWaterContainerCapacity(state))
            .OrderBy(entry => AiItemPool.WaterContainerCapacity(entry.Item.Type))
            .FirstOrDefault();
        if (container == null || !StoreItemInCamp(camp, container.Item))
            return null;

        container.Character.Items.Remove(container.Item);
        AiTelemetry.Report(state.Player,
            $"stocked {container.Item.ID} as critical water reserve at {camp.Title}");
        return container.Item;
    }

    static void CollectFutureRecruitmentPayment(ClassicAiState state, Location camp)
    {
        ItemType? paymentType = Recruitment.PlannedFutureSettlementPaymentType(state);
        if (paymentType == null || state.Player.Character.Items.Find(paymentType) != null ||
            state.Player.Character.Items.IsFull)
            return;

        Room? room = camp.Rooms.FirstOrDefault(candidate =>
            candidate.Items.Find(paymentType) != null);
        Item? payment = room?.Items.Find(paymentType);
        if (payment == null)
            return;
        room!.Items.Remove(payment);
        state.Player.Character.Items.Add(payment);
        AiTelemetry.Report(state.Player,
            $"reserved {payment.ID} from {camp.Title} for the recruit at " +
            $"{state.StrategicTarget!.Title}");
    }

    internal static void InstallProductionFromPool(ClassicAiState state, Location camp)
    {
        if (ExpansionPlanning.ShouldReserveProductionTool(state))
            return;

        string[] products = camp.ValidProductions
            .Where(production => Trading.NeedsProductionResult(state, camp, production.Produce.ID))
            .OrderByDescending(Trading.ProductionTradePriority)
            .Select(production => production.Produce.ID)
            .ToArray();
        if (products.Length == 0 || !state.Reserve.HasTrap(products))
            return;
        Item tool = state.Reserve.GetBestTrap(products);
        if (tool == null)
            return;
        MakeRoomForProductionChange(state, camp, tool.Type.Production);
        if (!StoreItemInCamp(camp, tool))
        {
            state.Reserve.Insert(tool);
            return;
        }
        AiTelemetry.Report(state.Player, $"installed {tool.ID} for food production at {camp.Title}");
    }

    internal static bool HasPortableBestProduction(ClassicAiState state, Location camp)
    {
        Production best = camp.ValidProductions
            .OrderByDescending(Trading.ProductionTradePriority)
            .ThenByDescending(production => production.Produce.FoodValue)
            .FirstOrDefault();
        if (best == null || CampEconomy.ProductionToolCount(camp, best) >=
            CampEconomy.DesiredProductionToolCount(state, camp, best))
            return false;

        bool carried = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production == best);
        bool pooled = state.Reserve.GetContents()
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

        if (Trading.NeedsPump(camp))
        {
            wanted.Add("item_industrial_pump");
            wanted.Add("item_hand_pump");
        }
        if (camp.Danger != null)
            wanted.Add("item_protective_suit");

        if (wanted.Count == 0)
            return;

        List<IItemCollection> sources = Trading.GetLocalItemSources(state, camp).ToList();
        IEnumerable<Character> builders = state.Player.Group
            .Concat(camp.CampNPC.Where(npc => npc.Player == state.Player))
            .Where(character => !character.IsDead);
        foreach (Character builder in builders)
        {
            Item result = state.RootGame.Constructions.TryConstructAny(
                sources, state.Reserve, state.RootGame, wanted.ToArray());
            if (result == null)
                continue;

            InstallConstructedItem(state, camp, result);
            AiTelemetry.Report(state.Player,
                $"{builder.Name} constructed {result.ID} at {camp.Title}");
            return;
        }
    }

    internal static void InstallConstructedItem(ClassicAiState state, Location camp, Item item)
    {
        if (Trading.IsPump(item))
        {
            Room source = camp.GetSourceRoom();
            if (source != null && !source.Items.IsFull)
                source.Items.Add(item);
            else
                StoreItemInCamp(camp, item);
            return;
        }

        if (item.Type.Production != null)
        {
            MakeRoomForProductionChange(state, camp, item.Type.Production);
            if (!StoreItemInCamp(camp, item))
                StoreInReserveOrAtLocation(state, item, camp);
            return;
        }

        StoreInReserveOrAtLocation(state, item, camp);
    }

    internal static void InstallLoosePump(ClassicAiState state, Location camp)
    {
        if (!Trading.NeedsPump(camp))
            return;
        Room source = camp.GetSourceRoom();
        if (source == null || source.Items.IsFull)
            return;

        Item pump = Trading.GetLocalItemSources(state, camp)
            .Select(collection => new { Collection = collection, Item = collection.FirstOrDefault(Trading.IsPump) })
            .Where(entry => entry.Item != null)
            .OrderByDescending(entry => entry.Item.ID == "item_industrial_pump")
            .FirstOrDefault()?.Item;
        if (pump == null)
            return;

        IItemCollection owner = Trading.GetLocalItemSources(state, camp).First(collection => collection.Contains(pump));
        owner.Remove(pump);
        source.Items.Add(pump);
        AiTelemetry.Report(state.Player, $"installed {pump.ID} at {camp.Title}'s water source");
    }

    internal static void CollectProducedSurplus(ClassicAiState state, Location camp)
    {
        if (camp.Production == null)
            return;

        int stock = camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce));
        int reserve = CampFoodItemReserve;
        int collected = 0;
        foreach (Room room in camp.Rooms)
        {
            foreach (Item item in room.Items.Where(item => item.Type == camp.Production.Produce).ToArray())
            {
                if (stock <= reserve)
                    break;
                if (state.Player.Group.GetFreeSlotCount() <= Trading.CargoSpaceReserve(state))
                {
                    if (!CargoManagement.TryReplaceCargo(
                        state, item, out Item replaced, out Character replacementCarrier))
                        break;
                    room.Items.Remove(item);
                    replacementCarrier.Items.Add(item);
                    room.Items.Add(replaced);
                    stock--;
                    collected++;
                    continue;
                }
                Character carrier = GroupInventory.FindCargoCarrier(state, item);
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

    internal static void CollectStoredTradeGoods(
        ClassicAiState state,
        Location camp,
        Item? depositedWaterReserve = null)
    {
        List<Item> collected = new();
        HashSet<Item> reservedWeapons = CampStoredWeaponReserveItems(camp);
        HashSet<Item> reservedWater = CampStoredWaterReserveItems(camp);
        if (depositedWaterReserve != null)
            reservedWater.Add(depositedWaterReserve);
        var candidates = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => !reservedWeapons.Contains(entry.Item) &&
                !reservedWater.Contains(entry.Item))
            .Where(entry => camp.Production == null || entry.Item.Type != camp.Production.Produce)
            .Where(entry => entry.Item.Type.Production == null ||
                !camp.ValidProductions.Contains(entry.Item.Type.Production))
            .Where(entry => !Trading.IsPump(entry.Item) && CargoManagement.CanCollectForTrade(state, entry.Item))
            .OrderByDescending(entry => entry.Item.FoodValue > 0)
            .ThenByDescending(entry => Trading.ConstructionMaterialPriority(state, entry.Item.ID))
            .ThenByDescending(entry => entry.Item.FoodValue)
            .ThenByDescending(entry => entry.Item.TradeValue)
            .ToArray();
        foreach (var candidate in candidates)
        {
            if (state.Player.Group.GetFreeSlotCount() <= Trading.CargoSpaceReserve(state))
            {
                if (!CargoManagement.TryReplaceCargo(
                    state, candidate.Item, out Item replaced, out Character replacementCarrier))
                    continue;
                candidate.Room.Items.Remove(candidate.Item);
                replacementCarrier.Items.Add(candidate.Item);
                candidate.Room.Items.Add(replaced);
                collected.Add(candidate.Item);
                continue;
            }
            Character carrier = GroupInventory.FindCargoCarrier(state, candidate.Item);
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

    internal static HashSet<Item> CampStoredWaterReserveItems(Location camp)
    {
        bool lowSurplus = CampEconomy.WaterSurplusPerDay(camp) <
            CampEconomy.TravelSupplySurplusTarget;
        if (!lowSurplus && !CampEconomy.NeedsReusableWaterReserve(camp))
            return new HashSet<Item>();

        // Containers deliberately committed to a water-poor camp are
        // infrastructure, not trade stock. Retaining the set also avoids
        // cycling indistinguishable containers through the camp on later visits.
        return camp.Rooms
            .SelectMany(room => room.Items)
            .Where(item => AiItemPool.IsWaterContainer(item.Type))
            .ToHashSet();
    }

}
