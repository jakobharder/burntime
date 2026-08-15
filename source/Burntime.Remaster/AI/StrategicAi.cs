using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal enum StrategicAiAction
{
    Recover,
    Recruit,
    ImproveCamp,
    ClaimNeutral,
    AttackHostile,
    Travel,
    Wait
}

internal sealed record StrategicAiDecision(
    StrategicAiAction Action,
    float Score,
    Location? Target = null,
    Location? NextStep = null,
    string Reason = "");

internal sealed class StrategicAiObservation
{
    public required Player Player { get; init; }
    public required Location Current { get; init; }
    public required IReadOnlyList<Character> Group { get; init; }
    public required bool CriticalSupplies { get; init; }
    public required bool SafeLocation { get; init; }
    public required int DesiredGroupSize { get; init; }
    public required bool NeutralExpansionAllowed { get; init; }
}

internal sealed class ClassicAiPolicy
{
    public int DesiredGroupSize { get; init; }
    public float MinimumAttackRatio { get; init; }
    public float HostileTargetScore { get; init; }
    public bool AllowGeneratedRecruitPaymentInCities { get; init; } = true;
    public bool AllowScheduledEquipment { get; init; } = true;
    public int SafeFoodFloor { get; init; } = 6;
    public int SafeWaterFloor { get; init; } = 4;
    public int SafeHealing { get; init; } = 25;

    public static ClassicAiPolicy ForDifficulty(int difficulty) => difficulty switch
    {
        0 => new ClassicAiPolicy { DesiredGroupSize = 2, MinimumAttackRatio = 1.35f, HostileTargetScore = 430 },
        1 => new ClassicAiPolicy { DesiredGroupSize = 3, MinimumAttackRatio = 1.05f, HostileTargetScore = 520 },
        _ => new ClassicAiPolicy { DesiredGroupSize = 4, MinimumAttackRatio = 0.75f, HostileTargetScore = 610 }
    };
}

internal static class StrategicAiTelemetry
{
    [ThreadStatic]
    public static Action<Player, string>? Sink;

    public static void Report(Player player, string message) => Sink?.Invoke(player, message);
}

