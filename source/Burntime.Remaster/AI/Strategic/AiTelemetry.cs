using System;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AiTelemetry
{
    [ThreadStatic]
    public static Action<Player, string>? Sink;

    public static void Report(Player player, string message) => Sink?.Invoke(player, message);
}
