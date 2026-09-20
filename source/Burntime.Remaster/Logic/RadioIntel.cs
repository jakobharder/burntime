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

    internal static bool IsAvailable(Player player, Location location) =>
        HasRadio(player) && !location.IsCity && location != player.Location &&
        location.Player != player;

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
