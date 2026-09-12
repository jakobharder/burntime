using System.Collections.Generic;
using System.Linq;
using Burntime.Platform;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal static class StartLocationPlacement
{
    public static void Apply(ClassicGame game, GameSettings settings)
    {
        if (settings.StartLocationRules.Equals(
            "dos_rotating_groups", System.StringComparison.OrdinalIgnoreCase))
            ApplyRotatingOriginalGroups(game, settings);
        else
            ApplyRegional(game, settings);
    }

    public static void ApplyRegional(ClassicGame game, GameSettings settings)
    {
        List<int> availableGroups = Enumerable.Range(1, settings.StartRegionCount).ToList();
        foreach (Player player in game.World.Players)
        {
            int groupListIndex = Burntime.Platform.Math.Random.Next(availableGroups.Count);
            int group = availableGroups[groupListIndex];
            availableGroups.RemoveAt(groupListIndex);
            SetRandomLocation(player, game, settings.GetStartLocation(group));
        }
    }

    public static void ApplyRotatingOriginalGroups(ClassicGame game, GameSettings settings)
    {
        int group = Burntime.Platform.Math.Random.Next(settings.OriginalStartGroupCount);
        foreach (Player player in game.World.Players)
        {
            SetRandomLocation(player, game, settings.GetOriginalStartLocations(group + 1));
            group = (group + 1) % settings.OriginalStartGroupCount;
        }
    }

    static void SetRandomLocation(Player player, ClassicGame game, int[] oneBasedLocations)
    {
        int locationIndex = oneBasedLocations[
            Burntime.Platform.Math.Random.Next(oneBasedLocations.Length)] - 1;
        Location location = game.World.Locations[locationIndex];
        player.Location = location;
        player.Character.Position = new Vector2(location.EntryPoint);
        player.Character.Path.MoveTo = new Vector2(location.EntryPoint);
    }
}
