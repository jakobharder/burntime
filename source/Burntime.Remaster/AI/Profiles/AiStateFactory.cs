using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

internal static class AiStateFactory
{
    internal static AiState? Create(
        StateManager container,
        AiProfile profile,
        Player player,
        AiSettings settings)
    {
        settings.Profile = profile;
        return profile switch
        {
            AiProfile.None => null,
            AiProfile.Dos => container.Create<DosAiState>(player, settings),
            AiProfile.Amiga => container.Create<AmigaAiState>(player, settings),
            _ => container.Create<ClassicAiState>(player, settings)
        };
    }
}
