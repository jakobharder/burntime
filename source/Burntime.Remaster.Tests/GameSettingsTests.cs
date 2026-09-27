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

static class GameSettingsTests
{
    internal static IEnumerable<Case<int>> ItemGenerationCases()
    {
        yield return Generation("default rate", "", 1, 1, new[] { "food" }, new[] { "rare" });
        yield return Generation("single count", "4", 4, 4, new[] { "food" }, new[] { "rare" });
        yield return Generation("range", "1 3", 1, 3, new[] { "food" }, new[] { "rare" });
        yield return Generation("larger range", "4 9", 4, 9, new[] { "food" }, new[] { "rare" });
        yield return Generation("inverted range", "9 4", 9, 9, new[] { "food" }, new[] { "rare" });
        yield return Generation("negative values", "-2 -1", 0, 0, new[] { "food" }, new[] { "rare" });
        yield return Generation("invalid minimum", "x 3", 1, 3, new[] { "food" }, new[] { "rare" });
        yield return Generation("invalid maximum", "3 x", 3, 3, new[] { "food" }, new[] { "rare" });

        yield return Int("repairable finds become progressively risk-gated", 0, () =>
        {
            var settings = new GameSettings("classic:rules/extended/game.txt");
            string[] pools =
            {
                "random_items_room",
                "random_items_closed_room",
                "random_items_danger_location"
            };
            bool[][] expected =
            {
                new[] { true, true, true },
                new[] { false, true, true },
                new[] { false, false, true }
            };

            for (int difficulty = 0; difficulty < expected.Length; difficulty++)
            {
                settings.SetDifficulty(difficulty);
                for (int pool = 0; pool < pools.Length; pool++)
                {
                    GameSettings.ItemGeneration generation =
                        settings.GetItemGeneration(pools[pool]);
                    bool allowsRepairable = generation.Include.Contains("repairable") &&
                        !generation.Exclude.Contains("repairable") &&
                        !generation.Exclude.Contains("rare");
                    Equal(expected[difficulty][pool], allowsRepairable,
                        $"difficulty {difficulty}, {pools[pool]}");
                }
            }
            return 0;
        });
    }

    static Case<int> Generation(
        string name,
        string rate,
        int minimum,
        int maximum,
        string[] include,
        string[] exclude) => Int(name, 0, () =>
        {
            GameSettings.ItemGeneration generation =
                GameSettings.ItemGeneration.FromStrings(
                    new[] { "food", "-rare" },
                    rate.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            Equal(minimum, generation.Minimum, "minimum");
            Equal(maximum, generation.Maximum, "maximum");
            Equal(string.Join(',', include), string.Join(',', generation.Include), "include");
            Equal(string.Join(',', exclude), string.Join(',', generation.Exclude), "exclude");
            return 0;
        });
}
