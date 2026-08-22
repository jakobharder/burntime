using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal readonly record struct DefenseEstimate(
    int ExpectedDefenders,
    int PlausibleMaximum,
    float EstimatedStrength,
    bool BasedOnContact);

internal static class DefenseIntelligence
{
    static readonly ConditionalWeakTable<Player, Knowledge> KnowledgeByPlayer = new();

    internal static void ObserveWorld(ClassicAiState state)
    {
        Knowledge knowledge = KnowledgeByPlayer.GetOrCreateValue(state.Player);
        int day = state.RootGame.World.Day;
        foreach (Location location in state.RootGame.World.Locations.Where(location => !location.IsCity))
        {
            knowledge.Camps.TryGetValue(location, out CampObservation observation);
            if (observation == null || observation.Owner != location.Player)
            {
                knowledge.Camps[location] = new CampObservation
                {
                    Owner = location.Player,
                    OwnerSinceDay = day
                };
            }
        }
    }

    internal static DefenseEstimate Estimate(ClassicAiState state, Location camp)
    {
        ObserveWorld(state);
        Knowledge knowledge = KnowledgeByPlayer.GetOrCreateValue(state.Player);
        CampObservation observation = knowledge.Camps[camp];
        int day = state.RootGame.World.Day;
        int capacity = SustainableDefenderCapacity(camp);
        int age = Math.Max(0, day - observation.OwnerSinceDay);
        int ownerCamps = observation.Owner == null
            ? 0
            : state.RootGame.World.Locations.Count(location => location.Player == observation.Owner);

        float fortification = 0.05f +
            Math.Min(0.35f, age / 120f) +
            Math.Min(0.25f, day / 300f) +
            Math.Min(0.20f, ownerCamps * 0.025f);
        if (IsStrategicallyImportant(camp))
            fortification += 0.10f;
        if (state.WasRecentlyContested(camp))
            fortification += 0.20f;

        int expected = 1;
        if (capacity >= 2 && fortification >= 0.55f)
            expected = 2;
        if (capacity >= 3 && fortification >= 0.95f)
            expected = 3;
        if (capacity >= 4 && fortification >= 1.25f)
            expected = 4;

        bool recentContact = observation.ObservedDay > 0 && day - observation.ObservedDay <= 20;
        float strengthPerDefender = 22f;
        if (recentContact)
        {
            int possibleReinforcements = Math.Max(0, day - observation.ObservedDay) / 10;
            expected = Math.Max(expected, Math.Min(capacity,
                observation.ObservedDefenders + possibleReinforcements));
            if (observation.ObservedDefenders > 0 && observation.ObservedStrength > 0)
                strengthPerDefender = observation.ObservedStrength / observation.ObservedDefenders;
        }

        expected = Math.Clamp(expected, 1, capacity);
        return new DefenseEstimate(
            expected,
            capacity,
            expected * strengthPerDefender,
            recentContact);
    }

    internal static void ObserveEncounter(
        ClassicAiState state,
        Location camp,
        IEnumerable<Character> defenders)
    {
        ObserveWorld(state);
        Character[] living = defenders.Where(character => !character.IsDead).ToArray();
        CampObservation observation = KnowledgeByPlayer.GetOrCreateValue(state.Player).Camps[camp];
        observation.ObservedDay = state.RootGame.World.Day;
        observation.ObservedDefenders = living.Length;
        observation.ObservedStrength = living.Sum(character =>
            character.AttackValue + character.DefenseValue + character.Health / 10f);
    }

    static int SustainableDefenderCapacity(Location camp)
    {
        int water = Math.Clamp(camp.Source?.Water ?? 0, 1, Group.MAX_PEOPLE);
        int food = 1;
        for (int guards = 1; guards <= Group.MAX_PEOPLE; guards++)
        {
            bool sustainable = camp.ValidProductions.Any(production =>
            {
                Production.Rate rate = production.GetRate(production.MaxToolCount, guards);
                return !rate.IsCampStarving && rate.FoodPerDay >= guards;
            });
            if (!sustainable)
                break;
            food = guards;
        }
        return Math.Max(1, Math.Min(water, food));
    }

    static bool IsStrategicallyImportant(Location camp)
    {
        int routes = Enumerable.Range(0, camp.Neighbors.Count)
            .Count(index => camp.WayLengths[index] > 0);
        return routes <= 2 || CampEconomy.IsWellEstablishedPotential(camp);
    }

    sealed class Knowledge
    {
        internal readonly Dictionary<Location, CampObservation> Camps = new();
    }

    sealed class CampObservation
    {
        internal Player Owner;
        internal int OwnerSinceDay;
        internal int ObservedDay;
        internal int ObservedDefenders;
        internal float ObservedStrength;
    }
}
