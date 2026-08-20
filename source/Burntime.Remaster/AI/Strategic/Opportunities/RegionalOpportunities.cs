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
        Location? camp = FindBestCampForDelivery(state);
        if (camp == null)
            return;
        float economicGain = System.Math.Max(
            System.Math.Max(0, EconomicReturn.MarginalCampImprovement(state, camp)),
            EconomicReturn.MarginalWaterImprovement(state, camp));
        StrategicAi.AddTravelCandidate(
            state,
            candidates,
            camp,
            score + System.Math.Min(400, economicGain * 180),
            $"deliver an upgrade worth about {economicGain:0.0} sustainable value/day");
    }

    static Location? FindBestCampForDelivery(ClassicAiState state)
    {
        bool carriesPump = state.Player.Group.SelectMany(character => character.Items)
            .Any(TradeTask.IsPump);
        bool completeRecipe = TradeTask.HasCompleteUsefulRecipe(state);
        bool reserveProductionTool = ExpansionTask.ShouldReserveProductionTool(state);
        bool hasProductionUpgrade = !reserveProductionTool && state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Any(location => LocalOpportunities.HasPortableBestProduction(state, location));
        if (!carriesPump && !completeRecipe && !hasProductionUpgrade)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Where(location =>
                (carriesPump && TradeTask.NeedsPump(location)) ||
                (!reserveProductionTool &&
                    LocalOpportunities.HasPortableBestProduction(state, location)) ||
                (completeRecipe && TradeTask.CanUseCompleteRecipeAtCamp(state, location)))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location)
            })
            .Where(candidate => candidate.Route != null)
            .OrderByDescending(candidate =>
                System.Math.Max(
                    EconomicReturn.MarginalCampImprovement(state, candidate.Location),
                    EconomicReturn.MarginalWaterImprovement(state, candidate.Location)))
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
