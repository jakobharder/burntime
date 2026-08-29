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

    internal static void ProvideCityWaterMinimum(ClassicAiState state) =>
        ProvideCityMinimum(state, includeFood: false, includeWater: true, refillContainers: true);

    internal static void ProvideCityFoodMinimum(ClassicAiState state) =>
        ProvideCityMinimum(state, includeFood: true, includeWater: false, refillContainers: false);

    static void ProvideCityMinimum(
        ClassicAiState state,
        bool includeFood,
        bool includeWater,
        bool refillContainers)
    {
        if (!state.Current.IsCity)
            return;

        int food = 0;
        int water = 0;
        foreach (Character character in state.Player.Group)
        {
            if (character == state.Player.Character && state.OwnedCampCount == 0)
                continue;
            int foodTarget = includeFood
                ? Math.Min(CityMinimum, character.MaxFood)
                : character.Food;
            int waterTarget = includeWater
                ? Math.Min(CityMinimum, character.MaxWater)
                : character.Water;
            food += Math.Max(0, foodTarget - character.Food);
            water += Math.Max(0, waterTarget - character.Water);
            character.Food = Math.Max(character.Food, foodTarget);
            character.Water = Math.Max(character.Water, waterTarget);
        }
        if (food > 0 || water > 0)
        {
            AiTelemetry.Report(state.Player,
                $"received city minimum of {food} food and {water} water");
        }

        // The AI cannot operate the city's facilities through the player UI.
        // Give each traveller at most one refill, using only containers the group
        // already owns. This keeps long city corridors viable without creating
        // inventory or turning the city into an indefinite source of supplies.
        Item[] refilled = refillContainers
            ? state.Player.Group.GetEmptyWaterItems()
            .Select(entry => entry.Item)
            .OrderByDescending(item =>
                AiItemPool.WaterContainerCapacity(item.Type))
            .Take(state.Player.Group.Count)
            .ToArray()
            : Array.Empty<Item>();
        foreach (Item item in refilled)
            item.MakeFull();
        if (refilled.Length > 0)
        {
            AiTelemetry.Report(state.Player,
                $"used city water service to refill {string.Join(", ", refilled.Select(item => item.ID))}");
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

    internal enum TripMode
    {
        Normal,
        Escape
    }

    internal static Location? FindDestination(
        ClassicAiState state,
        bool requireReachable,
        TripMode tripMode = TripMode.Normal)
    {
        Player player = state.Player;
        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool needsDoctor = player.Group.Any(character => character.Health <= 40);
        bool starvationEmergency = player.Group.Any(character => character.Food <= 1);
        bool dehydrated = player.Group.Any(character => character.Water == 0);
        bool canPayDoctor = player.Group
            .SelectMany(character => character.Items)
            .Any(item => item.HealValue > 0);

        bool escape = tripMode == TripMode.Escape;
        return state.RootGame.World.Locations
            .Where(location => location != state.Current &&
                (escape
                    ? !location.IsCity && location.Player == player
                    : location.IsCity || location.Player == null || location.Player == player))
            // Help is entirely camp-local. Filter before doing any route work.
            .Select(location => new
            {
                Location = location,
                Help = HelpScore(player, location, needsFood, needsWater, needsDoctor,
                    starvationEmergency, dehydrated, canPayDoctor)
            })
            .Where(candidate => candidate.Help > 0)
            .Select(location => new
            {
                location.Location,
                Route = RouteFinder.Find(player, state.Current, location.Location),
                ReturnRoute = escape || starvationEmergency || dehydrated
                    ? null
                    : RouteFinder.Find(player, location.Location, state.Current),
                location.Help
            })
            .Select(candidate => new
            {
                candidate.Location,
                candidate.Route,
                candidate.ReturnRoute,
                candidate.Help,
                Stable = candidate.Route != null &&
                    IsStableRecoveryDestination(state, candidate.Location, candidate.Route)
            })
            .Where(candidate => candidate.Route != null &&
                (!escape || candidate.Stable) &&
                (escape || starvationEmergency || dehydrated || candidate.ReturnRoute != null &&
                    CanProvisionReturnTrip(state, candidate.Location, candidate.Route,
                        candidate.ReturnRoute)) &&
                (!requireReachable || TravelSupplies.HasRouteSupplies(
                    player, candidate.Route, hostileTarget: false) ||
                    !escape && TravelSupplies.CanSurviveRecoveryRoute(player, candidate.Route)))
            .OrderByDescending(candidate =>
                (starvationEmergency || dehydrated) && candidate.Stable)
            .ThenByDescending(candidate => candidate.Help)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static bool WaitingForSuppliesWillBeFatal(ClassicAiState state)
    {
        if (CanSustainLocally(state))
            return false;

        return state.Player.Group.Any(character => character.Food <= 1) ||
            state.Player.Group.Any(character => character.Water == 0);
    }

    internal static bool NeedsCityRecoveryStaging(ClassicAiState state)
    {
        if (!state.Current.IsCity || state.OwnedCampCount == 0)
            return false;

        Player player = state.Player;
        // City minimums are only staging aid. Recovery is complete once the
        // party's current stats plus physical supplies cover a real route to a
        // camp that can provision both resources.
        return !state.RootGame.World.Locations
            .Where(location => location.Player == player &&
                CampEconomy.CanProvisionFood(location) &&
                CampEconomy.CanProvisionGroupWater(location, player.Group.Count))
            .Select(location => RouteFinder.Find(player, state.Current, location))
            .Any(route => route != null && TravelSupplies.HasRouteSupplies(
                player, route, hostileTarget: false));
    }

    internal static Location? FindLastChanceDestination(ClassicAiState state)
    {
        Player player = state.Player;
        bool starving = player.Group.Any(character => character.Food <= 1);
        bool dehydrated = player.Group.Any(character => character.Water == 0);
        if (!starving && !dehydrated)
            return null;

        return state.RootGame.World.Locations
            .Where(location => location != state.Current &&
                (location.IsCity || location.Player == null || location.Player == player))
            .Select(location => new
            {
                Location = location,
                Help = LastChanceHelpScore(state, location, starving, dehydrated)
            })
            .Where(candidate => candidate.Help > 0)
            .Select(candidate => new
            {
                candidate.Location,
                candidate.Help,
                Route = RouteFinder.Find(player, state.Current, candidate.Location)
            })
            .Where(candidate => candidate.Route?.NextStep != null)
            // All normally survivable routes were rejected before reaching this
            // fallback. Minimize unavoidable damage first, then prefer the stop
            // that repairs more of the active emergency.
            .OrderBy(candidate => TravelSupplies.ExpectedRecoveryRouteDamage(
                player, candidate.Route!))
            .ThenByDescending(candidate => candidate.Help)
            .ThenBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static bool CanSustainLocally(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool hasFood = !needsFood || current.Player == player &&
            CampEconomy.FoodSurplusPerDay(current) >= player.Group.Count;
        bool hasWater = !needsWater || current.Player == player &&
            CampEconomy.WaterSurplusPerDay(current) >= player.Group.Count;
        return hasFood && hasWater;
    }

    internal static bool CanBuildTravelReserve(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        if (current.Player != player)
            return false;

        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool canBuildFood = !needsFood ||
            CampEconomy.FoodSurplusPerDay(current) > player.Group.Count;
        bool canBuildWater = !needsWater ||
            CampEconomy.WaterSurplusPerDay(current) > player.Group.Count;
        return canBuildFood && canBuildWater;
    }

    internal static bool CanRecoverLocallyForTravel(ClassicAiState state)
    {
        if (!CanSustainLocally(state))
            return false;

        Player player = state.Player;
        Location current = state.Current;
        bool needsFood = player.Group.Any(character => character.Food <= 3);
        bool needsWater = player.Group.Any(character => character.Water <= 2);
        bool neighboringFoodSupport = Enumerable.Range(0, current.Neighbors.Count)
            .Any(index => current.WayLengths[index] > 0 &&
                current.Neighbors[index].Player == player &&
                CampEconomy.FoodSurplusPerDay(current.Neighbors[index]) > 0);
        bool canBuildFood = !needsFood || neighboringFoodSupport ||
            CampEconomy.FoodSurplusPerDay(current) > player.Group.Count;
        bool canBuildWater = !needsWater ||
            CampEconomy.WaterSurplusPerDay(current) > player.Group.Count;
        return canBuildFood && canBuildWater;
    }

    internal static bool CanProvisionReturnTrip(
        ClassicAiState state,
        Location destination,
        RouteFinder.Route outbound,
        RouteFinder.Route returnRoute)
    {
        Player player = state.Player;
        bool ownedCamp = !destination.IsCity && destination.Player == player;
        int foodSurplus = ownedCamp && destination.Production != null
            ? CampEconomy.FoodSurplusPerDay(destination)
            : 0;

        // Production must do more than feed the visiting group while it waits;
        // only the excess can build the reserve needed for the journey home.
        bool canBuildFoodReserve = foodSurplus > player.Group.Count;
        bool hasReturnFood = CanCoverReturnRoute(
            player.Group.Select(character => character.Food).ToArray(),
            player.Group.GetFoodInInventory(),
            outbound.Days,
            returnRoute.Days,
            destinationStock: 0,
            canBuildFoodReserve,
            destination.IsCity && state.OwnedCampCount > 0 ? CityMinimum : 0);

        int waterSurplus = !destination.IsCity
            ? CampEconomy.WaterSurplusPerDay(destination)
            : 0;
        bool canBuildWaterReserve = waterSurplus > player.Group.Count;
        bool hasReturnWater = CanCoverReturnRoute(
            player.Group.Select(character => character.Water).ToArray(),
            player.Group.GetWaterInInventory(),
            outbound.Days,
            returnRoute.Days,
            destinationStock: 0,
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
        bool producingCampFood = ownedCamp && location.Production != null &&
            CampEconomy.FoodSurplusPerDay(location) > 0;
        if (needsFood)
        {
            if (producingCampFood)
                score += starving ? 10 : 3;
        }

        bool replenishingLocalWater = !location.IsCity &&
            CampEconomy.WaterSurplusPerDay(location) > 0;
        if (needsWater)
        {
            if (replenishingLocalWater)
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

    static int LastChanceHelpScore(
        ClassicAiState state,
        Location location,
        bool starving,
        bool dehydrated)
    {
        Player player = state.Player;
        bool ownedCamp = !location.IsCity && location.Player == player;
        int score = 0;
        if (starving &&
            (ownedCamp && CampEconomy.CanProvisionFood(location) ||
                location.IsCity && state.OwnedCampCount > 0))
            score += 10;
        if (dehydrated &&
            (!location.IsCity && CampEconomy.WaterSurplusPerDay(location) > 0 ||
                location.IsCity && state.OwnedCampCount > 0))
            score += 10;
        return score;
    }

    static bool IsStableRecoveryDestination(
        ClassicAiState state,
        Location destination,
        RouteFinder.Route outbound)
    {
        Player player = state.Player;
        if (destination.IsCity && state.OwnedCampCount > 0)
            return HasSafeOnwardRecovery(state, destination, outbound);
        if (destination.Player == player && CampEconomy.CanProvisionFood(destination) &&
            CampEconomy.CanProvisionGroupWater(destination, player.Group.Count))
            return true;
        return HasSafeOnwardRecovery(state, destination, outbound);
    }

    static bool HasSafeOnwardRecovery(
        ClassicAiState state,
        Location destination,
        RouteFinder.Route outbound)
    {
        Player player = state.Player;
        return state.RootGame.World.Locations
            .Where(location => location != destination && location.Player == player &&
                CampEconomy.CanProvisionFood(location) &&
                CampEconomy.CanProvisionGroupWater(location, player.Group.Count))
            .Select(location => RouteFinder.Find(player, destination, location))
            .Where(route => route != null)
            .Any(onward => CanProvisionReturnTrip(
                state, destination, outbound, onward!));
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
                    Trading.CanSell(state, item))))
            .Where(payment => payment.Item.HealValue > 0)
            .OrderBy(payment => payment.Safe ? 0 : 1)
            // As with ordinary barter, full water containers are easier to
            // replace than empty containers and are spent first when appropriate.
            .ThenBy(payment => IsFullWaterContainer(payment.Item) ? 0 :
                AiItemPool.IsWaterContainer(payment.Item.Type) ? 2 : 1)
            .ThenBy(payment => Trading.SalePriority(payment.Item))
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
        Trading.TradeBenefit(state) * 1.5f;

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
