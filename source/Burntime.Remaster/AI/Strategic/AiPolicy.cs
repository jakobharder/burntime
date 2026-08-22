namespace Burntime.Remaster.AI;

internal sealed class AiPolicy
{
    public int TravelGroupSize { get; init; }
    public int AttackGroupSize { get; init; }
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
    public int ProactiveConflictDay { get; init; } = int.MaxValue;
    public float ProactiveConflictBonus { get; init; }
    public bool UseDetailedCombatEstimate { get; init; }
    public bool AllowGeneratedRecruitPaymentInCities { get; init; } = true;

    public static AiPolicy ForDifficulty(int difficulty) => difficulty switch
    {
        0 => new AiPolicy { TravelGroupSize = 2, AttackGroupSize = 2, MinimumAttackRatio = 1.35f,
            NeutralTargetScore = 690, HostileTargetScore = 430, ExpansionEconomyScore = 850,
            MaxHumanCampDefendersToAttack = 0, TreatLoneKnifeGuardAsUndefended = true,
            CriticalGarrisonTarget = 1, AttackCooldownTurns = 4 },
        1 => new AiPolicy { TravelGroupSize = 2, AttackGroupSize = 3, MinimumAttackRatio = 1.05f,
            NeutralTargetScore = 770, HostileTargetScore = 560, ExpansionEconomyScore = 900,
            MaxHumanCampDefendersToAttack = 1, CriticalGarrisonTarget = 2,
            AttackCooldownTurns = 2, StrategicHostileTargetBonus = 35 },
        _ => new AiPolicy { TravelGroupSize = 2, AttackGroupSize = 4, MinimumAttackRatio = 0.75f,
            NeutralTargetScore = 860, HostileTargetScore = 680, ExpansionEconomyScore = 950,
            MaxHumanCampDefendersToAttack = int.MaxValue, CriticalGarrisonTarget = 3,
            AttackCooldownTurns = 0, StrategicHostileTargetBonus = 100,
            ProactiveConflictDay = 35, ProactiveConflictBonus = 1200,
            UseDetailedCombatEstimate = true }
    };
}
