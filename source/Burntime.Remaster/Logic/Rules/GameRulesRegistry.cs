using System;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal static class GameRulesRegistry
{
    public static IGameRules Get(RuleSetId id) => id switch
    {
        RuleSetId.Dos => new DosRules(),
        RuleSetId.Amiga => new AmigaRules(),
        RuleSetId.Extended => new ExtendedRules(),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
