using System;
using System.Linq;
using Burntime.Framework;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.AI;

/// <summary>
/// Amiga AI identity and serialization boundary. Its recovered decision loop is
/// intentionally isolated here so it can evolve without changing Modern AI.
/// </summary>
[Serializable]
internal sealed class AmigaAiState : OriginalAiState
{
    // The original player record stores the day of the last completed trip;
    // its route filter relaxes after three stationary days. OptionalField
    // keeps experimental saves made before this fidelity pass loadable.
    [System.Runtime.Serialization.OptionalField]
    int lastArrivalDay;

    // Original nonzero character location means local maintenance is pending.
    // 0x951c replaces it with a completed pseudo-trip after local work; the
    // next day's 0x7264 branch then skips directly to routing.
    [System.Runtime.Serialization.OptionalField]
    bool localMaintenancePending = true;

    int ConflictBudget => new[] { 3, 6, 10 }[Difficulty];
    int GarrisonTarget => new[] { 2, 3, 4 }[Difficulty];
    protected override int MaximumGroupSize => Difficulty == 0 ? 3 : 4;

    protected override bool CanRecruit(Logic.Character candidate) =>
        candidate.Class is CharClass.Mercenary or CharClass.Technician or CharClass.Doctor &&
        Player.Character.Experience >= candidate.Experience - 4;

    protected override void HireRecruit(Logic.Character recruit) =>
        recruit.HireWithoutInitialization(Player);

    int ExpansionTarget()
    {
        AiStateOperations.ProgressBenchmark benchmark =
            AiStateOperations.GetProgressBenchmark(RootGame, Player);
        if (!benchmark.RestrictsAi)
            return AiStateOperations.ProgressRelativeLimitOrUnrestricted(benchmark, 0);
        int maximumMargin = new[] { 3, 7, 15 }[Difficulty];
        int margin = Burntime.Platform.Math.Random.Next(maximumMargin + 1);
        return AiStateOperations.ProgressRelativeLimitOrUnrestricted(benchmark, margin);
    }


    internal override void InitAfterLoad()
    {
        base.InitAfterLoad();
        if (lastArrivalDay < 0)
            lastArrivalDay = RootGame.World.Day;
    }

    public override void Turn()
    {
        if (Player.IsDead || Player.Character.IsDead)
            return;
        Player.RecalculateExperience();
        if (Player.IsTraveling)
            return;

        bool arrived = headedLocation != null && Current == headedLocation.Object;
        if (arrived)
        {
            headedLocation = null;
            localMaintenancePending = true;
        }

        if (localMaintenancePending)
        {
            RecoverGroup(arrived: true);
            lastArrivalDay = RootGame.World.Day;
            // 0x7286..0x729c: only this branch performs local work and clears
            // the pending state. A suppressed update retains maintenance.
            if (ExpansionTarget() > OwnedCampCount)
            {
                RecruitOneAtCurrentLocation();
                ResolveCurrentOpposition();
                TryClaimOrReinforceCurrentCamp();
                localMaintenancePending = false;
            }
        }

        // A normal arrival stamps the current day into the player record. The
        // route selector consequently leaves the party at the location until
        // the next AI update. Hostile camps are the exception: their owner
        // branch immediately sends the party back along its entry route.
        bool expansionRoute = ExpansionTarget() > OwnedCampCount;
        Logic.Location? next = ChooseNextStep(expansionRoute);
        if (next != null)
            StartTravel(next);
    }

    Logic.Location? ChooseNextStep(bool expansionRoute)
    {
        Logic.Location current = Player.Location;

        // From an enemy camp the original selector bypasses its normal mode
        // checks. The Remaster travel contract exposes the equivalent legal
        // edge as PreviousLocation.
        if (current.Player != null && current.Player != Player)
            return Player.PreviousLocation is Logic.Location previous &&
                Player.CanTravel(current, previous) ? previous : null;

        // A neutral producing camp first looks for an adjacent owned camp. If
        // none exists, the executable forces maintenance route mode.
        if (!current.IsCity && current.Player == null)
        {
            if (current.Production != null)
            {
                Logic.Location? returnCamp = current.Neighbors.FirstOrDefault(location =>
                    location.Player == Player && Player.CanTravel(current, location));
                if (returnCamp != null)
                    return returnCamp;
                expansionRoute = false;
            }
        }

        // The executable evaluates one pseudo-random route slot. A rejected
        // occupied route is not replaced with a globally better destination.
        if (current.Neighbors.Count == 0)
            return null;
        int slot = Burntime.Platform.Math.Random.Next(4);
        if (slot >= current.Neighbors.Count)
            slot &= 1;
        if (slot >= current.Neighbors.Count)
            slot = 0;
        Logic.Location candidate = current.Neighbors[slot];
        if (!Player.CanTravel(current, candidate))
            return null;

        int stationaryDays = RootGame.World.Day - lastArrivalDay;
        if (stationaryDays == 0)
            return null;
        if (stationaryDays >= 3)
            return candidate;

        if (expansionRoute)
        {
            if (candidate.Player == null)
                return candidate;
            if (candidate.Player == Player)
            {
                int stationed = candidate.CampNPC.Count(character =>
                    character.Player == Player && !character.IsDead);
                return ProductionCapacity(candidate) - stationed > 1 ? candidate : null;
            }
            return Player.Character.Food >= 8 ? candidate : null;
        }

        return candidate.IsCity || candidate.Player == Player ? candidate : null;
    }

