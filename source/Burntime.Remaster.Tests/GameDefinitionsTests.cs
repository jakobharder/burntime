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
using Burntime.Platform.IO;

namespace Burntime.Remaster.Tests;

using static Program;

static class GameDefinitionsTests
{
    internal static IEnumerable<Case<int>> PlayerColorCases()
    {
        foreach (var first in Enum.GetValues<BurntimePlayerColor>())
        foreach (var second in Enum.GetValues<BurntimePlayerColor>())
            yield return Int($"player colors {first}/{second}", 0, () =>
            {
                var assigned = PlayerColorSetup.Assign(first, second);
                Equal(first, assigned[0], "first player's choice");
                if (first != second) Equal(second, assigned[1], "second player's choice");
                Equal(4, assigned.Distinct().Count(), "all four players have distinct colors");
                Equal(4, assigned.Select(PlayerColorSetup.FlagId).Distinct().Count(), "distinct world flags");
                Equal(4, assigned.Select(PlayerColorSetup.IconId).Distinct().Count(), "distinct map icons");
                var other = PlayerColorSetup.OtherSelection(first, second, second);
                Equal(first, other, "choosing the other player's color swaps it");
                return 0;
            });

        yield return Int("default player colors preserve original assets", 0, () =>
        {
            var colors = PlayerColorSetup.Assign(BurntimePlayerColor.Green, BurntimePlayerColor.Red);
            Equal("0,8,12,4", string.Join(',', colors.Select(PlayerColorSetup.FlagId)), "original animated flag offsets");
            Equal("0,1,3,2", string.Join(',', colors.Select(PlayerColorSetup.IconId)), "original icon order");
            Equal("2,0,-1,1", string.Join(',', colors.Select(PlayerColorSetup.BodyColorSet)), "original recruit palettes");
            Equal(BurntimePlayerColor.Red, PlayerColorSetup.OtherSelection(BurntimePlayerColor.Green,
                BurntimePlayerColor.Black, BurntimePlayerColor.Red), "unoccupied choice preserves other player");
            return 0;
        });
    }

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
        yield return Int("new game preferences default to level one extended remaster", 0, () =>
        {
            ConfigFile config = new();
            NewGamePreferences preferences = NewGamePreferences.Load(
                config.GetSection("", true));
            Equal(1, preferences.Difficulty, "default difficulty");
            Equal(RuleSet.Extended, preferences.Rules, "default rules");
            Equal(AiProfile.Modern, preferences.Ai, "default AI");
            return 0;
        });
        yield return Int("new game preferences load supported values", 0, () =>
        {
            ConfigFile config = new();
            ConfigSection settings = config.GetSection("", true);
            settings.Set("mode_difficulty", 3);
            settings.Set("mode_rules", "classic");
            settings.Set("mode_opponent", "amiga");
            NewGamePreferences preferences = NewGamePreferences.Load(settings);
            Equal(3, preferences.Difficulty, "stored difficulty");
            Equal(RuleSet.Classic, preferences.Rules, "stored rules");
            Equal(AiProfile.Amiga, preferences.Ai, "stored AI");
            return 0;
        });
        yield return Int("new game preferences store canonical user settings", 0, () =>
        {
            ConfigFile config = new();
            ConfigSection settings = config.GetSection("", true);
            NewGamePreferences.Default.Store(settings);
            Equal("1", settings.GetString("mode_difficulty"), "difficulty text");
            Equal("extended", settings.GetString("mode_rules"), "rules text");
            Equal("remaster", settings.GetString("mode_opponent"), "AI text");
            return 0;
        });
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
