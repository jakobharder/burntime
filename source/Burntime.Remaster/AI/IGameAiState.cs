using Burntime.Data.BurnGfx.Save;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

/// <summary>
/// Non-serialized lifecycle and profile contract shared by game AI states.
/// </summary>
internal interface IGameAiState
{
    AiProfile Profile { get; }
    int Difficulty { get; }
    int? NaturalHealingThreshold { get; }

    void Turn();
    void InitAfterLoad();
    void InitializeNewGamePlayer(SaveGame source);
}
