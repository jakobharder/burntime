using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CampEconomy
{
    // A source producing three units per day can sustain a useful garrison and
    // still refill travel containers. This is the strategic threshold for a
    // camp with reliable water, rather than merely any water at all.
    internal const int PlentyOfWater = 3;

    public static bool HasProductionPotential(Location camp, string productId) =>
        camp.AvailableProducts != null &&
        camp.ValidProductions.Any(production => production.Produce.ID == productId);

    public static bool HasAdvancedFoodPotential(Location camp) =>
        HasProductionPotential(camp, "item_meat") ||
        HasProductionPotential(camp, "item_snake");

    public static bool HasRatFoodPotential(Location camp) =>
        HasProductionPotential(camp, "item_rats");

    public static bool HasPlentyOfWater(Location camp) =>
        camp.Source != null && camp.Source.Water >= PlentyOfWater;

    public static bool IsWellEstablishedPotential(Location camp) =>
        HasAdvancedFoodPotential(camp) && HasPlentyOfWater(camp);

    public static bool IsWellEstablished(Location camp) => camp.Player != null &&
        camp.Production != null &&
        camp.Production.Produce.ID is "item_meat" or "item_snake" &&
        HasPlentyOfWater(camp);

    public static bool IsAcceptableFirstCamp(Location camp) =>
        HasAdvancedFoodPotential(camp) || HasRatFoodPotential(camp);

    public static int RouteSecurityValue(Location camp)
    {
        int openRoutes = Enumerable.Range(0, camp.Neighbors.Count)
            .Count(index => camp.WayLengths[index] > 0);
        return openRoutes switch
        {
            2 => 120, // corridor or choke point
            >= 3 => 40, // connected hub, but less critical than a corridor
            _ => 0
        };
    }

    public static int TerritorialValue(Location camp)
    {
        int foodValue = (camp.AvailableProducts == null
            ? Enumerable.Empty<Production>()
            : camp.ValidProductions)
            .Select(production => production.Produce.ID switch
            {
                "item_meat" => 360,
                "item_snake" => 320,
                "item_rats" => 160,
                _ => 0
            })
            .DefaultIfEmpty()
            .Max();
        int waterValue = System.Math.Min(camp.Source?.Water ?? 0, 5) * 25;
        int establishedBonus = IsWellEstablishedPotential(camp) ? 300 : 0;
        int routeValue = foodValue == 0 ? RouteSecurityValue(camp) : 0;
        return foodValue + waterValue + establishedBonus + routeValue;
    }

    public static string StrategicRole(Location camp)
    {
        if (IsWellEstablishedPotential(camp))
            return "high-potential meat/snake camp";
        if (HasAdvancedFoodPotential(camp))
            return "meat/snake camp needing better water";
        if (HasRatFoodPotential(camp))
            return "starter rat camp";
        return RouteSecurityValue(camp) > 0
            ? "secondary route camp"
            : "low-priority camp";
    }

    public static int LivingGuardCount(Location camp, Player player) =>
        camp.CampNPC.Count(npc => npc.Player == player && !npc.IsDead);

    public static int ProductionToolCount(Location camp, Production production) => camp.Rooms
        .SelectMany(room => room.Items)
        .Concat(camp.CampNPC.SelectMany(npc => npc.Items))
        .Count(item => item.Type.Production == production);

    public static int DesiredProductionToolCount(
        ClassicAiState state,
        Location camp,
        Production production)
    {
        if (production.MaxToolCount <= 1)
            return production.MaxToolCount;

        int guards = LivingGuardCount(camp, state.Player);
        int localSecondGain = production.GetRate(production.MaxToolCount, guards).FoodPerDay -
            production.GetRate(production.MaxToolCount - 1, guards).FoodPerDay;

        // Move a second trap only when the extra production gained at an unstarted
        // camp repays the route within roughly one week. A remote first trap is
        // not automatically better than a productive second trap already here.
        bool worthwhileRedistribution = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player &&
                LocalOpportunities.ShouldPreferProductionAtCamp(state, location))
            .Where(location => location != camp && location.ValidProductions.Contains(production) &&
                ProductionToolCount(location, production) == 0)
            .Select(location => new
            {
                Route = RouteFinder.Find(state.Player, camp, location),
                FirstGain = production.GetRate(1, LivingGuardCount(location, state.Player)).FoodPerDay -
                    production.GetRate(0, LivingGuardCount(location, state.Player)).FoodPerDay
            })
            .Any(candidate => candidate.Route != null && candidate.FirstGain > localSecondGain &&
                candidate.Route.Days <= (candidate.FirstGain - localSecondGain) * 7);
        return worthwhileRedistribution ? 1 : production.MaxToolCount;
    }

    public static bool IsFoodStockCapped(Location camp) => camp.Production != null &&
        camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce)) >= Location.MaxStockFood;
}
