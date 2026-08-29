using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal sealed class DecisionContext
{
    public required Player Player { get; init; }
    public required Location Current { get; init; }
    public required IReadOnlyList<Character> Group { get; init; }
    public required bool CriticalSupplies { get; init; }
    public required bool SafeLocation { get; init; }
    public required int DesiredGroupSize { get; init; }
    public required bool NeutralExpansionAllowed { get; init; }

    public static DecisionContext Create(ClassicAiState state, AiPolicy policy)
    {
        Player player = state.Player;
        bool critical = player.Group.Any(character =>
            character.Health < 40 || character.Food <= 3 || character.Water <= 2) ||
            RecoveryServices.NeedsCityRecoveryStaging(state);
        bool safe = state.Current.IsCity || state.Current.Player == player;
        bool neutralAllowed = true;
        if (state.HasHumanPlayers)
        {
            neutralAllowed = state.OwnedCampCount <
                state.HumanCampBenchmark + state.Configuration.MaxAdvance;
        }

        return new DecisionContext
        {
            Player = player,
            Current = state.Current,
            Group = player.Group.ToArray(),
            CriticalSupplies = critical,
            SafeLocation = safe,
            DesiredGroupSize = policy.GroupSize,
            NeutralExpansionAllowed = neutralAllowed
        };
    }
}
