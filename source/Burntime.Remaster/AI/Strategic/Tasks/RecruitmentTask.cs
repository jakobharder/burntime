using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct RecruitmentNeeds(
    Location? ReinforcementCamp,
    int ReinforcementTarget = 0,
    bool IsAttackStaging = false);

internal static partial class RecruitmentTask
{
    const int StandingTravelGroupFoodSurplus = 4;
    const int InitialRecruitReserve = 5;

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
        if (target == null && state.OwnedCampCount == 0 && player.Group.Count == 1)
        {
            Location? firstCampWaypoint = FindFirstCampWaypoint(state, context, policy);
            StrategicAi.AddTravelCandidate(
                state, candidates, firstCampWaypoint, 2095,
                firstCampWaypoint == null
                    ? "advance to a viable first-camp waypoint"
                    : $"advance to viable first-camp waypoint {firstCampWaypoint.Title}");
        }
        bool hasCommittedSettler = target is { IsCity: false, Player: null } &&
            player.Group.Count > 1;
        Location? reinforcementCamp = hasCommittedSettler
            ? null
            : ReinforcementTask.FindBestCampForReinforcement(
                state, policy.CriticalGarrisonTarget);
        int reinforcementTarget = reinforcementCamp == null
            ? 0
            : ReinforcementTask.SustainableGarrisonTarget(
                reinforcementCamp, policy.CriticalGarrisonTarget);
        bool needsSettler = target is { IsCity: false, Player: null } &&
            player.Group.Count == 1;
        bool needsGarrisonFollower = reinforcementCamp != null &&
            player.Group.Count == 1;
        bool standingFollowerSupported =
            EmpireFoodSurplus(state) >= StandingTravelGroupFoodSurplus;
        int desiredGroupSize = standingFollowerSupported
            ? context.TravelGroupSize
            : 1;
        bool needsFollower = player.Group.Count < context.TravelGroupSize &&
            (needsSettler || needsGarrisonFollower || standingFollowerSupported);

        if (!TradeTask.ShouldVisitTrader(state) &&
            needsFollower &&
            RecruitmentTask.CanRecallFollower(state, policy.CriticalGarrisonTarget))
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

