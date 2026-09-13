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

static class HeadlessSimulationTests
{
    internal static IEnumerable<Case<bool>> DosConflictAttritionCases()
    {
        yield return new("blocked maintenance permits daily supply death", true,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfile.Dos, true, true, true));
        yield return new("successful refill clears the exemption", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfile.Dos, true, true, false));
        yield return new("unrelated DOS daily death remains unexpected", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfile.Dos, true, false, true));
        yield return new("Modern AI never receives the exemption", false,
            () => Burntime.Remaster.AI.HeadlessSimulation.IsExpectedDosConflictAttrition(
                AiProfile.Modern, true, true, true));
    }

    internal static IEnumerable<Case<bool>> AmigaFoodAttritionCases()
    {
        yield return new("daily food attrition remains permitted", true,
            () => HeadlessSimulation.IsExpectedAmigaFoodAttrition(
                AiProfile.Amiga, true, true, false));
        yield return new("water exhaustion is never exempted", false,
            () => HeadlessSimulation.IsExpectedAmigaFoodAttrition(
                AiProfile.Amiga, true, true, true));
        yield return new("non-daily deaths are not food attrition", false,
            () => HeadlessSimulation.IsExpectedAmigaFoodAttrition(
                AiProfile.Amiga, false, true, false));
        yield return new("Modern AI never receives the exemption", false,
            () => HeadlessSimulation.IsExpectedAmigaFoodAttrition(
                AiProfile.Modern, true, true, false));
    }
}