    bool TryClaimOrReinforceCurrentCamp()
    {
        Logic.Location current = Player.Location;
        if (current.IsCity || current.Player != null && current.Player != Player)
            return false;

        int stationed = current.CampNPC.Count(character =>
            character.Player == Player && !character.IsDead);

        if (current.Player == Player)
        {
            if (ProductionCapacity(current) - stationed <= 1)
                return TryUpgradeProduction(current);
            if (current.Source.Water - stationed <= 1)
                return TryInstallPump(current);
        }

        if (stationed >= GarrisonTarget)
            return false;

        Logic.Character? guard = Player.Group.Skip(1)
            .FirstOrDefault(character => !character.IsDead);
        if (guard == null)
            return false;

        string? protection = null;
        if (current.Danger != null)
        {
            if (current.Danger.Type == "gas")
            {
                if (stationed >= 2 || RootGame.World.Day < 80)
                    return false;
                protection = "item_gas_mask";
            }
            else
            {
                if (stationed >= 1 || RootGame.World.Day < 200)
                    return false;
                protection = "item_protective_suit";
            }

            if (guard.Items.IsFull)
                return false;
            Item item = RootGame.ItemTypes[protection].Generate();
            guard.Items.Add(item);
            guard.Protection = item;
        }

        bool claimed = current.Player == null;
        guard.JoinCamp();
        if (claimed && TryCreateCampItem(current, "item_knife"))
            current.Production = RootGame.Productions[0];
        AiTelemetry.Report(Player, claimed
            ? $"claimed {current.Title} with {guard.Name}"
            : $"stationed {guard.Name} at {current.Title}");
        return true;
    }

    bool TryInstallPump(Logic.Location location)
    {
        string? itemId = null;
        if (RootGame.World.Day >= 300)
            itemId = "item_industrial_pump";
        else if (RootGame.World.Day >= 100)
            itemId = "item_hand_pump";
        if (itemId == null || !TryCreateCampItem(location, itemId,
            location.Rooms.FirstOrDefault(room => room.IsWaterSource)))
            return false;
        AiTelemetry.Report(Player, $"installed {itemId} at {location.Title}");
        return true;
    }

    bool TryUpgradeProduction(Logic.Location location)
    {
        if (RootGame.World.Day < 30 || location.Production == null ||
            location.Production.ID >= 3)
            return false;
        Logic.Production next = RootGame.Productions[location.Production.ID + 1];
        if (!location.ValidProductions.Contains(next))
            return false;
        string itemId = next.ID switch
        {
            1 => "item_rat_trap",
            2 => "item_snake_trap",
            3 => "item_trap",
            _ => throw new InvalidOperationException()
        };
        if (!TryCreateCampItem(location, itemId))
            return false;
        location.Production = next;
        AiTelemetry.Report(Player, $"upgraded {location.Title} with {itemId}");
        return true;
    }

    bool TryCreateCampItem(Logic.Location location, string itemId,
        Logic.Room? preferredRoom = null)
    {
        if (!location.Rooms.Any(room => !room.Items.IsFull))
            return false;
        Item item = RootGame.ItemTypes[itemId].Generate();
        location.StoreItem(item, preferredRoom: preferredRoom);
        return true;
    }

    static int ProductionCapacity(Logic.Location location)
    {
        if (location.Production == null)
            return 0;
        int tools = location.GetProductionToolCount(location.Production);
        return location.Production.ID switch
        {
            0 => tools == 0 ? 1 : 3,
            1 => tools == 0 ? 0 : tools == 1 ? 3 : 4,
            2 => tools == 0 ? 0 : tools == 1 ? 4 : 5,
            3 => tools == 0 ? 0 : tools == 1 ? 5 : 7,
            _ => 0
        };
    }

