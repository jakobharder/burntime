using System;

namespace Burntime.Remaster.Logic.Generation;

public enum RuleSetId
{
    Dos = 0,
    Amiga = 1,
    Extended = 2
}

public enum AiProfileId
{
    Extended = 0,
    None = 1,
    Dos = 2,
    Amiga = 3
}

/// <summary>
/// Runtime identity exposed by a serialized AI state. Rules depend on this
/// neutral profile contract rather than concrete AI implementations.
/// </summary>
internal interface IAiProfileState
{
    AiProfileId Profile { get; }
}

public enum WorldId
{
    Original = 0
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
    RuleSetId Rules,
    string SettingsPath,
    string ItemsPath,
    string TraderPath,
    string ProductionPath,
    WorldId World,
    string MapPath,
    string WaysPath,
    GameFeature Features)
{
    public bool Has(GameFeature feature) => (Features & feature) == feature;
}

public static class GameDefinitions
{
    public static GameDefinition Get(RuleSetId rules, WorldId world = WorldId.Original)
    {
        if (world != WorldId.Original)
            throw new ArgumentOutOfRangeException(nameof(world));

        return rules switch
        {
            RuleSetId.Dos => new(
                rules,
                "rules/dos/gamesettings.txt",
                "items@rules/dos/items.txt",
                "rules/dos/trader.txt",
                "rules/dos/production.txt",
                world,
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.Construction),
            RuleSetId.Amiga => new(
                rules,
                "rules/amiga/gamesettings.txt",
                "items@rules/amiga/items.txt",
                "rules/amiga/trader.txt",
                "rules/amiga/production.txt",
                world,
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.Construction),
            RuleSetId.Extended => new(
                rules,
                "rules/extended/gamesettings.txt",
                "items@rules/extended/items.txt",
                "rules/extended/trader.txt",
                "rules/extended/production.txt",
                world,
                "maps/mat_000.burnmap",
                "ways@maps/mat_000-ways.txt",
                GameFeature.ExtendedItems | GameFeature.Construction | GameFeature.MutantDrops),
            _ => throw new ArgumentOutOfRangeException(nameof(rules))
        };
    }

    // Saved DataIDs from releases before per-rule item files remain resolvable.
    internal static string ResolveItemsPath(string path) => path switch
    {
        "items.txt" => "rules/extended/items.txt",
        "items_original.txt" => "rules/dos/items.txt",
        _ => path
    };

    // Old callers retain their names, but never load a second copy of settings.
    internal static string ResolveSettingsPath(string path) => path switch
    {
        "gamesettings_original.txt" => "rules/dos/gamesettings.txt",
        "gamesettings_extended.txt" => "rules/extended/gamesettings.txt",
        _ => path
    };

    public static RuleSetId ParseRules(string? value, RuleSetId fallback = RuleSetId.Extended) =>
        Enum.TryParse(value, ignoreCase: true, out RuleSetId rules) &&
            Enum.IsDefined(rules)
            ? rules
            : fallback;

    public static AiProfileId ParseAi(string? value, AiProfileId fallback = AiProfileId.Extended) =>
        Enum.TryParse(value, ignoreCase: true, out AiProfileId ai) &&
            Enum.IsDefined(ai)
            ? ai
            : fallback;
}
