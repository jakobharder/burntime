using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

internal static class AiStateFactory
{
    internal static AiState? Create(
        StateManager container,
        AiProfileId profile,
        Player player,
        AiSettings settings)
    {
        settings.Profile = profile;
        return profile switch
        {
            AiProfileId.None => null,
            AiProfileId.Dos => container.Create<DosAiState>(player, settings),
            AiProfileId.Amiga => container.Create<AmigaAiState>(player, settings),
            _ => container.Create<ClassicAiState>(player, settings)
        };
    }
}
