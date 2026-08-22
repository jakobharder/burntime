using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AiDecisionExecutor
{
    public static void Execute(ClassicAiState state, AiDecision decision)
    {
        Player player = state.Player;
        AiPolicy policy = AiPolicy.ForDifficulty(state.RootGame.World.Difficulty);
        switch (decision.Action)
        {
            case AiAction.Recruit:
                Character? recruit = state.Recruit(
                    allowGeneratedPayment: state.Current.IsCity && policy.AllowGeneratedRecruitPaymentInCities);
                AiTelemetry.Report(player, recruit == null
                    ? "could not afford an available recruit"
                    : $"hired {recruit.Name} ({recruit.Class})");
                if (recruit != null)
                {
                    LocalOpportunities.Apply(state);
                    if (player.Group.Contains(recruit) && decision.NextStep != null &&
                        player.CanTravel(state.Current, decision.NextStep))
                    {
                        player.Travel(decision.NextStep);
                        AiTelemetry.Report(player,
                            $"departs toward {decision.Target?.Title ?? decision.NextStep.Title} " +
                            $"with new settler via {decision.NextStep.Title}");
                    }
                }
                break;

            case AiAction.ReleaseFollower:
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
                    else if (state.Current.Player == player)
                    {
                        removed = state.StationSurplusFollower();
                        if (removed == null)
                            break;
                        AiTelemetry.Report(player,
                            $"stationed {removed.Name} at {state.Current.Title} instead of releasing them");
                    }
                    else
                    {
                        removed = state.ReleaseFollowerForSurvival();
                        if (removed == null)
                            break;
                        AiTelemetry.Report(player,
                            $"released {removed.Name} completely because the location cannot support a camp");
                    }
                    recovery = RecoveryServices.FindDestination(state, requireReachable: true);
                }
                while (recovery == null && player.Group.Count > 1);

                recovery = RecoveryServices.FindDestination(state, requireReachable: true);
                RouteFinder.Route? recoveryRoute = recovery == null
                    ? null
                    : RouteFinder.Find(player, state.Current, recovery);
                if (recoveryRoute?.NextStep != null &&
                    player.CanTravel(state.Current, recoveryRoute.NextStep))
                {
                    player.Travel(recoveryRoute.NextStep);
                    AiTelemetry.Report(player,
                        $"departs toward {recovery!.Title} after reducing the group to a survivable size");
                }
                break;

            case AiAction.StationFollower:
                Character? stationed = state.StationSurplusFollower();
                if (stationed != null)
                    AiTelemetry.Report(player,
                        $"stationed surplus follower {stationed.Name} at {state.Current.Title}");
                break;

            case AiAction.StationTradeFollower:
                Character? tradeFollower = state.StationTradeFollower();
                if (tradeFollower != null)
                    AiTelemetry.Report(player,
                        $"left {tradeFollower.Name} at {state.Current.Title} while preparing a city caravan");
                break;

            case AiAction.RecallFollower:
                Character? recalled = state.RecallCampFollower(policy.CriticalGarrisonTarget);
                if (recalled != null)
                    AiTelemetry.Report(player,
                        $"recalled {recalled.Name} from {state.Current.Title}");
                break;

            case AiAction.MobilizeFrontierFollower:
                Character? mobilized = state.MobilizeCampFollower(minimumGuards: 1);
                if (mobilized != null)
                    AiTelemetry.Report(player,
                        $"mobilized {mobilized.Name} from the frontier at {state.Current.Title}");
                break;

            case AiAction.ImproveCamp:
                if (state.ImproveCamp())
                    AiTelemetry.Report(player, $"improved production at {state.Current.Title}");
                break;

            case AiAction.ClaimNeutral:
                Character? npc = state.SelectCampNpc();
                if (npc != null)
                {
                    state.CreateCamp(npc);
                    LocalOpportunities.ProvisionGroupFromCampSurplus(state, state.Current);
                    AiTelemetry.Report(player, $"claimed {state.Current.Title} using {npc.Name}");
                }
                break;

            case AiAction.AttackHostile:
                StrategicCombatResolver.Resolve(state);
                break;

            case AiAction.Travel:
                // Supply, recruiting and shopping trips are intermediate steps of an
                // attack or settlement plan. Keep a non-city strategic destination.
                if (decision.NextStep != null &&
                    player.CanTravel(state.Current, decision.NextStep))
                {
                    if (decision.Target != null &&
                        (state.StrategicTarget == null || state.StrategicTarget.IsCity))
                        state.StrategicTarget = decision.Target;
                    player.Travel(decision.NextStep);
                    AiTelemetry.Report(player,
                        $"travels toward {decision.Target?.Title ?? decision.NextStep.Title} via {decision.NextStep.Title}");
                }
                else if (decision.NextStep != null)
                {
                    AiTelemetry.Report(player,
                        $"did not travel toward {decision.NextStep.Title}: route is no longer permitted");
                }
                break;

            case AiAction.Wait:
                break;
        }
    }
}