internal static class StrategicAiPlanner
{
    public static StrategicAiDecision Choose(ClassicAiState state)
    {
        Player player = state.Player;
        ClassicAiPolicy policy = ClassicAiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        StrategicAiObservation observation = Observe(state, policy);
        List<StrategicAiDecision> candidates = new();

        if (IsHostile(observation.Current, player))
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.AttackHostile,
                1200,
                observation.Current,
                Reason: "hostile camp blocks the current route"));
            return SelectAndReport(player, candidates);
        }

        if (observation.CriticalSupplies)
        {
            if (observation.SafeLocation)
            {
                candidates.Add(new StrategicAiDecision(
                    StrategicAiAction.Recover,
                    1100,
                    observation.Current,
                    Reason: "critical food, water, or health"));
            }
            else
            {
                AddTravelCandidate(state, candidates, FindNearestLogistics(state), 1050, "seek emergency supplies");
            }
        }

        if (state.CanClaim(observation.Current) && state.CanStationCamp())
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.ClaimNeutral,
                1250,
                observation.Current,
                Reason: "neutral sustainable camp at current location"));
        }

        bool generatedPaymentAllowed = observation.Current.IsCity && policy.AllowGeneratedRecruitPaymentInCities;
        if (player.Group.Count < observation.DesiredGroupSize && state.CanRecruit(generatedPaymentAllowed))
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.Recruit,
                player.Group.Count == 1 ? 980 : 760,
                observation.Current,
                Reason: "group needs another recruit"));
        }
        else if (player.Group.Count == 1)
        {
            AddTravelCandidate(state, candidates, FindNearestCity(state), 970, "leader needs a recruit before claiming camps");
        }

        if (state.NeedsCampImprovement())
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.ImproveCamp,
                500,
                observation.Current,
                Reason: "owned camp lacks compatible production equipment"));
        }

        if (state.WaitTurns > 0 && !observation.CriticalSupplies)
        {
            state.WaitTurns--;
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.Wait,
                900,
                Reason: "expansion cooldown"));
        }

        Location? target = ValidatePersistentTarget(state, observation, policy);
        if (target == null)
        {
            target = SelectTerritorialTarget(state, observation, policy);
            state.StrategicTarget = target;
        }

        if (target != null)
        {
            float score = IsHostile(target, player) ? policy.HostileTargetScore : 700;
            AddTravelCandidate(state, candidates, target, score, IsHostile(target, player)
                ? "advance toward hostile frontier"
                : "advance toward neutral territory");
        }

        if (player.Group.Count > 1 && player.Group.Count < observation.DesiredGroupSize && !state.HasHireableNpc())
            AddTravelCandidate(state, candidates, FindNearestCity(state), 560, "look for recruits");

        candidates.Add(new StrategicAiDecision(StrategicAiAction.Wait, 0, Reason: "no useful action"));
        return SelectAndReport(player, candidates);
    }

    static StrategicAiObservation Observe(ClassicAiState state, ClassicAiPolicy policy)
    {
        Player player = state.Player;
        bool critical = player.Group.Any(character => character.Health < 40 || character.Food <= 3 || character.Water <= 2);
        bool safe = state.Current.IsCity || state.Current.Player == player;
        bool neutralAllowed = state.OwnedCampCount < state.HumanCampBenchmark + state.Configuration.MaxAdvance;

        return new StrategicAiObservation
        {
            Player = player,
            Current = state.Current,
            Group = player.Group.ToArray(),
            CriticalSupplies = critical,
            SafeLocation = safe,
            DesiredGroupSize = policy.DesiredGroupSize,
            NeutralExpansionAllowed = neutralAllowed
        };
    }

    static StrategicAiDecision SelectAndReport(Player player, List<StrategicAiDecision> candidates)
    {
        StrategicAiDecision selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(_ => Burntime.Platform.Math.Random.Next())
            .First();
        string target = selected.Target == null ? "none" : selected.Target.Title;
        StrategicAiTelemetry.Report(player,
            $"decision {selected.Action}, target {target}, score {selected.Score:0}: {selected.Reason}");
        return selected;
    }

    static Location? ValidatePersistentTarget(
        ClassicAiState state,
        StrategicAiObservation observation,
        ClassicAiPolicy policy)
    {
        Location? target = state.StrategicTarget;
        if (target == null || target == observation.Current || target.IsCity)
            return null;
        if (target.Player == null)
            return observation.NeutralExpansionAllowed && state.CanClaim(target) ? target : null;
        if (target.Player == observation.Player)
            return null;
        return IsAttackSuitable(observation.Player, target, policy) ? target : null;
    }

    static Location? SelectTerritorialTarget(
        ClassicAiState state,
        StrategicAiObservation observation,
        ClassicAiPolicy policy)
    {
        List<(Location Location, float Score)> targets = new();
        foreach (Location location in state.RootGame.World.Locations)
        {
            if (location == observation.Current || location.IsCity)
                continue;

            Route? route = FindRoute(observation.Player, observation.Current, location);
            if (route == null)
                continue;

            if (location.Player == null && observation.NeutralExpansionAllowed && state.CanClaim(location))
            {
                targets.Add((location, 700 - route.Days * 8 + Jitter()));
            }
            else if (IsHostile(location, observation.Player) && IsAttackSuitable(observation.Player, location, policy))
            {
                float weakness = System.Math.Max(-100, 100 - DefenderStrength(location));
                targets.Add((location, policy.HostileTargetScore + weakness - route.Days * 6 + Jitter()));
            }
        }

        return targets.OrderByDescending(target => target.Score).FirstOrDefault().Location;
    }

    static bool IsAttackSuitable(Player player, Location target, ClassicAiPolicy policy)
    {
        float defenders = DefenderStrength(target);
        if (defenders <= 0)
            return true;
        return AttackerStrength(player) / defenders >= policy.MinimumAttackRatio;
    }

    static float AttackerStrength(Player player) => player.Group
        .Where(character => !character.IsDead)
        .Sum(character => character.AttackValue + character.DefenseValue + character.Health / 10f);

    static float DefenderStrength(Location location) => location.CampNPC
        .Where(character => !character.IsDead && character.Player == location.Player)
        .Sum(character => character.AttackValue + character.DefenseValue + character.Health / 10f);

    static int Jitter() => Burntime.Platform.Math.Random.Next(-20, 21);

    static bool IsHostile(Location location, Player player) =>
        !location.IsCity && location.Player != null && location.Player != player;

    static void AddTravelCandidate(
        ClassicAiState state,
        List<StrategicAiDecision> candidates,
        Location? target,
        float score,
        string reason)
    {
        if (target == null)
            return;
        Route? route = FindRoute(state.Player, state.Current, target);
        if (route?.NextStep == null)
            return;
        candidates.Add(new StrategicAiDecision(
            StrategicAiAction.Travel,
            score - route.Days,
            target,
            route.NextStep,
            reason));
    }

    static Location? FindNearestLogistics(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity || location.Player == state.Player)
            .Select(location => (Location: location, Route: FindRoute(state.Player, state.Current, location)))
            .Where(candidate => candidate.Route != null && candidate.Location != state.Current)
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    static Location? FindNearestCity(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.IsCity && location != state.Current)
            .Select(location => (Location: location, Route: FindRoute(state.Player, state.Current, location)))
            .Where(candidate => candidate.Route != null)
            .OrderBy(candidate => candidate.Route!.Days)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    internal static Route? FindRoute(Player player, Location start, Location target)
    {
        if (start == target)
            return new Route(target, 0);

        Dictionary<Location, int> distance = new() { [start] = 0 };
        Dictionary<Location, Location> previous = new();
        HashSet<Location> visited = new();

        while (true)
        {
            Location? current = distance
                .Where(pair => !visited.Contains(pair.Key))
                .OrderBy(pair => pair.Value)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (current == null)
                return null;
            if (current == target)
                break;
            visited.Add(current);

            for (int index = 0; index < current.Neighbors.Count; index++)
            {
                Location neighbor = current.Neighbors[index];
                int length = current.WayLengths[index];
                if (length <= 0 || (IsHostile(neighbor, player) && neighbor != target))
                    continue;

                int candidate = distance[current] + length;
                if (!distance.TryGetValue(neighbor, out int known) || candidate < known)
                {
                    distance[neighbor] = candidate;
                    previous[neighbor] = current;
                }
            }
        }

        Location step = target;
        while (previous.TryGetValue(step, out Location? parent) && parent != start)
            step = parent;
        return previous.ContainsKey(target) ? new Route(step, distance[target]) : null;
    }

    internal sealed record Route(Location NextStep, int Days);
}

