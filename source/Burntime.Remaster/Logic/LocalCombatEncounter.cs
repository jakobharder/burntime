using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.Logic;

/// <summary>
/// Coordinates one visible group attack without making its strikes atomic.
/// Every attacker approaches with its own weapon range. The defender then
/// answers each aggressor individually and flees once, after the complete
/// retaliation batch has finished.
/// </summary>
internal sealed class LocalCombatEncounter
{
    const float AttackApproachTimeout = 10;
    const float RetaliationApproachTimeout = 2;

    sealed class AttackOrder
    {
        internal readonly Character Attacker;
        internal readonly Character Defender;
        internal readonly float Range;
        internal float Elapsed;

        internal AttackOrder(Character attacker, Character defender, float range)
        {
            Attacker = attacker;
            Defender = defender;
            Range = range;
        }
    }

    readonly Character defender;
    readonly List<AttackOrder> attacks = new();
    readonly Queue<AttackOrder> retaliationQueue = new();
    AttackOrder? retaliation;
    Character? fleeFrom;

    internal bool IsComplete { get; private set; }

    internal LocalCombatEncounter(IEnumerable<Character> attackers, Character defender)
    {
        this.defender = defender;
        List<Character> participants = attackers
            .Where(attacker => attacker != null && attacker != defender && !attacker.IsDead)
            .Distinct()
            .ToList();

        // An encounter owns the defender's movement. In particular, a new
        // round must not spend its first seconds chasing the previous flee.
        defender.HoldForCombat();

        foreach (Character attacker in participants)
        {
            var order = new AttackOrder(attacker, defender, attacker.AttackRange);
            attacks.Add(order);
            attacker.BeginCombatApproach(defender, order.Range);
        }

        IsComplete = attacks.Count == 0 || defender.IsDead;
        if (IsComplete)
            defender.ReleaseCombatHold();
    }

    internal void Update(float elapsed)
    {
        if (IsComplete)
            return;
        if (defender.IsDead)
        {
            Cancel();
            return;
        }

        UpdateAttacks(elapsed);
        if (defender.IsDead)
        {
            Cancel();
            return;
        }

        // Complete the group attack phase before moving the defender. Starting
        // retaliation early makes the common target move while the remaining
        // attackers are still approaching and creates timing-dependent stalls.
        if (attacks.Count > 0)
            return;

        UpdateRetaliation(elapsed);

        if (attacks.Count == 0 && retaliation == null && retaliationQueue.Count == 0)
            Finish();
    }

    void UpdateAttacks(float elapsed)
    {
        foreach (AttackOrder order in attacks.ToArray())
        {
            Character attacker = order.Attacker;
            if (!CanContinue(attacker, order.Defender))
            {
                RemoveAttack(order);
                continue;
            }

            if (!attacker.IsInAttackRange(order.Defender, order.Range))
            {
                order.Elapsed += elapsed;
                if (order.Elapsed >= AttackApproachTimeout)
                    RemoveAttack(order);
                continue;
            }

            bool attacked = attacker.ResolveSingleAttack(order.Defender);
            attacks.Remove(order);
            if (attacked && !order.Defender.IsDead)
            {
                attacker.HoldForCombat();
                EnqueueRetaliation(attacker);
            }
            else
                attacker.ClearCombatApproach(order.Defender);
        }
    }

    void EnqueueRetaliation(Character attacker)
    {
        retaliationQueue.Enqueue(new AttackOrder(defender, attacker,
            defender.AttackRange));
    }

    void UpdateRetaliation(float elapsed)
    {
        if (retaliation == null)
        {
            while (retaliationQueue.Count > 0)
            {
                AttackOrder queuedOrder = retaliationQueue.Dequeue();
                if (!CanContinue(queuedOrder.Attacker, queuedOrder.Defender))
                {
                    queuedOrder.Defender.ReleaseCombatHold();
                    continue;
                }

                retaliation = queuedOrder;
                defender.BeginCombatApproach(queuedOrder.Defender, queuedOrder.Range);
                break;
            }
        }

        AttackOrder? order = retaliation;
        if (order == null)
            return;
        if (!CanContinue(order.Attacker, order.Defender))
        {
            CompleteRetaliation(order);
            return;
        }

        if (!order.Attacker.IsInAttackRange(order.Defender, order.Range))
        {
            order.Elapsed += elapsed;
            if (order.Elapsed >= RetaliationApproachTimeout)
            {
                // Every successful group strike earns a response. Pathfinding
                // failure must not silently discard it or leave input locked.
                order.Attacker.ResolveSingleAttack(order.Defender);
                CompleteRetaliation(order);
            }
            return;
        }
        order.Attacker.ResolveSingleAttack(order.Defender);
        CompleteRetaliation(order);
    }

    void CompleteRetaliation(AttackOrder order)
    {
        order.Attacker.ClearCombatApproach(order.Defender);
        order.Defender.ReleaseCombatHold();
        retaliation = null;
        // Once every response has struck, the defender may flee.
        fleeFrom = order.Defender;

        // Keep the defender from resuming autonomous movement during the frame
        // between two responses. The next response replaces this hold.
        if (retaliationQueue.Count > 0)
            defender.HoldForCombat();
    }

    static bool CanContinue(Character attacker, Character target) =>
        !attacker.IsDead && !target.IsDead && attacker.Location == target.Location;

    void RemoveAttack(AttackOrder order)
    {
        order.Attacker.ClearCombatApproach(order.Defender);
        attacks.Remove(order);
    }

    internal void CancelOffense()
    {
        foreach (AttackOrder order in attacks.ToArray())
            RemoveAttack(order);
    }

    internal void Cancel()
    {
        CancelOffense();
        if (retaliation != null)
        {
            retaliation.Attacker.ClearCombatApproach(retaliation.Defender);
            retaliation.Defender.ReleaseCombatHold();
        }
        retaliation = null;
        foreach (AttackOrder order in retaliationQueue)
            order.Defender.ReleaseCombatHold();
        retaliationQueue.Clear();
        defender.ReleaseCombatHold();
        IsComplete = true;
    }

    void Finish()
    {
        // This is deliberately after both the offensive orders and every queued
        // retaliation. The defender never flees between individual responses.
        defender.ReleaseCombatHold();
        if (fleeFrom != null && !defender.IsDead)
        {
            defender.BeginFleeFrom(fleeFrom);
        }
        IsComplete = true;
    }
}
