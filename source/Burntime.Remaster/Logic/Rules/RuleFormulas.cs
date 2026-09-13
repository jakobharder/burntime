using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

/// <summary>
/// Deterministic arithmetic shared by the rule books and their table-driven tests.
/// Stateful behavior and random selection remain in the rule implementations.
/// </summary>
internal static class RuleFormulas
{
    internal static int CombatExperience(bool fightClass, int experience) =>
        fightClass ? experience : experience / 2;

    internal static int CampBossExperience(int baseExperience, int ownedCamps) =>
        Math.Min(99, baseExperience + 3 * ownedCamps);

    internal static int AmigaBossExperience(
        int foodOutput,
        int waterOutput,
        int employees) =>
        Math.Min(99, 38 + (foodOutput + waterOutput + 3 * employees) / 4);

    internal static int BossExperience(
        BossExperienceRule rule,
        Player player,
        ClassicGame game)
    {
        if (rule != BossExperienceRule.AmigaEconomy)
            return CampBossExperience(
                player.BaseExperience, player.GetOwnedLocationCount(game.World));

        Location[] camps = game.World.Locations
            .Where(location => location.Player == player)
            .ToArray();
        int foodOutput = camps.Sum(location => location.GetFoodProductionRate().FoodPerDay);
        int waterOutput = camps.Sum(location => location.Source.Water);
        int employees = player.Group.Count + camps.Sum(location =>
            location.CampNPC.Count(character => character.Player == player && !character.IsDead));
        return AmigaBossExperience(foodOutput, waterOutput, employees);
    }

    internal static bool DosCanRecruit(int bossExperience, int recruitExperience) =>
        3 * bossExperience / 2 >= recruitExperience;

    internal static bool AmigaCanRecruit(int bossExperience, int recruitExperience) =>
        bossExperience >= recruitExperience - 4;

    internal static bool CanRecruit(RecruitmentRule rule, int bossExperience, int recruitExperience) =>
        rule == RecruitmentRule.AmigaXpPlus4
            ? AmigaCanRecruit(bossExperience, recruitExperience)
            : DosCanRecruit(bossExperience, recruitExperience);

    internal static int DoctorResult(int health, int healingPoints, float factor, int cap) =>
        Math.Max(health, Math.Min(cap, health + (int)(healingPoints * factor)));

    internal static int NaturalHealingThreshold(
        bool doctorAvailable,
        int? thresholdOverride = null) =>
        doctorAvailable ? 50 : thresholdOverride ?? 70;

    internal static int OriginalServiceValue(IEnumerable<float> tradeValues) =>
        (int)tradeValues.Sum();

    internal static int OriginalPaymentValue(IEnumerable<float> tradeValues) =>
        (int)Math.Round(tradeValues.Sum() * 4, MidpointRounding.AwayFromZero);

    internal static bool AcceptTrade(IItemCollection offered, IItemCollection wanted, int difficultyFactor) =>
        AcceptTrade(offered.Select(item => item.TradeValue), wanted.Select(item => item.TradeValue), difficultyFactor);

    internal static bool AcceptTrade(IEnumerable<float> offered, IEnumerable<float> wanted, int difficultyFactor)
        => AcceptTrade(offered, wanted, difficultyFactor, 1f);

    internal static bool AcceptTrade(
        IEnumerable<float> offered,
        IEnumerable<float> wanted,
        int difficultyFactor,
        float profileFactor)
    {
        float[] values = offered.ToArray();
        return values.Length > 0 &&
            EffectiveTradeValue(values, difficultyFactor, profileFactor) >=
            OriginalPaymentValue(wanted) / 4f;
    }

    internal static float EffectiveTradeFactor(int difficultyFactor, float profileFactor = 1f) =>
        difficultyFactor / 100f * profileFactor;

