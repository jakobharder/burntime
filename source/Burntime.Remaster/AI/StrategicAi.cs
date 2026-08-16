using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal enum StrategicAiAction
{
    Recover,
    Recruit,
    StationFollower,
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
    public float NeutralTargetScore { get; init; }
    public float HostileTargetScore { get; init; }
    public float ExpansionEconomyScore { get; init; }
    public int MaxHumanCampDefendersToAttack { get; init; }
    public bool TreatUnarmedLoneHumanGuardAsUndefended { get; init; }
    public int CriticalGarrisonTarget { get; init; }
    public int AttackCooldownTurns { get; init; }
    public int RetaliationTurns { get; init; } = 20;
    public int ContestedCampMemoryTurns { get; init; } = 16;
    public float StrategicHostileTargetBonus { get; init; }
    public bool UseDetailedCombatEstimate { get; init; }
    public bool AllowGeneratedRecruitPaymentInCities { get; init; } = true;
    public int SafeFoodFloor { get; init; } = 10;
    public int SafeWaterFloor { get; init; } = 10;
    public int SafeHealing { get; init; } = 25;

    public static ClassicAiPolicy ForDifficulty(int difficulty) => difficulty switch
    {
        0 => new ClassicAiPolicy { DesiredGroupSize = 2, MinimumAttackRatio = 1.35f,
            NeutralTargetScore = 690, HostileTargetScore = 430, ExpansionEconomyScore = 850,
            MaxHumanCampDefendersToAttack = 0, TreatUnarmedLoneHumanGuardAsUndefended = true,
            CriticalGarrisonTarget = 1, AttackCooldownTurns = 4 },
        1 => new ClassicAiPolicy { DesiredGroupSize = 3, MinimumAttackRatio = 1.05f,
            NeutralTargetScore = 770, HostileTargetScore = 560, ExpansionEconomyScore = 900,
            MaxHumanCampDefendersToAttack = 1, CriticalGarrisonTarget = 2,
            AttackCooldownTurns = 2, StrategicHostileTargetBonus = 35 },
        _ => new ClassicAiPolicy { DesiredGroupSize = 4, MinimumAttackRatio = 0.75f,
            NeutralTargetScore = 860, HostileTargetScore = 680, ExpansionEconomyScore = 950,
            MaxHumanCampDefendersToAttack = int.MaxValue, CriticalGarrisonTarget = 3,
            AttackCooldownTurns = 0, StrategicHostileTargetBonus = 100,
            UseDetailedCombatEstimate = true }
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
            if (!IsAttackSuitable(state, player, observation.Current, policy))
            {
                if (player.PreviousLocation != null)
                {
                    candidates.Add(new StrategicAiDecision(
                        StrategicAiAction.Travel,
                        1250,
                        player.PreviousLocation,
                        player.PreviousLocation,
                        "retreat from an attack that is no longer safe"));
                }
                else
                {
                    candidates.Add(new StrategicAiDecision(
                        StrategicAiAction.Wait,
                        1250,
                        observation.Current,
                        Reason: "will not initiate an unsuitable attack without a retreat route"));
                }
            }
            else
            {
                candidates.Add(new StrategicAiDecision(
                    StrategicAiAction.AttackHostile,
                    1200,
                    observation.Current,
                    Reason: "hostile camp blocks the current route"));
            }
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
        Location? reinforcementCamp = StrategicAiEconomy.FindBestCampForReinforcement(
            state, policy.CriticalGarrisonTarget);
        bool needsGarrisonRecruit = reinforcementCamp != null &&
            player.Group.Count >= observation.DesiredGroupSize && player.Group.Count < Group.MAX_PEOPLE;
        if ((player.Group.Count < observation.DesiredGroupSize || needsGarrisonRecruit) &&
            state.CanRecruit(generatedPaymentAllowed))
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.Recruit,
                player.Group.Count == 1 ? 980 : needsGarrisonRecruit ? 830 : 760,
                observation.Current,
                Reason: needsGarrisonRecruit
                    ? $"critical camp {reinforcementCamp!.Title} needs another guard"
                    : "group needs another recruit"));
        }
        else if (player.Group.Count == 1)
        {
            Location? preparationCamp = StrategicAiEconomy.FindBestCampForCityPreparation(state);
            AddTravelCandidate(state, candidates, preparationCamp ?? FindNearestCity(state), 970,
                preparationCamp == null
                    ? "leader needs a recruit before claiming camps"
                    : "fill the caravan before recruiting in a city");
        }

        if (reinforcementCamp != null && player.Group.Count > observation.DesiredGroupSize)
        {
            if (reinforcementCamp == observation.Current)
            {
                candidates.Add(new StrategicAiDecision(
                    StrategicAiAction.StationFollower,
                    1080,
                    observation.Current,
                    Reason: $"raise critical garrison toward {policy.CriticalGarrisonTarget} guards"));
            }
            else
            {
                AddTravelCandidate(state, candidates, reinforcementCamp, 1080,
                    $"reinforce critical camp toward {policy.CriticalGarrisonTarget} guards");
            }
        }
        else if (reinforcementCamp != null && player.Group.Count >= observation.DesiredGroupSize &&
            !state.HasHireableNpc())
        {
            AddTravelCandidate(state, candidates, FindNearestCity(state), 800,
                "find a recruit for a critical camp garrison");
        }

        if (state.NeedsCampImprovement())
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.ImproveCamp,
                500,
                observation.Current,
                Reason: "owned camp lacks compatible production equipment"));
        }

        Location? target = ValidatePersistentTarget(state, observation, policy);
        if (target == null)
        {
            target = SelectTerritorialTarget(state, observation, policy);
            state.StrategicTarget = target;
        }

        bool earlyEconomy = state.OwnedCampCount < 3;
        bool expansionNeedsEquipment = StrategicAiEconomy.NeedsExpansionTool(state);
        // Economy work prepares the next expansion push. Once a claim or attack is
        // already safe and reachable, gathering and trading must not postpone it.
        bool economyGrowthNeeded = StrategicAiEconomy.ShouldPrioritizeEconomicGrowth(state);
        float preparedEconomyScore = target == null || economyGrowthNeeded
            ? float.PositiveInfinity
            : 300;
        if (StrategicAiEconomy.ShouldContinueTrading(state))
        {
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.Wait,
                System.Math.Min(preparedEconomyScore,
                    expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 900 : 740),
                observation.Current,
                Reason: "continue trading surplus goods for needed equipment"));
        }
        else if (StrategicAiEconomy.ShouldVisitTrader(state))
        {
            Location? tradeCity = StrategicAiEconomy.FindBestTradeCity(state) ?? FindNearestCity(state);
            AddTravelCandidate(state, candidates, tradeCity,
                System.Math.Min(preparedEconomyScore,
                    expansionNeedsEquipment ? policy.ExpansionEconomyScore : earlyEconomy ? 880 : 720),
                "deliver surplus goods and trade for needed equipment");
        }

        Location? collectionCamp = StrategicAiEconomy.FindBestCampForCollection(state);
        bool preventsFoodWaste = collectionCamp != null &&
            StrategicAiEconomy.ShouldPreventFoodWaste(state, collectionCamp);
        AddTravelCandidate(state, candidates, collectionCamp,
            System.Math.Min(preparedEconomyScore,
                preventsFoodWaste
                    ? policy.ExpansionEconomyScore + 10
                    : expansionNeedsEquipment ? policy.ExpansionEconomyScore - 10 : earlyEconomy ? 850 : 700),
            preventsFoodWaste
                ? "collect capped food stock before production is wasted"
                : "collect camp surplus to finance expansion");

        Location? deliveryCamp = StrategicAiEconomy.FindBestCampForDelivery(state);
        AddTravelCandidate(state, candidates, deliveryCamp,
            System.Math.Min(preparedEconomyScore,
                expansionNeedsEquipment ? policy.ExpansionEconomyScore - 20 : earlyEconomy ? 840 : 690),
            "deliver functional equipment or a complete recipe to camp");

        if (state.WaitTurns > 0 && !observation.CriticalSupplies)
        {
            state.WaitTurns--;
            candidates.Add(new StrategicAiDecision(
                StrategicAiAction.Wait,
                900,
                Reason: "expansion cooldown"));
        }

        if (target != null)
        {
            float score = IsHostile(target, player) ? policy.HostileTargetScore : policy.NeutralTargetScore;
            AddTravelCandidate(state, candidates, target, score, IsHostile(target, player)
                ? "advance toward hostile frontier"
                : "advance toward neutral territory");
        }

        if (player.Group.Count > 1 && player.Group.Count < observation.DesiredGroupSize && !state.HasHireableNpc())
        {
            Location? preparationCamp = StrategicAiEconomy.FindBestCampForCityPreparation(state);
            AddTravelCandidate(state, candidates, preparationCamp ?? FindNearestCity(state), 560,
                preparationCamp == null ? "look for recruits" : "collect trade cargo before looking for recruits");
        }

        string idleReason = StrategicAiEconomy.NeedsExpansionTool(state)
            ? "expansion blocked: no portable production tool and no affordable or collectible route"
            : player.Group.Where(character => character != player.Character)
                .Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0)
                ? "expansion blocked: followers are not armed and no equipment route is available"
                : "no reachable expansion target with current supplies";
        candidates.Add(new StrategicAiDecision(StrategicAiAction.Wait, 0, Reason: idleReason));
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
            DesiredGroupSize = StrategicAiEconomy.RecommendedGroupSize(state, policy.DesiredGroupSize),
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
        Route? route = FindRoute(observation.Player, observation.Current, target);
        if (route == null || !HasRouteSupplies(observation.Player, route, IsHostile(target, observation.Player)))
        {
            StrategicAiTelemetry.Report(observation.Player,
                $"abandoned target {target.Title}: route exceeds safe food or water reserves");
            return null;
        }
        if (target.Player == null)
            return observation.NeutralExpansionAllowed && state.CanClaim(target) ? target : null;
        if (target.Player == observation.Player)
            return null;
        if (!IsAttackSuitable(state, observation.Player, target, policy))
        {
            StrategicAiTelemetry.Report(observation.Player,
                $"abandoned target {target.Title}: group is not safely prepared to attack");
            return null;
        }
        return target;
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
            if (route == null || !HasRouteSupplies(observation.Player, route, IsHostile(location, observation.Player)))
                continue;

            if (location.Player == null && observation.NeutralExpansionAllowed && state.CanClaim(location))
            {
                targets.Add((location, policy.NeutralTargetScore - route.Days * 8 + Jitter()));
            }
            else if (IsHostile(location, observation.Player) &&
                IsAttackSuitable(state, observation.Player, location, policy))
            {
                float weakness = System.Math.Max(-100, 100 - AssessedDefenderStrength(location, policy));
                float strategicBonus = StrategicAiEconomy.IsStrategicLocation(state, location)
                    ? policy.StrategicHostileTargetBonus
                    : 0;
                targets.Add((location, policy.HostileTargetScore + weakness + strategicBonus - route.Days * 6 + Jitter()));
            }
        }

        return targets.OrderByDescending(target => target.Score).FirstOrDefault().Location;
    }

    static bool IsAttackSuitable(ClassicAiState state, Player player, Location target, ClassicAiPolicy policy)
    {
        Character[] followers = player.Group
            .Where(character => character != player.Character && !character.IsDead)
            .ToArray();
        if (followers.Length == 0 || followers.Any(character =>
            (character.Items.FindBestWeapon()?.DamageValue ?? 0) <= 0))
            return false;

        Character[] livingDefenders = target.CampNPC
            .Where(character => !character.IsDead && character.Player == target.Player)
            .ToArray();
        if (target.Player?.Type == PlayerType.Human && !state.IsRetaliatingAgainst(target.Player))
        {
            int visibleDefenders = livingDefenders.Length;
            if (policy.TreatUnarmedLoneHumanGuardAsUndefended && visibleDefenders == 1 &&
                (livingDefenders[0].Items.FindBestWeapon()?.DamageValue ?? 0) <= 0)
                visibleDefenders = 0;
            if (visibleDefenders > policy.MaxHumanCampDefendersToAttack)
                return false;
        }

        float defenders = policy.UseDetailedCombatEstimate
            ? livingDefenders.Sum(character =>
                character.AttackValue + character.DefenseValue + character.Health / 10f)
            : livingDefenders.Sum(character =>
                (character.Items.FindBestWeapon()?.DamageValue ?? character.BaseAttackValue) + 10f);
        if (defenders <= 0)
            return true;
        return AttackerStrength(player) / defenders >= policy.MinimumAttackRatio;
    }

    static bool HasRouteSupplies(Player player, Route route, bool hostileTarget)
    {
        int margin = hostileTarget ? 3 : 0;
        int required = route.Days + margin;
        return player.Group.GetLowestFoodWithInventory() >= required &&
            player.Group.GetLowestWaterWithInventory() >= required;
    }

    static float AttackerStrength(Player player) => player.Group
        .Where(character => !character.IsDead)
        .Sum(character => character.AttackValue + character.DefenseValue + character.Health / 10f);

    static float AssessedDefenderStrength(Location location, ClassicAiPolicy policy)
    {
        Character[] defenders = location.CampNPC
            .Where(character => !character.IsDead && character.Player == location.Player)
            .ToArray();
        return policy.UseDetailedCombatEstimate
            ? defenders.Sum(character =>
                character.AttackValue + character.DefenseValue + character.Health / 10f)
            : defenders.Sum(character =>
                (character.Items.FindBestWeapon()?.DamageValue ?? character.BaseAttackValue) + 10f);
    }

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

    static Location? FindNearestOwnedCamp(ClassicAiState state)
    {
        return state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location != state.Current)
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

            case StrategicAiAction.StationFollower:
                Character? stationed = state.StationSurplusFollower();
                if (stationed != null)
                    StrategicAiTelemetry.Report(player,
                        $"stationed surplus follower {stationed.Name} at {state.Current.Title}");
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

        for (int round = 1; round <= MaxRounds; round++)
        {
            List<Character> defenders = originalDefenders.Where(character => !character.IsDead).ToList();
            if (defenders.Count == 0)
                break;

            if (!attacker.Group.Any(character => character != attacker.Character && !character.IsDead))
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
                    .FirstOrDefault();
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
        if (defendersDefeated)
        {
            Character? guard = attacker.Group
                .Where(character => character != attacker.Character && !character.IsDead)
                .OrderBy(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();

            location.Player = null;
            if (guard != null)
            {
                state.CreateCamp(guard);
                state.MarkRecentlyCaptured(location, ClassicAiPolicy.ForDifficulty(
                    state.RootGame.World.Difficulty));
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
            state.StrategicTarget = null;
            Location? retreat = attacker.PreviousLocation;
            if (retreat != null && attacker.CanTravel(location, retreat))
            {
                attacker.Travel(retreat);
                StrategicAiTelemetry.Report(attacker,
                    $"retreated from {location.Title} to {retreat.Title} before risking the leader");
            }
            else
            {
                StrategicAiTelemetry.Report(attacker,
                    $"failed to defeat {location.Title}'s defenders; leader survived");
            }
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
