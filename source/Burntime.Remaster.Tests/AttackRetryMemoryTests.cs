using System.Collections.Generic;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Tests;

using static Program;

static class AttackRetryMemoryTests
{
    internal static IEnumerable<Case<bool>> Cases()
    {
        yield return Bool("fully healed damage permits one retry, then defers", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            Equal(false, memory.Record(target, owner, attackers, defenders, 0, false).Blocked, "first attempt");
            Equal(true, memory.CanRetry(target, owner, attackers), "one retry");
            Equal(true, memory.Record(target, owner, attackers, defenders, 0, false).Blocked, "same starting defense");
            return !memory.CanRetry(target, owner, attackers);
        });
        yield return Bool("damage retained until next encounter permits continued attrition", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            memory.Record(target, owner, attackers, defenders, 0, false);
            var weakened = defenders with { Health = 95, Strength = 29.5f };
            var result = memory.Record(target, owner, attackers, weakened, 0, false);
            return !result.Blocked && result.Comparison.Contains("defense weaker");
        });
        yield return Bool("old injury is not lasting progress", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            defenders = defenders with { Health = 5, Strength = 20.5f };
            memory.Record(target, owner, attackers, defenders, 0, false);
            return memory.Record(target, owner, attackers, defenders, 0, false).Blocked;
        });
        yield return Bool("kill during a retry is productive even if earlier damage healed", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            memory.Record(target, owner, attackers, defenders, 0, false);
            return !memory.Record(target, owner, attackers, defenders, 1, false).Blocked;
        });
        yield return Bool("recovery, reinforcements and equipment can reopen a deferred target", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            memory.Record(target, owner, attackers, defenders, 0, false);
            memory.Record(target, owner, attackers, defenders, 0, false);
            Equal(true, memory.CanRetry(target, owner, attackers with { BossHealth = 57 }), "boss recovery");
            Equal(true, memory.CanRetry(target, owner, attackers with { Count = 3 }), "reinforcement");
            Equal(true, memory.CanRetry(target, owner, attackers with { Strength = 70 }), "equipment improvement");
            return !memory.CanRetry(target, owner, attackers with { BossHealth = 48 });
        });
        yield return Bool("new local evidence reopens defense but stale knowledge does not", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            memory.Record(target, owner, attackers, defenders, 0, false);
            memory.Record(target, owner, attackers, defenders, 0, false);
            Equal(false, memory.CanRetry(target, owner, attackers), "no remote health inspection");
            return memory.CanRetry(target, owner, attackers, defenders with { Health = 80, Strength = 28 });
        });
        yield return Bool("other targets do not erase a stalemate; ownership and capture do", true, () =>
        {
            var (memory, target, owner, attackers, defenders) = Fixture();
            var other = target.Container.Create<Location>();
            memory.Record(target, owner, attackers, defenders, 0, false);
            memory.Record(target, owner, attackers, defenders, 0, false);
            memory.Record(other, owner, attackers, defenders, 0, false);
            Equal(false, memory.CanRetry(target, owner, attackers), "per-camp memory");
            var newOwner = target.Container.Create<Player>(new object[] { 2 });
            Equal(true, memory.CanRetry(target, newOwner, attackers), "new owner");
            memory.Record(target, owner, attackers, defenders, 1, true);
            return memory.CanRetry(target, owner, attackers);
        });
    }

    static (AttackRetryMemory, Location, Player, AttackRetryMemory.Force, AttackRetryMemory.Force) Fixture()
    {
        var (_, _, owner, manager) = EncounterPlayers(RuleSet.Extended);
        var target = manager.Create<Location>(); target.Player = owner;
        return (new(), target, owner, new(2, 147, 60, 47), new(1, 100, 30, 0));
    }
}