    internal static float EffectiveTradeValue(
        IEnumerable<float> tradeValues,
        int difficultyFactor,
        float profileFactor = 1f) =>
        OriginalPaymentValue(tradeValues) / 4f *
        EffectiveTradeFactor(difficultyFactor, profileFactor);

    internal static bool OriginalAcceptTrade(
        int offered,
        int wanted,
        int difficultyFactor) =>
        offered * difficultyFactor / 100 >= wanted;

    internal static bool AmigaTraderRemovalCandidate(int titleId, int round) =>
        titleId < 0x37 || (titleId & 7) == (round & 7) ||
        (titleId & 7) == ((round + 5) & 7);

    internal static int DosWaterOutput(int baseOutput, bool handPump, bool industrialPump)
    {
        if (industrialPump)
            return baseOutput + Math.Max(2, baseOutput / 2);
        if (handPump)
            return baseOutput + Math.Max(1, baseOutput / 4);
        return baseOutput;
    }

    internal static int OriginalWaterOutput(
        int baseOutput,
        bool handPump,
        bool industrialPump)
    {
        if (industrialPump)
            return Math.Max(6, baseOutput + baseOutput / 2);
        if (handPump)
            return Math.Max(3, baseOutput + baseOutput / 4);
        return baseOutput;
    }

    internal static int ExtendedWaterOutput(
        int baseOutput,
        bool handPump,
        bool industrialPump)
    {
        int boost = industrialPump ? 5 : handPump ? 2 : 0;
        return baseOutput + boost;
    }

    internal static int WaterOutput(
        WaterOutputRule rule,
        int baseOutput,
        bool handPump,
        bool industrialPump)
    {
        if (rule == WaterOutputRule.RemasterFixed)
            return ExtendedWaterOutput(baseOutput, handPump, industrialPump);
        return rule == WaterOutputRule.DosProportional
            ? DosWaterOutput(baseOutput, handPump, industrialPump)
            : OriginalWaterOutput(baseOutput, handPump, industrialPump);
    }

    internal static int ExperienceTier(int combatExperience, int tierWidth)
    {
        if (tierWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(tierWidth));
        return Math.Clamp(combatExperience / tierWidth, 0, 3);
    }

    static ReadOnlySpan<int> OriginalDamageTable(int[] damage, int combatExperience, int tierWidth)
    {
        if (damage.Length is 1 or 4)
            return damage;
        if (damage.Length != 16 || tierWidth <= 0)
            throw new ArgumentException("Original combat requires one value, four rolls, or four tiers of four rolls.");
        int tier = ExperienceTier(combatExperience, tierWidth);
        return damage.AsSpan(tier * 4, 4);
    }

    internal static CombatPreview OriginalCombatPreview(int[] damageValues, int combatExperience, int tierWidth)
    {
        ReadOnlySpan<int> damage = OriginalDamageTable(damageValues, combatExperience, tierWidth);
        int minimum = damage[0], maximum = damage[0];
        foreach (int value in damage)
        {
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        return new(minimum, maximum, null);
    }

    internal static int OriginalDamage(
        int[] damageValues,
        int combatExperience,
        int tierWidth,
        int rollIndex) =>
        OriginalDamageTable(damageValues, combatExperience, tierWidth)[damageValues.Length == 1 ? 0 : rollIndex];

    internal static float OriginalStrategicStrength(
        int[] damageValues,
        int combatExperience,
        int tierWidth,
        int health,
        bool detailed)
    {
        ReadOnlySpan<int> damage = OriginalDamageTable(damageValues, combatExperience, tierWidth);
        int sum = 0;
        foreach (int value in damage)
            sum += value;
        return sum / (float)damage.Length + (detailed ? health / 10f : 10f);
    }

    internal static int ApplyArmour(int damage, int protectionPercent) =>
        damage <= 0 ? 0 : Math.Max(1, (int)Math.Round(
            damage * (100 - Math.Clamp(protectionPercent, 0, 100)) / 100f,
            MidpointRounding.AwayFromZero));
}