        bool canRecruit = state.CanRecruit(generatedPaymentAllowed);
        RouteFinder.Route? settlementRoute = needsSettler
            ? RouteFinder.Find(player, context.Current, target!)
            : null;
        (int recruitFood, int recruitWater) = ProjectedRecruitReserves();
        bool settlementReady = !needsSettler || settlementRoute != null &&
            SupplyCalculator.HasProjectedRecruitTerritorialSupplies(
                player, context.Current, settlementRoute, recruitFood, recruitWater);

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
            StrategicAi.AddTravelCandidate(
                state, candidates, preparationCamp, 2120,
                $"provision at an owned camp before recruiting a settler for {target!.Title}");
            candidates.Add(new AiDecision(
                AiAction.Wait,
                2110,
                context.Current,
                Reason: $"delay recruitment until the projected two-person route to {target!.Title} is supplied"));
        }
        else if (needsFollower && !remoteSettlerPlanned && canRecruit)
        {
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
                    : "group needs another recruit"));
        }
        else if (needsFollower && !remoteSettlerPlanned && !canRecruit)
        {
            if (needsSettler || needsGarrisonFollower || standingFollowerSupported)
            {
                Location? preparationCamp = TradeTask.FindBestCampForCityPreparation(state);
                Location? recruitmentCity = needsSettler
                    ? FindSafeSettlementRecruitmentCity(state, context, policy, target!)
                    : StrategicAi.FindNearestCity(state);
                Location? firstCampWaypoint = needsSettler &&
                    state.OwnedCampCount == 0 && preparationCamp == null
                    ? FindFirstCampWaypoint(state, context, policy)
                    : null;
                StrategicAi.AddTravelCandidate(
                    state, candidates, preparationCamp ?? recruitmentCity,
                    needsSettler ? 2090 : needsGarrisonFollower ? 1030 : 970,
                    needsSettler
                        ? $"find a safely staged settler for {target!.Title}"
                        : needsGarrisonFollower
                        ? $"find a guard for delivery to {reinforcementCamp!.Title}"
                        : preparationCamp == null
                        ? "leader needs a recruit before claiming camps"
                        : "fill the caravan before recruiting in a city");
                StrategicAi.AddTravelCandidate(
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
        AiContext context,
        AiPolicy policy)
    {
        return Enumerable.Range(0, context.Current.Neighbors.Count)
            .Where(index => context.Current.WayLengths[index] > 0)
            .Select(index => context.Current.Neighbors[index])
            .Where(waypoint => !waypoint.IsCity && waypoint.Player == null &&
                CampEconomy.IsAcceptableFirstCamp(waypoint) &&
                state.CanClaim(waypoint) && CampEconomy.CanSustainCamp(waypoint))
            .Select(waypoint => new
            {
                Waypoint = waypoint,
                Route = RouteFinder.Find(context.Player, context.Current, waypoint),
                LocalRecruit = state.FindRecruitAt(
                    waypoint, requireAffordable: true, allowGeneratedPayment: false),
                ProjectedContext = new AiContext
                {
                    Player = context.Player,
                    Current = waypoint,
                    Group = context.Group,
                    CriticalSupplies = context.CriticalSupplies,
                    SafeLocation = false,
                    TravelGroupSize = context.TravelGroupSize,
                    NeutralExpansionAllowed = context.NeutralExpansionAllowed
                }
            })
            .Where(candidate => candidate.Route != null && candidate.Route.Days > 0 &&
                SupplyCalculator.HasRouteSupplies(
                    context.Player, candidate.Route, hostileTarget: false))
            .Where(candidate => candidate.LocalRecruit != null &&
                    state.RecruitmentSupplyCost(
                        candidate.LocalRecruit, allowGeneratedPayment: false,
                        out _, out _) &&
                    CanRemainAtNewCamp(state, candidate.Waypoint, candidate.LocalRecruit) ||
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
        AiContext context,
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
                SupplyCalculator.CanSurviveRecoveryRoute(
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
            $"continue the survivable solo approach to route recruit {stop.Recruit!.Name} at {stop.Location.Title}"));
        return true;
    }

    static bool AddDestinationRecruitCandidate(
        ClassicAiState state,
        AiContext context,
        Location target,
        List<AiDecision> candidates)
    {
        Character recruit = state.FindRecruitAt(
            target, requireAffordable: true, allowGeneratedPayment: false);
        if (recruit == null || !state.RecruitmentSupplyCost(
            recruit, allowGeneratedPayment: false, out int paymentFood, out int paymentWater))
            return false;

        RouteFinder.Route? outbound = RouteFinder.Find(state.Player, context.Current, target);
        RouteFinder.Route? returnRoute = state.OwnedCampCount > 0
            ? FindProvisionedReturnRoute(state, context, target)
            : RouteFinder.Find(state.Player, target, context.Current);
        if (outbound == null || returnRoute == null ||
            !SupplyCalculator.HasRouteSupplies(
                state.Player, outbound, hostileTarget: false, paymentFood, paymentWater))
            return false;

        bool hasRoundTripSupplies = SupplyCalculator.HasSettlementRoundTripSupplies(
            state.Player, outbound, returnRoute, paymentFood, paymentWater);
        bool canRefillAtSettlement = CampEconomy.CanProvisionTravelGroupWater(
            target, state.Player.Group.Count);
        if (!hasRoundTripSupplies && !(canRefillAtSettlement &&
            SupplyCalculator.HasSettlementRoundTripFood(
                state.Player, outbound, returnRoute)))
            return false;

        candidates.Add(new AiDecision(
            AiAction.Travel,
            2110 - outbound.Days,
            target,
            outbound.NextStep,
            $"hire {recruit.Name} at {target.Title} with the reserved requested item, then establish the camp"));
        return true;
    }

    static bool AddRouteRecruitCandidate(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location target,
        List<AiDecision> candidates)
    {
        RouteFinder.Route? direct = RouteFinder.Find(state.Player, context.Current, target);
        if (direct == null)
            return false;

        var stop = state.RootGame.World.Locations
            .Where(location => location != context.Current && location != target)
            .Select(location =>
            {
                bool generatedPayment = location.IsCity &&
                    policy.AllowGeneratedRecruitPaymentInCities;
                Character recruit = state.FindRecruitAt(
                    location, requireAffordable: true,
                    allowGeneratedPayment: generatedPayment);
                RouteFinder.Route? toStop = RouteFinder.Find(
                    state.Player, context.Current, location);
                RouteFinder.Route? onward = RouteFinder.Find(state.Player, location, target);
                bool funded = state.RecruitmentSupplyCost(
                    recruit, generatedPayment, out int paymentFood, out int paymentWater);
                return new
                {
                    Location = location,
                    Recruit = recruit,
                    ToStop = toStop,
                    Onward = onward,
                    Funded = funded,
                    PaymentFood = paymentFood,
                    PaymentWater = paymentWater
                };
            })
            // Restrict this preference to actual shortest-route stops. Off-route
            // cities remain the final fallback handled below.
            .Where(candidate => candidate.Recruit != null && candidate.Funded &&
                (candidate.Location.IsCity || candidate.Location.Player == state.Player ||
                    CampEconomy.CanSustainCamp(candidate.Location)) &&
                candidate.ToStop != null && candidate.Onward != null &&
                candidate.ToStop.Days + candidate.Onward.Days == direct.Days &&
                SupplyCalculator.HasStagedRecruitOutboundSupplies(
                    state.Player, candidate.ToStop, candidate.Onward,
                    candidate.Location.IsCity ? RecoveryServices.CityMinimum : 0,
                    InitialRecruitReserve, InitialRecruitReserve,
                    candidate.PaymentFood, candidate.PaymentWater))
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
            $"pick up {stop.Recruit!.Name} at route stop {stop.Location.Title} before settling {target.Title}"));
        return true;
    }

    static Location? FindSafeSettlementRecruitmentCity(
        ClassicAiState state,
        AiContext context,
        AiPolicy policy,
        Location target)
    {
        int cityMinimum = state.OwnedCampCount > 0 ? RecoveryServices.CityMinimum : 0;
        bool requiresSustainableRecovery = state.OwnedCampCount > 0;
        return state.RootGame.World.Locations
            .Where(city => city.IsCity && city != context.Current)
            .Select(city =>
            {
                Character recruit = state.FindRecruitAt(
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
                    : CanRemainAtNewCamp(state, target, recruit)
                        ? new RouteFinder.Route(target, 0)
                        : RouteFinder.Find(state.Player, target, city);
                bool funded = state.RecruitmentSupplyCost(
                    recruit,
                    policy.AllowGeneratedRecruitPaymentInCities,
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
                SupplyCalculator.HasStagedRecruitSettlementSupplies(
                    state.Player, candidate.ToCity, candidate.Onward,
                    candidate.SettlementReturn, cityMinimum,
                    candidate.RecruitFood, candidate.RecruitWater,
                    candidate.PaymentFood, candidate.PaymentWater) &&
                SupplyCalculator.HasStagingCityReturnSupplies(
                    state.Player, candidate.ToCity, candidate.Return, cityMinimum,
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
            toolCount += state.Pool.GetContents()
                .Where(entry => entry.Type.Production == production)
                .Sum(entry => entry.Count);
            return production.GetRate(toolCount, npcCount: 1).FoodPerDay >= 2;
        });
    }

    static RouteFinder.Route? FindProvisionedReturnRoute(
        ClassicAiState state,
        AiContext context,
        Location start) => state.RootGame.World.Locations
        .Where(camp => camp.Player == context.Player &&
            CampEconomy.CanProvisionFood(camp) &&
            CampEconomy.CanProvisionTravelGroupWater(
                camp, context.Player.Group.Count))
        .Select(camp => RouteFinder.Find(context.Player, start, camp))
        .Where(route => route != null)
        .OrderBy(route => route!.Days)
        .FirstOrDefault();

    internal static ItemType? PlannedFutureSettlementPaymentType(ClassicAiState state)
    {
        Location? target = state.HasSettlementPlan && state.Player.Group.Count == 1
            ? state.StrategicTarget
            : null;
        if (target == null || target.IsCity || target.Player != null)
            return null;

        Character recruit = state.FindRecruitAt(
            target, requireAffordable: true, allowGeneratedPayment: false) ??
            state.FindRecruitAt(target, requireAffordable: false, allowGeneratedPayment: false);
        if (recruit == null)
            return null;
        return recruit.HireItems
            .OrderBy(type => state.Player.Character.Items.Find(type) == null ? 1 : 0)
            .ThenBy(type => type.TradeValue)
            .FirstOrDefault();
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
        int attackGroupSize = AttackTask.RequiredAttackGroupSize(state, target, policy);
        if (player.Group.Count < travelGroupSize)
        {
            AddRecruitOrCityCandidate(state, context, policy, candidates,
                1040, 1030, $"restore the two-person travel group before attacking {target.Title}");
            return new RecruitmentNeeds(null);
        }

        if (attackGroupSize <= travelGroupSize ||
            player.Group.Count >= attackGroupSize)
            return new RecruitmentNeeds(null);

        int stagingTarget = attackGroupSize - travelGroupSize + 1;
        Location? stagingCamp = ReinforcementTask.FindAttackStagingCamp(
            state, target, stagingTarget);
        if (stagingCamp == null)
            return new RecruitmentNeeds(null);

        int stagedGuards = CampEconomy.LivingGuardCount(stagingCamp, player);
        if (context.Current == stagingCamp &&
            player.Group.Count > travelGroupSize && stagedGuards > 1)
        {
            candidates.Add(new AiDecision(
                AiAction.MobilizeFrontierFollower,
                1100,
                stagingCamp,
                Reason: $"temporarily mobilize another frontier guard for the attack on {target.Title}"));
            return new RecruitmentNeeds(null);
        }

        if (stagedGuards < stagingTarget)
            return new RecruitmentNeeds(
                stagingCamp, stagingTarget, IsAttackStaging: true);

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

        return new RecruitmentNeeds(null);
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
            ? ReinforcementTask.SustainableGarrisonTarget(
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
                CampEconomy.CanProvisionTravelGroupWater(
                    location, state.Player.Group.Count + 1))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(state.Player, state.Current, location),
                Onward = RouteFinder.Find(state.Player, location, target)
            })
            .Where(candidate => candidate.Route != null && candidate.Onward != null &&
                SupplyCalculator.HasTerritorialRouteSupplies(
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
