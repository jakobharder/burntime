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

static class GameDefinitionsTests
{
    internal static IEnumerable<Case<int>> ProfileParsingCases()
    {
        yield return Int("DOS rules case-insensitive", (int)RuleSet.Dos,
            () => (int)GameDefinitions.ParseRules("dOs"));
        yield return Int("Classic rules case-insensitive", (int)RuleSet.Classic,
            () => (int)GameDefinitions.ParseRules("cLaSsIc"));
        yield return Int("invalid rules fallback", (int)RuleSet.Amiga,
            () => (int)GameDefinitions.ParseRules("invalid", RuleSet.Amiga));
        yield return Int("numeric undefined rules fallback", (int)RuleSet.Extended,
            () => (int)GameDefinitions.ParseRules("99"));
        yield return Int("Amiga AI case-insensitive", (int)AiProfile.Amiga,
            () => (int)GameDefinitions.ParseAi("aMiGa"));
        yield return Int("Modern AI case-insensitive", (int)AiProfile.Modern,
            () => (int)GameDefinitions.ParseAi("mOdErN"));
        yield return Int("missing AI defaults Modern", (int)AiProfile.Modern,
            () => (int)GameDefinitions.ParseAi(null));
        yield return Int("numeric undefined AI fallback", (int)AiProfile.Modern,
            () => (int)GameDefinitions.ParseAi("99"));
    }

    internal static IEnumerable<Case<int>> RuleRegistryCases()
    {
        yield return Int("DOS registry", (int)RuleSet.Dos,
            () => (int)new GameRules(RuleSet.Dos).Id);
        yield return Int("Amiga registry", (int)RuleSet.Amiga,
            () => (int)new GameRules(RuleSet.Amiga).Id);
        yield return Int("Classic registry", (int)RuleSet.Classic,
            () => (int)new GameRules(RuleSet.Classic).Id);
        yield return Int("Extended registry", (int)RuleSet.Extended,
            () => (int)new GameRules(RuleSet.Extended).Id);
    }
}