internal static class StrategicAiExecutor
{
    public static void Execute(ClassicAiState state, StrategicAiDecision decision)
    {
        Player player = state.Player;
        ClassicAiPolicy policy = ClassicAiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        switch (decision.Action)
        {
            case StrategicAiAction.Recover:
                state.AddEmergencySupplies(policy);
                StrategicAiTelemetry.Report(player, "received limited emergency supplies at a safe location");
                break;

            case StrategicAiAction.Recruit:
                Character? recruit = state.Recruit(
                    allowGeneratedPayment: state.Current.IsCity && policy.AllowGeneratedRecruitPaymentInCities);
                StrategicAiTelemetry.Report(player, recruit == null
                    ? "could not afford an available recruit"
                    : $"hired {recruit.Name} ({recruit.Class})");
                break;

            case StrategicAiAction.ImproveCamp:
                if (state.ImproveCamp())
                    StrategicAiTelemetry.Report(player, $"improved production at {state.Current.Title}");
                break;

            case StrategicAiAction.ClaimNeutral:
                Character? npc = state.SelectCampNpc();
                if (npc != null)
                {
                    state.CreateCamp(npc);
                    StrategicAiTelemetry.Report(player, $"claimed {state.Current.Title} using {npc.Name}");
                }
                break;

            case StrategicAiAction.AttackHostile:
                StrategicCombatResolver.Resolve(state);
                break;

            case StrategicAiAction.Travel:
                if (decision.Target != null)
                    state.StrategicTarget = decision.Target;
                if (decision.NextStep != null)
                {
                    player.Travel(decision.NextStep);
                    StrategicAiTelemetry.Report(player,
                        $"travels toward {decision.Target?.Title ?? decision.NextStep.Title} via {decision.NextStep.Title}");
                }
                break;

            case StrategicAiAction.Wait:
                break;
        }
    }
}

