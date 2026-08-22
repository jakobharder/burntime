using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Data.BurnGfx;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Treats cities as limited waypoints and uses paid doctors with physical
/// group inventory. Food and water facilities are deliberately ignored.
/// </summary>
internal static class RecoveryServices
{
    internal const int CityMinimum = 3;
    const int ReturnTripSupply = 3;
    const int HealthTarget = 70;

    internal static void ApplyCityMinimum(ClassicAiState state)
    {
        if (!state.Current.IsCity)
            return;

        int food = 0;
        int water = 0;
        foreach (Character character in state.Player.Group)
        {
            if (character == state.Player.Character && state.OwnedCampCount == 0)
                continue;
            int foodTarget = Math.Min(CityMinimum, character.MaxFood);
            int waterTarget = Math.Min(CityMinimum, character.MaxWater);
            food += Math.Max(0, foodTarget - character.Food);
            water += Math.Max(0, waterTarget - character.Water);
            character.Food = Math.Max(character.Food, foodTarget);
            character.Water = Math.Max(character.Water, waterTarget);
        }
        if (food > 0 || water > 0)
        {
            AiTelemetry.Report(state.Player,
                $"received city minimum of {food} food and {water} water; " +
                "containers were not refilled");
        }
    }

    internal static void UseDoctor(ClassicAiState state)
    {
        Player player = state.Player;
        float benefit = DoctorPaymentBenefit(state);
        if (!HasDoctor(state.Current))
            return;

        foreach (Character patient in player.Group
            .Where(character => character.Health <= 40)
            .OrderBy(character => character.Health))
        {
            int needed = HealthTarget - patient.Health;
            // Food is the doctor's actual payment. Once treatment is needed,
            // it outranks the portable-food reserve just as it would for a human.
            Payment[] payment = BuildDoctorPayment(state, needed, benefit);
            int purchased = DoctorValue(payment, benefit);
            int supplied = Math.Min(needed, purchased);
            if (supplied <= 0)
                continue;
            Consume(payment);
            patient.Health = Math.Min(100, patient.Health + supplied);
            ReportDoctorVisit(state, payment, supplied, benefit, patient.Name);
        }
    }

    internal static int RequiredReturnFoodInventory(ClassicAiState state) =>
        state.Player.Group.Sum(character => Math.Max(0, ReturnTripSupply - character.Food));

    internal static int RequiredReturnWaterInventory(ClassicAiState state) =>
        state.Player.Group.Sum(character => Math.Max(0, ReturnTripSupply - character.Water));