    protected override void RecoverGroup(bool arrived)
    {
        if (!arrived)
            return;

        ApplyArrivalRecovery(Player);

        // 0x726a..0x7282 calls 0x7b1e for ground items, then rooms when
        // unowned/self-owned. This is not DOS's one-record deletion shortcut.
        Logic.Character[] party = RootGame.World.AllCharacters.Where(c =>
            c.Player == Player && c.IsWithBoss && !c.IsDead).ToArray();
        int productionTool = Player.Location.Production?.ID switch
        {
            0 => 0x3d, 1 => 0x43, 2 => 0x45, 3 => 0x44, _ => 0
        };
        OriginalItemRecords.CleanupAmiga(Player.Location, party,
            item => RootGame.ItemTypes.GetOriginalTitleId(item.Type), productionTool);
    }

    internal static void ApplyArrivalRecovery(Logic.Player player)
    {
        bool arrivedInCity = player.Location.IsCity;
        // 0x7ca2 skips the hostile-origin check for non-city destinations.
        bool leftHostileCamp = arrivedInCity && player.PreviousLocation?.Player != null &&
            player.PreviousLocation.Player != player;
        int food = leftHostileCamp ? 3 : 9;
        int health = leftHostileCamp ? 20 : arrivedInCity ? 30 : 0;
        foreach (Logic.Character character in player.Group)
        {
            if (player.Location.Player == null || player.Location.Player == player)
                character.Food = System.Math.Min(9, character.Food + food);
            character.Health += health;
        }
    }

    protected override void PrepareRecruit(Logic.Character recruit) { }

    protected override int StrategicDamage(Logic.Character attacker, Logic.Character defender)
    {
        string? weaponId = attacker.SelectOriginalWeapon(allowUnloadedRifle: true)?.ID;
        int basis = weaponId switch
        {
            "item_knife" => 9,
            "item_axe" => 12,
            "item_pitchfork" => 14,
            "item_loaded_rifle" => 20,
            "item_unloaded_rifle" => 6,
            _ => 5
        };
        int randomMask = weaponId == "item_pitchfork" ? 7 : 3;
        int experience = attacker.Experience;
        if (attacker.Class != CharClass.Mercenary)
            experience /= 2;
        int damage = basis + basis * experience / 100;

        if (attacker.IsWithBoss)
        {
            int minimum = RootGame.World.Day switch
            {
                < 100 => 0,
                < 200 => 12,
                < 300 => 18,
                _ => 25
            };
            damage = System.Math.Max(damage, minimum);
            if (RootGame.World.Day >= 200)
                randomMask = 7;
        }
        return damage + Burntime.Platform.Math.Random.Next(randomMask + 1);
    }

    protected override bool ResolveCurrentOpposition()
    {
        Logic.Location current = Player.Location;
        if (current.Player == null || current.Player == Player)
            return false;

        Logic.Character[] defenders = GetDefenders(current).ToArray();
        if (defenders.Length == 0)
        {
            current.Player = null;
            return false;
        }

        int normalPairCounter = new[] { 3, 4, 6 }[Difficulty];
        foreach (Logic.Character attacker in Player.Group.Where(character => !character.IsDead).ToArray())
        {
            bool advanceToNextAttacker = false;
            foreach (Logic.Character defender in defenders.Where(character => !character.IsDead))
            {
                // The original rolls each fighter's strength once per pairing.
                int attack = StrategicDamage(attacker, defender);
                int retaliation = StrategicDamage(defender, attacker);
                int pairExchanges = attack > retaliation ? normalPairCounter + 1
                    : attacker.Health >= 80 ? 3 : 0;
                if (pairExchanges == 0)
                    continue;
                int remainingExchanges = ConflictBudget;
                bool defenderKilled = false;

                while (pairExchanges-- > 0 && remainingExchanges > 0 &&
                    !attacker.IsDead && !defender.IsDead)
                {
                    // The AI boss withdraws from a pairing at 65 health.
                    if (attacker == Player.Character && attacker.Health <= 65)
                        break;

                    defender.Health -= attack;
                    if (defender.IsDead)
                    {
                        defenderKilled = true;
                        break;
                    }
                    attacker.Health -= retaliation;
                    remainingExchanges--;
                }

                // Exhausting the difficulty budget terminates this entire AI
                // conflict pass. A surviving defender advances to the next
                // attacker; only a kill lets the same attacker scan onward.
                if (remainingExchanges == 0)
                    return ReportContested(current);
                if (!defenderKilled)
                {
                    advanceToNextAttacker = true;
                    break;
                }
            }
            if (advanceToNextAttacker)
                continue;
        }

        if (defenders.All(character => character.IsDead))
        {
            current.Player = null;
            AiTelemetry.Report(Player, $"defeated opposition at {current.Title}");
            return false;
        }

        return ReportContested(current);
    }

    bool ReportContested(Logic.Location current)
    {
        mode = Mode.WaitInterval;
        wait = 1;
        AiTelemetry.Report(Player, $"remains contested at {current.Title}");
        return true;
    }
}
