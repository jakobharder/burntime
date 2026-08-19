using System.Collections.Generic;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct RecruitmentNeeds(
    int TravelGroupSize,
    Location? ReinforcementCamp,
    int ReinforcementTarget = 0,
    bool IsAttackStaging = false);

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
        if (preparingAttack)
            return AddAttackPreparationCandidates(state, context, policy, target!, candidates);

        Player player = state.Player;
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        int desiredGroupSize = context.TravelGroupSize;
        Location? reinforcementCamp = ReinforcementTask.FindBestCampForReinforcement(
            state, policy.CriticalGarrisonTarget);

        if (!TradeTask.ShouldVisitTrader(state) &&
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
        bool needsSettler = target is { IsCity: false, Player: null } &&
            player.Group.Count == 1;
        if ((player.Group.Count < desiredGroupSize || needsGarrisonRecruit) &&
            state.CanRecruit(generatedPaymentAllowed))
        {
            candidates.Add(new AiDecision(
                AiAction.Recruit,
                needsSettler ? 2100 : context.Current.IsCity ? 990 :
                    player.Group.Count == 1 ? 980 : needsGarrisonRecruit ? 830 : 760,
                context.Current,
                Reason: needsSettler
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
                needsSettler ? 2090 : 970,
                needsSettler
                    ? $"find a settler for {target!.Title}"
                    : preparationCamp == null
                    ? "leader needs a recruit before claiming camps"
                    : "fill the caravan before recruiting in a city");
        }

        return new RecruitmentNeeds(
            desiredGroupSize, reinforcementCamp, policy.CriticalGarrisonTarget);
    }

    static RecruitmentNeeds AddAttackPreparationCandidates(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location target,
        List<AiDecision> candidates)
    {
        Player player = state.Player;
        int travelGroupSize = context.TravelGroupSize;
        if (player.Group.Count < travelGroupSize)
        {
            AddRecruitOrCityCandidate(state, context, policy, candidates,
                1040, 1030, $"restore the two-person travel group before attacking {target.Title}");
            return new RecruitmentNeeds(travelGroupSize, null);
        }

        if (policy.AttackGroupSize <= travelGroupSize ||
            player.Group.Count >= policy.AttackGroupSize)
            return new RecruitmentNeeds(policy.AttackGroupSize, null);

        int stagingTarget = policy.AttackGroupSize - travelGroupSize + 1;
        Location? stagingCamp = ReinforcementTask.FindAttackStagingCamp(
            state, target, stagingTarget);
        if (stagingCamp == null)
            return new RecruitmentNeeds(travelGroupSize, null);

        int stagedGuards = CampEconomy.LivingGuardCount(stagingCamp, player);
        if (context.Current == stagingCamp &&
            player.Group.Count > travelGroupSize && stagedGuards > 1)
        {
            candidates.Add(new AiDecision(
                AiAction.MobilizeFrontierFollower,
                1100,
                stagingCamp,
                Reason: $"temporarily mobilize another frontier guard for the attack on {target.Title}"));
            return new RecruitmentNeeds(policy.AttackGroupSize, null);
        }

        if (stagedGuards < stagingTarget && player.Group.Count <= travelGroupSize)
        {
            AddRecruitOrCityCandidate(state, context, policy, candidates,
                1040, 1030,
                $"recruit a guard to stage at frontier camp {stagingCamp.Title}");
            return new RecruitmentNeeds(
                travelGroupSize, stagingCamp, stagingTarget, IsAttackStaging: true);
        }

        if (stagedGuards < stagingTarget)
            return new RecruitmentNeeds(
                travelGroupSize, stagingCamp, stagingTarget, IsAttackStaging: true);

        if (context.Current != stagingCamp)
        {
            StrategicAi.AddTravelCandidate(state, candidates, stagingCamp, 1090,
                $"assemble the attack group for {target.Title} at frontier camp {stagingCamp.Title}");
        }
        else if (stagedGuards > 1)
        {
            candidates.Add(new AiDecision(
                AiAction.MobilizeFrontierFollower,
                1100,
                stagingCamp,
                Reason: $"temporarily mobilize a frontier guard for the attack on {target.Title}"));
        }

        return new RecruitmentNeeds(policy.AttackGroupSize, null);
    }

    static void AddRecruitOrCityCandidate(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        List<AiDecision> candidates,
        float recruitScore,
        float travelScore,
        string reason)
    {
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        if (state.CanRecruit(generatedPaymentAllowed))
        {
            candidates.Add(new AiDecision(
                AiAction.Recruit,
                recruitScore,
                context.Current,
                Reason: reason));
            return;
        }

        StrategicAi.AddTravelCandidate(
            state, candidates, StrategicAi.FindNearestCity(state), travelScore, reason);
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

}
