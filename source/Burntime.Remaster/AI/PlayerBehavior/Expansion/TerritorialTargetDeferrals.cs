using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Short-lived memory for strategic destinations which could not make progress.
/// This deliberately lives outside <see cref="ClassicAiState"/> so it is never
/// serialized. Loading a game may therefore reconsider the same destination.
/// </summary>
internal static class TerritorialTargetDeferrals
{
    // Give other economic and logistics work time to run before reconsidering
    // a target, while durable personnel/combat progress may still wake it early.
    const int RetryDelay = 20;
    // The strategic map is small; retaining a full campaign's recent failures
    // prevents target rotation from evicting the first blocker after four camps.
    const int MaximumEntries = 16;

    static readonly ConditionalWeakTable<ClassicAiState, Memory> memories = new();

    internal static bool TryDefer(
        ClassicAiState state,
        Location target,
        string reason,
        out int retryDay)
    {
        retryDay = 0;
        if (!TryClassify(reason, out Blocker blocker))
            return false;

        retryDay = state.RootGame.World.Day + RetryDelay;
        DeferUntil(state, target, blocker, retryDay);
        return true;
    }

    internal static void DeferForTurns(
        ClassicAiState state,
        Location target,
        int turns)
    {
        DeferUntil(
            state,
            target,
            Blocker.None,
            state.RootGame.World.Day + turns);
    }

    static void DeferUntil(
        ClassicAiState state,
        Location target,
        Blocker blocker,
        int retryDay)
    {
        Memory memory = memories.GetOrCreateValue(state);
        memory.Entries[target] = new Entry(
            target.Player,
            blocker,
            retryDay,
            Progress(state, blocker),
            memory.NextSequence++);

        if (memory.Entries.Count > MaximumEntries)
        {
            Location oldest = null;
            long oldestSequence = long.MaxValue;
            foreach ((Location location, Entry entry) in memory.Entries)
            {
                if (entry.Sequence >= oldestSequence)
                    continue;
                oldest = location;
                oldestSequence = entry.Sequence;
            }
            if (oldest != null)
                memory.Entries.Remove(oldest);
        }
    }

    internal static bool IsDeferred(ClassicAiState state, Location target)
    {
        if (!memories.TryGetValue(state, out Memory memory) ||
            !memory.Entries.TryGetValue(target, out Entry entry))
            return false;

        bool ownershipChanged = target.Player != entry.Owner;
        bool retryReached = state.RootGame.World.Day >= entry.RetryDay;
        // Daily camp provisioning makes food and water totals oscillate even
        // when a route remains impossible. Do not treat that incidental refill
        // as enough progress to immediately restart the same stalled plan.
        // Personnel and combat improvements are durable and may safely wake a
        // deferred target before its short retry delay expires.
        bool madeProgress = entry.Blocker is not
            (Blocker.RouteSupplies or Blocker.SettlementSupplies) &&
            Progress(state, entry.Blocker) > entry.Progress;
        if (!ownershipChanged && !retryReached && !madeProgress)
            return true;

        memory.Entries.Remove(target);
        return false;
    }

    static bool TryClassify(string reason, out Blocker blocker)
    {
        if (reason.Contains("waiting for route supplies", StringComparison.Ordinal))
            blocker = Blocker.RouteSupplies;
        else if (reason.Contains("waiting for travel reserves", StringComparison.Ordinal) ||
            reason.Contains("projected two-person route", StringComparison.Ordinal))
            blocker = Blocker.SettlementSupplies;
        else if (reason.Contains("waiting for", StringComparison.Ordinal) &&
            reason.Contains("attackers", StringComparison.Ordinal))
            blocker = Blocker.Attackers;
        else if (reason.Contains("safe combat readiness", StringComparison.Ordinal))
            blocker = Blocker.CombatReadiness;
        else
        {
            blocker = default;
            return false;
        }
        return true;
    }

    static int Progress(ClassicAiState state, Blocker blocker) => blocker switch
    {
        Blocker.RouteSupplies or Blocker.SettlementSupplies =>
            Math.Min(
                state.Player.Group.GetLowestFoodWithInventory(),
                state.Player.Group.GetLowestWaterWithInventory()),
        Blocker.Attackers => state.Player.Group.Count,
        Blocker.CombatReadiness => (int)MathF.Round(
            CombatStrength.Attacker(state.Player) * 10),
        _ => 0
    };

    enum Blocker
    {
        None,
        RouteSupplies,
        Attackers,
        CombatReadiness,
        SettlementSupplies
    }

    sealed class Memory
    {
        public Dictionary<Location, Entry> Entries { get; } = new();
        public long NextSequence { get; set; }
    }

    readonly record struct Entry(
        Player Owner,
        Blocker Blocker,
        int RetryDay,
        int Progress,
        long Sequence);
}
