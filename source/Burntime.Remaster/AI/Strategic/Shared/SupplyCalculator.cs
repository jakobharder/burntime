using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class SupplyCalculator
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
        if (route.NextStep.Player == player &&
            !CampEconomy.CanProvisionTravelGroupWater(
                route.NextStep, player.Group.Count))
            return false;
        RouteFinder.Route? safeLeg = RouteFinder.Find(player, start, route.NextStep);
        return safeLeg != null && HasRouteSupplies(player, safeLeg, hostileTarget: false);
    }

    public static bool CanSurviveRecoveryRoute(Player player, RouteFinder.Route route)
    {
        int foodShortageDays = System.Math.Max(0,
            route.Days - player.Group.GetLowestFoodWithInventory());
        int waterShortageDays = System.Math.Max(0,
            route.Days - player.Group.GetLowestWaterWithInventory());
        int expectedDamage = (foodShortageDays + waterShortageDays) * 25;

        // A human player may deliberately arrive hungry, thirsty, or wounded
        // when a real recovery facility is the destination. Never approve a
        // route expected to kill even the weakest current group member.
        return player.Group.Min(character => character.Health) > expectedDamage;
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
            CampEconomy.CanProvisionTravelGroupWater(
                route.NextStep, projectedGroupSize) ||
            CampEconomy.StoredFoodValue(route.NextStep) >= projectedGroupSize * 3 &&
            CampEconomy.CanProvisionTravelGroupWater(
                route.NextStep, projectedGroupSize);
        return sustainableWaypoint && firstLeg != null &&
            HasProjectedRecruitRouteSupplies(
                player, firstLeg, recruitFood, recruitWater);
    }

    static bool HasProjectedRecruitRouteSupplies(
        Player player,
        RouteFinder.Route route,
        int recruitFood,
        int recruitWater)
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
        return Group.GetLowestAfterDistribution(food, player.Group.GetFoodInInventory()) >= route.Days &&
            Group.GetLowestAfterDistribution(water, player.Group.GetWaterInInventory()) >= route.Days;
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
