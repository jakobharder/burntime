using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Turn-local economy and camp maintenance. All durable state remains in existing
/// character, trader, room, and AI-pool inventories.
/// </summary>
internal static class StrategicAiEconomy
{
    static readonly HashSet<string> ConstructionMaterials = new()
    {
        "item_wire", "item_woodpile", "item_screws", "item_spring", "item_tin",
        "item_broken_pump", "item_spare_parts", "item_rags", "item_hose", "item_iron_pipe",
        "item_unloaded_rifle", "item_unloaded_pistol", "item_ammunition"
    };

    public static void Run(ClassicAiState state)
    {
        EquipEmpire(state);

        if (state.Current.Player == state.Player)
            MaintainCurrentCamp(state);

        ConstructPortableWeapon(state);

        if (state.Current.IsCity)
            TradeAtCity(state);

        // A purchase or construction may satisfy an equipment need immediately.
        EquipEmpire(state);
    }

    public static bool ShouldVisitTrader(ClassicAiState state)
    {
        if (state.Current.IsCity || state.Player.Group.GetFreeSlotCount() > 2)
            return false;
        bool hasTradeGoods = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => CanSell(state, item));
        return hasTradeGoods && (NeedsWeapons(state) || NeedsAnyConstruction(state) ||
            state.Player.Group.SelectMany(character => character.Items).Count(item => item.FoodValue > 0) > 2);
    }

    public static Location FindBestTradeCity(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity && location != state.Current && location.LocalTrader != null)
            .Select(location => new
            {
                Location = location,
                Route = StrategicAiPlanner.FindRoute(state.Player, state.Current, location),
                StockScore = location.LocalTrader.Object.Items
                    .Select(item => ShoppingPriority(state, item))
                    .DefaultIfEmpty(0)
                    .Max()
            })
            .Where(candidate => candidate.Route != null && candidate.StockScore > 0)
            .OrderByDescending(candidate => candidate.StockScore - candidate.Route.Days * 5)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool ShouldContinueTrading(ClassicAiState state)
    {
        Trader trader = state.Current.LocalTrader;
        if (!state.Current.IsCity || trader == null)
            return false;
        float cheapestWanted = trader.Items
            .Where(item => ShoppingPriority(state, item) > 0)
            .Select(item => item.TradeValue)
            .DefaultIfEmpty(float.MaxValue)
            .Min();
        float availableValue = state.Player.Group.SelectMany(character => character.Items)
            .Where(item => CanSell(state, item))
            .Sum(item => item.TradeValue);
        return cheapestWanted != float.MaxValue && availableValue >= cheapestWanted;
    }

    public static bool NeedsTechnician(ClassicAiState state)
    {
        return !state.Player.Group.Any(character => character.Class == CharClass.Technician) &&
            state.RootGame.World.Locations.Any(location => location.Player == state.Player &&
                (HasConstructibleProductionNeed(location) || NeedsPump(location)));
    }

    public static bool ShouldReserveProductionTool(ClassicAiState state)
    {
        int needed = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Sum(location => System.Math.Max(0, 2 - CompatibleToolCount(location)));
        if (state.OwnedCampCount == 0)
            needed = System.Math.Max(1, needed);
        return state.Pool.ProductionToolCount <= needed;
    }

    static void EquipEmpire(ClassicAiState state)
    {
        Player player = state.Player;
        var camps = state.RootGame.World.Locations.Where(location => location.Player == player).ToArray();
        var frontierGuards = camps.Where(IsFrontier)
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead));
        var rearGuards = camps.Where(location => !IsFrontier(location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead));

        foreach (Character guard in frontierGuards)
            EquipWeapon(state, guard, upgradeWeakWeapon: true, "frontier guard");
        foreach (Character follower in player.Group.Where(character => character != player.Character && !character.IsDead))
            EquipWeapon(state, follower, upgradeWeakWeapon: false, "follower");
        foreach (Character guard in rearGuards)
            EquipWeapon(state, guard, upgradeWeakWeapon: false, "camp guard");

        foreach (Location camp in camps.Where(location => location.Danger != null))
        {
            foreach (Character guard in camp.CampNPC.Where(npc => npc.Player == player && !npc.IsDead))
                EquipDangerProtection(state, guard, camp);
        }

        IEnumerable<Character> npcs = frontierGuards
            .Concat(player.Group.Where(character => character != player.Character && !character.IsDead))
            .Concat(rearGuards)
            .Distinct();
        foreach (Character npc in npcs)
        {
            if (HasWaterContainer(npc) || npc.Items.IsFull || !state.Pool.HasWaterContainer())
                continue;
            Item container = state.Pool.GetBestWaterContainer();
            npc.Items.Add(container);
            StrategicAiTelemetry.Report(player, $"equipped {npc.Name} with {container.ID}");
        }
    }

    static void EquipWeapon(
        ClassicAiState state,
        Character character,
        bool upgradeWeakWeapon,
        string role)
    {
        Item current = character.Items.FindBestWeapon();
        int currentDamage = current?.DamageValue ?? 0;
        int desiredMinimum = upgradeWeakWeapon && currentDamage < 33 ? currentDamage : currentDamage > 0 ? int.MaxValue : 0;
        bool reserveProductionTool = ShouldReserveProductionTool(state);
        if (desiredMinimum == int.MaxValue ||
            !state.Pool.HasBetterWeapon(desiredMinimum, allowProductionTool: !reserveProductionTool))
        {
            if (current != null)
                character.Weapon = current;
            return;
        }

        Item weapon = state.Pool.GetBestWeapon(desiredMinimum, allowProductionTool: !reserveProductionTool);
        if (weapon == null)
            return;

        if (character.Items.IsFull && current != null)
        {
            character.Items.Remove(current);
            state.Pool.Insert(current);
        }
        if (!character.Items.Add(weapon))
        {
            state.Pool.Insert(weapon);
            return;
        }

        character.Weapon = weapon;
        string location = character.IsStationed ? $" at {character.Location.Title}" : "";
        StrategicAiTelemetry.Report(state.Player, $"equipped {role} {character.Name}{location} with {weapon.ID}");
    }

    static void EquipDangerProtection(ClassicAiState state, Character guard, Location camp)
    {
        if (guard.Items.FindBestProtection(null, camp.Danger.Type) != null || guard.Items.IsFull)
            return;

        Item protection = camp.Danger.Type == "radiation"
            ? state.Pool.GetProtectionSuit()
            : state.Pool.GetGasMask();
        if (protection == null)
            return;

        guard.Items.Add(protection);
        guard.Protection = protection;
        StrategicAiTelemetry.Report(state.Player,
            $"equipped {guard.Name} at {camp.Title} against {camp.Danger.Type} with {protection.ID}");
    }

    static void MaintainCurrentCamp(ClassicAiState state)
    {
        Location camp = state.Current;
        InstallProductionFromPool(state, camp);
        InstallLoosePump(state, camp);
        ConstructForCamp(state, camp);
        CollectProducedSurplus(state, camp);
    }

    static void InstallProductionFromPool(ClassicAiState state, Location camp)
    {
        string[] products = state.AvailableProducts(camp);
        if (!state.Pool.HasTrap(products) || CompatibleToolCount(camp) >= 2)
            return;

        Item tool = state.Pool.GetBestTrap(products);
        if (tool == null)
            return;
        camp.StoreItemRandom(tool);
        camp.AutoSelectFoodProduction(onlyIfStarving: false);
        StrategicAiTelemetry.Report(state.Player, $"installed {tool.ID} for food production at {camp.Title}");
    }

    static void ConstructForCamp(ClassicAiState state, Location camp)
    {
        List<string> wanted = new();
        string[] products = state.AvailableProducts(camp);
        if (CompatibleToolCount(camp) < 2)
        {
            if (products.Contains("item_meat"))
                wanted.Add("item_trap");
            if (products.Contains("item_rats"))
                wanted.Add("item_rat_trap");
        }

        if (NeedsPump(camp))
        {
            wanted.Add("item_industrial_pump");
            wanted.Add("item_hand_pump");
        }
        if (camp.Danger != null)
            wanted.Add("item_protective_suit");

        if (wanted.Count == 0)
            return;

        List<IItemCollection> sources = GetLocalItemSources(state, camp).ToList();
        IEnumerable<Character> builders = state.Player.Group
            .Concat(camp.CampNPC.Where(npc => npc.Player == state.Player))
            .Where(character => !character.IsDead);
        foreach (Character builder in builders)
        {
            Item result = state.RootGame.Constructions.TryConstructAny(
                builder, sources, state.RootGame, wanted.ToArray());
            if (result == null)
                continue;

            InstallConstructedItem(state, camp, result);
            StrategicAiTelemetry.Report(state.Player,
                $"{builder.Name} constructed {result.ID} at {camp.Title}");
            return;
        }
    }

    static void ConstructPortableWeapon(ClassicAiState state)
    {
        if (!NeedsWeapons(state))
            return;

        List<IItemCollection> sources = state.Player.Group.Select(character => (IItemCollection)character.Items).ToList();
        foreach (Character builder in state.Player.Group.Where(character => !character.IsDead))
        {
            Item weapon = state.RootGame.Constructions.TryConstructAny(
                builder, sources, state.RootGame, "item_loaded_rifle", "item_loaded_pistol");
            if (weapon == null)
                continue;
            state.Pool.Insert(weapon);
            StrategicAiTelemetry.Report(state.Player, $"{builder.Name} assembled {weapon.ID}");
            return;
        }
    }

    static void InstallConstructedItem(ClassicAiState state, Location camp, Item item)
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

    static void InstallLoosePump(ClassicAiState state, Location camp)
    {
        if (!NeedsPump(camp))
            return;
        Room source = camp.GetSourceRoom();
        if (source == null || source.Items.IsFull)
            return;

        Item pump = GetLocalItemSources(state, camp)
            .Select(collection => new { Collection = collection, Item = collection.FirstOrDefault(IsPump) })
            .Where(entry => entry.Item != null)
            .OrderByDescending(entry => entry.Item.ID == "item_industrial_pump")
            .FirstOrDefault()?.Item;
        if (pump == null)
            return;

        IItemCollection owner = GetLocalItemSources(state, camp).First(collection => collection.Contains(pump));
        owner.Remove(pump);
        source.Items.Add(pump);
        StrategicAiTelemetry.Report(state.Player, $"installed {pump.ID} at {camp.Title}'s water source");
    }

    static void CollectProducedSurplus(ClassicAiState state, Location camp)
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
            StrategicAiTelemetry.Report(state.Player,
                $"collected surplus {camp.Production.Produce.ID} x{collected} from {camp.Title} for trade");
    }

    static void TradeAtCity(ClassicAiState state)
    {
        Trader trader = state.Current.LocalTrader;
        if (trader == null || trader.Items.Count == 0)
            return;

        Item target = trader.Items
            .Where(item => ShoppingPriority(state, item) > 0)
            .OrderByDescending(item => ShoppingPriority(state, item))
            .ThenByDescending(item => item.TradeValue)
            .FirstOrDefault();
        if (target == null)
            return;

        int targetValue = (int)target.TradeValue;
        int maxOffers = trader.Items.MaxCount - trader.Items.Count + 1;
        if (targetValue <= 0 || maxOffers <= 0)
            return;

        List<TradeAsset> temporaryPoolAssets = TakeSurplusPoolContainers(state);
        List<TradeAsset> candidates = state.Player.Group
            .SelectMany(character => character.Items.Select(item => new TradeAsset(character.Items, item, false)))
            .Where(asset => asset.Item.ID != target.ID && CanSell(state, asset.Item))
            .Concat(temporaryPoolAssets)
            .OrderBy(asset => SalePriority(asset.Item))
            .ThenByDescending(asset => asset.Item.TradeValue)
            .ToList();

        List<TradeAsset> offers = new();
        float offeredValue = 0;
        int remainingFood = state.Player.Group.GetFoodInInventory();
        foreach (TradeAsset candidate in candidates)
        {
            if (offers.Count >= maxOffers)
                break;
            if (candidate.Item.FoodValue > 0 &&
                remainingFood - candidate.Item.FoodValue < state.Player.Group.Count * 3)
                continue;

            offers.Add(candidate);
            offeredValue += candidate.Item.TradeValue;
            remainingFood -= candidate.Item.FoodValue;
            if ((int)offeredValue >= targetValue)
                break;
        }

        if (offers.Count == 0 || (int)offeredValue < targetValue)
        {
            RestoreUnusedPoolAssets(state, temporaryPoolAssets);
            return;
        }

        if (!AiItemPool.Accepts(target.Type) && state.Player.Group.GetFreeSlotCount() == 0 &&
            offers.All(offer => offer.FromPool))
        {
            RestoreUnusedPoolAssets(state, temporaryPoolAssets);
            return;
        }

        trader.Items.Remove(target);
        foreach (TradeAsset offer in offers)
        {
            if (!offer.FromPool)
                offer.Owner.Remove(offer.Item);
            trader.Items.Add(offer.Item);
        }

        if (AiItemPool.Accepts(target.Type))
            state.Pool.Insert(target);
        else
            state.Player.Group.First(character => !character.Items.IsFull).Items.Add(target);

        RestoreUnusedPoolAssets(state, temporaryPoolAssets.Except(offers));
        StrategicAiTelemetry.Report(state.Player,
            $"traded {string.Join(", ", offers.Select(offer => offer.Item.ID))} for {target.ID} with {trader.Name}");
    }

    static List<TradeAsset> TakeSurplusPoolContainers(ClassicAiState state)
    {
        List<TradeAsset> assets = new();
        while (state.Pool.WaterContainerCount > 1)
        {
            Item item = state.Pool.TakeLeastWaterContainer();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        return assets;
    }

    static void RestoreUnusedPoolAssets(ClassicAiState state, IEnumerable<TradeAsset> assets)
    {
        foreach (TradeAsset asset in assets.Where(asset => asset.FromPool))
            state.Pool.Insert(asset.Item);
    }

    static float ShoppingPriority(ClassicAiState state, Item item)
    {
        if (item.DamageValue > 0 && NeedsWeapons(state))
            return 1000 + item.DamageValue;
        if (item.Type.IsClass("protection") && NeedsDangerProtection(state, item.Type))
            return 950 + item.TradeValue;
        if (item.Type.Production != null && NeedsProduction(state, item.Type))
            return 900 + item.FoodValue;
        if (IsPump(item) && NeedsAnyPump(state))
            return 850 + (item.ID == "item_industrial_pump" ? 20 : 0);
        if (ConstructionMaterials.Contains(item.ID) && NeedsConstructionMaterials(state, item.ID))
            return 700 + item.TradeValue;
        if (AiItemPool.IsWaterContainer(item.Type) && NeedsBetterWaterContainers(state, item.Type))
            return 600 + AiItemPool.WaterContainerCapacity(item.Type);

        int lowestFood = state.Player.Group.SelectMany(character => character.Items)
            .Where(candidate => candidate.FoodValue > 0)
            .Select(candidate => candidate.FoodValue)
            .DefaultIfEmpty(0)
            .Min();
        if (item.FoodValue > lowestFood && lowestFood > 0 && state.Player.Group.GetFreeSlotCount() <= 2)
            return 400 + item.FoodValue;
        return 0;
    }

    static bool CanSell(ClassicAiState state, Item item)
    {
        if (state.Player.Group.Any(character => character.Weapon == item || character.Protection == item))
            return false;
        if (IsPump(item) || (item.Type.Production != null && state.OwnedCampCount == 0))
            return false;
        if (AiItemPool.IsWaterContainer(item.Type))
            return false;
        if (item.Type.IsClass("weapon") && NeedsWeapons(state))
            return false;
        if (ConstructionMaterials.Contains(item.ID) && NeedsAnyConstruction(state))
            return false;
        if (item.ID == "item_advice")
            return false;
        return true;
    }

    static int SalePriority(Item item)
    {
        if (item.FoodValue > 0)
            return item.FoodValue <= 3 ? 0 : 3;
        if (item.Type.IsClass("useless"))
            return 1;
        if (item.Type.IsClass("protection"))
            return 2;
        return 3;
    }

    static bool NeedsWeapons(ClassicAiState state)
    {
        IEnumerable<Character> followers = state.Player.Group.Where(character => character != state.Player.Character);
        IEnumerable<Character> guards = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player));
        return followers.Concat(guards).Any(character =>
            (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0 ||
            (character.IsStationed && IsFrontier(character.Location) &&
             (character.Items.FindBestWeapon()?.DamageValue ?? 0) < 33));
    }

    static bool NeedsProduction(ClassicAiState state, ItemType type)
    {
        if (type.Production == null)
            return false;
        Location[] camps = state.RootGame.World.Locations.Where(location => location.Player == state.Player).ToArray();
        return camps.Length == 0 || camps.Any(location =>
            location.ValidProductions.Contains(type.Production) && CompatibleToolCount(location) < 2);
    }

    static bool NeedsDangerProtection(ClassicAiState state, ItemType type)
    {
        bool ownedCampNeed = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location.Danger != null)
            .Any(location => type.GetProtection(location.Danger.Type) != null &&
                location.CampNPC.Any(guard => guard.Player == state.Player &&
                    guard.Items.FindBestProtection(null, location.Danger.Type) == null));
        bool expansionNeed = state.RootGame.World.Locations
            .Where(location => !location.IsCity && location.Player == null && location.Danger != null)
            .Any(location => type.GetProtection(location.Danger.Type) != null &&
                (location.Danger.Type == "radiation"
                    ? !state.Pool.HasProtectionSuit()
                    : !state.Pool.HasGasMask()));
        return ownedCampNeed || expansionNeed;
    }

    static bool NeedsBetterWaterContainers(ClassicAiState state, ItemType offered)
    {
        int offeredCapacity = AiItemPool.WaterContainerCapacity(offered);
        IEnumerable<Character> npcs = state.Player.Group.Where(character => character != state.Player.Character)
            .Concat(state.RootGame.World.Locations.Where(location => location.Player == state.Player)
                .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player)));
        return npcs.Any(npc => !HasWaterContainer(npc)) || offeredCapacity > state.Pool.BestWaterContainerCapacity;
    }

    static bool NeedsConstructionMaterials(ClassicAiState state, string itemId)
    {
        string[] recipe = FocusedRecipe(state);
        return recipe.Contains(itemId) &&
            !state.Player.Group.SelectMany(character => character.Items).Any(item => item.ID == itemId);
    }

    static string[] FocusedRecipe(ClassicAiState state)
    {
        List<string[]> recipes = new();
        IEnumerable<Item> carried = state.Player.Group.SelectMany(character => character.Items);
        if (NeedsWeapons(state))
        {
            if (carried.Any(item => item.ID == "item_unloaded_rifle"))
                recipes.Add(new[] { "item_unloaded_rifle", "item_ammunition" });
            if (carried.Any(item => item.ID == "item_unloaded_pistol"))
                recipes.Add(new[] { "item_unloaded_pistol", "item_ammunition" });
        }
        if (state.RootGame.World.Locations.Any(location => location.Player == state.Player &&
            HasConstructibleProductionNeed(location) &&
            location.ValidProductions.Any(production => production.Produce.ID == "item_meat")))
            recipes.Add(new[] { "item_spring", "item_tin", "item_wire" });
        if (state.RootGame.World.Locations.Any(location => location.Player == state.Player &&
            HasConstructibleProductionNeed(location) &&
            location.ValidProductions.Any(production => production.Produce.ID == "item_rats")))
            recipes.Add(new[] { "item_wire", "item_woodpile", "item_screws" });
        if (NeedsAnyPump(state))
        {
            recipes.Add(new[] { "item_broken_pump", "item_rags", "item_hose" });
            recipes.Add(new[] { "item_spare_parts", "item_iron_pipe", "item_rags", "item_hose" });
        }

        return recipes
            .OrderByDescending(recipe => recipe.Count(id => carried.Any(item => item.ID == id)))
            .ThenBy(recipe => recipe.Length)
            .FirstOrDefault() ?? Array.Empty<string>();
    }

    static bool NeedsAnyConstruction(ClassicAiState state) =>
        state.RootGame.World.Locations.Any(location => location.Player == state.Player &&
            (HasConstructibleProductionNeed(location) || NeedsPump(location))) || NeedsWeapons(state);

    static bool NeedsAnyPump(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => location.Player == state.Player && NeedsPump(location));

    static bool NeedsPump(Location camp)
    {
        Room source = camp.GetSourceRoom();
        if (source == null || source.Items.Any(IsPump))
            return false;
        int guards = camp.CampNPC.Count();
        return camp.Source.Water < System.Math.Max(2, guards + 1);
    }

    static bool IsPump(Item item) => item.Type.IsClass("pump") || item.ID == "item_industrial_pump";

    static int CompatibleToolCount(Location camp)
    {
        HashSet<Production> valid = camp.ValidProductions.ToHashSet();
        return camp.Rooms.SelectMany(room => room.Items)
            .Concat(camp.CampNPC.SelectMany(npc => npc.Items))
            .Count(item => item.Type.Production != null && valid.Contains(item.Type.Production));
    }

    static bool HasConstructibleProductionNeed(Location camp)
    {
        return CompatibleToolCount(camp) < 2 && camp.ValidProductions
            .Any(production => production.Produce.ID is "item_rats" or "item_meat");
    }

    static bool IsFrontier(Location location) => location.Neighbors
        .Any(neighbor => !neighbor.IsCity && neighbor.Player != null && neighbor.Player != location.Player);

    static bool HasWaterContainer(Character character) => character.Items
        .Any(item => AiItemPool.IsWaterContainer(item.Type));

    static IEnumerable<IItemCollection> GetLocalItemSources(ClassicAiState state, Location camp)
    {
        foreach (Character character in state.Player.Group)
            yield return character.Items;
        foreach (Character character in camp.CampNPC.Where(npc => npc.Player == state.Player))
            yield return character.Items;
        foreach (Room room in camp.Rooms)
            yield return room.Items;
    }

    sealed record TradeAsset(IItemCollection Owner, Item Item, bool FromPool);
}
