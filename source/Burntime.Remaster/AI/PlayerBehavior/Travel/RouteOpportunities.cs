using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class RouteOpportunities
{
    public static void AddCityTradeCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        Location? tradeCity,
        float score)
    {
        Location? routeAlignedPickup = FindCityTradePickupCamp(state, tradeCity);
        Location? regionalTradeStop = routeAlignedPickup == null
            ? Trading.FindBestRegionalTradeStop(state, tradeCity)
            : null;
        Location? destination = routeAlignedPickup ?? regionalTradeStop ?? tradeCity;
        if (!HasProvisionedReturn(state, destination))
            return;
        RouteFinder.Route? route = destination?.IsCity == true
            ? RouteFinder.FindSupportedTradeRoute(
                state.Player, state.Current, destination)
            : destination == null
                ? null
                : RouteFinder.Find(state.Player, state.Current, destination);
        AiTurnController.AddTravelCandidate(
            state,
            candidates,
            destination,
            score,
            routeAlignedPickup != null
                ? "fill the city caravan at an owned camp near the trader"
                : regionalTradeStop != null
                    ? "offer local exports to a nearby roaming trader before the regional market"
                    : "deliver surplus goods and trade for needed equipment",
            knownRoute: route);
    }

    static bool HasProvisionedReturn(ClassicAiState state, Location? destination)
    {
        if (destination == null)
            return false;
        RouteFinder.Route? outbound = destination.IsCity
            ? RouteFinder.FindSupportedTradeRoute(
                state.Player, state.Current, destination)
            : RouteFinder.Find(state.Player, state.Current, destination);
        if (outbound == null)
            return false;

        return state.RootGame.World.Locations
            .Where(camp => camp.Player == state.Player &&
                CampEconomy.CanProvisionFood(camp) &&
                CampEconomy.CanProvisionGroupWater(
                    camp, state.Player.Group.Count))
            .Select(camp => RouteFinder.Find(state.Player, destination, camp))
            .Where(route => route != null)
            .Any(returnRoute => RecoveryServices.CanProvisionReturnTrip(
                state, destination, outbound, returnRoute!));
    }

    public static Location? FindCityTradePickupCamp(
        ClassicAiState state,
        Location? tradeCity)
    {
        if (state.Player.Group.GetFreeSlotCount() == 0 || Trading.HasPreparedTradeCargo(state))
            return null;

        if (tradeCity == null)
            return null;
        int freeSlots = state.Player.Group.GetFreeSlotCount();
        Item[] carriedCapital = Trading.TradeCapital(state);
        return state.RootGame.World.Locations
            .Where(camp => camp.Player == state.Player && camp != state.Current)
            .Select(camp => new
            {
                Camp = camp,
                ToCamp = RouteFinder.Find(state.Player, state.Current, camp),
                ToCity = RouteFinder.Find(state.Player, camp, tradeCity),
                NeighborsCity = camp.Neighbors.Contains(tradeCity)
            })
            .Where(candidate => candidate.ToCamp != null && candidate.ToCity != null)
            .Select(candidate => new
            {
                candidate.Camp,
                candidate.ToCamp,
                candidate.ToCity,
                candidate.NeighborsCity,
                Slots = CargoManagement.ProjectedCampCollectibleCount(
                    state, candidate.Camp, candidate.ToCamp!.Days),
                Value = CargoManagement.ProjectedCampCollectibleValue(
                    state, candidate.Camp, candidate.ToCamp!.Days)
            })
            .Where(candidate =>
                candidate.Slots > 0 &&
                Trading.IsSubstantialTradeLot(
                    carriedCapital.Length + candidate.Slots,
                    carriedCapital.Sum(item => item.TradeValue) + candidate.Value))
            .OrderByDescending(candidate => EconomicReturn.TripValuePerDay(
                candidate.Value, candidate.ToCamp!.Days + candidate.ToCity!.Days))
            .ThenByDescending(candidate => candidate.Slots >= freeSlots)
            .ThenByDescending(candidate => candidate.NeighborsCity)
            .ThenBy(candidate => candidate.ToCamp!.Days + candidate.ToCity!.Days)
            .Select(candidate => candidate.Camp)
            .FirstOrDefault();
    }
}
