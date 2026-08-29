using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class TravelSupplies
{
    public static bool HasRouteSupplies(
        Player player,
        RouteFinder.Route route,
        bool hostileTarget,
        int unavailableFood = 0,
        int unavailableWater = 0)
    {
        int foodRequired = route.Days + 
            System.Math.Max(0, hostileTarget ? (route.Days - 1) : 0);
        int waterRequired = route.Days +
            System.Math.Max(0, hostileTarget ? route.Days : 0);
        int[] food = player.Group.Select(character => character.Food).ToArray();
        int[] water = player.Group.Select(character => character.Water).ToArray();
        return Group.GetLowestAfterDistribution(food,
                System.Math.Max(0, player.Group.GetFoodInInventory() - unavailableFood)) >= foodRequired &&
            Group.GetLowestAfterDistribution(water,
                System.Math.Max(0, player.Group.GetWaterInInventory() - unavailableWater)) >= waterRequired;
    }

    public static bool HasSettlementRoundTripFood(
        Player player,
        RouteFinder.Route outbound,
        RouteFinder.Route returnRoute) =>
        player.Group.GetLowestFoodWithInventory() >= outbound.Days + returnRoute.Days;

    public static bool HasSettlementRoundTripSupplies(
        Player player,
        RouteFinder.Route outbound,
        RouteFinder.Route returnRoute,
        int unavailableFood = 0,
        int unavailableWater = 0)
    {
        int required = outbound.Days + returnRoute.Days;
        int[] food = player.Group.Select(character => character.Food).ToArray();
        int[] water = player.Group.Select(character => character.Water).ToArray();
        return Group.GetLowestAfterDistribution(food,
                System.Math.Max(0, player.Group.GetFoodInInventory() - unavailableFood)) >= required &&
            Group.GetLowestAfterDistribution(water,
                System.Math.Max(0, player.Group.GetWaterInInventory() - unavailableWater)) >= required;
    }

    public static bool HasStagedRecruitSettlementSupplies(
        Player player,
        RouteFinder.Route toCity,
        RouteFinder.Route onward,
        RouteFinder.Route settlementReturn,
        int cityMinimum,
        int recruitFood,
        int recruitWater,
        int unavailableFood,
        int unavailableWater)
    {
        if (player.Group.Count != 1)
            return false;

        Character leader = player.Character;
        return HasStagedRecruitStat(
                leader.Food, leader.MaxFood,
                player.Group.GetFoodInInventory() - unavailableFood,
                toCity.Days, onward.Days, settlementReturn.Days,
                cityMinimum, recruitFood) &&
            HasStagedRecruitStat(
                leader.Water, leader.MaxWater,
                player.Group.GetWaterInInventory() - unavailableWater,
                toCity.Days, onward.Days, settlementReturn.Days,
                cityMinimum, recruitWater);
    }

    public static bool HasStagedRecruitOutboundSupplies(
        Player player,
        RouteFinder.Route toStop,
        RouteFinder.Route onward,
        int arrivalMinimum,
        int recruitFood,
        int recruitWater,
        int unavailableFood,
        int unavailableWater,
        int soloFoodReturnDays = 0)
    {
        if (player.Group.Count != 1)
            return false;

        Character leader = player.Character;
        return HasStagedRecruitStat(
                leader.Food, leader.MaxFood,
                player.Group.GetFoodInInventory() - unavailableFood,
                toStop.Days, onward.Days, soloFoodReturnDays,
                arrivalMinimum, recruitFood) &&
            HasStagedRecruitStat(
                leader.Water, leader.MaxWater,
                player.Group.GetWaterInInventory() - unavailableWater,
                toStop.Days, onward.Days, returnDays: 0,
                arrivalMinimum, recruitWater);
    }

    public static bool HasStagingCityReturnSupplies(
        Player player,
        RouteFinder.Route toCity,
        RouteFinder.Route returnRoute,
        int cityMinimum,
        int unavailableFood,
        int unavailableWater)
    {
        if (player.Group.Count != 1)
            return false;

        Character leader = player.Character;
        return HasStagingReturnStat(
                leader.Food, leader.MaxFood,
                player.Group.GetFoodInInventory() - unavailableFood,
                toCity.Days, returnRoute.Days, cityMinimum) &&
            HasStagingReturnStat(
                leader.Water, leader.MaxWater,
                player.Group.GetWaterInInventory() - unavailableWater,
                toCity.Days, returnRoute.Days, cityMinimum);
    }

    public static bool HasTerritorialRouteSupplies(
        Player player,
        Location start,
        RouteFinder.Route route,
        bool hostileTarget)
    {
        if (HasRouteSupplies(player, route, hostileTarget))
            return true;

        // A distant campaign may be advanced one safe leg at a time. The full
        // attack reserve is still required before the final move into enemy land.
        if (route.NextStep.Player != player && !route.NextStep.IsCity)
            return false;
        if (route.NextStep.Player == player)
        {
            int foodRequired = route.Days +
                System.Math.Max(0, hostileTarget ? route.Days - 1 : 0);
            int waterRequired = route.Days +
                System.Math.Max(0, hostileTarget ? route.Days : 0);
            int foodAvailable = Group.GetLowestAfterDistribution(
                player.Group.Select(character => character.Food).ToArray(),
                player.Group.GetFoodInInventory());
            int waterAvailable = Group.GetLowestAfterDistribution(
                player.Group.Select(character => character.Water).ToArray(),
                player.Group.GetWaterInInventory());
            int reserveBuildingSurplus = player.Group.Count + 1;

            // An owned stop may break a long route into legs only when it can
            // rebuild whichever reserve the group lacks. Merely feeding the
            // visitors, or holding a finite stored item, cannot support onward
            // travel indefinitely.
            if (foodAvailable < foodRequired &&
                CampEconomy.FoodSurplusPerDay(route.NextStep) < reserveBuildingSurplus)
                return false;
            if (waterAvailable < waterRequired &&
                CampEconomy.WaterSurplusPerDay(route.NextStep) < reserveBuildingSurplus)
                return false;
        }
        RouteFinder.Route? safeLeg = RouteFinder.Find(player, start, route.NextStep);
        return safeLeg != null && HasRouteSupplies(player, safeLeg, hostileTarget: false);
    }

    public static bool CanSurviveRecoveryRoute(Player player, RouteFinder.Route route)
    {
        int expectedDamage = ExpectedRecoveryRouteDamage(player, route);

        // A human player may deliberately arrive hungry, thirsty, or wounded
        // when a real recovery facility is the destination. Never approve a
        // route expected to kill even the weakest current group member.
        return player.Group.Min(character => character.Health) > expectedDamage;
    }

    public static int ExpectedRecoveryRouteDamage(Player player, RouteFinder.Route route)
    {
        int foodShortageDays = System.Math.Max(0,
            route.Days - player.Group.GetLowestFoodWithInventory());
        int waterShortageDays = System.Math.Max(0,
            route.Days - player.Group.GetLowestWaterWithInventory());
        return (foodShortageDays + waterShortageDays) * 25;
    }

    public static bool IsDehydrationTravelNoWorseThanWaiting(
        Player player,
        Location current,
        RouteFinder.Route route)
    {
        if (!player.Group.Any(character => character.Water == 0))
            return false;

        int routeWaterShortageDays = System.Math.Max(0,
            route.Days - player.Group.GetLowestWaterWithInventory());

        int residentConsumption = current.Player == null
            ? 0
            : CampEconomy.LivingGuardCount(current, current.Player);
        int dailyWaterSurplus = System.Math.Max(0,
            (current.Source?.Water ?? 0) - residentConsumption);
        int localWater = player.Group.GetWaterInInventory() +
            CampEconomy.StoredWaterValue(current) +
            (current.Source?.Reserve ?? 0) +
            dailyWaterSurplus * route.Days;
        int[] localReserves = player.Group.Select(character => character.Water).ToArray();
        int waitWaterShortageDays = System.Math.Max(0, route.Days -
            Group.GetLowestAfterDistribution(localReserves, localWater));

        return routeWaterShortageDays <= waitWaterShortageDays;
    }

    public static bool HasProjectedRecruitTerritorialSupplies(
        Player player,
        Location start,
        RouteFinder.Route route,
        int recruitFood,
        int recruitWater)
    {
        if (HasProjectedRecruitRouteSupplies(player, route, recruitFood, recruitWater))
            return true;

        // Longer settlement journeys are approved one productive waypoint at
        // a time. Production continues while the group travels, and the normal
        // local provisioning pass reassesses the next leg on arrival.
        if (route.NextStep.Player != player && !route.NextStep.IsCity)
            return false;
        RouteFinder.Route? firstLeg = RouteFinder.Find(player, start, route.NextStep);
        int projectedGroupSize = player.Group.Count + 1;
        bool sustainableWaypoint = route.NextStep.IsCity ||
            CampEconomy.FoodSurplusPerDay(route.NextStep) >= projectedGroupSize &&
            CampEconomy.CanProvisionGroupWater(
                route.NextStep, projectedGroupSize) ||
            CampEconomy.StoredFoodValue(route.NextStep) >= projectedGroupSize * 3 &&
            CampEconomy.CanProvisionGroupWater(
                route.NextStep, projectedGroupSize);
        return sustainableWaypoint && firstLeg != null &&
            HasProjectedRecruitRouteSupplies(
                player, firstLeg, recruitFood, recruitWater);
    }

    public static bool HasProjectedRecruitSoloReturnFood(
        Player player,
        RouteFinder.Route outbound,
        int recruitFood,
        int returnDays,
        int unavailableFood = 0)
    {
        if (player.Group.Count != 1)
            return true;
        int available = player.Character.Food + recruitFood +
            System.Math.Max(0, player.Group.GetFoodInInventory() - unavailableFood);
        return available >= outbound.Days * 2 + returnDays;
    }

    internal static bool HasProjectedRecruitRouteSupplies(
        Player player,
        RouteFinder.Route route,
        int recruitFood,
        int recruitWater,
        int unavailableFood = 0,
        int unavailableWater = 0)
    {
        // A settler recruited for a concrete camp departs with the leader on the
        // hiring turn, just as a human group can. The first normal consumption is
        // therefore already represented by the first day of the route.
        int[] food = player.Group.Select(character => character.Food)
            .Append(recruitFood)
            .ToArray();
        int[] water = player.Group.Select(character => character.Water)
            .Append(recruitWater)
            .ToArray();
        return Group.GetLowestAfterDistribution(food,
                System.Math.Max(0, player.Group.GetFoodInInventory() - unavailableFood)) >= route.Days &&
            Group.GetLowestAfterDistribution(water,
                System.Math.Max(0, player.Group.GetWaterInInventory() - unavailableWater)) >= route.Days;
    }

    /// <summary>
    /// Projects the existing group to a recruitment city, reserves any food or
    /// water used as payment, adds the recruit, and verifies that everybody can
    /// reach a sustainable return camp. This is shared by settlement and attack
    /// recruitment planning.
    /// </summary>
    public static bool HasStagedRecruitCityReturnSupplies(
        Player player,
        RouteFinder.Route toCity,
        RouteFinder.Route returnRoute,
        int cityMinimum,
        int recruitFood,
        int recruitWater,
        int unavailableFood,
        int unavailableWater)
    {
        return HasStagedRecruitCityReturnStat(
                player.Group.Select(character => character.Food).ToArray(),
                player.Group.GetFoodInInventory() - unavailableFood,
                toCity.Days, returnRoute.Days, cityMinimum, recruitFood) &&
            HasStagedRecruitCityReturnStat(
                player.Group.Select(character => character.Water).ToArray(),
                player.Group.GetWaterInInventory() - unavailableWater,
                toCity.Days, returnRoute.Days, cityMinimum, recruitWater);
    }

    static bool HasStagedRecruitCityReturnStat(
        int[] reserves,
        int inventory,
        int cityDays,
        int returnDays,
        int cityMinimum,
        int recruitReserve)
    {
        Group.DistributeToLowest(reserves, System.Math.Max(0, inventory));
        if (reserves.Any(reserve => reserve < cityDays))
            return false;
        for (int index = 0; index < reserves.Length; index++)
            reserves[index] = System.Math.Max(cityMinimum, reserves[index] - cityDays);
        return reserves.Append(recruitReserve).Min() >= returnDays;
    }

    static bool HasStagedRecruitStat(
        int reserve,
        int maximum,
        int inventory,
        int cityDays,
        int onwardDays,
        int returnDays,
        int cityMinimum,
        int recruitReserve)
    {
        if (!ProjectSoloArrival(
            reserve, maximum, inventory, cityDays, cityMinimum,
            out int arrival, out int remainingInventory))
            return false;

        int leaderDeparture = arrival;
        int recruitDeparture = recruitReserve;
        int onwardInventory = System.Math.Max(0, onwardDays - leaderDeparture) +
            System.Math.Max(0, onwardDays - recruitDeparture);
        if (remainingInventory < onwardInventory)
            return false;

        int leaderAtSettlement = System.Math.Max(0, leaderDeparture - onwardDays);
        return leaderAtSettlement + remainingInventory - onwardInventory >= returnDays;
    }

    static bool HasStagingReturnStat(
        int reserve,
        int maximum,
        int inventory,
        int cityDays,
        int returnDays,
        int cityMinimum)
    {
        if (!ProjectSoloArrival(
            reserve, maximum, inventory, cityDays, cityMinimum,
            out int arrival, out int remainingInventory))
            return false;
        return arrival + remainingInventory >= returnDays;
    }

    static bool ProjectSoloArrival(
        int reserve,
        int maximum,
        int inventory,
        int travelDays,
        int arrivalMinimum,
        out int arrival,
        out int remainingInventory)
    {
        inventory = System.Math.Max(0, inventory);
        int deficit = System.Math.Max(0, travelDays - reserve);
        if (inventory < deficit)
        {
            arrival = 0;
            remainingInventory = 0;
            return false;
        }

        remainingInventory = inventory - deficit;
        arrival = System.Math.Min(maximum,
            System.Math.Max(arrivalMinimum, System.Math.Max(0, reserve - travelDays)));
        return true;
    }

}
