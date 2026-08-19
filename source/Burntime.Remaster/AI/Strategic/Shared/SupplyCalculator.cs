using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class SupplyCalculator
{
    public static bool HasRouteSupplies(
        Player player,
        RouteFinder.Route route,
        bool hostileTarget)
    {
        int margin = hostileTarget ? 3 : 0;
        int required = route.Days + margin;
        return player.Group.GetLowestFoodWithInventory() >= required &&
            player.Group.GetLowestWaterWithInventory() >= required;
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
        RouteFinder.Route? safeLeg = RouteFinder.Find(player, start, route.NextStep);
        return safeLeg != null && HasRouteSupplies(player, safeLeg, hostileTarget: false);
    }
}
