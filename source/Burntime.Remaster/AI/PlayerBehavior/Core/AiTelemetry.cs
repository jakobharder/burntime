using System;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class AiTelemetry
{
    [ThreadStatic]
    public static Action<Player, string>? Sink;

    [ThreadStatic]
    public static Action<Player, AiTelemetryEvent>? EventSink;

    [ThreadStatic]
    public static Action<Player, Location>? AttackPlanStarted;

    public static void Report(Player player, string message) => Sink?.Invoke(player, message);

    public static void Report(Player player, AiTelemetryEvent telemetryEvent) =>
        EventSink?.Invoke(player, telemetryEvent);
}

internal enum AiTelemetryEvent
{
    DosMaintenanceBlockedByConflict,
    DosMaintenanceCompleted
}
