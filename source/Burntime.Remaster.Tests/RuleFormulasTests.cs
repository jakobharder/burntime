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

static class RuleFormulasTests
{
    internal static IEnumerable<Case<int>> CombatExperienceCases()
    {
        yield return Int("fight class uses full XP", 99,
            () => RuleFormulas.CombatExperience(true, 99));
        yield return Int("other class uses half XP", 49,
            () => RuleFormulas.CombatExperience(false, 99));
        yield return Int("odd XP rounds down", 18,
            () => RuleFormulas.CombatExperience(false, 37));
    }

    internal static IEnumerable<Case<int>> ExperienceTierCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        {
            var rules = new GameRules(rule);
            int width = 25;
            foreach (int xp in new[] { -1, 0, 24, 25, 26, 49, 50, 51, 52, 74, 75, 77, 78, 99, 100, 500 })
            {
                int expected = xp < width ? 0 : xp < width * 2 ? 1 : xp < width * 3 ? 2 : 3;
                yield return Int($"{rule} XP {xp}", expected, () => rules.GetExperienceTier(xp));
            }
            foreach (CharClass characterClass in new[] { CharClass.Boss, CharClass.Doctor, CharClass.Technician, CharClass.Mercenary })
            {
                var character = new Burntime.Remaster.Logic.Character { Class = characterClass, Experience = 50 };
                int dots = 3;
                yield return Int($"{rule} {characterClass} map dots at 50 XP", dots,
                    () => rules.GetExperienceTier(character.Experience) + 1);
            }
        }
    }

    internal static IEnumerable<Case<int>> BossExperienceCases()
    {
        yield return Int("DOS initial", 37, () => RuleFormulas.CampBossExperience(37, 0));
        yield return Int("DOS camps", 97, () => RuleFormulas.CampBossExperience(37, 20));
        yield return Int("DOS cap", 99, () => RuleFormulas.CampBossExperience(37, 40));
        yield return Int("Amiga initial", 38,
            () => RuleFormulas.AmigaBossExperience(0, 0, 0));
        yield return Int("Amiga economy", 45,
            () => RuleFormulas.AmigaBossExperience(8, 8, 4));
        yield return Int("Amiga integer truncation", 39,
            () => RuleFormulas.AmigaBossExperience(1, 1, 1));
        yield return Int("Amiga cap", 99,
            () => RuleFormulas.AmigaBossExperience(100, 100, 20));
        yield return Int("Custom base and camps", 67,
            () => RuleFormulas.CampBossExperience(37, 10));
        yield return Int("Camp experience cap", 99,
            () => RuleFormulas.CampBossExperience(90, 10));
    }

    static bool ExtendedCanRecruit(int bossExperience, int recruitExperience) =>
        new GameRules(RuleSet.Extended).MeetsRecruitmentExperience(
            new Burntime.Remaster.Logic.Character { Experience = bossExperience },
            new Burntime.Remaster.Logic.Character { Experience = recruitExperience });

    internal static IEnumerable<Case<bool>> RecruitmentCases()
    {
        yield return Bool("DOS boundary allowed", true,
            () => RuleFormulas.DosCanRecruit(40, 60));
        yield return Bool("DOS boundary denied", false,
            () => RuleFormulas.DosCanRecruit(40, 61));
        yield return Bool("DOS integer truncation allowed", true,
            () => RuleFormulas.DosCanRecruit(39, 58));
        yield return Bool("DOS integer truncation denied", false,
            () => RuleFormulas.DosCanRecruit(39, 59));
        yield return Bool("Amiga four-point allowance", true,
            () => RuleFormulas.AmigaCanRecruit(40, 44));
        yield return Bool("Amiga beyond allowance", false,
            () => RuleFormulas.AmigaCanRecruit(40, 45));
        yield return Bool("Extended no longer rounds two-thirds down to 66 percent", false,
            () => ExtendedCanRecruit(33, 50));
        yield return Bool("Extended exact two-thirds ceiling", true,
            () => ExtendedCanRecruit(34, 50));
        yield return Bool("Extended boundary allowed", true,
            () => ExtendedCanRecruit(40, 60));
        yield return Bool("Extended boundary denied", false,
            () => ExtendedCanRecruit(39, 60));
        yield return Bool("Extended high boundary allowed", true,
            () => ExtendedCanRecruit(66, 99));
        yield return Bool("Extended high boundary denied", false,
            () => ExtendedCanRecruit(65, 99));
    }

    internal static IEnumerable<Case<int>> DoctorCases()
    {
        yield return Int("DOS four points per food", 94,
            () => RuleFormulas.DoctorResult(90, 1, 4, 95));
        yield return Int("DOS caps at 95", 95,
            () => RuleFormulas.DoctorResult(91, 1, 4, 95));
        yield return Int("DOS empty payment preserves high health", 99,
            () => RuleFormulas.DoctorResult(99, 0, 4, 95));
        yield return Int("Amiga three points per food", 93,
            () => RuleFormulas.DoctorResult(90, 1, 3, 95));
        yield return Int("Amiga caps at 95", 95,
            () => RuleFormulas.DoctorResult(93, 1, 3, 95));
        yield return Int("Amiga empty payment preserves high health", 99,
            () => RuleFormulas.DoctorResult(99, 0, 3, 95));
        yield return Int("Extended healing", 92,
            () => RuleFormulas.DoctorResult(80, 12, 1, 95));
        yield return Int("Extended doctor cap", 95,
            () => RuleFormulas.DoctorResult(95, 12, 1, 95));
        yield return Int("natural healing starts at 70", 70,
            () => RuleFormulas.NaturalHealingThreshold(doctorAvailable: false));
        yield return Int("caller can override natural healing threshold", 50,
            () => RuleFormulas.NaturalHealingThreshold(doctorAvailable: false, thresholdOverride: 50));
        yield return Int("doctor lowers natural healing threshold to 50", 50,
            () => RuleFormulas.NaturalHealingThreshold(doctorAvailable: true));
        foreach (float factor in new[] { 1f, 0.75f })
        foreach (int health in new[] { 94, 95, 96, 100 })
        {
            yield return Int($"doctor empty payment at {health}, factor {factor}", health,
                () => RuleFormulas.DoctorResult(health, 0, factor, 95));
            yield return Int($"doctor paid treatment at {health}, factor {factor}", Math.Max(health, 95),
                () => RuleFormulas.DoctorResult(health, 12, factor, 95));
        }
    }

    internal static IEnumerable<Case<int>> ServiceValueCases()
    {
        yield return Int("meat", 6,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f }));
        yield return Int("bible", 11,
            () => RuleFormulas.OriginalServiceValue(new[] { 11.25f }));
        yield return Int("two meats truncate after summing", 13,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f, 6.75f }));
        yield return Int("mixed payment", 18,
            () => RuleFormulas.OriginalServiceValue(new[] { 6.75f, 11.25f }));
        yield return Int("empty payment", 0,
            () => RuleFormulas.OriginalServiceValue(Array.Empty<float>()));
    }

    internal static IEnumerable<Case<bool>> BarterCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            yield return Bool($"{rule} preserves fractional barter value", rule != RuleSet.Dos, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var offer = manager.Create<ItemList>();
                var demand = manager.Create<ItemList>();
                offer.Add(TestItem(manager, "offer", trade: 6.75f));
                demand.Add(TestItem(manager, "demand", trade: 6f));
                return new GameRules(rule).AcceptTrade(offer, demand, 1);
            });
        yield return Bool("empty offers still rejected", false, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            return new GameRules(RuleSet.Extended).AcceptTrade(
                manager.Create<ItemList>(), manager.Create<ItemList>(), 0);
        });
        yield return Bool("reserve quote preserves quarter values", true,
            () => RuleFormulas.AcceptTrade(new[] { 6.75f }, new[] { 6f }, 95));
        yield return Bool("reserve quote checks entire basket", false,
            () => RuleFormulas.AcceptTrade(new[] { 6.75f }, new[] { 6f, 0.5f }, 95));
        yield return Bool("reserve quote rejects empty offers", false,
            () => RuleFormulas.AcceptTrade(Array.Empty<float>(), Array.Empty<float>(), 100));
        yield return Bool("profile trade factor composes with ruleset", true,
            () => RuleFormulas.AcceptTrade(new[] { 88f }, new[] { 100f }, 95, 1.2f));
        yield return Bool("profile trade factor still observes ruleset", false,
            () => RuleFormulas.AcceptTrade(new[] { 87f }, new[] { 100f }, 95, 1.2f));
        yield return Bool("default profile factor is neutral", false,
            () => RuleFormulas.AcceptTrade(new[] { 100f }, new[] { 100f }, 95));
        yield return Bool("easy equality", true,
            () => RuleFormulas.OriginalAcceptTrade(100, 100, 100));
        yield return Bool("easy short offer", false,
            () => RuleFormulas.OriginalAcceptTrade(99, 100, 100));
        yield return Bool("DOS normal boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(125, 100, 80));
        yield return Bool("DOS normal below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(124, 100, 80));
        yield return Bool("DOS hard boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(167, 100, 60));
        yield return Bool("DOS hard below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(166, 100, 60));
        yield return Bool("Amiga normal boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(106, 100, 95));
        yield return Bool("Amiga normal below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(105, 100, 95));
        yield return Bool("Amiga hard boundary", true,
            () => RuleFormulas.OriginalAcceptTrade(112, 100, 90));
        yield return Bool("Amiga hard below boundary", false,
            () => RuleFormulas.OriginalAcceptTrade(111, 100, 90));
        yield return Bool("empty-value trade equality", true,
            () => RuleFormulas.OriginalAcceptTrade(0, 0, 100));

        yield return Bool("payment scaling preserves meat value", true,
            () => RuleFormulas.OriginalPaymentValue(new[] { 6.75f }) == 27);
        yield return Bool("payment scaling preserves bible value", true,
            () => RuleFormulas.OriginalPaymentValue(new[] { 11.25f }) == 45);
    }

    internal static IEnumerable<Case<int>> WaterOutputCases()
    {
        foreach (var (source, hand, industrial) in new[]
        {
            (0, 1, 2), (1, 2, 3), (2, 3, 4), (3, 4, 5),
            (4, 5, 6), (5, 6, 7), (6, 7, 9), (7, 8, 10),
            (8, 10, 12), (9, 11, 13), (10, 12, 15), (12, 15, 18)
        })
        {
            yield return Int($"corrected DOS base {source}, no pump", source,
                () => new GameRules(RuleSet.Dos).CalculateWaterOutput(source, false, false));
            yield return Int($"corrected DOS base {source}, hand", hand,
                () => new GameRules(RuleSet.Dos).CalculateWaterOutput(source, true, false));
            yield return Int($"corrected DOS base {source}, industrial", industrial,
                () => new GameRules(RuleSet.Dos).CalculateWaterOutput(source, false, true));
            yield return Int($"corrected DOS base {source}, both", industrial,
                () => new GameRules(RuleSet.Dos).CalculateWaterOutput(source, true, true));
        }
        (int Base, bool Hand, bool Industrial, int Original, int Extended)[] cases =
        {
            (1, false, false, 1, 1),
            (1, true, false, 3, 3),
            (1, false, true, 6, 6),
            (4, true, false, 5, 6),
            (4, false, true, 6, 9),
            (10, true, false, 12, 12),
            (10, false, true, 15, 15),
            (10, true, true, 15, 15),
            (12, false, false, 12, 12),
            (12, true, false, 15, 14),
            (12, false, true, 18, 17),
            (12, true, true, 18, 17)
        };
        foreach (var entry in cases)
        {
            string pumps = entry.Industrial ? "industrial" : entry.Hand ? "hand" : "none";
            yield return Int($"original base {entry.Base}, {pumps}", entry.Original,
                () => RuleFormulas.OriginalWaterOutput(entry.Base, entry.Hand, entry.Industrial));
            yield return Int($"extended base {entry.Base}, {pumps}", entry.Extended,
                () => RuleFormulas.ExtendedWaterOutput(entry.Base, entry.Hand, entry.Industrial));
        }
    }
}
