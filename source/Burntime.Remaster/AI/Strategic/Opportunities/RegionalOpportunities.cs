using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class RegionalOpportunities
{
    public static void AddDeliveryCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        float score)
    {
        StrategicAi.AddTravelCandidate(
            state,
            candidates,
            FindBestCampForDelivery(state),
            score,
            "deliver functional equipment or a complete recipe to camp");
    }

    static Location? FindBestCampForDelivery(ClassicAiState state)
    {
        bool carriesPump = state.Player.Group.SelectMany(character => character.Items)
            .Any(TradeTask.IsPump);
        bool completeRecipe = TradeTask.HasCompleteUsefulRecipe(state);
        bool hasProductionUpgrade = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Any(location => LocalOpportunities.HasPortableBestProduction(state, location));
        if (!carriesPump && !completeRecipe && !hasProductionUpgrade)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Where(location =>
                (carriesPump && TradeTask.NeedsPump(location)) ||
                LocalOpportunities.HasPortableBestProduction(state, location) ||
                (completeRecipe && TradeTask.CanUseCompleteRecipeAtCamp(state, location)))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location)
            })
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
