using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct RecruitmentNeeds(
    Location? ReinforcementCamp,
    int ReinforcementTarget = 0,
    bool IsAttackStaging = false);

internal static partial class Recruitment
{
    const int StandingGroupFoodSurplus = 4;
    const int InitialRecruitReserve = 5;
    static readonly ConditionalWeakTable<Player, SurvivalReleaseMemory>
        SurvivalReleaseByPlayer = new();

    sealed class SurvivalReleaseMemory
    {
        internal HashSet<Location> Locations { get; } = new();
    }

    internal static void MarkSurvivalRelease(ClassicAiState state) =>
        SurvivalReleaseByPlayer.GetOrCreateValue(state.Player).Locations.Add(state.Current);

    static bool CanRecruitAtCurrentLocation(ClassicAiState state) =>
        !SurvivalReleaseByPlayer.GetOrCreateValue(state.Player)
            .Locations.Contains(state.Current);

    public static RecruitmentNeeds AddCandidates(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location? target,
        bool preparingAttack,
        bool shouldVisitTrader,
        List<AiDecision> candidates)
    {
        if (preparingAttack)
            return AddAttackPreparationCandidates(state, context, policy, target!, candidates);

        Player player = state.Player;
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        if (target == null && state.OwnedCampCount == 0 && player.Group.Count == 1)
        {
            Location? firstCampWaypoint = FindFirstCampWaypoint(state, context, policy);
            AiTurnController.AddTravelCandidate(
                state, candidates, firstCampWaypoint, 2095,
                firstCampWaypoint == null
                    ? "advance to a viable first-camp waypoint"
                    : $"advance to viable first-camp waypoint {firstCampWaypoint.Title}");
        }
        bool hasCommittedSettler = target is { IsCity: false, Player: null } &&
            player.Group.Count > 1;
        Location? reinforcementCamp = hasCommittedSettler
            ? null
            : ReinforcementPlanning.FindBestCampForReinforcement(
                state, policy.CriticalGarrisonTarget);
        int reinforcementTarget = reinforcementCamp == null
            ? 0
            : ReinforcementPlanning.SustainableGarrisonTarget(
                reinforcementCamp, policy.CriticalGarrisonTarget);
        bool needsSettler = target is { IsCity: false, Player: null } &&
            player.Group.Count == 1;
        bool needsGarrisonFollower = reinforcementCamp != null &&
            player.Group.Count == 1;
        bool standingFollowerSupported =
            EmpireFoodSurplus(state) >= StandingGroupFoodSurplus;
        int desiredGroupSize = standingFollowerSupported
            ? context.DesiredGroupSize
            : 1;
        bool needsFollower = player.Group.Count < context.DesiredGroupSize &&
            (needsSettler || needsGarrisonFollower || standingFollowerSupported);

        if (!shouldVisitTrader &&
            needsFollower &&
            Recruitment.CanRecallFollower(state, policy.CriticalGarrisonTarget))
        {
            candidates.Add(new AiDecision(
                AiAction.RecallFollower,
                needsSettler ? 2130 : 1010,
                context.Current,
                Reason: needsSettler
                    ? $"recall a settler for {target!.Title}"
                    : needsGarrisonFollower
                    ? $"recall a guard for delivery to {reinforcementCamp!.Title}"
                    : $"recall a surplus camp follower toward {desiredGroupSize} people"));
        }

        bool canRecruit = CanRecruitAtCurrentLocation(state) &&
            state.CanRecruit(generatedPaymentAllowed);
        RouteFinder.Route? settlementRoute = needsSettler
            ? RouteFinder.Find(player, context.Current, target!)
            : null;
        ClassicAiState.RecruitmentPlan? localSettler = needsSettler
            ? state.FindRecruitAt(context.Current, requireAffordable: true,
                allowGeneratedPayment: generatedPaymentAllowed)
            : null;
        bool localSettlerFunded = state.RecruitmentSupplyCost(
            localSettler, out int localPaymentFood, out _);
        RouteFinder.Route? soloReturn = needsSettler
            ? state.OwnedCampCount > 0
                ? FindProvisionedReturnRoute(state, context, target!)
                : RouteFinder.Find(player, target!, context.Current)
            : null;
        (int recruitFood, int recruitWater) = ProjectedRecruitReserves();
        bool settlementReady = !needsSettler || settlementRoute != null &&
            TravelSupplies.HasProjectedRecruitTerritorialSupplies(
                player, context.Current, settlementRoute, recruitFood, recruitWater) &&
            localSettlerFunded && soloReturn != null &&
            TravelSupplies.HasProjectedRecruitSoloReturnFood(
                player, settlementRoute, recruitFood,
                CanRemainAtNewCamp(state, target!, localSettler?.Recruit)
                    ? 0
                    : soloReturn.Days,
                localPaymentFood);

        // Prefer picking the settler up as late in the journey as possible. A
        // solo leader puts much less pressure on food and water while crossing
        // the supply network than a two-person group recruited at the origin.
        bool remoteSettlerPlanned = needsSettler &&
            (AddDestinationRecruitCandidate(state, context, target!, candidates) ||
                AddRouteRecruitCandidate(state, context, policy, target!, candidates));

        if (needsSettler && !remoteSettlerPlanned && canRecruit && !settlementReady)
        {
            Location? preparationCamp = FindBestOwnedSettlementStagingCamp(
                state, target!);
            AiTurnController.AddTravelCandidate(
                state, candidates, preparationCamp, 2120,
                $"provision at an owned camp before recruiting a settler for {target!.Title}");
            candidates.Add(new AiDecision(
                AiAction.Wait,
                2110,
                context.Current,
                Reason: $"delay recruitment until the projected two-person route to {target!.Title} is supplied"));
        }
        ClassicAiState.RecruitmentPlan? safeLocalFollower =
            needsFollower && !needsSettler && canRecruit
                ? FindSafeLocalRecruit(state, context, policy)
                : null;
        if (needsFollower && !remoteSettlerPlanned && canRecruit &&
            (!needsSettler || settlementReady) &&
            (needsSettler || safeLocalFollower != null))
        {
            Character? plannedRecruit = needsSettler
                ? localSettler?.Recruit
                : safeLocalFollower?.Recruit;
            candidates.Add(new AiDecision(
                AiAction.Recruit,
                needsSettler ? 2100 : needsGarrisonFollower ? 1040 :
                    context.Current.IsCity ? 990 : 980,
                needsSettler ? target : context.Current,
                needsSettler ? settlementRoute?.NextStep : null,
                Reason: needsSettler
                    ? $"recruit a settler for {CampEconomy.StrategicRole(target!)} at {target!.Title}"
                    : needsGarrisonFollower
                    ? $"critical camp {reinforcementCamp!.Title} needs another guard"
                    : context.Current.IsCity
                    ? $"city opportunity: recruit toward {desiredGroupSize} people"
                    : "group needs another recruit",
                Recruit: plannedRecruit));
        }
        else if (needsFollower && !remoteSettlerPlanned && !canRecruit)
        {
            if (needsSettler || needsGarrisonFollower || standingFollowerSupported)
            {
                Location? preparationCamp = Trading.FindBestCampForCityPreparation(state);
                Location? recruitmentCity = needsSettler
                    ? FindSafeSettlementRecruitmentCity(state, context, policy, target!)
                    : FindNearestRecruitmentCity(state, policy);
                Location? firstCampWaypoint = needsSettler &&
                    state.OwnedCampCount == 0 && preparationCamp == null
                    ? FindFirstCampWaypoint(state, context, policy)
                    : null;
                AiTurnController.AddTravelCandidate(
                    state, candidates, preparationCamp ?? recruitmentCity,
                    needsSettler ? 2090 : needsGarrisonFollower ? 1030 : 970,
                    needsSettler
                        ? $"find a safely staged settler for {target!.Title}"
                        : needsGarrisonFollower
                        ? $"find a guard for delivery to {reinforcementCamp!.Title}"
                        : preparationCamp == null
                        ? "leader needs a recruit before claiming camps"
                        : "fill the caravan before recruiting in a city");
                AiTurnController.AddTravelCandidate(
                    state, candidates, firstCampWaypoint, 2095,
                    firstCampWaypoint == null
                        ? "advance to a viable first-camp waypoint"
                        : $"advance to viable first-camp waypoint {firstCampWaypoint.Title}");
            }
        }

        return new RecruitmentNeeds(reinforcementCamp, reinforcementTarget);
    }

