using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CampEconomy
{
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
