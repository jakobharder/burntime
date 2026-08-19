using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    const int StandingFoodDays = 9;
    const int StandingWaterDays = 7;
    const int MinimumTradeValue = 25;
    const int MinimumTradeItems = 4;
    const int MaximumCaravanPeople = 2;
    internal const int DailyTradeFoodMargin = 2;

    internal static readonly ConditionalWeakTable<Player, TradeFailureState>
        LastReportedTradeFailure = new();

    internal static readonly HashSet<string> ConstructionMaterials =
        AiItemPool.ConstructionMaterialIds.ToHashSet();

    public static bool ShouldVisitTrader(ClassicAiState state)
    {
        // Every local trader was already checked at the start of this turn. Do not
        // shuttle directly between cities when today's stock cannot complete a
        // useful trade. A substantial one-camp pickup may still take the group out
        // of an otherwise idle city and prepare the next attempt.
        if (state.Current.IsCity)
            return RouteOpportunities.FindCityTradePickupCamp(state) != null;
        if (FindBestTradeCity(state) == null)
            return false;

        // Trader stock is random and may change before arrival. Commit once the
        // group has a substantial export lot, then fill the attainable caravan
        // capacity at the departure camp.
        if (HasPreparedTradeCargo(state))
            return true;

        // A stocked owned camp on the route may initiate the trip even when the
        // travelling group is empty. This is the same single pickup used by an
        // already loaded caravan, not a chain of collection errands.
        return RouteOpportunities.FindCityTradePickupCamp(state) != null;
    }

    public static Location FindBestTradeCity(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity && location != state.Current && location.LocalTrader != null)
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                AssortmentScore = TraderAssortmentOpportunityScore(state, location.LocalTrader)
            })
            .Where(candidate => candidate.Route != null && candidate.AssortmentScore > 0)
            .OrderByDescending(candidate => candidate.AssortmentScore - candidate.Route.Days * 5)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static float TraderOpportunityScore(ClassicAiState state, Trader trader)
    {
        float[] opportunities = trader.Items
            .GroupBy(item => item.ID)
            .Select(group => group.Max(item => TradeTask.ShoppingPriority(state, item)))
            .Where(priority => priority > 0)
            .OrderByDescending(priority => priority)
            .Take(3)
            .ToArray();
        if (opportunities.Length == 0)
            return 0;
        return opportunities[0] + opportunities.Skip(1).Sum() * 0.35f;
    }

    internal static float TraderAssortmentOpportunityScore(ClassicAiState state, Trader trader)
    {
        float[] opportunities = trader.GetAssortment()
            .Select(type => TradeTask.AssortmentShoppingPriority(state, type))
            .Where(priority => priority > 0)
            .OrderByDescending(priority => priority)
            .Take(3)
            .ToArray();
        if (opportunities.Length == 0)
            return 0;
        return opportunities[0] + opportunities.Skip(1).Sum() * 0.25f;
    }

    public static bool ShouldContinueTrading(ClassicAiState state)
    {
        return state.Current.IsCity && EncounteredTraders(state).Any(trader => TradeTask.CanPlanTrade(state, trader));
    }

    internal static IEnumerable<Trader> EncounteredTraders(ClassicAiState state)
    {
        List<Trader> traders = new();
        if (state.Current.IsCity && state.Current.LocalTrader != null)
            traders.Add(state.Current.LocalTrader);

        traders.AddRange(state.Current.Characters
            .OfType<Trader>()
            .Where(trader => !trader.IsDead && trader.Items.Count > 0)
            .OrderByDescending(trader => TraderOpportunityScore(state, trader))
            .Where(trader => !traders.Contains(trader)));
        return traders;
    }

    internal static int CargoSpaceReserve(ClassicAiState state) => 0;

    internal static bool IsTradeCaravanReady(ClassicAiState state) =>
        HasAffordableHighReturnTradeCargo(state) ||
        HasSubstantialTradeCargo(state) &&
        OccupiedCargoSlots(state) >= DesiredCaravanSlots(state);

    internal static int DesiredPortableFood(ClassicAiState state) =>
        state.Player.Group.Count * StandingFoodDays;

    internal static int DesiredPortableWaterCapacity(ClassicAiState state) =>
        state.Player.Group.Count * StandingWaterDays;

    internal static int DesiredCaravanSlots(ClassicAiState state) => state.Player.Group
        .Take(MaximumCaravanPeople)
        .Sum(character => character.Items.MaxCount);

    internal static int OccupiedCargoSlots(ClassicAiState state) => state.Player.Group
        .Sum(character => character.Items.Count);

    internal static int PortableWaterCapacity(ClassicAiState state) => state.Player.Group
        .SelectMany(character => character.Items)
        .Where(item => AiItemPool.IsWaterContainer(item.Type))
        .Sum(item => AiItemPool.WaterContainerCapacity(item.Type));

    internal static int PortableFoodSupply(ClassicAiState state) =>
        state.Player.Group.GetFoodReserve() + state.Player.Group.GetFoodInInventory();

    internal static int PortableWaterSupply(ClassicAiState state) =>
        state.Player.Group.GetWaterReserve() + PortableWaterCapacity(state);

    internal static Item[] TradeCapital(ClassicAiState state)
    {
        bool mayLiquidateReserves = state.RootGame.World.Locations.Any(city =>
            city.IsCity && city.LocalTrader != null && CityHasHighReturnReservePurchase(state, city));
        return state.Player.Group.SelectMany(character => character.Items)
            .Where(item => TradeTask.CanSell(state, item) ||
                (mayLiquidateReserves && TradeTask.IsHighReturnLiquidReserve(item)))
            .ToArray();
    }

    internal static bool HasSubstantialTradeCargo(ClassicAiState state)
    {
        Item[] capital = TradeCapital(state);
        return capital.Length >= MinimumTradeItems ||
            capital.Sum(item => item.TradeValue) >= MinimumTradeValue;
    }

    internal static bool HasPreparedTradeCargo(ClassicAiState state) =>
        HasSubstantialTradeCargo(state) || HasAffordableHighReturnTradeCargo(state);

    internal static bool HasAffordableHighReturnTradeCargo(ClassicAiState state)
    {
        Item[] assets = state.Player.Group.SelectMany(character => character.Items)
            .Where(item => TradeTask.CanSell(state, item) || TradeTask.IsHighReturnLiquidReserve(item))
            .ToArray();
        if (assets.Length == 0)
            return false;

        int meleeWeapons = assets.Count(item =>
            item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));
        float spendableValue = assets
            .Where(item => item.DamageValue == 0 || AiItemPool.IsFirearm(item.Type))
            .Sum(item => item.TradeValue) +
            assets.Where(item => item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type))
                .OrderBy(item => item.TradeValue)
                .Take(System.Math.Max(0, meleeWeapons - 1))
                .Sum(item => item.TradeValue);
        float buyingPower = spendableValue * TradeTask.TradeBenefit(state);
        if (buyingPower <= 0)
            return false;

        return state.RootGame.World.Locations
            .Where(city => city.IsCity && city != state.Current && city.LocalTrader != null &&
                RouteFinder.Find(state.Player, state.Current, city) != null)
            .SelectMany(city =>
            {
                Trader trader = city.LocalTrader;
                return trader.GetAssortment();
            })
            .Any(type => IsHighReturnReservePurchase(state, type) && type.TradeValue <= buyingPower);
    }

    internal static bool CityHasHighReturnReservePurchase(ClassicAiState state, Location city)
    {
        Trader trader = city.LocalTrader;
        return trader != null && trader.GetAssortment().Any(type =>
            IsHighReturnReservePurchase(state, type));
    }

    internal static bool IsHighReturnReservePurchase(ClassicAiState state, ItemType type) =>
        type.ID == "item_snake_trap" &&
            (TradeTask.HasRegionalSnakeTrapNeed(state) || TradeTask.HasOwnedProductionNeed(state, "item_snake")) ||
        TradeTask.AdvancesOwnedMeatTrapRecipe(state, type.ID) ||
        TradeTask.IsEarlyRareProductionPurchase(state, type.ID);

    public static Location FindBestCampForCityPreparation(ClassicAiState state)
    {
        // Maintaining the current camp already collected everything useful. Always
        // continue to the city from there instead of cycling through another camp.
        // A valuable caravan may still use a near-route pickup while it has space;
        // value readiness alone should not discard that existing opportunity.
        if (state.Current.Player == state.Player || state.Player.Group.GetFreeSlotCount() == 0)
            return null;

        RouteFinder.Route directCityRoute = state.RootGame.World.Locations
            .Where(location => location.IsCity && location != state.Current)
            .Select(location => RouteFinder.Find(state.Player, state.Current, location))
            .Where(route => route != null)
            .OrderBy(route => route.Days)
            .FirstOrDefault();
        if (directCityRoute == null)
            return null;

        // At most one pickup stop, and only when it adds no more than two travel
        // days compared with going directly to the nearest city.
        Item[] carriedCapital = TradeCapital(state);
        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Select(location => new
            {
                Location = location,
                ToCamp = RouteFinder.Find(state.Player, state.Current, location),
                ToCity = state.RootGame.World.Locations
                    .Where(city => city.IsCity && city != location)
                    .Select(city => RouteFinder.Find(state.Player, location, city))
                    .Where(route => route != null)
                    .OrderBy(route => route.Days)
                    .FirstOrDefault()
            })
            .Where(candidate => candidate.ToCamp != null && candidate.ToCity != null &&
                candidate.ToCamp.Days + candidate.ToCity.Days <= directCityRoute.Days + 2)
            .Select(candidate => new
            {
                candidate.Location,
                Detour = candidate.ToCamp.Days + candidate.ToCity.Days - directCityRoute.Days,
                TravelDays = candidate.ToCamp.Days,
                Value = LocalOpportunities.ProjectedCampCollectibleValue(state, candidate.Location, candidate.ToCamp.Days)
            })
            .Where(candidate => IsSubstantialTradeLot(
                carriedCapital.Length + LocalOpportunities.ProjectedCampCollectibleCount(
                    state, candidate.Location, candidate.TravelDays),
                carriedCapital.Sum(item => item.TradeValue) + candidate.Value))
            .OrderByDescending(candidate => candidate.Value - candidate.Detour * 10)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public static bool ShouldFillCityCaravanBeforeDeparture(ClassicAiState state)
    {
        if (state.Current.Player != state.Player || state.Player.Group.GetFreeSlotCount() == 0)
            return false;
        Location tradeCity = FindBestTradeCity(state);
        if (tradeCity == null)
            return false;

        if (IsTradeCaravanReady(state))
            return false;

        Production.Rate rate = state.Current.GetFoodProductionRate();
        int guards = CampEconomy.LivingGuardCount(state.Current, state.Player);
        int localDailyExport = System.Math.Max(0, rate.FoodPerDay - guards);
        return rate.ItemDropInterval > 0 && localDailyExport > state.Player.Group.Count;
    }

    public static bool ShouldReduceTradeCaravan(ClassicAiState state)
    {
        if (state.Current.Player != state.Player || state.Player.Group.Count <= MaximumCaravanPeople ||
            !LocalOpportunities.ShouldPreferProductionAtCamp(state, state.Current))
            return false;
        int guards = CampEconomy.LivingGuardCount(state.Current, state.Player);
        return ReinforcementTask.CanSupportAdditionalGuard(state, state.Current, guards);
    }

    internal static bool IsSubstantialTradeLot(int items, float value) =>
        items >= MinimumTradeItems || value >= MinimumTradeValue;

}
