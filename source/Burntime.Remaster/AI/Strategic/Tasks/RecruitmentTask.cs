using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct RecruitmentNeeds(
    int DesiredGroupSize,
    Location? ReinforcementCamp);

internal static partial class RecruitmentTask
{
    public static RecruitmentNeeds AddCandidates(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location? target,
        bool preparingAttack,
        List<AiDecision> candidates)
    {
        Player player = state.Player;
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        int desiredGroupSize = preparingAttack
            ? CanProvisionFullAttackGroup(state, target!, policy.DesiredGroupSize)
                ? policy.DesiredGroupSize
                : context.DesiredGroupSize
            : context.DesiredGroupSize;
        Location? reinforcementCamp = preparingAttack
            ? null
            : ReinforcementTask.FindBestCampForReinforcement(
                state, policy.CriticalGarrisonTarget);

        if (!preparingAttack && !TradeTask.ShouldVisitTrader(state) &&
            player.Group.Count < desiredGroupSize &&
            RecruitmentTask.CanRecallFollower(state, policy.CriticalGarrisonTarget))
        {
            candidates.Add(new AiDecision(
                AiAction.RecallFollower,
                1010,
                context.Current,
                Reason: $"recall a surplus camp follower toward {desiredGroupSize} people"));
        }

        bool needsGarrisonRecruit = reinforcementCamp != null &&
            player.Group.Count >= desiredGroupSize && player.Group.Count < Group.MAX_PEOPLE;
        bool needsSettler = !preparingAttack && target is { IsCity: false, Player: null } &&
            player.Group.Count == 1;
        if ((player.Group.Count < desiredGroupSize || needsGarrisonRecruit) &&
            state.CanRecruit(generatedPaymentAllowed))
        {
            candidates.Add(new AiDecision(
                AiAction.Recruit,
                preparingAttack ? 1040 : needsSettler ? 1700 : context.Current.IsCity ? 990 :
                    player.Group.Count == 1 ? 980 : needsGarrisonRecruit ? 830 : 760,
                context.Current,
                Reason: preparingAttack
                    ? $"prepare attack on {target!.Title}: recruit toward {desiredGroupSize} people"
                    : needsSettler
                    ? $"recruit a settler for {CampEconomy.StrategicRole(target!)} at {target!.Title}"
                    : needsGarrisonRecruit
                    ? $"critical camp {reinforcementCamp!.Title} needs another guard"
                    : context.Current.IsCity
                    ? $"city opportunity: recruit toward {desiredGroupSize} people"
                    : "group needs another recruit"));
        }
        else if (player.Group.Count < desiredGroupSize)
        {
            Location? preparationCamp = TradeTask.FindBestCampForCityPreparation(state);
            StrategicAi.AddTravelCandidate(
                state, candidates, preparationCamp ?? StrategicAi.FindNearestCity(state),
                preparingAttack ? 1030 : needsSettler ? 1690 : 970,
                preparingAttack
                    ? $"prepare attack on {target!.Title}: find recruits"
                    : needsSettler
                    ? $"find a settler for {target!.Title}"
                    : preparationCamp == null
                    ? "leader needs a recruit before claiming camps"
                    : "fill the caravan before recruiting in a city");
        }

        return new RecruitmentNeeds(desiredGroupSize, reinforcementCamp);
    }

    public static int RecommendedGroupSize(ClassicAiState state, int difficultyMaximum)
    {
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .ToArray();
        if (camps.Length == 0)
            return 2;

        int dailyExportFood = camps.Sum(camp =>
        {
            Production.Rate rate = camp.GetFoodProductionRate();
            int guards = CampEconomy.LivingGuardCount(camp, state.Player);
            return camp.Production == null || rate.IsCampStarving
                ? 0
                : System.Math.Max(0, rate.FoodPerDay - guards);
        });

        // Preserve daily output for barter and improvements instead of treating
        // merely non-starving camps as permission to grow the roaming group.
        int supported = System.Math.Max(2, dailyExportFood - TradeTask.DailyTradeFoodMargin);
        return System.Math.Min(difficultyMaximum, supported);
    }

    public static bool CanRecallFollower(ClassicAiState state, int criticalGarrisonTarget)
    {
        if (state.Current.Player != state.Player)
            return false;
        int guards = CampEconomy.LivingGuardCount(state.Current, state.Player);
        int minimum = ReinforcementTask.IsCriticalCamp(state, state.Current)
            ? criticalGarrisonTarget
            : 1;
        return guards > minimum;
    }

    static bool CanProvisionFullAttackGroup(
        ClassicAiState state,
        Location target,
        int desiredGroupSize)
    {
        if (state.Player.Group.Count >= desiredGroupSize)
            return true;
        RouteFinder.Route? route = RouteFinder.Find(state.Player, state.Current, target);
        if (route == null)
            return false;

        // Attackers are a temporary mobilization, not the permanent roaming
        // group. Existing camp maintenance leaves protected local food stock in
        // place, so accumulated portable provisions may fund the expedition even
        // when daily production cannot sustain this larger group indefinitely.
        int requiredPerPerson = route.Days + 3;
        int food = state.Player.Group.GetFoodReserve() + state.Player.Group.GetFoodInInventory();
        int water = state.Player.Group.GetWaterReserve() + state.Player.Group.GetWaterInInventory();
        return food >= desiredGroupSize * requiredPerPerson &&
            water >= desiredGroupSize * requiredPerPerson;
    }
}
