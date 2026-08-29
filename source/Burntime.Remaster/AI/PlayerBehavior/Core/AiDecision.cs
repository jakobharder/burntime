using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal enum AiAction
{
    Recruit,
    ReleaseFollower,
    DismissFollower,
    StationFollower,
    StationTradeFollower,
    RecallFollower,
    MobilizeFrontierFollower,
    CancelAttackPlan,
    ImproveCamp,
    ClaimNeutral,
    AttackHostile,
    EmergencyEscape,
    Travel,
    Wait
}

internal enum AiActionResult
{
    ContinuePlanning,
    EndTurn,
    StateUnchanged
}

internal sealed record AiDecision(
    AiAction Action,
    float Score,
    Location? Target = null,
    Location? NextStep = null,
    string Reason = "",
    Character? Recruit = null,
    bool CommitJourney = false)
{
    internal AiActionResult Execute(ClassicAiState state)
    {
        Player player = state.Player;
        AiPolicy policy = AiPolicy.ForDifficulty(state.Difficulty);
        switch (Action)
        {
            case AiAction.Recruit:
                Character? recruit = state.Recruit(
                    allowGeneratedPayment: state.Current.IsCity && policy.AllowGeneratedRecruitPaymentInCities,
                    plannedRecruit: Recruit);
                AiTelemetry.Report(player, recruit == null
                    ? "could not afford an available recruit"
                    : $"hired {recruit.Name} ({recruit.Class})");
                if (recruit == null)
                    return AiActionResult.StateUnchanged;

                // HireNpc already gives the recruit a water container and a
                // weapon when available. Normalize only the travelling party;
                // do not repeat all local trading and empire maintenance.
                GroupManagement.MaintainGroupEquipment(state);
                if (player.Group.Contains(recruit) && NextStep != null &&
                    player.CanTravel(state.Current, NextStep))
                {
                    GroupManagement.PrepareCampWaterReservesForDeparture(state);
                    player.Travel(NextStep);
                    AiTelemetry.Report(player,
                        $"departs toward {Target?.Title ?? NextStep.Title} " +
                        $"with new settler via {NextStep.Title}");
                    return AiActionResult.EndTurn;
                }
                return AiActionResult.ContinuePlanning;

            case AiAction.ReleaseFollower:
                int initialGroupSize = player.Group.Count;
                Recruitment.MarkSurvivalRelease(state);
                Location? recovery;
                do
                {
                    Character? removed;
                    if (state.Current.Player == null &&
                        CampEconomy.HasFoodProductionPotential(state.Current) &&
                        (state.Current.Source?.Water ?? 0) > 1)
                    {
                        removed = state.SelectCampNpc();
                        if (removed == null)
                            break;
                        state.CreateCamp(removed);
                        AiTelemetry.Report(player,
                            $"created an emergency camp at {state.Current.Title} using {removed.Name} instead of releasing them");
                    }
                    else if (state.Current.Player == player &&
                        ReinforcementPlanning.WantsAdditionalGuard(
                            state, state.Current, policy.CriticalGarrisonTarget))
                    {
                        removed = state.StationSurplusFollower();
                        if (removed != null)
                        {
                            AiTelemetry.Report(player,
                                $"stationed {removed.Name} at {state.Current.Title} instead of releasing them");
                        }
                        else
                        {
                            removed = state.ReleaseFollowerForSurvival();
                            if (removed == null)
                                break;
                            AiTelemetry.Report(player,
                                $"released {removed.Name} because the strategically wanted garrison " +
                                $"at {state.Current.Title} cannot sustainably support them");
                        }
                    }
                    else
                    {
                        removed = state.ReleaseFollowerForSurvival();
                        if (removed == null)
                            break;
                        AiTelemetry.Report(player,
                            state.Current.Player == player
                                ? $"released {removed.Name} because {state.Current.Title} does not need another strategic guard"
                                : $"released {removed.Name} completely because the location cannot support a camp");
                    }
                    recovery = RecoveryServices.FindDestination(state, requireReachable: true);
                }
                while (recovery == null && player.Group.Count > 1 &&
                    !RecoveryServices.CanRecoverLocallyForTravel(state));

                recovery = RecoveryServices.FindDestination(state, requireReachable: true);
                bool lastChance = recovery == null &&
                    RecoveryServices.WaitingForSuppliesWillBeFatal(state);
                if (lastChance)
                    recovery = RecoveryServices.FindLastChanceDestination(state);
                RouteFinder.Route? recoveryRoute = recovery == null
                    ? null
                    : RouteFinder.Find(player, state.Current, recovery);
                if (recoveryRoute?.NextStep != null &&
                    player.CanTravel(state.Current, recoveryRoute.NextStep))
                {
                    GroupManagement.PrepareCampWaterReservesForDeparture(state);
                    player.Travel(recoveryRoute.NextStep);
                    AiTelemetry.Report(player,
                        lastChance
                            ? $"takes the least-bad route toward {recovery!.Title} after reducing the group"
                            : $"departs toward {recovery!.Title} after reducing the group to a survivable size");
                    return AiActionResult.EndTurn;
                }
                return player.Group.Count != initialGroupSize
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.DismissFollower:
                Character? dismissed = state.DismissSurplusFollower();
                if (dismissed != null)
                    AiTelemetry.Report(player,
                        $"dismissed surplus follower {dismissed.Name}; no strategic garrison needs another guard");
                return dismissed != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.StationFollower:
                Character? stationed = state.StationSurplusFollower();
                if (stationed != null)
                    AiTelemetry.Report(player,
                        $"stationed surplus follower {stationed.Name} at {state.Current.Title}");
                return stationed != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.StationTradeFollower:
                Character? tradeFollower = state.StationTradeFollower();
                if (tradeFollower != null)
                    AiTelemetry.Report(player,
                        $"left {tradeFollower.Name} at {state.Current.Title} while preparing a city caravan");
                return tradeFollower != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.RecallFollower:
                Character? recalled = state.RecallCampFollower(policy.CriticalGarrisonTarget);
                if (recalled != null)
                    AiTelemetry.Report(player,
                        $"recalled {recalled.Name} from {state.Current.Title}");
                return recalled != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.MobilizeFrontierFollower:
                Character? mobilized = state.MobilizeCampFollower(minimumGuards: 1);
                if (mobilized != null)
                    AiTelemetry.Report(player,
                        $"mobilized {mobilized.Name} from the frontier at {state.Current.Title}");
                return mobilized != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.CancelAttackPlan:
                if (Target == null || !state.HasAttackPlan)
                    return AiActionResult.StateUnchanged;
                state.DeferAttacksForFailedCityRecruitment(Target, policy);
                AiTelemetry.Report(player,
                    $"abandoned attack plan for {Target.Title}: recruitment was unavailable at {state.Current.Title}; returning to economic and territorial planning");
                return AiActionResult.ContinuePlanning;

            case AiAction.ImproveCamp:
                if (!state.ImproveCamp())
                    return AiActionResult.StateUnchanged;
                AiTelemetry.Report(player, $"improved production at {state.Current.Title}");
                return AiActionResult.ContinuePlanning;

            case AiAction.ClaimNeutral:
                Character? npc = state.SelectCampNpc();
                if (npc != null)
                {
                    state.CreateCamp(npc);
                    GroupManagement.ProvisionGroupFromCampSurplus(state, state.Current);
                    AiTelemetry.Report(player, $"claimed {state.Current.Title} using {npc.Name}");
                }
                return npc != null
                    ? AiActionResult.ContinuePlanning
                    : AiActionResult.StateUnchanged;

            case AiAction.AttackHostile:
                CombatResolver.Resolve(state);
                return player.IsDead
                    ? AiActionResult.EndTurn
                    : AiActionResult.ContinuePlanning;

            case AiAction.EmergencyEscape:
                string[] released = player.Group
                    .Where(character => character != player.Character)
                    .Select(character => character.Name)
                    .ToArray();
                foreach (Character follower in player.Group
                    .Where(character => character != player.Character)
                    .ToArray())
                    follower.Dismiss();

                int oldFood = player.Character.Food;
                int oldWater = player.Character.Water;
                player.Character.Food = 9;
                player.Character.Water = 5;
                state.StrategicTarget = Target;
                state.ResetNonProgressWatchdog();
                if (Target != null)
                    state.CommitJourney(Target,
                        "reach the sustainable camp selected by emergency escape");
                AiTelemetry.Report(player,
                    $"emergency watchdog bailout released {released.Length} follower(s)" +
                    (released.Length == 0 ? "" : $" ({string.Join(", ", released)})") +
                    $", reset {player.Character.Name} from F{oldFood}/W{oldWater} to F9/W5, " +
                    $"and selected one-way escape toward {Target?.Title ?? "no destination"}");

                if (NextStep != null && player.CanTravel(state.Current, NextStep))
                {
                    GroupManagement.PrepareCampWaterReservesForDeparture(state);
                    player.Travel(NextStep);
                    AiTelemetry.Report(player,
                        $"emergency watchdog escape travels toward {Target?.Title ?? NextStep.Title} " +
                        $"via {NextStep.Title}");
                    return AiActionResult.EndTurn;
                }
                AiTelemetry.Report(player,
                    "emergency watchdog bailout could not begin travel because the selected route is no longer permitted");
                return AiActionResult.StateUnchanged;

            case AiAction.Travel:
                // Supply, recruiting and shopping trips are intermediate steps of an
                // attack or settlement plan. Keep a non-city strategic destination.
                if (NextStep != null &&
                    player.CanTravel(state.Current, NextStep))
                {
                    if (CommitJourney && Target != null)
                        state.CommitJourney(Target, Reason);
                    if (Target != null &&
                        (state.StrategicTarget == null || state.StrategicTarget.IsCity))
                        state.StrategicTarget = Target;
                    GroupManagement.PrepareCampWaterReservesForDeparture(state);
                    player.Travel(NextStep);
                    AiTelemetry.Report(player,
                        $"travels toward {Target?.Title ?? NextStep.Title} via {NextStep.Title}");
                    return AiActionResult.EndTurn;
                }
                else if (NextStep != null)
                {
                    AiTelemetry.Report(player,
                        $"did not travel toward {NextStep.Title}: route is no longer permitted");
                }
                return AiActionResult.StateUnchanged;

            case AiAction.Wait:
                return AiActionResult.EndTurn;
        }

        return AiActionResult.StateUnchanged;
    }
}
