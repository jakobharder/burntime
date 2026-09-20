using System;
using System.Linq;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic;

internal static class MetalDetectorScavenging
{
    internal const double ChancePerTravelDay = 0.01;

    internal static double DiscoveryChance(int travelDays) =>
        travelDays <= 0 ? 0 : 1 - System.Math.Pow(1 - ChancePerTravelDay, travelDays);

    internal static bool IsDiscovery(int travelDays, double roll) =>
        roll >= 0 && roll < DiscoveryChance(travelDays);

    internal static bool TryDiscoverAmmunition(Player player, int travelDays)
    {
        if (player.Type != PlayerType.Human || player.Location is null ||
            player.Location.IsCity || travelDays <= 0 ||
            player.Container.Root is not ClassicGame game ||
            game.Rules != RuleSet.Extended ||
            !player.Party.Any(character => !character.IsDead &&
                character.HasItemFunction(ItemFunction.MetalDetection)) ||
            !IsDiscovery(travelDays, Burntime.Platform.Math.Random.NextSingle()))
            return false;

        Item ammunition = game.ItemTypes["item_ammunition"].Generate();
        player.Location.Items.DropAt(ammunition, player.Location.EntryPoint);
        return true;
    }
}
