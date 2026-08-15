using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Turn-local economy and camp maintenance. All durable state remains in existing
/// character, trader, room, and AI-pool inventories.
/// </summary>
internal static class StrategicAiEconomy
{
    static readonly ConditionalWeakTable<Player, TradeFailureState> LastReportedTradeFailure = new();

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
        bool hasTradeGoods = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => CanSell(state, item));
        return hasTradeGoods && FindBestTradeCity(state) != null;
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
            .Where(candidate => candidate.Route != null && candidate.StockScore > 0 &&
                CanPlanTrade(state, candidate.Location.LocalTrader))
            .OrderByDescending(candidate => candidate.StockScore - candidate.Route.Days * 5)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool ShouldContinueTrading(ClassicAiState state)
    {
        Trader trader = state.Current.LocalTrader;
        if (!state.Current.IsCity || trader == null)
            return false;
        return CanPlanTrade(state, trader);
    }

    public static int RecommendedGroupSize(ClassicAiState state, int difficultyMaximum)
    {
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .ToArray();
        if (camps.Length == 0)
            return 2;

        int selfSupportingCamps = camps.Count(camp =>
        {
            Production.Rate rate = camp.GetFoodProductionRate();
            return camp.Production != null && !rate.IsCampStarving && rate.FoodPerDay > camp.CampNPC.Count();
        });

        // Keep one useful companion by default. Larger transport/attack groups are
        // affordable only after multiple camps produce a genuine food surplus.
        int supported = 2;
        if (camps.Length >= 2 && selfSupportingCamps >= 1)
            supported++;
        if (camps.Length >= 4 && selfSupportingCamps >= 2)
            supported++;
        return System.Math.Min(difficultyMaximum, supported);
    }

    public static bool NeedsExpansionTool(ClassicAiState state)
    {
        Location[] neutralCamps = state.RootGame.World.Locations
            .Where(location => !location.IsCity && location.Player == null)
            .ToArray();
        if (neutralCamps.Length == 0)
            return false;

        bool carried = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production != null && neutralCamps.Any(location =>
                location.ValidProductions.Contains(item.Type.Production)));
        bool pooled = neutralCamps.Any(location => state.Pool.HasTrap(state.AvailableProducts(location)));
        return !carried && !pooled;
    }

    public static bool CanBootstrapCamp(ClassicAiState state, Location location)
    {
        if (IsThreatened(state, location))
            return false;
        return location.ValidProductions.Any(production =>
            !production.GetRate(toolCount: 0, npcCount: 1).IsCampStarving);
    }

    public static Location FindBestCampForCollection(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Select(location => new
            {
                Location = location,
                Route = StrategicAiPlanner.FindRoute(state.Player, state.Current, location),
                Value = CampCollectibleValue(state, location)
            })
            .Where(candidate => candidate.Route != null && candidate.Value > 0)
            .OrderByDescending(candidate => candidate.Value - candidate.Route.Days * 2)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static Location FindBestCampForDelivery(ClassicAiState state)
    {
        bool carriesPump = state.Player.Group.SelectMany(character => character.Items).Any(IsPump);
        int carriedToolCount = state.Player.Group.SelectMany(character => character.Items)
            .Count(item => item.Type.Production != null);
        bool carriesTool = carriedToolCount > 1 ||
            (carriedToolCount > 0 && !HasNeutralExpansionOpportunity(state));
        bool completeRecipe = HasCompleteFocusedRecipe(state);
        bool hasReservedProduction = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Any(location => state.Pool.HasTrap(state.AvailableProducts(location)));
        if (!carriesPump && !carriesTool && !completeRecipe && !hasReservedProduction)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Where(location =>
                (carriesPump && NeedsPump(location)) ||
                (carriesTool && HasConstructibleProductionNeed(location)) ||
                (state.Pool.HasTrap(state.AvailableProducts(location)) && HasConstructibleProductionNeed(location)) ||
                (completeRecipe && (HasConstructibleProductionNeed(location) || NeedsPump(location))))
            .Select(location => new
            {
                Location = location,
                Route = StrategicAiPlanner.FindRoute(state.Player, state.Current, location)
            })
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Route.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
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
        var frontierGuards = camps.Where(location => IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead));
        var rearGuards = camps.Where(location => !IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == player && !npc.IsDead));

        foreach (Character guard in frontierGuards)
            EquipWeapon(state, guard, upgradeWeakWeapon: true, "frontier guard");
        foreach (Character follower in player.Group.Where(character => character != player.Character && !character.IsDead))
            EquipWeapon(state, follower, upgradeWeakWeapon: false, "follower");
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
        bool allowProductionTool = currentDamage == 0 || !reserveProductionTool;
        if (desiredMinimum == int.MaxValue ||
            !state.Pool.HasBetterWeapon(desiredMinimum, allowProductionTool))
        {
            if (current != null)
                character.Weapon = current;
            return;
        }

        Item weapon = state.Pool.GetBestWeapon(desiredMinimum, allowProductionTool);
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
        CollectStoredTradeGoods(state, camp);
    }

    static void InstallProductionFromPool(ClassicAiState state, Location camp)
    {
        string[] products = state.AvailableProducts(camp);
        if (!state.Pool.HasTrap(products) || CompatibleToolCount(camp) >= 2)
            return;
        if (NeedsExpansionTool(state) && state.Pool.ProductionToolCount <= 1)
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

    static void CollectStoredTradeGoods(ClassicAiState state, Location camp)
    {
        List<Item> collected = new();
        foreach (Room room in camp.Rooms)
        {
            foreach (Item item in room.Items.ToArray())
            {
                if (camp.Production != null && item.Type == camp.Production.Produce)
                    continue;
                if (item.Type.Production != null && camp.ValidProductions.Contains(item.Type.Production))
                    continue;
                if (IsPump(item) || !CanSell(state, item))
                    continue;
                Character carrier = state.Player.Group.FirstOrDefault(character => !character.Items.IsFull);
                if (carrier == null)
                    break;
                room.Items.Remove(item);
                carrier.Items.Add(item);
                collected.Add(item);
            }
        }

        if (collected.Count > 0)
            StrategicAiTelemetry.Report(state.Player,
                $"collected stored trade goods from {camp.Title}: " +
                string.Join(", ", collected.GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)));
    }

    static float CampCollectibleValue(ClassicAiState state, Location camp)
    {
        float value = 0;
        if (camp.Production != null)
        {
            int stock = camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce));
            int reserve = System.Math.Max(2, camp.CampNPC.Count(npc => npc.Player == state.Player));
            value += System.Math.Max(0, stock - reserve) * camp.Production.Produce.TradeValue;
        }

        value += camp.Rooms.SelectMany(room => room.Items)
            .Where(item => (camp.Production == null || item.Type != camp.Production.Produce) &&
                (item.Type.Production == null || !camp.ValidProductions.Contains(item.Type.Production)) &&
                !IsPump(item) && CanSell(state, item))
            .Sum(item => item.TradeValue);
        return value;
    }

    static void TradeAtCity(ClassicAiState state)
    {
        Trader trader = state.Current.LocalTrader;
        if (trader == null || trader.Items.Count == 0)
            return;

        TradePlan plan = CreateTradePlan(state, trader);
        if (plan == null)
        {
            if (trader.Items.Any(item => ShoppingPriority(state, item) > 0) &&
                state.Player.Group.SelectMany(character => character.Items).Any(item => CanSell(state, item)))
            {
                string signature = trader.Name;
                TradeFailureState failure = LastReportedTradeFailure.GetOrCreateValue(state.Player);
                if (failure.Signature != signature)
                {
                    StrategicAiTelemetry.Report(state.Player,
                        $"could not complete a useful trade with {trader.Name}: insufficient safe offers or inventory space");
                    failure.Signature = signature;
                }
            }
            return;
        }

        trader.Items.Remove(plan.Target);
        foreach (TradeAsset offer in plan.Offers)
        {
            if (!offer.FromPool)
                offer.Owner.Remove(offer.Item);
            trader.Items.Add(offer.Item);
        }

        if (AiItemPool.Accepts(plan.Target.Type))
            state.Pool.Insert(plan.Target);
        else
            state.Player.Group.First(character => !character.Items.IsFull).Items.Add(plan.Target);

        RestoreUnusedPoolAssets(state, plan.TemporaryPoolAssets.Except(plan.Offers));
        LastReportedTradeFailure.Remove(state.Player);
        StrategicAiTelemetry.Report(state.Player,
            $"traded {string.Join(", ", plan.Offers.Select(offer => offer.Item.ID))} for " +
            $"{plan.Target.ID} with {trader.Name} (AI barter value x{TradeBenefit(state):0.0})");
    }

    static bool CanPlanTrade(ClassicAiState state, Trader trader)
    {
        TradePlan plan = CreateTradePlan(state, trader);
        if (plan == null)
            return false;
        RestoreUnusedPoolAssets(state, plan.TemporaryPoolAssets);
        return true;
    }

    static TradePlan CreateTradePlan(ClassicAiState state, Trader trader)
    {
        if (trader == null || trader.Items.Count == 0)
            return null;

        int maxOffers = trader.Items.MaxCount - trader.Items.Count + 1;
        if (maxOffers <= 0)
            return null;

        List<TradeAsset> temporaryPoolAssets = TakeSurplusPoolContainers(state);
        List<TradeAsset> allCandidates = state.Player.Group
            .SelectMany(character => character.Items.Select(item => new TradeAsset(character.Items, item, false)))
            .Where(asset => CanSell(state, asset.Item))
            .Concat(temporaryPoolAssets)
            .OrderBy(asset => SalePriority(asset.Item))
            .ThenByDescending(asset => asset.Item.TradeValue)
            .ToList();

        foreach (Item target in trader.Items
            .Where(item => item.TradeValue > 0 && ShoppingPriority(state, item) > 0)
            .OrderByDescending(item => ShoppingPriority(state, item))
            .ThenBy(item => item.TradeValue))
        {
            List<TradeAsset> offers = new();
            float offeredValue = 0;
            int remainingFood = state.Player.Group.GetFoodReserve() + state.Player.Group.GetFoodInInventory();
            foreach (TradeAsset candidate in allCandidates.Where(asset => asset.Item.ID != target.ID))
            {
                if (offers.Count >= maxOffers)
                    break;
                if (candidate.Item.FoodValue > 0 &&
                    remainingFood - candidate.Item.FoodValue < state.Player.Group.Count * 6)
                    continue;

                offers.Add(candidate);
                offeredValue += candidate.Item.TradeValue * TradeBenefit(state);
                remainingFood -= candidate.Item.FoodValue;
                if ((int)offeredValue >= (int)target.TradeValue)
                    break;
            }

            bool canStoreTarget = AiItemPool.Accepts(target.Type) ||
                state.Player.Group.GetFreeSlotCount() > 0 || offers.Any(offer => !offer.FromPool);
            if (offers.Count > 0 && (int)offeredValue >= (int)target.TradeValue && canStoreTarget)
                return new TradePlan(target, offers, temporaryPoolAssets);
        }

        RestoreUnusedPoolAssets(state, temporaryPoolAssets);
        return null;
    }

    static float TradeBenefit(ClassicAiState state) => state.RootGame.World.Difficulty switch
    {
        0 => 1.0f,
        1 => 1.2f,
        _ => 1.5f
    };

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
        bool earlyEconomy = state.OwnedCampCount < 3;
        if (NeedsExpansionTool(state) && item.Type.Production != null &&
            IsUsefulForNeutralExpansion(state, item.Type.Production))
            return 1200 + item.FoodValue + item.DamageValue;
        if (earlyEconomy && item.Type.Production != null && NeedsProduction(state, item.Type))
            return 1050 + item.FoodValue;
        if (item.DamageValue > 0 && NeedsWeapons(state))
            return 1000 + item.DamageValue;
        if (item.Type.IsClass("protection") && NeedsDangerProtection(state, item.Type))
            return 950 + item.TradeValue;
        if (item.Type.Production != null && NeedsProduction(state, item.Type))
            return 900 + item.FoodValue;
        if (IsPump(item) && NeedsAnyPump(state))
            return (earlyEconomy ? 970 : 850) + (item.ID == "item_industrial_pump" ? 20 : 0);
        if (ConstructionMaterials.Contains(item.ID) && NeedsConstructionMaterials(state, item.ID))
        {
            int missing = FocusedRecipe(state).Count(id =>
                !state.Player.Group.SelectMany(character => character.Items).Any(item => item.ID == id));
            return (earlyEconomy ? 780 : 700) + System.Math.Max(0, 4 - missing) * 35 + item.TradeValue;
        }
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
        if ((IsPump(item) && NeedsAnyPump(state)) ||
            (item.Type.Production != null && state.OwnedCampCount == 0))
            return false;
        if (item.Type.Production != null && HasNeutralExpansionOpportunity(state) &&
            state.Pool.ProductionToolCount == 0 &&
            state.Player.Group.SelectMany(character => character.Items)
                .Count(candidate => candidate.Type.Production != null) <= 1)
            return false;
        if (AiItemPool.IsWaterContainer(item.Type))
            return false;
        if (item.Type.IsClass("weapon") && NeedsWeapons(state))
            return false;
        if (ConstructionMaterials.Contains(item.ID) && FocusedRecipe(state).Contains(item.ID) &&
            state.Player.Group.SelectMany(character => character.Items).Count(candidate => candidate.ID == item.ID) <= 1)
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
            .Where(location => location.Player == state.Player && IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player));
        return state.Player.Group.Count == 1 ||
            followers.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0) ||
            guards.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) < 33);
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

    static bool HasNeutralExpansionOpportunity(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => !location.IsCity && location.Player == null);

    static bool IsUsefulForNeutralExpansion(ClassicAiState state, Production production) =>
        state.RootGame.World.Locations.Any(location => !location.IsCity && location.Player == null &&
            location.ValidProductions.Contains(production));

    static bool IsThreatened(ClassicAiState state, Location origin)
    {
        int radius = state.RootGame.World.Difficulty == 0 ? 1 : 2;
        HashSet<Location> threats = state.RootGame.World.Locations
            .Where(location => location.Player != null && location.Player != state.Player)
            .ToHashSet();
        foreach (Player opponent in state.RootGame.World.Players.Where(player =>
            player != state.Player && !player.IsDead && player.Location != null))
            threats.Add(opponent.Location);
        if (threats.Contains(origin))
            return true;

        Queue<(Location Location, int Distance)> queue = new();
        HashSet<Location> visited = new() { origin };
        queue.Enqueue((origin, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Distance >= radius)
                continue;
            for (int index = 0; index < current.Location.Neighbors.Count; index++)
            {
                if (current.Location.WayLengths[index] <= 0)
                    continue;
                Location neighbor = current.Location.Neighbors[index];
                if (!visited.Add(neighbor))
                    continue;
                if (threats.Contains(neighbor))
                    return true;
                queue.Enqueue((neighbor, current.Distance + 1));
            }
        }
        return false;
    }

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

    static bool HasCompleteFocusedRecipe(ClassicAiState state)
    {
        string[] recipe = FocusedRecipe(state);
        IEnumerable<Item> carried = state.Player.Group.SelectMany(character => character.Items);
        return recipe.Length > 0 && recipe.All(itemId => carried.Any(item => item.ID == itemId));
    }

    sealed record TradeAsset(IItemCollection Owner, Item Item, bool FromPool);
    sealed record TradePlan(Item Target, List<TradeAsset> Offers, List<TradeAsset> TemporaryPoolAssets);
    sealed class TradeFailureState
    {
        public string Signature;
    }
}
