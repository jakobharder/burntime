using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Coarse camp-status view owned by <see cref="AiTurnContext"/>. Strategic actions
/// deliberately reuse its captured and lazily derived values after local state
/// changes; execution-time safety checks continue to inspect live state.
/// </summary>
internal sealed class CampAssessment
{
    internal required Location Camp { get; init; }
    internal required bool Threatened { get; init; }
    internal required int FoodSurplus { get; init; }
    internal required int WaterSurplus { get; init; }
    internal required Dictionary<Production, int> DesiredProductionTools { get; init; }
    internal float? MarginalCampImprovement { get; set; }
    internal float? MarginalWaterImprovement { get; set; }
    internal required bool NeedsPump { get; init; }
    internal required bool TravelWaterBottleneck { get; init; }
    internal bool HasPortableProductionUpgrade { get; set; }
    internal bool CanUseCompleteRecipe { get; set; }
    internal bool DeliveryEligible { get; set; }
    internal bool CriticalWaterDelivery { get; set; }
    internal RouteFinder.Route? DeliveryRoute { get; set; }

    internal float DeliveryGain { get; set; }

    internal float GetMarginalCampImprovement(ClassicAiState state) =>
        MarginalCampImprovement ??=
            EconomicReturn.CalculateMarginalCampImprovement(state, Camp);

    internal float GetMarginalWaterImprovement(ClassicAiState state) =>
        MarginalWaterImprovement ??=
            EconomicReturn.CalculateMarginalWaterImprovement(state, Camp);
}

internal sealed class CampAssessments
{
    readonly ClassicAiState state;
    Dictionary<Location, CampAssessment> camps = new();
    bool captured;
    bool deliveryBuilt;

    internal CampAssessments(ClassicAiState state)
    {
        this.state = state;
    }

    internal void Refresh()
    {
        camps = new();
        deliveryBuilt = false;
        captured = true;
        camps = BuildBasic();
    }

    internal bool TryGet(
        Location camp,
        out CampAssessment assessment)
    {
        if (captured && camps.TryGetValue(camp, out assessment!))
            return true;

        assessment = null!;
        return false;
    }

    internal IReadOnlyCollection<CampAssessment> GetDeliveryAssessments()
    {
        if (!captured)
            Refresh();

        if (!deliveryBuilt)
        {
            BuildDelivery(camps.Values);
            deliveryBuilt = true;
        }
        return camps.Values;
    }

    Dictionary<Location, CampAssessment> BuildBasic()
    {
        Player player = state.Player;
        Location[] ownedCamps = state.RootGame.World.Locations
            .Where(location => location.Player == player && !location.IsCity)
            .ToArray();
        Dictionary<Location, CampAssessment> result = new();

        // Build and publish the basic camp-local facts first. Desired production
        // calculations compare camps with one another, so they can then reuse the
        // turn's threat result instead of repeating a bounded BFS for every pair.
        foreach (Location camp in ownedCamps)
        {
            result[camp] = new CampAssessment
            {
                Camp = camp,
                Threatened = ReinforcementPlanning.CalculateIsThreatened(state, camp),
                FoodSurplus = CampEconomy.FoodSurplusPerDay(camp),
                WaterSurplus = CampEconomy.WaterSurplusPerDay(camp),
                DesiredProductionTools = new Dictionary<Production, int>(),
                NeedsPump = Trading.NeedsPump(camp),
                TravelWaterBottleneck = CampEconomy.IsTravelWaterBottleneck(
                    camp, player.Group.Count)
            };
        }

        // Publish basic entries before any lazy calculations. Helpers deriving
        // desired production and economic return can now reuse this snapshot.
        camps = result;

        return result;
    }

    void BuildDelivery(IEnumerable<CampAssessment> assessments)
    {
        Player player = state.Player;

        bool carriesPump = player.Group.SelectMany(character => character.Items)
            .Any(Trading.IsPump);
        int portableCapacity = Trading.PortableWaterCapacity(state);
        bool carriesSpareWaterContainer = player.Group
            .SelectMany(character => character.Items)
            .Where(item => AiItemPool.IsWaterContainer(item.Type))
            .Any(item => portableCapacity - AiItemPool.WaterContainerCapacity(item.Type) >=
                Trading.DesiredWaterContainerCapacity(state));
        Trading.ConstructionOpportunity[] completeRecipes = AiTurnContext.For(state).Needs
            .HasCompleteUsefulRecipe
            ? Trading.CompleteUsefulConstructionOpportunities(state).ToArray()
            : System.Array.Empty<Trading.ConstructionOpportunity>();
        bool reserveProductionTool = ExpansionPlanning.ShouldReserveProductionTool(state);
        bool carriesProductionTool = !reserveProductionTool &&
            (player.Group.SelectMany(character => character.Items)
                .Any(item => item.Type.Production != null) ||
             state.Reserve.GetContents().Any(entry => entry.Type.Production != null));

        foreach (CampAssessment assessment in assessments)
        {
            Location camp = assessment.Camp;
            assessment.HasPortableProductionUpgrade = carriesProductionTool &&
                CampManagement.HasPortableBestProduction(state, camp);
            assessment.CanUseCompleteRecipe = completeRecipes.Length > 0 &&
                Trading.CanUseCompleteRecipeAtCamp(state, camp, completeRecipes);
            bool canConstructPump = assessment.NeedsPump && completeRecipes.Any(
                opportunity => opportunity.Result is "item_hand_pump" or "item_industrial_pump");
            bool canUseNonWaterRecipe = completeRecipes
                .Where(opportunity => opportunity.Result is not
                    ("item_hand_pump" or "item_industrial_pump"))
                .Any(opportunity => Trading.CanUseCompleteRecipeAtCamp(
                    state, camp, new[] { opportunity }));
            bool immediateWaterUpgrade = assessment.NeedsPump && carriesPump ||
                canConstructPump;
            bool reusableContainerUpgrade = carriesSpareWaterContainer &&
                CampEconomy.NeedsReusableWaterReserve(camp, player.Group.Count);
            bool waterUpgrade = immediateWaterUpgrade || reusableContainerUpgrade;
            bool criticalWaterDelivery = assessment.TravelWaterBottleneck && waterUpgrade;
            bool ordinaryWaterDelivery = !assessment.TravelWaterBottleneck && waterUpgrade;
            bool otherDelivery =
                assessment.HasPortableProductionUpgrade ||
                canUseNonWaterRecipe;
            if ((waterUpgrade || otherDelivery) && camp != state.Current)
                assessment.DeliveryRoute = RouteFinder.Find(player, state.Current, camp);

            bool safelySuppliedWaterDelivery = criticalWaterDelivery &&
                assessment.DeliveryRoute != null &&
                TravelSupplies.HasRouteSupplies(
                    player, assessment.DeliveryRoute, hostileTarget: false);
            assessment.CriticalWaterDelivery = safelySuppliedWaterDelivery;
            assessment.DeliveryEligible = safelySuppliedWaterDelivery ||
                ordinaryWaterDelivery || otherDelivery;
            if (assessment.DeliveryEligible)
            {
                assessment.DeliveryGain = System.Math.Max(
                    System.Math.Max(0, assessment.GetMarginalCampImprovement(state)),
                    assessment.GetMarginalWaterImprovement(state));
            }
            if (assessment.DeliveryEligible && camp != state.Current &&
                assessment.DeliveryRoute == null)
                assessment.DeliveryRoute = RouteFinder.Find(player, state.Current, camp);
        }
    }
}
