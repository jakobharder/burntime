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

    static readonly HashSet<string> ConstructionMaterials =
        AiItemPool.ConstructionMaterialIds.ToHashSet();

    public static void Run(ClassicAiState state)
    {
        RemoveAdviceItems(state);
        RefillConstructionReserve(state);
        ConstructPortableEconomicUpgrade(state);
        EquipEmpire(state);

        if (state.Current.Player == state.Player)
            MaintainCurrentCamp(state);

        ConstructPortableWeapon(state);

        Trader trader = FindEncounteredTrader(state);
        if (trader != null)
        {
            TradeWithTrader(state, trader);
            RefillConstructionReserve(state);
            ConstructPortableEconomicUpgrade(state);
            ConstructPortableWeapon(state);
        }

        // A purchase or construction may satisfy an equipment need immediately.
        EquipEmpire(state);
    }

    static void ConstructPortableEconomicUpgrade(ClassicAiState state)
    {
        string[] wanted = UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Result is
                "item_trap" or "item_rat_trap" or "item_protective_suit")
            .OrderByDescending(opportunity => opportunity.EconomicValue)
            .Select(opportunity => opportunity.Result)
            .Distinct()
            .ToArray();
        if (wanted.Length == 0)
            return;

        List<IItemCollection> sources = state.Player.Group
            .Select(character => (IItemCollection)character.Items)
            .ToList();
        Item result = state.RootGame.Constructions.TryConstructAny(
            sources, state.Pool, state.RootGame, wanted);
        if (result == null)
            return;

        state.Pool.Insert(result);
        StrategicAiTelemetry.Report(state.Player,
            $"assembled {result.ID} from shared construction materials");
    }

    static void RefillConstructionReserve(ClassicAiState state)
    {
        List<(IItemCollection Owner, Item Item)> available = new();
        if (state.Current.Player == state.Player)
        {
            available.AddRange(state.Current.Rooms
                .SelectMany(room => room.Items.Select(item => ((IItemCollection)room.Items, item))));
            available.AddRange(state.Current.CampNPC
                .Where(character => character.Player == state.Player)
                .SelectMany(character => character.Items
                    .Where(item => character.Weapon != item && character.Protection != item)
                    .Select(item => ((IItemCollection)character.Items, item))));
        }
        available.AddRange(state.Player.Group
            .SelectMany(character => character.Items
                .Where(item => character.Weapon != item && character.Protection != item)
                .Select(item => ((IItemCollection)character.Items, item))));

        List<string> reserved = new();
        foreach (string itemId in AiItemPool.ConstructionMaterialIds)
        {
            if (state.Pool.GetConstructionMaterialCount(itemId) > 0)
                continue;

            (IItemCollection Owner, Item Item) candidate = available
                .FirstOrDefault(entry => entry.Item.ID == itemId);
            if (candidate.Item == null || !state.Pool.TryReserveConstructionMaterial(candidate.Item))
                continue;

            candidate.Owner.Remove(candidate.Item);
            available.Remove(candidate);
            reserved.Add(itemId);
        }

        if (reserved.Count > 0)
            StrategicAiTelemetry.Report(state.Player,
                $"reserved construction materials: {string.Join(", ", reserved)}");
    }

    static void RemoveAdviceItems(ClassicAiState state)
    {
        IEnumerable<IItemCollection> inventories = state.Player.Group
            .Select(character => (IItemCollection)character.Items)
            .Concat(state.RootGame.World.Locations
                .Where(location => location.Player == state.Player)
                .SelectMany(location => location.Rooms.Select(room => (IItemCollection)room.Items)
                    .Concat(location.CampNPC
                        .Where(character => character.Player == state.Player)
                        .Select(character => (IItemCollection)character.Items))));
        foreach (IItemCollection inventory in inventories)
        {
            foreach (Item advice in inventory.Where(item => item.ID == "item_advice").ToArray())
                inventory.Remove(advice);
        }
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
                StockScore = TraderOpportunityScore(state, location.LocalTrader),
                AssortmentScore = TraderAssortmentOpportunityScore(state, location.LocalTrader)
            })
            .Where(candidate => candidate.Route != null &&
                (candidate.StockScore > 0 || candidate.AssortmentScore > 0) &&
                (CanPlanTrade(state, candidate.Location.LocalTrader) || candidate.AssortmentScore > 0))
            .OrderByDescending(candidate => System.Math.Max(candidate.StockScore, candidate.AssortmentScore) -
                candidate.Route.Days * 5)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    static float TraderOpportunityScore(ClassicAiState state, Trader trader)
    {
        float[] opportunities = trader.Items
            .GroupBy(item => item.ID)
            .Select(group => group.Max(item => ShoppingPriority(state, item)))
            .Where(priority => priority > 0)
            .OrderByDescending(priority => priority)
            .Take(3)
            .ToArray();
        if (opportunities.Length == 0)
            return 0;
        return opportunities[0] + opportunities.Skip(1).Sum() * 0.35f;
    }

    static float TraderAssortmentOpportunityScore(ClassicAiState state, Trader trader)
    {
        float[] materialOpportunities = trader.GetAssortment()
            .Select(type => ConstructionMaterialPriority(state, type.ID))
            .Where(priority => priority > 0)
            .OrderByDescending(priority => priority)
            .Take(3)
            .ToArray();
        if (materialOpportunities.Length == 0)
            return 0;
        return materialOpportunities[0] * 0.9f +
            materialOpportunities.Skip(1).Sum() * 0.25f;
    }

    public static bool ShouldContinueTrading(ClassicAiState state)
    {
        Trader trader = state.Current.LocalTrader;
        if (!state.Current.IsCity || trader == null)
            return false;
        return CanPlanTrade(state, trader);
    }

    static Trader FindEncounteredTrader(ClassicAiState state)
    {
        // Preserve normal city trading. Roaming traders are purely opportunistic:
        // use one when expansion or collection happens to cross its path, but never
        // wait for it or make it a travel destination.
        if (state.Current.IsCity && state.Current.LocalTrader != null)
            return state.Current.LocalTrader;

        return state.Current.Characters
            .OfType<Trader>()
            .Where(trader => !trader.IsDead && trader.Items.Count > 0)
            .OrderByDescending(trader => TraderOpportunityScore(state, trader))
            .FirstOrDefault(trader => CanPlanTrade(state, trader));
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

    public static bool ShouldPreferProductionAtCamp(ClassicAiState state, Location location) =>
        location.Danger == null && !IsThreatened(state, location);

    public static Location FindBestCampForCollection(ClassicAiState state)
    {
        int sellableCargo = state.Player.Group.SelectMany(character => character.Items)
            .Count(item => CanSell(state, item));
        if (state.Player.Group.GetFreeSlotCount() <= state.Player.Group.Count ||
            sellableCargo >= state.Player.Group.Count * 2)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Select(location => new
            {
                Location = location,
                Route = StrategicAiPlanner.FindRoute(state.Player, state.Current, location),
                Value = CampCollectibleValue(state, location),
                FoodBlocked = IsFoodStockCapped(location)
            })
            .Where(candidate => candidate.Route != null && candidate.Value > 0)
            .OrderByDescending(candidate => candidate.FoodBlocked)
            .ThenByDescending(candidate => candidate.Value - candidate.Route.Days * 2)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool IsFoodStockCapped(Location camp) => camp.Production != null &&
        camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce)) >= Location.MaxStockFood;

    public static bool ShouldPreventFoodWaste(ClassicAiState state, Location camp) =>
        IsFoodStockCapped(camp) &&
        state.Player.Group.GetFreeSlotCount() > state.Player.Group.Count &&
        state.Player.Group.SelectMany(character => character.Items).Count(item => CanSell(state, item)) <
            state.Player.Group.Count * 2;

    public static Location FindBestCampForDelivery(ClassicAiState state)
    {
        bool carriesPump = state.Player.Group.SelectMany(character => character.Items).Any(IsPump);
        bool completeRecipe = HasCompleteUsefulRecipe(state);
        bool hasProductionUpgrade = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Any(location => HasPortableBestProduction(state, location));
        if (!carriesPump && !completeRecipe && !hasProductionUpgrade)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Where(location =>
                (carriesPump && NeedsPump(location)) ||
                HasPortableBestProduction(state, location) ||
                (completeRecipe && CanUseCompleteRecipeAtCamp(state, location)))
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

    public static Location FindBestCampForStationing(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Select(location => new
            {
                Location = location,
                Route = StrategicAiPlanner.FindRoute(state.Player, state.Current, location),
                Threatened = IsThreatened(state, location),
                Guards = location.CampNPC.Count(npc => npc.Player == state.Player && !npc.IsDead)
            })
            .Where(candidate => candidate.Route != null)
            .OrderByDescending(candidate => candidate.Threatened)
            .ThenBy(candidate => candidate.Guards)
            .ThenBy(candidate => candidate.Route.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool ShouldReserveProductionTool(ClassicAiState state)
    {
        if (!HasNeutralExpansionOpportunity(state))
            return false;
        int portableTools = state.Pool.ProductionToolCount + state.Player.Group
            .SelectMany(character => character.Items)
            .Count(item => item.Type.Production != null);
        return portableTools <= 1;
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

        // The AI pool is shared empire-wide. Put its best compatible production
        // tools to work immediately instead of leaving them in hidden stock.
        foreach (Location camp in camps
            .OrderByDescending(location => ShouldPreferProductionAtCamp(state, location))
            .ThenBy(location => location.GetFoodProductionRate().FoodPerDay))
            InstallProductionFromPool(state, camp);

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

        CarryStrategicProtection(state);
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

    static void CarryStrategicProtection(ClassicAiState state)
    {
        if (DesiredProtectionReserve(state) < 2 || state.Pool.ProtectionCount == 0 ||
            state.Player.Group.SelectMany(character => character.Items)
                .Any(item => AiItemPool.IsHazardProtection(item.Type)))
            return;

        Character carrier = state.Player.Group.FirstOrDefault(character => !character.Items.IsFull);
        if (carrier == null)
            return;
        Item protection = state.Pool.GetBestGeneralProtection();
        if (protection == null)
            return;
        carrier.Items.Add(protection);
        carrier.Protection = protection;
        StrategicAiTelemetry.Report(state.Player,
            $"carried {protection.ID} as strategic hazard protection");
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
        CollectRedundantProductionTools(state, camp);
        InstallProductionFromPool(state, camp);
        InstallLoosePump(state, camp);
        ConstructForCamp(state, camp);
        CollectProducedSurplus(state, camp);
        CollectStoredTradeGoods(state, camp);
    }

    static void InstallProductionFromPool(ClassicAiState state, Location camp)
    {
        string[] products = camp.ValidProductions
            .Where(production => NeedsProductionResult(state, camp, production.Produce.ID))
            .OrderByDescending(ProductionTradePriority)
            .Select(production => production.Produce.ID)
            .ToArray();
        if (products.Length == 0 || !state.Pool.HasTrap(products))
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

    static bool HasPortableBestProduction(ClassicAiState state, Location camp)
    {
        Production best = camp.ValidProductions
            .OrderByDescending(ProductionTradePriority)
            .ThenByDescending(production => production.Produce.FoodValue)
            .FirstOrDefault();
        if (best == null || ProductionToolCount(camp, best) >= DesiredProductionToolCount(state, camp, best))
            return false;

        bool carried = state.Player.Group.SelectMany(character => character.Items)
            .Any(item => item.Type.Production == best);
        bool pooled = state.Pool.GetContents()
            .Any(entry => entry.Count > 0 && entry.Type.Production == best);
        return carried || pooled;
    }

    static void ConstructForCamp(ClassicAiState state, Location camp)
    {
        List<string> wanted = new();
        Production meat = camp.ValidProductions.FirstOrDefault(production => production.Produce.ID == "item_meat");
        Production rats = camp.ValidProductions.FirstOrDefault(production => production.Produce.ID == "item_rats");
        if (meat != null && ProductionToolCount(camp, meat) < DesiredProductionToolCount(state, camp, meat))
            wanted.Add("item_trap");
        if (rats != null && ProductionToolCount(camp, rats) < DesiredProductionToolCount(state, camp, rats))
            wanted.Add("item_rat_trap");

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
                sources, state.Pool, state.RootGame, wanted.ToArray());
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
                sources, state.Pool, state.RootGame, "item_loaded_rifle", "item_loaded_pistol");
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

    static void TradeWithTrader(ClassicAiState state, Trader trader)
    {
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
        }

        PruneAndStoreTraderOffers(trader, plan.Offers.Select(offer => offer.Item));

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

    static void PruneAndStoreTraderOffers(Trader trader, IEnumerable<Item> offers)
    {
        Item[] offeredItems = offers.ToArray();
        if (trader.Items.MaxCount == ItemList.Infinite)
        {
            foreach (Item item in offeredItems)
                trader.Items.Add(item);
            return;
        }

        HashSet<Item> kept = trader.Items.Concat(offeredItems)
            .OrderByDescending(item => item.TradeValue)
            .ThenBy(item => item.Type.IsClass("useless"))
            .Take(trader.Items.MaxCount)
            .ToHashSet();
        foreach (Item item in trader.Items.Where(item => !kept.Contains(item)).ToArray())
            trader.Items.Remove(item);
        foreach (Item item in offeredItems.Where(kept.Contains))
            trader.Items.Add(item);
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

        List<TradeAsset> temporaryPoolAssets = TakeSurplusPoolAssets(state);
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

    static List<TradeAsset> TakeSurplusPoolAssets(ClassicAiState state)
    {
        List<TradeAsset> assets = new();
        while (state.Pool.WaterContainerCount > 1)
        {
            Item item = state.Pool.TakeLeastWaterContainer();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        while (GlobalProtectionStock(state) > DesiredProtectionReserve(state) && state.Pool.ProtectionCount > 0)
        {
            Item item = state.Pool.TakeLeastProtection();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        // Three portable production tools are enough to seed the next camps. Convert
        // additional low-tier tools into denser trade value instead of hoarding them.
        while (state.Pool.ProductionToolCount > 3)
        {
            Item item = state.Pool.TakeLeastProductionTool();
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
            return 1200 + ProductionTradePriority(item.Type.Production);
        if (item.Type.Production != null && NeedsProduction(state, item.Type))
        {
            bool rearCampNeed = state.RootGame.World.Locations.Any(location =>
                location.Player == state.Player && ShouldPreferProductionAtCamp(state, location) &&
                location.ValidProductions.Contains(item.Type.Production) &&
                ProductionToolCount(location, item.Type.Production) <
                    DesiredProductionToolCount(state, location, item.Type.Production));
            return (rearCampNeed ? 1125 : earlyEconomy ? 1050 : 900) +
                ProductionTradePriority(item.Type.Production);
        }
        if (item.DamageValue > 0 && NeedsWeapons(state))
            return 1000 + item.DamageValue;
        if (AiItemPool.IsHazardProtection(item.Type) &&
            (NeedsDangerProtection(state, item.Type) || GlobalProtectionStock(state) < DesiredProtectionReserve(state)))
            return 950 + item.TradeValue;
        if (IsPump(item) && NeedsAnyPump(state))
            return (earlyEconomy ? 970 : 850) + (item.ID == "item_industrial_pump" ? 20 : 0);
        float materialPriority = ConstructionMaterialPriority(state, item.ID);
        if (materialPriority > 0)
            return materialPriority + item.TradeValue;

        int lowestFood = state.Player.Group.SelectMany(character => character.Items)
            .Where(candidate => candidate.FoodValue > 0)
            .Select(candidate => candidate.FoodValue)
            .DefaultIfEmpty(0)
            .Min();
        int lowerFoodItems = state.Player.Group.SelectMany(character => character.Items)
            .Count(candidate => candidate.FoodValue > 0 && candidate.FoodValue < item.FoodValue);
        if (item.FoodValue > lowestFood && lowestFood > 0 &&
            (state.Player.Group.GetFreeSlotCount() <= 3 || lowerFoodItems >= 2))
        {
            return 640 + (item.FoodValue - lowestFood) * 12 + item.TradeValue;
        }
        if (AiItemPool.IsWaterContainer(item.Type) && NeedsBetterWaterContainers(state, item.Type))
            return 600 + AiItemPool.WaterContainerCapacity(item.Type);
        if (IsTradeValueUpgrade(state, item))
            return 500 + item.TradeValue;
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
        if (AiItemPool.IsHazardProtection(item.Type) &&
            GlobalProtectionStock(state) <= DesiredProtectionReserve(state))
            return false;
        if (item.Type.IsClass("weapon") && NeedsWeapons(state))
            return false;
        if (ConstructionMaterials.Contains(item.ID) && ConstructionMaterialPriority(state, item.ID) > 0 &&
            state.Pool.GetConstructionMaterialCount(item.ID) == 0 &&
            state.Player.Group.SelectMany(character => character.Items).Count(candidate => candidate.ID == item.ID) <= 1)
            return false;
        if (item.ID == "item_advice")
            return false;
        return true;
    }

    static int SalePriority(Item item)
    {
        if (item.Type.IsClass("useless"))
            return 0;
        if (item.FoodValue > 0)
            return item.FoodValue <= 3 ? 1 : 3;
        if (item.Type.IsClass("protection"))
            return 2;
        return 3;
    }

    static float ProductionTradePriority(Production production) =>
        production.Produce.ID switch
        {
            "item_meat" => 240,
            "item_snake" => 200,
            "item_rats" => 100,
            _ => 0
        } + production.Produce.TradeValue + production.Produce.FoodValue;

    static bool IsTradeValueUpgrade(ClassicAiState state, Item target)
    {
        if (AiItemPool.Accepts(target.Type) || target.TradeValue <= 0)
            return false;
        float highestUselessValue = state.Player.Group.SelectMany(character => character.Items)
            .Where(item => item.Type.IsClass("useless") && CanSell(state, item))
            .Select(item => item.TradeValue)
            .DefaultIfEmpty(0)
            .Max();
        return highestUselessValue > 0 && target.TradeValue > highestUselessValue;
    }

    static bool NeedsWeapons(ClassicAiState state)
    {
        IEnumerable<Character> followers = state.Player.Group.Where(character => character != state.Player.Character);
        IEnumerable<Character> guards = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player));
        return followers.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0) ||
            guards.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) < 33);
    }

    static bool NeedsProduction(ClassicAiState state, ItemType type)
    {
        if (type.Production == null)
            return false;
        Location[] camps = state.RootGame.World.Locations.Where(location => location.Player == state.Player).ToArray();
        return camps.Length == 0 || camps.Any(location =>
        {
            if (!location.ValidProductions.Contains(type.Production) ||
                ProductionToolCount(location, type.Production) >=
                    DesiredProductionToolCount(state, location, type.Production) ||
                !ShouldPreferProductionAtCamp(state, location))
                return false;
            Production best = location.ValidProductions
                .OrderByDescending(ProductionTradePriority)
                .ThenByDescending(production => production.Produce.FoodValue)
                .FirstOrDefault();
            return best == type.Production;
        });
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

    static int DesiredProtectionReserve(ClassicAiState state)
    {
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .ToArray();
        if (camps.Length == 0)
            return 0;
        return camps.Length >= 5 || camps.Any(location => location.Danger != null) ? 2 : 1;
    }

    static int GlobalProtectionStock(ClassicAiState state) => state.Pool.ProtectionCount +
        state.Player.Group.SelectMany(character => character.Items)
            .Count(item => AiItemPool.IsHazardProtection(item.Type));

    static bool NeedsBetterWaterContainers(ClassicAiState state, ItemType offered)
    {
        int offeredCapacity = AiItemPool.WaterContainerCapacity(offered);
        IEnumerable<Character> npcs = state.Player.Group.Where(character => character != state.Player.Character)
            .Concat(state.RootGame.World.Locations.Where(location => location.Player == state.Player)
                .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player)));
        return npcs.Any(npc => !HasWaterContainer(npc)) || offeredCapacity > state.Pool.BestWaterContainerCapacity;
    }

    static float ConstructionMaterialPriority(ClassicAiState state, string itemId)
    {
        if (!ConstructionMaterials.Contains(itemId) ||
            state.Pool.GetConstructionMaterialCount(itemId) > 0)
            return 0;

        ConstructionOpportunity[] opportunities = UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Materials.Contains(itemId))
            .ToArray();
        if (opportunities.Length == 0)
            return 0;

        float best = opportunities.Max(opportunity =>
        {
            int missing = opportunity.Materials.Count(component => !HasConstructionComponent(state, component));
            float completion = missing switch
            {
                <= 1 => 1250,
                2 => 980,
                _ => 820
            };
            return completion + opportunity.EconomicValue;
        });
        return best + System.Math.Max(0, opportunities.Length - 1) * 25;
    }

    static IEnumerable<ConstructionOpportunity> UsefulConstructionOpportunities(ClassicAiState state)
    {
        if (NeedsWeapons(state))
        {
            if (HasConstructionComponent(state, "item_unloaded_rifle"))
                yield return new("item_loaded_rifle", new[] { "item_unloaded_rifle", "item_ammunition" }, 90);
            if (HasConstructionComponent(state, "item_unloaded_pistol"))
                yield return new("item_loaded_pistol", new[] { "item_unloaded_pistol", "item_ammunition" }, 80);
        }

        if (HasPotentialProductionNeed(state, "item_meat"))
            yield return new("item_trap", new[] { "item_spring", "item_tin", "item_wire" }, 90);

        if (HasPotentialProductionNeed(state, "item_rats"))
            yield return new("item_rat_trap", new[] { "item_wire", "item_woodpile", "item_screws" }, 75);

        if (NeedsAnyPump(state))
        {
            yield return new("item_hand_pump", new[] { "item_broken_pump", "item_rags", "item_hose" }, 45);
            yield return new("item_industrial_pump",
                new[] { "item_spare_parts", "item_iron_pipe", "item_rags", "item_hose" }, 55);
        }

        ItemType protectiveSuit = state.RootGame.ItemTypes["item_protection_suit"];
        if (NeedsDangerProtection(state, protectiveSuit))
            yield return new("item_protective_suit",
                new[] { "item_gas_mask", "item_gloves", "item_protective_overall", "item_boots" }, 50);
    }

    static bool HasConstructionComponent(ClassicAiState state, string itemId) =>
        state.Pool.GetConstructionMaterialCount(itemId) > 0 ||
        state.Player.Group.SelectMany(character => character.Items).Any(item => item.ID == itemId);

    static bool HasCompleteUsefulRecipe(ClassicAiState state) => UsefulConstructionOpportunities(state)
        .Any(opportunity => opportunity.Materials.All(component => HasConstructionComponent(state, component)));

    static bool CanUseCompleteRecipeAtCamp(ClassicAiState state, Location camp) =>
        UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Materials.All(component => HasConstructionComponent(state, component)))
            .Any(opportunity => opportunity.Result switch
            {
                "item_trap" => NeedsProductionResult(state, camp, "item_meat"),
                "item_rat_trap" => NeedsProductionResult(state, camp, "item_rats"),
                "item_hand_pump" or "item_industrial_pump" => NeedsPump(camp),
                "item_protective_suit" => camp.Danger != null,
                _ => false
            });

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

    static int ProductionToolCount(Location camp, Production production) => camp.Rooms
        .SelectMany(room => room.Items)
        .Concat(camp.CampNPC.SelectMany(npc => npc.Items))
        .Count(item => item.Type.Production == production);

    static int DesiredProductionToolCount(ClassicAiState state, Location camp, Production production)
    {
        if (production.MaxToolCount <= 1)
            return production.MaxToolCount;

        // Spread the first valuable trap across suitable camps before concentrating
        // second copies, because the first trap produces the largest economic gain.
        bool allCompatibleCampsStarted = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && ShouldPreferProductionAtCamp(state, location))
            .Where(location => location.ValidProductions.Contains(production))
            .All(location => ProductionToolCount(location, production) >= 1);
        return allCompatibleCampsStarted ? production.MaxToolCount : 1;
    }

    static bool NeedsProductionResult(
        ClassicAiState state,
        Location camp,
        string productId)
    {
        Production production = camp.ValidProductions
            .FirstOrDefault(candidate => candidate.Produce.ID == productId);
        if (production == null || ProductionToolCount(camp, production) >=
            DesiredProductionToolCount(state, camp, production))
            return false;

        float candidateValue = ProductionTradePriority(production);
        float installedValue = camp.ValidProductions
            .Where(candidate => ProductionToolCount(camp, candidate) > 0)
            .Select(ProductionTradePriority)
            .DefaultIfEmpty(float.MinValue)
            .Max();
        return candidateValue >= installedValue;
    }

    static bool HasPotentialProductionNeed(ClassicAiState state, string productId) =>
        state.RootGame.World.Locations.Any(location => !location.IsCity &&
            location.ValidProductions.Any(production => production.Produce.ID == productId) &&
            (location.Player == null ||
                (location.Player == state.Player && ShouldPreferProductionAtCamp(state, location) &&
                    NeedsProductionResult(state, location, productId))));

    static void CollectRedundantProductionTools(ClassicAiState state, Location camp)
    {
        List<Item> collected = new();
        float bestInstalledValue = camp.ValidProductions
            .Where(production => ProductionToolCount(camp, production) > 0)
            .Select(ProductionTradePriority)
            .DefaultIfEmpty(float.MinValue)
            .Max();
        foreach (Production production in camp.ValidProductions)
        {
            int desired = ProductionTradePriority(production) < bestInstalledValue
                ? 0
                : DesiredProductionToolCount(state, camp, production);
            int excess = ProductionToolCount(camp, production) - desired;
            if (excess <= 0)
                continue;

            IEnumerable<(IItemCollection Owner, Item Item)> candidates = camp.Rooms
                .SelectMany(room => room.Items
                    .Where(item => item.Type.Production == production)
                    .Select(item => ((IItemCollection)room.Items, item)))
                .Concat(camp.CampNPC
                    .Where(npc => npc.Player == state.Player)
                    .SelectMany(npc => npc.Items
                        .Where(item => item.Type.Production == production && npc.Weapon != item)
                        .Select(item => ((IItemCollection)npc.Items, item))));

            foreach ((IItemCollection owner, Item item) in candidates.Take(excess).ToArray())
            {
                owner.Remove(item);
                state.Pool.Insert(item);
                collected.Add(item);
            }
        }

        if (collected.Count > 0)
            StrategicAiTelemetry.Report(state.Player,
                $"reclaimed redundant production tools from {camp.Title}: " +
                string.Join(", ", collected.GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)));
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

    sealed record ConstructionOpportunity(string Result, string[] Materials, int EconomicValue);
    sealed record TradeAsset(IItemCollection Owner, Item Item, bool FromPool);
    sealed record TradePlan(Item Target, List<TradeAsset> Offers, List<TradeAsset> TemporaryPoolAssets);
    sealed class TradeFailureState
    {
        public string Signature;
    }
}
