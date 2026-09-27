namespace Burntime.Remaster.Logic.Rules;

internal static class LocationCycle
{
    internal static bool IsDue(int day, int locationId, int interval) =>
        interval > 0 && Turn(day, locationId) % interval == 0;

    internal static int Turn(int day, int locationId) => day + locationId;
}
