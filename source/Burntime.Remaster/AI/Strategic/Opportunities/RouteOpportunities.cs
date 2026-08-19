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
        Location? routeAlignedPickup = FindCityTradePickupCamp(state);
        StrategicAi.AddTravelCandidate(
            state,
            candidates,
            routeAlignedPickup ?? tradeCity,
            score,
            routeAlignedPickup == null
                ? "deliver surplus goods and trade for needed equipment"
                : "fill the city caravan at an owned camp near the trader");
    }

    public static Location? FindCityTradePickupCamp(ClassicAiState state)
    {
        if (state.Player.Group.GetFreeSlotCount() == 0)
            return null;

        Location? tradeCity = TradeTask.FindBestTradeCity(state);
        if (tradeCity == null)
            return null;
        // A substantial load collected at an owned camp completes the one allowed
        // pickup. Continue to the city instead of chaining through another camp.
        if (state.Current.Player == state.Player && TradeTask.HasPreparedTradeCargo(state))
            return null;
        int freeSlots = state.Player.Group.GetFreeSlotCount();
        Item[] carriedCapital = TradeTask.TradeCapital(state);
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
                Slots = LocalOpportunities.ProjectedCampCollectibleCount(
                    state, candidate.Camp, candidate.ToCamp!.Days),
                Value = LocalOpportunities.ProjectedCampCollectibleValue(
                    state, candidate.Camp, candidate.ToCamp!.Days)
            })
            .Where(candidate =>
                candidate.Slots > 0 &&
                TradeTask.IsSubstantialTradeLot(
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
