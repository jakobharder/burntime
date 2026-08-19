using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal enum AiAction
{
    Recover,
    Recruit,
    StationFollower,
    StationTradeFollower,
    RecallFollower,
    MobilizeFrontierFollower,
    ImproveCamp,
    ClaimNeutral,
    AttackHostile,
    Travel,
    Wait
}

internal sealed record AiDecision(
    AiAction Action,
    float Score,
    Location? Target = null,
    Location? NextStep = null,
    string Reason = "");
