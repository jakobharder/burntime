using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal sealed class AiContext
{
    public required Player Player { get; init; }
    public required Location Current { get; init; }
    public required IReadOnlyList<Character> Group { get; init; }
    public required bool CriticalSupplies { get; init; }
    public required bool SafeLocation { get; init; }
    public required int TravelGroupSize { get; init; }
    public required bool NeutralExpansionAllowed { get; init; }

    public static AiContext Create(ClassicAiState state, AiPolicy policy)
    {
        Player player = state.Player;
        bool critical = player.Group.Any(character =>
            character.Health < 40 || character.Food <= 3 || character.Water <= 2);
        bool safe = state.Current.IsCity || state.Current.Player == player;
        bool neutralAllowed = !state.HasHumanPlayers ||
            state.OwnedCampCount < state.HumanCampBenchmark + state.Configuration.MaxAdvance;

        return new AiContext
        {
            Player = player,
            Current = state.Current,
            Group = player.Group.ToArray(),
            CriticalSupplies = critical,
            SafeLocation = safe,
            TravelGroupSize = policy.TravelGroupSize,
            NeutralExpansionAllowed = neutralAllowed
        };
    }
}
