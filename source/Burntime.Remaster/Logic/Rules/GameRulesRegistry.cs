using System;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal static class GameRulesRegistry
{
    public static IGameRules Get(RuleSet id) => id switch
    {
        RuleSet.Dos => new DosRules(),
        RuleSet.Amiga => new AmigaRules(),
        RuleSet.Classic => new ClassicRules(),
        RuleSet.Extended => new ExtendedRules(),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