internal static class StrategicCombatResolver
{
    const int MaxRounds = 100;

    public static void Resolve(ClassicAiState state)
    {
        Player attacker = state.Player;
        Location location = state.Current;
        Player? defenderOwner = location.Player;
        if (defenderOwner == null || defenderOwner == attacker || location.IsCity)
        {
            state.StrategicTarget = null;
            return;
        }

        List<Character> originalDefenders = location.CampNPC
            .Where(character => character.Player == defenderOwner && !character.IsDead)
            .ToList();
        List<Character> originalAttackers = attacker.Group.Where(character => !character.IsDead).ToList();
        StrategicAiTelemetry.Report(attacker,
            $"attacks {defenderOwner.Name}'s camp at {location.Title}: " +
            $"{attacker.Group.Count} attackers against {originalDefenders.Count} defenders");

        for (int round = 1; round <= MaxRounds && !attacker.Character.IsDead; round++)
        {
            List<Character> defenders = originalDefenders.Where(character => !character.IsDead).ToList();
            if (defenders.Count == 0)
                break;

            foreach (Character fighter in attacker.Group.Where(character => !character.IsDead).ToArray())
            {
                Character? target = defenders.Where(character => !character.IsDead).OrderBy(character => character.Health).FirstOrDefault();
                if (target == null)
                    break;
                DealDamage(fighter, target);
            }

            defenders = originalDefenders.Where(character => !character.IsDead).ToList();
            foreach (Character fighter in defenders)
            {
                Character? target = attacker.Group
                    .Where(character => !character.IsDead && character != attacker.Character)
                    .OrderBy(character => character.Health)
                    .FirstOrDefault() ?? (attacker.Character.IsDead ? null : attacker.Character);
                if (target == null)
                    break;
                DealDamage(fighter, target);
            }
        }

        foreach (Character casualty in originalDefenders.Where(character => character.IsDead))
            StrategicAiTelemetry.Report(attacker, $"defeated defender {casualty.Name} at {location.Title}");
        foreach (Character casualty in originalAttackers.Where(character => character.IsDead && character != attacker.Character))
            StrategicAiTelemetry.Report(attacker, $"lost follower {casualty.Name} in the attack on {location.Title}");

        bool defendersDefeated = originalDefenders.All(character => character.IsDead);
        if (defendersDefeated && !attacker.Character.IsDead)
        {
            Character? guard = attacker.Group
                .Where(character => character != attacker.Character && !character.IsDead)
                .OrderBy(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();

            location.Player = null;
            if (guard != null)
            {
                state.CreateCamp(guard);
                StrategicAiTelemetry.Report(attacker, $"captured {location.Title} and stationed {guard.Name}");
            }
            else
            {
                state.StrategicTarget = null;
                state.ResetWait();
                StrategicAiTelemetry.Report(attacker, $"won at {location.Title}, but it remains neutral without a surviving follower");
            }
        }
        else
        {
            StrategicAiTelemetry.Report(attacker, attacker.Character.IsDead
                ? $"lost the attack on {location.Title}; the leader was killed"
                : $"failed to defeat {location.Title}'s defenders");
        }
    }

    static void DealDamage(Character attacker, Character defender)
    {
        int attack = attacker.PrepareStrategicAttack();
        float defense = defender.PrepareStrategicDefense();
        float randomFactor = 0.85f + (float)Burntime.Platform.Math.Random.NextDouble() * 0.30f;
        int damage = (int)System.Math.Max(1, (attack - defense) * randomFactor);
        defender.Health -= damage;
    }
}
