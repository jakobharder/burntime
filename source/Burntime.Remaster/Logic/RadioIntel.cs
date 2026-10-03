using System;
using System.Linq;
using Burntime.Remaster.AI;

namespace Burntime.Remaster.Logic;

internal readonly record struct RadioReport(
    int Bosses,
    int Mercenaries,
    int Technicians,
    int Doctors,
    int Threat,
    int Food,
    int Water)
{
    public int Defenders => Bosses + Mercenaries + Technicians + Doctors;
}

internal static class RadioIntel
{
    internal static bool HasRadio(Player player) =>
        player.Party.Any(character => !character.IsDead &&
            character.HasItemFunction(ItemFunction.RemoteIntel));

    internal static bool HasStoredRadio(Location location) =>
        location.Rooms.Any(room => room.Items.Any(item =>
            item.Type.HasFunction(ItemFunction.RemoteIntel)));

    internal static bool HasAccess(Player player) =>
        HasRadio(player) || (player.Location != null && HasStoredRadio(player.Location));

    internal static bool CanSeeHealth(Player player, Location location) =>
        location == player.Location ||
        (location.Player == player && HasAccess(player) && HasStoredRadio(location));

    internal static bool IsAvailable(Player player, Location location)
    {
        if (!HasAccess(player) || location.IsCity || location == player.Location ||
            location.Player == player)
            return false;

        if (player.Location != null && player.Location.Neighbors.Contains(location))
            return true;

        ClassicGame? game = player.Container.Root as ClassicGame;
        return game?.World?.Locations.Any(station => station.Player == player &&
            HasStoredRadio(station) && station.Neighbors.Contains(location)) == true;
    }

    internal static RadioReport Create(ClassicGame game, Player viewer, Location location)
    {
        Character[] defenders = AiStateOperations.GetCampDefenders(
                location, viewer, game.World.Players)
            .Where(character => character.Class is CharClass.Boss or CharClass.Mercenary or
                CharClass.Technician or CharClass.Doctor)
            .ToArray();
        float strength = defenders.Sum(character =>
            game.RuleBook.CalculateStrategicStrength(character, detailed: true));

        return new(
            defenders.Count(character => character.Class == CharClass.Boss),
            defenders.Count(character => character.Class == CharClass.Mercenary),
            defenders.Count(character => character.Class == CharClass.Technician),
            defenders.Count(character => character.Class == CharClass.Doctor),
            ThreatLevel(strength),
            location.GetFoodProductionRate().FoodPerDay,
            location.Source.Water);
    }

    internal static int ThreatLevel(float strength) =>
        strength <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(strength / 25), 1, 4);
}
