using System;
using Burntime.Platform.IO;

namespace Burntime.Remaster.AI;

internal sealed class AiPolicy
{
    const int DifficultyCount = 3;
    static ConfigFile? config;
    static readonly AiPolicy?[] policies = new AiPolicy[DifficultyCount];

    public int CampMaxAdvance { get; init; }
    public int GroupSize { get; init; }
    public int AttackGroupSize { get; init; }
    public float MinimumAttackRatio { get; init; }
    public float NeutralTargetScore { get; init; }
    public float HostileTargetScore { get; init; }
    public float ExpansionEconomyScore { get; init; }
    public int MaxHumanCampDefendersToAttack { get; init; }
    public bool TreatLoneKnifeGuardAsUndefended { get; init; }
    public int CriticalGarrisonTarget { get; init; }
    public int AttackCooldownTurns { get; init; }
    public int RetaliationTurns { get; init; }
    public int ContestedCampMemoryTurns { get; init; }
    public int FailedAttackMemoryTurns { get; init; }
    public int ProgressingAttackRetryTurns { get; init; }
    public int AttackPlanTurns { get; init; }
    public int AttackPlanRetryDelay { get; init; }
    public float StrategicHostileTargetBonus { get; init; }
    public int ProactiveConflictDay { get; init; }
    public float ProactiveConflictBonus { get; init; }
    public bool UseDetailedCombatEstimate { get; init; }
    public bool AllowGeneratedRecruitPaymentInCities { get; init; }
    public int MinimumRecruitExperience { get; init; }
    public int MaximumRecruitExperience { get; init; }
    public int ThreatRadius { get; init; }
    public int PitchforkLimit { get; init; }
    public float TradeBenefit { get; init; }
    public int SlumpMaterialGrantLimit { get; init; }
    public bool DieWhenTrapped { get; init; }

    static ConfigFile Config
    {
        get
        {
            if (config != null)
                return config;
            config = new ConfigFile();
            if (!config.Open("ai.txt"))
                throw new InvalidOperationException("Could not load AI configuration 'ai.txt'.");
            return config;
        }
    }

    internal static AiSettings SettingsForPlayer(int playerIndex, int gameDifficulty)
    {
        string[] configured = Config["players"].GetStrings("difficulties");
        if (playerIndex < 0 || playerIndex >= configured.Length)
            return SettingsFor(gameDifficulty);
        string value = configured[playerIndex];
        return SettingsFor(ParseDifficulty(value, gameDifficulty));
    }

    internal static AiSettings SettingsFor(int difficulty)
    {
        AiPolicy policy = ForDifficulty(difficulty);
        return new AiSettings
        {
            MaxAdvance = policy.CampMaxAdvance,
            Difficulty = difficulty
        };
    }

    internal static AiPolicy ForDifficulty(int difficulty)
    {
        ValidateDifficulty(difficulty);
        return policies[difficulty] ??= LoadPolicy(difficulty);
    }

    static int ParseDifficulty(string value, int gameDifficulty = -1) =>
        value.Trim().ToLowerInvariant() switch
        {
            "game" or "inherit" when gameDifficulty >= 0 => gameDifficulty,
            "easy" or "0" => 0,
            "normal" or "1" => 1,
            "hard" or "2" => 2,
            _ => throw new InvalidOperationException(
                $"Invalid AI difficulty '{value}' in ai.txt; use game, easy, normal, or hard.")
        };

    static AiPolicy LoadPolicy(int difficulty)
    {
        ConfigSection section = Config[difficulty.ToString()];
        int[] recruitExperience = section.GetInts("recruit_experience");
        if (recruitExperience.Length != 2)
            throw new InvalidOperationException(
                $"AI difficulty {difficulty} must define a two-value recruit_experience in ai.txt.");
        int proactiveConflictDay = section.GetInt("proactive_conflict_day");
        int maxHumanDefenders = section.GetInt("max_human_camp_defenders");
        int slumpMaterialGrants = section.GetInt("slump_material_grants");
        if (slumpMaterialGrants < 0)
            throw new InvalidOperationException(
                $"AI difficulty {difficulty} must define a non-negative slump_material_grants value in ai.txt.");
        return new AiPolicy
        {
            CampMaxAdvance = section.GetInt("camp_max_advance"),
            GroupSize = section.GetInt("travel_group_size"),
            AttackGroupSize = section.GetInt("attack_group_size"),
            MinimumAttackRatio = section.GetFloat("minimum_attack_ratio"),
            NeutralTargetScore = section.GetFloat("neutral_target_score"),
            HostileTargetScore = section.GetFloat("hostile_target_score"),
            ExpansionEconomyScore = section.GetFloat("expansion_economy_score"),
            MaxHumanCampDefendersToAttack = maxHumanDefenders < 0
                ? int.MaxValue
                : maxHumanDefenders,
            TreatLoneKnifeGuardAsUndefended = section.GetBool(
                "treat_lone_knife_guard_as_undefended"),
            CriticalGarrisonTarget = section.GetInt("critical_garrison_target"),
            AttackCooldownTurns = section.GetInt("attack_cooldown_turns"),
            RetaliationTurns = section.GetInt("retaliation_turns"),
            ContestedCampMemoryTurns = section.GetInt("contested_camp_memory_turns"),
            FailedAttackMemoryTurns = section.GetInt("failed_attack_memory_turns"),
            ProgressingAttackRetryTurns = section.GetInt("progressing_attack_retry_turns"),
            AttackPlanTurns = section.GetInt("attack_plan_turns"),
            AttackPlanRetryDelay = section.GetInt("attack_plan_retry_delay"),
            StrategicHostileTargetBonus = section.GetFloat("strategic_hostile_target_bonus"),
            ProactiveConflictDay = proactiveConflictDay < 0
                ? int.MaxValue
                : proactiveConflictDay,
            ProactiveConflictBonus = section.GetFloat("proactive_conflict_bonus"),
            UseDetailedCombatEstimate = section.GetBool("use_detailed_combat_estimate"),
            AllowGeneratedRecruitPaymentInCities = section.GetBool(
                "allow_generated_recruit_payment_in_cities"),
            MinimumRecruitExperience = recruitExperience[0],
            MaximumRecruitExperience = recruitExperience[1] < 0
                ? int.MaxValue
                : recruitExperience[1],
            ThreatRadius = section.GetInt("threat_radius"),
            PitchforkLimit = section.GetInt("pitchfork_limit"),
            TradeBenefit = section.GetFloat("trade_benefit"),
            SlumpMaterialGrantLimit = slumpMaterialGrants,
            DieWhenTrapped = section.GetBool("die_when_trapped")
        };
    }

    static void ValidateDifficulty(int difficulty)
    {
        if (difficulty is < 0 or >= DifficultyCount)
            throw new ArgumentOutOfRangeException(nameof(difficulty),
                "AI difficulty must be easy, normal, or hard.");
    }
}
