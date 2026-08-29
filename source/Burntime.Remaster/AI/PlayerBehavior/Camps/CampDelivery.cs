using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CampDelivery
{
    public static void AddDeliveryCandidate(
        ClassicAiState state,
        List<AiDecision> candidates,
        float score)
    {
        CampAssessment? assessment = AiTurnContext.For(state).Camps
            .GetDeliveryAssessments()
            .Where(candidate => candidate.Camp.Player == state.Player &&
                candidate.Camp != state.Current && candidate.DeliveryEligible &&
                candidate.DeliveryRoute != null)
            .OrderByDescending(candidate => candidate.CriticalWaterDelivery)
            .ThenByDescending(candidate => candidate.DeliveryGain)
            .ThenBy(candidate => candidate.DeliveryRoute!.Days)
            .FirstOrDefault();
        if (assessment == null)
            return;

        AiTurnController.AddTravelCandidate(
            state,
            candidates,
            assessment.Camp,
            (assessment.CriticalWaterDelivery ? System.Math.Max(score, 2200) : score) +
                System.Math.Min(400, assessment.DeliveryGain * 180),
            assessment.CriticalWaterDelivery
                ? "secure a critical water waypoint"
                : $"deliver an upgrade worth about {assessment.DeliveryGain:0.0} sustainable value/day",
            knownRoute: assessment.DeliveryRoute);
    }

    static bool CarriesPump(ClassicAiState state) => state.Player.Group
        .SelectMany(character => character.Items)
        .Any(Trading.IsPump);

    static bool CarriesSpareWaterContainer(ClassicAiState state)
    {
        int portableCapacity = Trading.PortableWaterCapacity(state);
        return state.Player.Group.SelectMany(character => character.Items)
            .Where(item => AiItemPool.IsWaterContainer(item.Type))
            .Any(item => portableCapacity - AiItemPool.WaterContainerCapacity(item.Type) >=
                Trading.DesiredWaterContainerCapacity(state));
    }

    static Location? FindBestCampForDelivery(ClassicAiState state)
    {
        bool carriesPump = CarriesPump(state);
        bool carriesSpareWaterContainer = CarriesSpareWaterContainer(state);
        bool completeRecipe = Trading.HasCompleteUsefulRecipe(state);
        bool reserveProductionTool = ExpansionPlanning.ShouldReserveProductionTool(state);
        bool hasProductionUpgrade = !reserveProductionTool && state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .Any(location => CampManagement.HasPortableBestProduction(state, location));
        if (!carriesPump && !carriesSpareWaterContainer && !completeRecipe && !hasProductionUpgrade)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
            .Where(location =>
                (carriesPump && Trading.NeedsPump(location)) ||
                (carriesSpareWaterContainer &&
                    CampEconomy.IsTravelWaterBottleneck(location)) ||
                (!reserveProductionTool &&
                    CampManagement.HasPortableBestProduction(state, location)) ||
                (completeRecipe && Trading.CanUseCompleteRecipeAtCamp(state, location)))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location)
            })
            .Where(candidate => candidate.Route != null)
            .OrderByDescending(candidate =>
                (carriesPump || carriesSpareWaterContainer) &&
                CampEconomy.IsTravelWaterBottleneck(candidate.Location))
            .ThenByDescending(candidate =>
                System.Math.Max(
                    EconomicReturn.MarginalCampImprovement(state, candidate.Location),
                    EconomicReturn.MarginalWaterImprovement(state, candidate.Location)))
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }
}
