using System;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

// An isolated fixture inside a real loaded world. Runs the ordinary AI and
// daily simulation; does not preselect an attack plan or force an action.
internal sealed class WeakFrontierScenario
{
    readonly Player attacker;
    readonly Location target;
    readonly Character defender;
    internal bool HasProgress { get; private set; }
    int healthBeforeAction;

    internal void BeforeAction() => healthBeforeAction = defender.Health;
    internal void AfterAction(Player player)
    {
        // Only damage during this attacker's action counts, never daily hunger
        // or thirst damage to the stationary opponent.
        if (player == attacker && (target.Player == attacker || defender.Health < healthBeforeAction))
            HasProgress = true;
    }

    internal WeakFrontierScenario(ClassicGame game, int difficulty)
    {
        attacker = game.World.Players[0];
        Player enemy = game.World.Players[1];
        foreach (Player player in game.World.Players)
        {
            player.AiState = null;
            if (player != attacker)
                player.Location = game.World.Locations.First(location => location.IsCity);
        }

        var camps = game.World.Locations.Where(location => !location.IsCity &&
            location.Danger == null && location.Rooms.Count > 0).Take(2).ToArray();
        Location home = camps[0];
        target = camps[1];
        foreach (Location location in new[] { home, target })
        {
            foreach (Location neighbor in location.Neighbors.ToArray())
                location.Neighbors.Remove(neighbor);
            location.WayLengths = Array.Empty<int>();
        }
        home.Neighbors.Add(target); home.WayLengths = new[] { 1 };
        target.Neighbors.Add(home); target.WayLengths = new[] { 1 };
        home.Player = attacker;
        target.Player = enemy;
        attacker.Location = home;

        // Remove unrelated residents from the fixture, then reuse real mercenary
        // instances so death, movement and inventory processing stay realistic.
        Character[] recruits = game.World.AllCharacters.Where(character =>
            character.Class == CharClass.Mercenary && !character.IsDead).Take(6).ToArray();
        foreach (Character resident in home.Characters.Concat(target.Characters).Distinct().ToArray())
            resident.Location = game.World.Locations.First(location => location.IsCity);

        Production production = game.ItemTypes["item_trap"].Production;
        foreach (Location camp in new[] { home, target })
        {
            camp.AvailableProducts = new[] { production.ID };
            camp.Source.Water = 10;
            camp.Source.Reserve = 20;
            foreach (Room room in camp.Rooms) room.Items.Clear();
            camp.Rooms[0].Items.Add(game.ItemTypes["item_trap"].Generate());
            camp.Rooms[0].Items.Add(game.ItemTypes["item_trap"].Generate());
            camp.SelectProduction(production);
        }
        foreach (Character member in attacker.Party.ToArray()) attacker.Party.Remove(member);
        attacker.Party.Add(attacker.Character);
        Prepare(attacker.Character, attacker, null, strong: true);
        for (int i = 0; i < 2; i++)
        {
            Prepare(recruits[i], attacker, null, strong: true);
            attacker.Party.Add(recruits[i]);
        }
        for (int i = 2; i < 5; i++)
            Prepare(recruits[i], attacker, home, strong: true);
        defender = recruits[5];
        Prepare(defender, enemy, target, strong: false);
        attacker.AiState = attacker.Container.Create<ClassicAiState>(new object[]
            { attacker, new AiSettings { Difficulty = difficulty } });

        void Prepare(Character character, Player owner, Location? stationed, bool strong)
        {
            character.Player = owner;
            character.Location = stationed!;
            character.Health = 100;
            character.Experience = strong ? 75 : 0;
            character.Food = character.MaxFood;
            character.Water = character.MaxWater;
            character.Items.Clear();
            character.Items.Add(game.ItemTypes[strong ? "item_axe" : "item_knife"].Generate());
            character.Items.Add(game.ItemTypes["item_full_canteen"].Generate());
            character.Items.Add(game.ItemTypes["item_meat"].Generate());
        }
    }
}