    internal static Location? FindDestination(ClassicAiState state, bool requireReachable)
    {
        Player player = state.Player;
        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool needsDoctor = player.Group.Any(character => character.Health <= 40);
        bool starving = player.Group.Any(character => character.Food == 0);
        bool dehydrated = player.Group.Any(character => character.Water == 0);
        bool canPayDoctor = player.Group
            .SelectMany(character => character.Items)
            .Any(item => item.HealValue > 0);

        return state.RootGame.World.Locations
            .Where(location => location != state.Current &&
                (location.IsCity || location.Player == null || location.Player == player))
            .Select(location => new
            {
                Location = location,
                Route = RouteFinder.Find(player, state.Current, location),
                ReturnRoute = RouteFinder.Find(player, location, state.Current),
                Help = HelpScore(player, location, needsFood, needsWater, needsDoctor,
                    starving, dehydrated, canPayDoctor)
            })
            .Where(candidate => candidate.Route != null && candidate.ReturnRoute != null &&
                candidate.Help > 0 &&
                CanProvisionReturnTrip(state, candidate.Location, candidate.Route,
                    candidate.ReturnRoute) &&
                (!requireReachable || SupplyCalculator.HasRouteSupplies(
                    player, candidate.Route, hostileTarget: false) ||
                    SupplyCalculator.CanSurviveRecoveryRoute(player, candidate.Route)))
            .OrderByDescending(candidate => candidate.Help)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static bool CanWaitForLocalRecovery(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool hasFood = !needsFood || current.Player == player &&
            (CampEconomy.StoredFoodValue(current) > 0 ||
                CampEconomy.FoodSurplusPerDay(current) >= player.Group.Count);
        bool hasWater = !needsWater || current.Player == player &&
            CampEconomy.CanProvisionTravelGroupWater(current, player.Group.Count);
        return hasFood && hasWater;
    }

    internal static bool CanProvisionReturnTrip(
        ClassicAiState state,
        Location destination,
        RouteFinder.Route outbound,
        RouteFinder.Route returnRoute)
    {
        Player player = state.Player;
        bool ownedCamp = !destination.IsCity && destination.Player == player;
        int storedFood = ownedCamp
            ? CampEconomy.StoredFoodValue(destination)
            : 0;
        int foodProduction = ownedCamp && destination.Production != null
            ? CampEconomy.FoodSurplusPerDay(destination)
            : 0;

        // Production must do more than feed the visiting group while it waits;
        // only the excess can build the reserve needed for the journey home.
        bool canBuildFoodReserve = foodProduction > player.Group.Count;
        bool hasReturnFood = CanCoverReturnRoute(
            player.Group.Select(character => character.Food).ToArray(),
            player.Group.GetFoodInInventory(),
            outbound.Days,
            returnRoute.Days,
            storedFood,
            canBuildFoodReserve,
            destination.IsCity && state.OwnedCampCount > 0 ? CityMinimum : 0);

        int storedWater = 0;
        int waterProduction = 0;
        if (!destination.IsCity)
        {
            storedWater += destination.Source?.Reserve ?? 0;
            waterProduction = destination.Source?.Water ?? 0;
            if (ownedCamp)
                storedWater += CampEconomy.StoredWaterValue(destination);
        }
        bool canBuildWaterReserve = waterProduction > player.Group.Count;
        bool hasReturnWater = CanCoverReturnRoute(
            player.Group.Select(character => character.Water).ToArray(),
            player.Group.GetWaterInInventory(),
            outbound.Days,
            returnRoute.Days,
            storedWater,
            canBuildWaterReserve,
            destination.IsCity && state.OwnedCampCount > 0 ? CityMinimum : 0);

        return hasReturnFood && hasReturnWater;
    }

    static bool CanCoverReturnRoute(
        int[] reserves,
        int carriedSupply,
        int outboundDays,
        int returnDays,
        int destinationStock,
        bool canBuildReserve,
        int destinationFloor)
    {
        Group.DistributeToLowest(reserves, carriedSupply);
        for (int index = 0; index < reserves.Length; index++)
            reserves[index] = Math.Max(destinationFloor,
                Math.Max(0, reserves[index] - outboundDays));

        if (canBuildReserve)
            return true;

        Group.DistributeToLowest(reserves, destinationStock);
        return reserves.Min() >= returnDays;
    }

    static int HelpScore(
        Player player,
        Location location,
        bool needsFood,
        bool needsWater,
        bool needsDoctor,
        bool starving,
        bool dehydrated,
        bool canPayDoctor)
    {
        int score = 0;
        bool ownedCamp = !location.IsCity && location.Player == player;
        bool stockedCampFood = ownedCamp && CampEconomy.StoredFoodValue(location) > 0;
        bool producingCampFood = ownedCamp && location.Production != null &&
            CampEconomy.FoodSurplusPerDay(location) > 0;
        if (needsFood)
        {
            if (stockedCampFood)
                score += starving ? 12 : 4;
            else if (producingCampFood)
                score += starving ? 10 : 3;
        }

        bool storedCampWater = ownedCamp && CampEconomy.StoredWaterValue(location) > 0;
        bool availableLocalWater = !location.IsCity &&
            (location.Source?.Reserve > 0 || storedCampWater);
        bool replenishingLocalWater = !location.IsCity && location.Source?.Water > 0;
        if (needsWater)
        {
            if (availableLocalWater)
                score += dehydrated ? 12 : 2;
            else if (replenishingLocalWater)
                score += dehydrated ? 10 : 1;
        }
        if (needsDoctor)
        {
            if (HasDoctor(location))
                score += canPayDoctor ? 8 : 2;
            if (!canPayDoctor && location.Player == player &&
                location.Rooms.SelectMany(room => room.Items).Any(item => item.HealValue > 0))
                score += 7;
        }
        return score;
    }

    static bool HasDoctor(Location location) => location.Map?.Entrances?
        .Any(entrance => entrance.RoomType == RoomType.Doctor) == true;

    internal static bool NeedsDoctorPayment(ClassicAiState state) =>
        state.Player.Group.Any(character => character.Health <= 40) &&
        HasDoctor(state.Current);

    static Payment[] BuildDoctorPayment(
        ClassicAiState state,
        int needed,
        float benefit)
    {
        List<Payment> candidates = state.Player.Group
            .SelectMany(character => character.Items.Select(item =>
                new Payment(character.Items, item,
                    TradeTask.CanSell(state, item))))
            .Where(payment => payment.Item.HealValue > 0)
            .OrderBy(payment => payment.Safe ? 0 : 1)
            // As with ordinary barter, full water containers are easier to
            // replace than empty containers and are spent first when appropriate.
            .ThenBy(payment => IsFullWaterContainer(payment.Item) ? 0 :
                AiItemPool.IsWaterContainer(payment.Item.Type) ? 2 : 1)
            .ThenBy(payment => TradeTask.SalePriority(payment.Item))
            .ThenBy(payment => payment.Item.TradeValue)
            .ToList();

        List<Payment> selected = new();
        foreach (Payment candidate in candidates)
        {
            selected.Add(candidate);
            if (DoctorValue(selected, benefit) >= needed)
                break;
        }

        // Reduce overpayment using the same post-greedy rule as barter offers.
        foreach (Payment candidate in selected.OrderByDescending(payment =>
            payment.Item.HealValue).ToArray())
        {
            if (DoctorValue(selected.Where(payment => payment != candidate), benefit) >= needed)
                selected.Remove(candidate);
        }
        return selected.ToArray();
    }

    static float DoctorPaymentBenefit(ClassicAiState state) =>
        TradeTask.TradeBenefit(state) * 1.5f;

    static int DoctorValue(IEnumerable<Payment> payment, float benefit) =>
        (int)(payment.Sum(entry => entry.Item.HealValue) * benefit);

    static void Consume(IEnumerable<Payment> payment)
    {
        foreach (Payment entry in payment)
            entry.Owner.Remove(entry.Item);
    }

    static void ReportDoctorVisit(
        ClassicAiState state,
        IReadOnlyCollection<Payment> payment,
        int supplied,
        float benefit,
        string patient)
    {
        AiTelemetry.Report(state.Player,
            $"paid doctor with {string.Join(", ", payment.Select(entry => entry.Item.ID))} " +
            $"(trade value {(int)payment.Sum(entry => entry.Item.TradeValue)}, " +
            $"AI service value x{benefit:0.0}) for {supplied} health for {patient}");
    }

    static bool IsFullWaterContainer(Item item) =>
        item.WaterValue > 0 && item.Type.Empty != null;

    readonly record struct Payment(IItemCollection Owner, Item Item, bool Safe);

}