    static Location? FindFirstCampWaypoint(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy)
    {
        return Enumerable.Range(0, context.Current.Neighbors.Count)
            .Where(index => context.Current.WayLengths[index] > 0)
            .Select(index => context.Current.Neighbors[index])
            .Where(waypoint => !waypoint.IsCity && waypoint.Player == null &&
                CampEconomy.IsAcceptableFirstCamp(waypoint) &&
                state.CanClaim(waypoint) &&
                ExpansionPlanning.HasTravellingHazardProtection(state, waypoint) &&
                CampEconomy.CanSustainCamp(waypoint))
            .Select(waypoint => new
            {
                Waypoint = waypoint,
                Route = RouteFinder.Find(context.Player, context.Current, waypoint),
                LocalRecruit = state.FindRecruitAt(
                    waypoint, requireAffordable: true, allowGeneratedPayment: false),
                ProjectedContext = new DecisionContext
                {
                    Player = context.Player,
                    Current = waypoint,
                    Group = context.Group,
                    CriticalSupplies = context.CriticalSupplies,
                    SafeLocation = false,
                    DesiredGroupSize = context.DesiredGroupSize,
                    NeutralExpansionAllowed = context.NeutralExpansionAllowed
                }
            })
            .Where(candidate => candidate.Route != null && candidate.Route.Days > 0 &&
                TravelSupplies.HasRouteSupplies(
                    context.Player, candidate.Route, hostileTarget: false))
            .Where(candidate => candidate.LocalRecruit != null &&
                    state.RecruitmentSupplyCost(
                        candidate.LocalRecruit, out _, out _) &&
                    CanRemainAtNewCamp(
                        state, candidate.Waypoint, candidate.LocalRecruit.Recruit) ||
                FindSafeSettlementRecruitmentCity(
                    state, candidate.ProjectedContext, policy, candidate.Waypoint) != null)
            .OrderByDescending(candidate => candidate.LocalRecruit != null)
            .ThenByDescending(candidate => CampEconomy.TerritorialValue(candidate.Waypoint))
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Waypoint)
            .FirstOrDefault();
    }

    static int EmpireFoodSurplus(ClassicAiState state) => state.RootGame.World.Locations
        .Where(location => location.Player == state.Player)
        .Sum(CampEconomy.FoodSurplusPerDay);

    internal static (int Food, int Water) ProjectedRecruitReserves() =>
        (InitialRecruitReserve, InitialRecruitReserve);

    internal static bool TryAddCriticalRouteRecruitContinuation(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        List<AiDecision> candidates)
    {
        Location? target = state.StrategicTarget;
        if (!state.HasSettlementPlan || target == null || target.Player != null ||
            state.Player.Group.Count != 1)
            return false;

        RouteFinder.Route? direct = RouteFinder.Find(state.Player, context.Current, target);
        if (direct == null)
            return false;

        var stop = state.RootGame.World.Locations
            .Where(location => location != context.Current && location != target &&
                (location.IsCity || location.Player == state.Player ||
                    CampEconomy.CanSustainCamp(location)))
            .Select(location => new
            {
                Location = location,
                Recruit = state.FindRecruitAt(
                    location, requireAffordable: true,
                    allowGeneratedPayment: location.IsCity &&
                        policy.AllowGeneratedRecruitPaymentInCities),
                ToStop = RouteFinder.Find(state.Player, context.Current, location),
                Onward = RouteFinder.Find(state.Player, location, target)
            })
            .Where(candidate => candidate.Recruit != null &&
                candidate.ToStop != null && candidate.Onward != null &&
                candidate.ToStop.Days + candidate.Onward.Days == direct.Days &&
                TravelSupplies.CanSurviveRecoveryRoute(
                    state.Player, candidate.ToStop))
            .OrderBy(candidate => candidate.Onward!.Days)
            .ThenBy(candidate => candidate.ToStop!.Days)
            .FirstOrDefault();
        if (stop == null)
            return false;

        candidates.Add(new AiDecision(
            AiAction.Travel,
            1115 - stop.ToStop!.Days,
            stop.Location,
            stop.ToStop.NextStep,
            $"continue the survivable solo approach to route recruit {stop.Recruit!.Recruit.Name} at {stop.Location.Title}"));
        return true;
    }

    static bool AddDestinationRecruitCandidate(
        ClassicAiState state,
        DecisionContext context,
        Location target,
        List<AiDecision> candidates)
    {
        ClassicAiState.RecruitmentPlan recruit = state.FindRecruitAt(
            target, requireAffordable: true, allowGeneratedPayment: false);
        if (recruit == null || !state.RecruitmentSupplyCost(
            recruit, out int paymentFood, out int paymentWater))
            return false;

        RouteFinder.Route? outbound = RouteFinder.Find(state.Player, context.Current, target);
        RouteFinder.Route? returnRoute = state.OwnedCampCount > 0
            ? FindProvisionedReturnRoute(state, context, target)
            : RouteFinder.Find(state.Player, target, context.Current);
        if (outbound == null || returnRoute == null ||
            !TravelSupplies.HasRouteSupplies(
                state.Player, outbound, hostileTarget: false, paymentFood, paymentWater))
            return false;

        bool hasRoundTripSupplies = TravelSupplies.HasSettlementRoundTripSupplies(
            state.Player, outbound, returnRoute, paymentFood, paymentWater);
        bool canRefillAtSettlement = CampEconomy.CanProvisionGroupWater(
            target, state.Player.Group.Count);
        if (!hasRoundTripSupplies && !(canRefillAtSettlement &&
            TravelSupplies.HasSettlementRoundTripFood(
                state.Player, outbound, returnRoute)))
            return false;

        candidates.Add(new AiDecision(
            AiAction.Travel,
            2110 - outbound.Days,
            target,
            outbound.NextStep,
            state.OwnedCampCount == 0 && state.Player.Group.Count == 1
                ? $"pick up free first settler {recruit.Recruit.Name} at {target.Title}, then establish the camp"
                : $"hire {recruit.Recruit.Name} at {target.Title} with the reserved requested item, then establish the camp"));
        return true;
    }

    static bool AddRouteRecruitCandidate(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location target,
        List<AiDecision> candidates)
    {
        RouteFinder.Route? direct = RouteFinder.Find(state.Player, context.Current, target);
        RouteFinder.Route? soloReturn = FindProvisionedReturnRoute(
            state, context, target);
        if (direct == null || soloReturn == null)
            return false;

        var stop = state.RootGame.World.Locations
            .Where(location => location != context.Current && location != target)
            .Select(location =>
            {
                bool generatedPayment = location.IsCity &&
                    policy.AllowGeneratedRecruitPaymentInCities;
                ClassicAiState.RecruitmentPlan recruit = state.FindRecruitAt(
                    location, requireAffordable: true,
                    allowGeneratedPayment: generatedPayment);
                RouteFinder.Route? toStop = RouteFinder.Find(
                    state.Player, context.Current, location);
                RouteFinder.Route? onward = RouteFinder.Find(state.Player, location, target);
                bool funded = state.RecruitmentSupplyCost(
                    recruit, out int paymentFood, out int paymentWater);
                return new
                {
                    Location = location,
                    Recruit = recruit,
                    ToStop = toStop,
                    Onward = onward,
                    Funded = funded,
                    PaymentFood = paymentFood,
                    PaymentWater = paymentWater,
                    SoloReturnDays = CanRemainAtNewCamp(state, target, recruit?.Recruit)
                        ? 0
                        : soloReturn.Days
                };
            })
            // Restrict this preference to actual shortest-route stops. Off-route
            // cities remain the final fallback handled below.
            .Where(candidate => candidate.Recruit != null && candidate.Funded &&
                (candidate.Location.IsCity || candidate.Location.Player == state.Player ||
                    CampEconomy.CanSustainCamp(candidate.Location)) &&
                candidate.ToStop != null && candidate.Onward != null &&
                candidate.ToStop.Days + candidate.Onward.Days == direct.Days &&
                TravelSupplies.HasStagedRecruitOutboundSupplies(
                    state.Player, candidate.ToStop, candidate.Onward,
                    candidate.Location.IsCity ? RecoveryServices.CityMinimum : 0,
                    InitialRecruitReserve, InitialRecruitReserve,
                    candidate.PaymentFood, candidate.PaymentWater,
                    candidate.SoloReturnDays))
            .OrderBy(candidate => candidate.Onward!.Days)
            .ThenBy(candidate => candidate.ToStop!.Days)
            .FirstOrDefault();
        if (stop == null)
            return false;

        candidates.Add(new AiDecision(
            AiAction.Travel,
            2108 - stop.ToStop!.Days,
            stop.Location,
            stop.ToStop.NextStep,
            $"pick up {stop.Recruit!.Recruit.Name} at route stop {stop.Location.Title} before settling {target.Title}"));
        return true;
    }

    static Location? FindSafeSettlementRecruitmentCity(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location target)
    {
        int cityMinimum = state.OwnedCampCount > 0 ? RecoveryServices.CityMinimum : 0;
        bool requiresSustainableRecovery = state.OwnedCampCount > 0;
        return state.RootGame.World.Locations
            .Where(city => city.IsCity && city != context.Current)
            .Select(city =>
            {
                ClassicAiState.RecruitmentPlan recruit = state.FindRecruitAt(
                    city,
                    requireAffordable: true,
                    allowGeneratedPayment: policy.AllowGeneratedRecruitPaymentInCities);
                RouteFinder.Route? toCity = RouteFinder.Find(state.Player, context.Current, city);
                RouteFinder.Route? onward = RouteFinder.Find(state.Player, city, target);
                RouteFinder.Route? returnRoute = requiresSustainableRecovery
                    ? FindProvisionedReturnRoute(state, context, city)
                    : RouteFinder.Find(state.Player, city, context.Current);
                RouteFinder.Route? settlementReturn = requiresSustainableRecovery
                    ? FindProvisionedReturnRoute(state, context, target)
                    : CanRemainAtNewCamp(state, target, recruit?.Recruit)
                        ? new RouteFinder.Route(target, 0)
                        : RouteFinder.Find(state.Player, target, city);
                bool funded = state.RecruitmentSupplyCost(
                    recruit,
                    out int paymentFood,
                    out int paymentWater);
                (int recruitFood, int recruitWater) = ProjectedRecruitReserves();
                return new
                {
                    City = city,
                    Recruit = recruit,
                    ToCity = toCity,
                    Onward = onward,
                    Return = returnRoute,
                    SettlementReturn = settlementReturn,
                    Funded = funded,
                    RecruitFood = recruitFood,
                    RecruitWater = recruitWater,
                    PaymentFood = paymentFood,
                    PaymentWater = paymentWater
                };
            })
            .Where(candidate => candidate.Recruit != null && candidate.Funded &&
                candidate.ToCity != null && candidate.Onward != null && candidate.Return != null &&
                candidate.SettlementReturn != null &&
                TravelSupplies.HasStagedRecruitSettlementSupplies(
                    state.Player, candidate.ToCity, candidate.Onward,
                    candidate.SettlementReturn, cityMinimum,
                    candidate.RecruitFood, candidate.RecruitWater,
                    candidate.PaymentFood, candidate.PaymentWater) &&
                TravelSupplies.HasStagingCityReturnSupplies(
                    state.Player, candidate.ToCity, candidate.Return, cityMinimum,
                    candidate.PaymentFood, candidate.PaymentWater) &&
                TravelSupplies.HasStagedRecruitCityReturnSupplies(
                    state.Player, candidate.ToCity, candidate.Return, cityMinimum,
                    candidate.RecruitFood, candidate.RecruitWater,
                    candidate.PaymentFood, candidate.PaymentWater))
            .OrderBy(candidate => candidate.ToCity!.Days + candidate.Onward!.Days)
            .Select(candidate => candidate.City)
            .FirstOrDefault();
    }

    static bool CanRemainAtNewCamp(
        ClassicAiState state,
        Location target,
        Character recruit)
    {
        // Once the first camp is founded, its guard and the remaining solo leader
        // may stay there instead of carrying supplies for an artificial immediate
        // return to the recruitment city. Only waive that return when the actual
        // equipment they bring can sustainably feed both people and the local
        // water source can do the same.
        if (target.IsCity || (target.Source?.Water ?? 0) < 2 || recruit == null)
            return false;

        Item exactPayment = recruit.HireItems
            .Select(type => state.Player.Character.Items.Find(type))
            .FirstOrDefault(item => item != null);
        return target.ValidProductions.Any(production =>
        {
            int toolCount = state.Player.Group
                .SelectMany(character => character.Items)
                .Concat(recruit.Items)
                .Count(item => item.Type.Production == production && item != exactPayment);
            toolCount += state.Reserve.GetContents()
                .Where(entry => entry.Type.Production == production)
                .Sum(entry => entry.Count);
            return production.GetRate(toolCount, npcCount: 1).FoodPerDay >= 2;
        });
    }

    static RouteFinder.Route? FindProvisionedReturnRoute(
        ClassicAiState state,
        DecisionContext context,
        Location start) => state.RootGame.World.Locations
        .Where(camp => camp.Player == context.Player &&
            CampEconomy.CanProvisionFood(camp) &&
            CampEconomy.CanProvisionGroupWater(
                camp, context.Player.Group.Count))
        .Select(camp => RouteFinder.Find(context.Player, start, camp))
        .Where(route => route != null)
        .OrderBy(route => route!.Days)
        .FirstOrDefault();

    internal static ItemType? PlannedFutureSettlementPaymentType(ClassicAiState state)
    {
        if (state.OwnedCampCount == 0 && state.Player.Group.Count == 1)
            return null;

        Location? target = state.HasSettlementPlan && state.Player.Group.Count == 1
            ? state.StrategicTarget
            : null;
        if (target == null || target.IsCity || target.Player != null)
            return null;

        ClassicAiState.RecruitmentPlan recruit = state.FindRecruitAt(
            target, requireAffordable: true, allowGeneratedPayment: false) ??
            state.FindRecruitAt(target, requireAffordable: false, allowGeneratedPayment: false);
        if (recruit == null)
            return null;
        return recruit.Recruit.HireItems
            .OrderBy(type => state.Player.Character.Items.Find(type) == null ? 1 : 0)
            .ThenBy(type => type.TradeValue)
            .FirstOrDefault();
    }

    static RecruitmentNeeds AddAttackPreparationCandidates(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location target,
        List<AiDecision> candidates)
    {
        Player player = state.Player;
        int travelGroupSize = context.DesiredGroupSize;
        int attackGroupSize = AttackPlanning.RequiredAttackGroupSize(state, target, policy);
        if (player.Group.Count < travelGroupSize)
        {
            if (CanRecallFollower(state, policy.CriticalGarrisonTarget))
            {
                candidates.Add(new AiDecision(
                    AiAction.RecallFollower,
                    1060,
                    context.Current,
                    Reason: $"recall a surplus guard before attacking {target.Title}"));
            }
            if (AddFailedCityAttackRecruitmentCancellation(
                state, context, policy, target, candidates))
                return new RecruitmentNeeds(null);
            AddRecruitOrCityCandidate(state, context, policy, candidates,
                1040, 1030, $"restore the two-person travel group before attacking {target.Title}");
            return new RecruitmentNeeds(null);
        }

        if (attackGroupSize <= travelGroupSize ||
            player.Group.Count >= attackGroupSize)
            return new RecruitmentNeeds(null);

        int stagingTarget = attackGroupSize - travelGroupSize + 1;
        Location? stagingCamp = ReinforcementPlanning.FindAttackStagingCamp(
            state, target, stagingTarget);
        int stagedGuards = stagingCamp == null
            ? 0
            : CampEconomy.LivingGuardCount(stagingCamp, player);
        if (stagingCamp != null && context.Current == stagingCamp && stagedGuards > 1)
        {
            candidates.Add(new AiDecision(
                AiAction.MobilizeFrontierFollower,
                1100,
                stagingCamp,
                Reason: $"temporarily mobilize another frontier guard for the attack on {target.Title}"));
            return new RecruitmentNeeds(null);
        }

        if (stagingCamp != null && stagedGuards >= stagingTarget)
        {
            AiTurnController.AddTravelCandidate(state, candidates, stagingCamp, 1090,
                $"assemble the attack group for {target.Title} at frontier camp {stagingCamp.Title}");
            return new RecruitmentNeeds(null);
        }

        if (CanRecallFollower(state, policy.CriticalGarrisonTarget))
        {
            candidates.Add(new AiDecision(
                AiAction.RecallFollower,
                1085,
                context.Current,
                Reason: $"recall a surplus guard to assemble {attackGroupSize} attackers for {target.Title}"));
            return new RecruitmentNeeds(null);
        }

        int candidatesBeforeRecruitment = candidates.Count;
        if (AddFailedCityAttackRecruitmentCancellation(
            state, context, policy, target, candidates))
            return new RecruitmentNeeds(null);
        AddRecruitOrCityCandidate(state, context, policy, candidates,
            1080, 1070, $"recruit toward {attackGroupSize} attackers for {target.Title}");
        if (candidates.Count > candidatesBeforeRecruitment)
            return new RecruitmentNeeds(null);

        return stagingCamp == null
            ? new RecruitmentNeeds(null)
            : new RecruitmentNeeds(stagingCamp, stagingTarget, IsAttackStaging: true);
    }

    static bool AddFailedCityAttackRecruitmentCancellation(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        Location target,
        List<AiDecision> candidates)
    {
        if (!context.Current.IsCity)
            return false;

        bool generatedPaymentAllowed = policy.AllowGeneratedRecruitPaymentInCities;
        ClassicAiState.RecruitmentPlan? recruit = state.CanRecruit(generatedPaymentAllowed)
            ? FindSafeLocalRecruit(state, context, policy)
            : null;
        if (recruit != null)
            return false;

        candidates.Add(new AiDecision(
            AiAction.CancelAttackPlan,
            1200,
            target,
            Reason: $"cancel attack on {target.Title}: no safe, affordable recruit is available at {context.Current.Title}"));
        return true;
    }

    static void AddRecruitOrCityCandidate(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy,
        List<AiDecision> candidates,
        float recruitScore,
        float travelScore,
        string reason)
    {
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        if (!CanRecruitAtCurrentLocation(state))
            return;
        if (state.CanRecruit(generatedPaymentAllowed))
        {
            ClassicAiState.RecruitmentPlan? safeRecruit = FindSafeLocalRecruit(
                state, context, policy);
            if (safeRecruit != null)
            {
                candidates.Add(new AiDecision(
                    AiAction.Recruit,
                    recruitScore,
                    context.Current,
                    Reason: reason,
                    Recruit: safeRecruit.Recruit));
            }
            return;
        }

        Location? recruitmentCity = FindNearestRecruitmentCity(state, policy);
        Location? preparationCamp = recruitmentCity == null
            ? Trading.FindBestCampForCityPreparation(state)
            : null;
        AiTurnController.AddTravelCandidate(
            state, candidates, preparationCamp ?? recruitmentCity, travelScore,
            preparationCamp == null
                ? reason
                : $"collect recruitment-trip supplies before {reason}");
    }

    internal static Location? FindNearestRecruitmentCity(
        ClassicAiState state,
        AiPolicy policy)
    {
        bool allowGeneratedPayment = policy.AllowGeneratedRecruitPaymentInCities;
        int cityMinimum = state.OwnedCampCount > 0
            ? RecoveryServices.CityMinimum
            : 0;
        DecisionContext context = DecisionContext.Create(state, policy);
        return state.RootGame.World.Locations
            .Where(city => city.IsCity && city != state.Current)
            .Select(city => new
            {
                City = city,
                Recruit = state.FindRecruitAt(city, requireAffordable: true,
                    allowGeneratedPayment: allowGeneratedPayment),
                Route = RouteFinder.Find(state.Player, state.Current, city),
                Return = FindProvisionedReturnRoute(state, context, city)
            })
            .Select(candidate =>
            {
                bool funded = state.RecruitmentSupplyCost(
                    candidate.Recruit, out int paymentFood, out int paymentWater);
                (int recruitFood, int recruitWater) = ProjectedRecruitReserves();
                return new
                {
                    candidate.City,
                    candidate.Route,
                    candidate.Return,
                    candidate.Recruit,
                    Funded = funded,
                    PaymentFood = paymentFood,
                    PaymentWater = paymentWater,
                    RecruitFood = recruitFood,
                    RecruitWater = recruitWater
                };
            })
            .Where(candidate => candidate.Recruit != null && candidate.Funded &&
                candidate.Route != null && candidate.Return != null &&
                TravelSupplies.HasStagedRecruitCityReturnSupplies(
                    state.Player, candidate.Route, candidate.Return, cityMinimum,
                    candidate.RecruitFood, candidate.RecruitWater,
                    candidate.PaymentFood, candidate.PaymentWater))
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.City)
            .FirstOrDefault();
    }

    static ClassicAiState.RecruitmentPlan? FindSafeLocalRecruit(
        ClassicAiState state,
        DecisionContext context,
        AiPolicy policy)
    {
        bool generatedPaymentAllowed = context.Current.IsCity &&
            policy.AllowGeneratedRecruitPaymentInCities;
        ClassicAiState.RecruitmentPlan? plan = state.FindRecruitAt(
            context.Current, requireAffordable: true,
            allowGeneratedPayment: generatedPaymentAllowed);
        if (plan == null || !state.RecruitmentSupplyCost(
                plan, out int paymentFood, out int paymentWater))
            return null;

        Character recruit = plan.Recruit;
        bool projectedCritical = context.Player.Group.Any(character =>
                character.Health < 40 || character.Food <= 3 || character.Water <= 2) ||
            recruit.Health < 40 || recruit.Food <= 3 || recruit.Water <= 2;
        if (!projectedCritical)
            return plan;

        int projectedGroupSize = context.Player.Group.Count + 1;
        if (context.Current.Player == context.Player &&
            CampEconomy.FoodSurplusPerDay(context.Current) > projectedGroupSize &&
            CampEconomy.WaterSurplusPerDay(context.Current) > projectedGroupSize)
            return plan;

        bool hasRecovery = state.RootGame.World.Locations
            .Where(location => location.Player == context.Player &&
                CampEconomy.FoodSurplusPerDay(location) > projectedGroupSize &&
                CampEconomy.WaterSurplusPerDay(location) > projectedGroupSize)
            .Select(location => RouteFinder.Find(
                context.Player, context.Current, location))
            .Any(route => route != null &&
                TravelSupplies.HasProjectedRecruitRouteSupplies(
                    context.Player, route, recruit.Food, recruit.Water,
                    paymentFood, paymentWater));
        return hasRecovery ? plan : null;
    }

    internal static bool HasSafeLocalRecruit(
        ClassicAiState state,
        AiPolicy policy)
    {
        if (!state.Current.IsCity || !CanRecruitAtCurrentLocation(state))
            return false;
        bool generatedPaymentAllowed = policy.AllowGeneratedRecruitPaymentInCities;
        return state.CanRecruit(generatedPaymentAllowed) &&
            FindSafeLocalRecruit(
                state,
                DecisionContext.Create(state, policy),
                policy) != null;
    }

    public static bool CanRecallFollower(ClassicAiState state, int criticalGarrisonTarget)
    {
        if (state.Current.Player != state.Player)
            return false;
        int guards = CampEconomy.LivingGuardCount(state.Current, state.Player);
        int minimum = ReinforcementPlanning.IsCriticalCamp(state, state.Current)
            ? ReinforcementPlanning.SustainableGarrisonTarget(
                state.Current, criticalGarrisonTarget)
            : 1;
        return guards > minimum;
    }

    static Location? FindBestOwnedSettlementStagingCamp(
        ClassicAiState state,
        Location target) =>
        state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current &&
                CampEconomy.CanProvisionFood(location) &&
                CampEconomy.CanProvisionGroupWater(
                    location, state.Player.Group.Count + 1))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                Onward = RouteFinder.Find(state.Player, location, target)
            })
            .Where(candidate => candidate.Route != null && candidate.Onward != null &&
                TravelSupplies.HasTerritorialRouteSupplies(
                    state.Player, state.Current, candidate.Route, hostileTarget: false))
            // A staging camp must make progress toward the settlement. Choosing
            // only by distance from the leader can bounce forever between two
            // nearby camps whose full reserves still cannot support the onward
            // route.
            .OrderBy(candidate => candidate.Onward!.Days)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();

}
