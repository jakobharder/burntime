using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct EconomicReturnAssessment(
    Production? Production,
    int ToolCount,
    int GuardCount,
    int FoodPerDay,
    int SurplusFoodPerDay,
    float ExportItemsPerDay,
    float TradeValuePerDay,
    float SustainableValuePerDay);

internal static class EconomicReturn
{
    public static EconomicReturnAssessment Assess(
        Location camp,
        Production? production,
        int toolCount,
        int guardCount,
        int? waterPerDay = null)
    {
        if (production == null)
            return new EconomicReturnAssessment(null, toolCount, guardCount, 0, -guardCount, 0, 0, 0);

        Production.Rate rate = production.GetRate(toolCount, guardCount);
        int surplusFood = rate.FoodPerDay - guardCount;
        float exportItems = surplusFood > 0 && production.Produce.FoodValue > 0
            ? surplusFood / (float)production.Produce.FoodValue
            : 0;
        float tradeValue = exportItems * production.Produce.TradeValue;
        int water = waterPerDay ?? camp.Source?.Water ?? 0;
        float waterReliability = water switch
        {
            >= CampEconomy.PlentyOfWater => 1f,
            2 => 0.75f,
            1 => 0.5f,
            _ => 0.25f
        };
        return new EconomicReturnAssessment(
            production,
            toolCount,
            guardCount,
            rate.FoodPerDay,
            surplusFood,
            exportItems,
            tradeValue,
            tradeValue * waterReliability);
    }

    public static EconomicReturnAssessment CurrentCamp(ClassicAiState state, Location camp)
    {
        int guards = CampEconomy.LivingGuardCount(camp, state.Player);
        Production? production = camp.Production;
        int tools = production == null ? 0 : CampEconomy.ProductionToolCount(camp, production);
        return Assess(camp, production, tools, guards);
    }

    public static EconomicReturnAssessment PotentialCamp(Location camp, int guardCount = 1)
    {
        if (camp.AvailableProducts == null)
            return Assess(camp, null, 0, guardCount);
        return camp.ValidProductions
            .Select(production => Assess(camp, production, production.MaxToolCount, guardCount))
            .OrderByDescending(result => result.SustainableValuePerDay)
            .ThenByDescending(result => result.SurplusFoodPerDay)
            .FirstOrDefault();
    }

    public static float MarginalCampImprovement(ClassicAiState state, Location camp)
    {
        if (AiTurnContext.For(state).Camps.TryGet(camp, out CampAssessment assessment))
            return assessment.GetMarginalCampImprovement(state);

        return CalculateMarginalCampImprovement(state, camp);
    }

    internal static float CalculateMarginalCampImprovement(ClassicAiState state, Location camp)
    {
        EconomicReturnAssessment current = CurrentCamp(state, camp);
        int guards = System.Math.Max(1, current.GuardCount);
        if (camp.AvailableProducts == null)
            return 0;

        return camp.ValidProductions
            .Select(production =>
            {
                int installed = CampEconomy.ProductionToolCount(camp, production);
                int next = System.Math.Min(production.MaxToolCount, installed + 1);
                return Assess(camp, production, next, guards).SustainableValuePerDay;
            })
            .DefaultIfEmpty(current.SustainableValuePerDay)
            .Max() - current.SustainableValuePerDay;
    }

    public static float MarginalWaterImprovement(ClassicAiState state, Location camp)
    {
        if (AiTurnContext.For(state).Camps.TryGet(camp, out CampAssessment assessment))
            return assessment.GetMarginalWaterImprovement(state);

        return CalculateMarginalWaterImprovement(state, camp);
    }

    internal static float CalculateMarginalWaterImprovement(ClassicAiState state, Location camp)
    {
        if (!Trading.NeedsPump(camp))
            return 0;
        EconomicReturnAssessment current = CurrentCamp(state, camp);
        if (current.Production == null)
            return 0;
        int water = camp.Source?.Water ?? 0;
        int handPumpWater = water + System.Math.Max(2, water / 4);
        return System.Math.Max(0,
            Assess(camp, current.Production, current.ToolCount,
                current.GuardCount, handPumpWater).SustainableValuePerDay -
            current.SustainableValuePerDay);
    }

    public static float ProductionToolReturn(ClassicAiState state, Production production) =>
        state.RootGame.World.Locations
            .Where(camp => camp.Player == state.Player && camp.Danger == null &&
                camp.ValidProductions.Contains(production))
            .Select(camp =>
            {
                int guards = System.Math.Max(1, CampEconomy.LivingGuardCount(camp, state.Player));
                int installed = CampEconomy.ProductionToolCount(camp, production);
                int next = System.Math.Min(production.MaxToolCount, installed + 1);
                float current = CurrentCamp(state, camp).SustainableValuePerDay;
                return System.Math.Max(0,
                    Assess(camp, production, next, guards).SustainableValuePerDay - current);
            })
            .DefaultIfEmpty(0)
            .Max();

    public static float BestEmpireProductionImprovement(ClassicAiState state) => state.RootGame.World.Locations
        .Where(camp => camp.Player == state.Player &&
            CampManagement.ShouldPreferProductionAtCamp(state, camp))
        .Select(camp => System.Math.Max(0, MarginalCampImprovement(state, camp)))
        .DefaultIfEmpty(0)
        .Max();

    public static float BestEmpireImprovement(ClassicAiState state) => state.RootGame.World.Locations
        .Where(camp => camp.Player == state.Player &&
            CampManagement.ShouldPreferProductionAtCamp(state, camp))
        .Select(camp => System.Math.Max(
            System.Math.Max(0, MarginalCampImprovement(state, camp)),
            MarginalWaterImprovement(state, camp)))
        .DefaultIfEmpty(0)
        .Max();

    public static float SustainableEmpireIncome(ClassicAiState state) => state.RootGame.World.Locations
        .Where(camp => camp.Player == state.Player)
        .Sum(camp => CurrentCamp(state, camp).SustainableValuePerDay);

    public static float TripValuePerDay(float collectibleValue, int travelDays) =>
        collectibleValue / System.Math.Max(1, travelDays + 1);
}
