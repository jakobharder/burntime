using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class EconomyObservationTests
{
    internal static IEnumerable<Case<bool>> EconomyObservationCases()
    {
        yield return new("hunting does not exempt economic camps", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 0, false, false));
        yield return new("new recruit combat damage receives grace", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 60, false, true));
        yield return new("dead guard still receives combat grace", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsCombatDamage(100, 0, true, false));
        yield return new("ordinary daily starvation damage", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 77, 0, 4));
        yield return new("terminal small health drop counts", true,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(14, 0, 6, 0));
        yield return new("small nonlethal damage is not a supply penalty", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 98, 0, 4));
        yield return new("supplied damage is not starvation", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(100, 77, 4, 4));
        yield return new("dead characters cannot repeatedly count", false,
            () => Burntime.Remaster.AI.EconomyObservation.IsSupplyDamage(0, 0, 0, 0));
    }
}
