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
            case AiAction.Recover:
                bool usedCheat = state.RecoverAtSafeLocation(policy);
                AiTelemetry.Report(player, usedCheat
                    ? "received limited emergency supplies at a safe location"
                    : "recovered using carried or stored supplies");
                break;

            case AiAction.Recruit:
                Character? recruit = state.Recruit(
                    allowGeneratedPayment: state.Current.IsCity && policy.AllowGeneratedRecruitPaymentInCities);
                AiTelemetry.Report(player, recruit == null
                    ? "could not afford an available recruit"
                    : $"hired {recruit.Name} ({recruit.Class})");
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

            case AiAction.ImproveCamp:
                if (state.ImproveCamp())
                    AiTelemetry.Report(player, $"improved production at {state.Current.Title}");
                break;

            case AiAction.ClaimNeutral:
                Character? npc = state.SelectCampNpc();
                if (npc != null)
                {
                    state.CreateCamp(npc);
                    AiTelemetry.Report(player, $"claimed {state.Current.Title} using {npc.Name}");
                }
                break;

            case AiAction.AttackHostile:
                StrategicCombatResolver.Resolve(state);
                break;

            case AiAction.Travel:
                // Supply, recruiting and shopping trips are intermediate steps of an
                // attack plan. Do not let their immediate destination replace its target.
                if (decision.Target != null && !state.HasAttackPlan)
                    state.StrategicTarget = decision.Target;
                if (decision.NextStep != null)
                {
                    player.Travel(decision.NextStep);
                    AiTelemetry.Report(player,
                        $"travels toward {decision.Target?.Title ?? decision.NextStep.Title} via {decision.NextStep.Title}");
                }
                break;

            case AiAction.Wait:
                break;
        }
    }
}
