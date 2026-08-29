using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Runtime-only owner of information shared throughout one AI player turn.
/// Economic needs, live party status, and coarse camp status remain separate
/// views so callers can be explicit about the kind of information they use.
/// </summary>
internal sealed class AiTurnContext
{
    static readonly ConditionalWeakTable<ClassicAiState, AiTurnContext> ByState = new();

    readonly ClassicAiState state;
    StrategicNeeds? needs;
    Location? bestTradeCity;
    bool bestTradeCityResolved;

    private AiTurnContext(ClassicAiState state)
    {
        this.state = state;
        Policy = AiPolicy.ForDifficulty(state.Difficulty);
        Camps = new CampAssessments(state);
    }

    internal AiPolicy Policy { get; }
    internal StrategicNeeds Needs => needs ??= new StrategicNeeds(state);
    internal CampAssessments Camps { get; }
    internal DecisionContext Decision { get; private set; } = null!;
    internal Location? BestTradeCity
    {
        get
        {
            if (!bestTradeCityResolved)
            {
                bestTradeCity = Trading.FindBestTradeCity(state);
                bestTradeCityResolved = true;
            }
            return bestTradeCity;
        }
    }

    internal static AiTurnContext Begin(ClassicAiState state)
    {
        ByState.Remove(state);
        AiTurnContext context = new(state);
        ByState.Add(state, context);
        context.RefreshNeeds();
        return context;
    }

    internal static AiTurnContext For(ClassicAiState state) =>
        ByState.TryGetValue(state, out AiTurnContext? context)
            ? context
            : Begin(state);

    internal void RefreshNeeds() => needs = new StrategicNeeds(state);

    internal DecisionContext RefreshDecisionContext() =>
        Decision = DecisionContext.Create(state, Policy);
}
