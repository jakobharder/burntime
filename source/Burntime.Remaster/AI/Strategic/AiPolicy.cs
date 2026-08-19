namespace Burntime.Remaster.AI;

internal sealed class AiPolicy
{
    public int DesiredGroupSize { get; init; }
    public float MinimumAttackRatio { get; init; }
    public float NeutralTargetScore { get; init; }
    public float HostileTargetScore { get; init; }
    public float ExpansionEconomyScore { get; init; }
    public int MaxHumanCampDefendersToAttack { get; init; }
    public bool TreatLoneKnifeGuardAsUndefended { get; init; }
    public int CriticalGarrisonTarget { get; init; }
    public int AttackCooldownTurns { get; init; }
    public int RetaliationTurns { get; init; } = 20;
    public int ContestedCampMemoryTurns { get; init; } = 16;
    public int FailedAttackMemoryTurns { get; init; } = 20;
    public int AttackPlanTurns { get; init; } = 40;
    public int AttackPlanRetryDelay { get; init; } = 10;
    public float StrategicHostileTargetBonus { get; init; }
    public bool UseDetailedCombatEstimate { get; init; }
    public bool AllowGeneratedRecruitPaymentInCities { get; init; } = true;
    public int SafeFoodFloor { get; init; } = 10;
    public int SafeWaterFloor { get; init; } = 10;
    public int SafeHealing { get; init; } = 25;

    public static AiPolicy ForDifficulty(int difficulty) => difficulty switch
    {
        0 => new AiPolicy { DesiredGroupSize = 2, MinimumAttackRatio = 1.35f,
            NeutralTargetScore = 690, HostileTargetScore = 430, ExpansionEconomyScore = 850,
            MaxHumanCampDefendersToAttack = 0, TreatLoneKnifeGuardAsUndefended = true,
            CriticalGarrisonTarget = 1, AttackCooldownTurns = 4 },
        1 => new AiPolicy { DesiredGroupSize = 3, MinimumAttackRatio = 1.05f,
            NeutralTargetScore = 770, HostileTargetScore = 560, ExpansionEconomyScore = 900,
            MaxHumanCampDefendersToAttack = 1, CriticalGarrisonTarget = 2,
            AttackCooldownTurns = 2, StrategicHostileTargetBonus = 35 },
        _ => new AiPolicy { DesiredGroupSize = 4, MinimumAttackRatio = 0.75f,
            NeutralTargetScore = 860, HostileTargetScore = 680, ExpansionEconomyScore = 950,
            MaxHumanCampDefendersToAttack = int.MaxValue, CriticalGarrisonTarget = 3,
            AttackCooldownTurns = 0, StrategicHostileTargetBonus = 100,
            UseDetailedCombatEstimate = true }
    };
}
