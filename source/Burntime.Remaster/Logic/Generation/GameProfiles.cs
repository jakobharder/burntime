using System;

namespace Burntime.Remaster.Logic.Generation;

public enum RuleSet
{
    Dos = 0,
    Amiga = 1,
    Extended = 2,
    Classic = 3
}

public enum AiProfile
{
    Modern = 0,
    None = 1,
    Dos = 2,
    Amiga = 3
}

[Flags]
public enum GameFeature
{
    None = 0,
    ExtendedItems = 1 << 0,
    Construction = 1 << 1,
    MutantDrops = 1 << 2
}

public sealed record GameDefinition(
    RuleSet Rules,
    string SettingsPath,
    string ItemsPath,
    string TraderPath,
    string ProductionPath,
    string MapPath,
    string WaysPath,
    GameFeature Features)
{
    public bool Has(GameFeature feature) => (Features & feature) == feature;
}

public static class GameDefinitions
{
    public static GameDefinition Get(RuleSet rules)
    {
        return rules switch
        {
            RuleSet.Dos => new(
                rules,
                "rules/dos/game.txt",
                "items@rules/dos/items.txt",
                "rules/dos/trader.txt",
                "rules/dos/production.txt",
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.Construction),
            RuleSet.Amiga => new(
                rules,
                "rules/amiga/game.txt",
                "items@rules/amiga/items.txt",
                "rules/amiga/trader.txt",
                "rules/amiga/production.txt",
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.Construction),
            RuleSet.Classic => new(
                rules,
                "rules/classic/game.txt",
                "items@rules/dos/items.txt",
                "rules/dos/trader.txt",
                "rules/amiga/production.txt",
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.Construction),
            RuleSet.Extended => new(
                rules,
                "rules/extended/game.txt",
                "items@rules/extended/items.txt",
                "rules/extended/trader.txt",
                "rules/extended/production.txt",
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.ExtendedItems | GameFeature.Construction | GameFeature.MutantDrops),
            _ => throw new ArgumentOutOfRangeException(nameof(rules))
        };
    }

    public static RuleSet ParseRules(string? value, RuleSet fallback = RuleSet.Extended) =>
        Enum.TryParse(value, ignoreCase: true, out RuleSet rules) &&
            Enum.IsDefined(rules)
            ? rules
            : fallback;

    public static AiProfile ParseAi(string? value, AiProfile fallback = AiProfile.Modern) =>
        Enum.TryParse(value, ignoreCase: true, out AiProfile ai) &&
            Enum.IsDefined(ai)
            ? ai
            : fallback;
}
