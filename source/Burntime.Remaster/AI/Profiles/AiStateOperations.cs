using System.Linq;
using System.Collections.Generic;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

/// <summary>
/// Stateless dispatch and world queries shared by sibling AI implementations.
/// Keeping this outside their serialized inheritance preserves save contracts.
/// </summary>
internal static class AiStateOperations
{
    internal readonly record struct ProgressBenchmark(bool RestrictsAi, int StrongestCampCount);

    internal static bool Turn(AiState? state)
    {
        switch (state)
        {
            case OriginalAiState original:
                original.Turn();
                return true;
            case ClassicAiState modern:
                modern.Turn();
                return true;
            default:
                return false;
        }
    }

    internal static void InitAfterLoad(AiState? state)
    {
        if (state is OriginalAiState original)
            original.InitAfterLoad();
        else if (state is ClassicAiState modern)
            modern.InitAfterLoad();
    }

    internal static void InitializeNewGamePlayer(
        AiState? state,
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        if (state is OriginalAiState original)
            original.InitializeNewGamePlayer(source);
        else if (state is ClassicAiState modern)
            modern.InitializeNewGamePlayer(source);
    }

    internal static bool TryGetDifficulty(AiState? state, out int difficulty)
    {
        if (state is OriginalAiState original)
        {
            difficulty = original.Difficulty;
            return true;
        }
        if (state is ClassicAiState modern)
        {
            difficulty = modern.Difficulty;
            return true;
        }
        difficulty = 0;
        return false;
    }

    internal static AiProfile GetProfile(Player player) => player.AiState switch
    {
        DosAiState => AiProfile.Dos,
        AmigaAiState => AiProfile.Amiga,
        ClassicAiState => AiProfile.Modern,
        _ when player.Type == PlayerType.Ai && player.IsDead => AiProfile.None,
        _ => AiProfile.Modern
    };

    internal static int? GetNaturalHealingThreshold(AiState? state) =>
        state is AmigaAiState ? 50 : null;

    internal static IEnumerable<Character> GetCampDefenders(
        Location location, Player? attacker, IEnumerable<Player> opponents)
    {
        var enemies = opponents.Where(opponent => opponent != attacker).Distinct().ToArray();
        return location.CampNPC.Where(character =>
                enemies.Contains(character.Player) && !character.IsDead)
            .Concat(enemies.Where(opponent => !opponent.IsDead && !opponent.IsTraveling &&
                    opponent.Location == location)
                .SelectMany(opponent => opponent.Group.Where(character => !character.IsDead)))
            .Distinct();
    }

    internal static int OwnedCampCount(ClassicGame game, Player player) =>
        game.World.Locations.Count(location => location.Player == player);

    /// <summary>
    /// Returns the benchmark used by AI profile limits. Living human players
    /// take precedence. Once none remain, the strongest living AI opponent is
    /// used instead. A sole surviving player has no relative restriction.
    /// </summary>
    internal static ProgressBenchmark GetProgressBenchmark(
        ClassicGame game,
        Player player)
    {
        Player[] candidates = game.World.Players.Where(candidate =>
            candidate.Type == PlayerType.Human && !candidate.IsDead).ToArray();
        if (candidates.Length == 0)
        {
            candidates = game.World.Players.Where(candidate =>
                candidate != player &&
                candidate.Type == PlayerType.Ai &&
                !candidate.IsDead).ToArray();
        }
        if (candidates.Length == 0)
            return new ProgressBenchmark(false, 0);
        int strongest = candidates.Max(candidate => game.World.Locations.Count(location =>
            location.Player == candidate));
        return new ProgressBenchmark(true, strongest);
    }

    internal static int ProgressRelativeLimitOrUnrestricted(
        ProgressBenchmark benchmark,
        int additionalAllowance) => benchmark.RestrictsAi
            ? benchmark.StrongestCampCount + additionalAllowance
            : int.MaxValue;
}
