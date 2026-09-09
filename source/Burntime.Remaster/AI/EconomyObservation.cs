using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

// Headless-only observations. No game state is changed or serialized into saves.
internal sealed class EconomyObservation
{
    readonly ClassicGame game;
    readonly Dictionary<Location, (Player? Owner, int Since, int LastDamage)> tenure = new();
    readonly List<object> turns = new();
    Dictionary<Character, (int Health, Location Location, bool Owned)> actionHealth = new();
    Dictionary<Character, (int Health, Player Owner, bool Boss, bool Hazard)> dailyHealth = new();
    readonly Dictionary<Character, int> lastCombatDamage = new();
    readonly Dictionary<Player, int> withdrawals = new();
    Dictionary<Location, (Player Owner, int Food)> actionFood = new();

    internal EconomyObservation(ClassicGame game)
    {
        this.game = game;
        foreach (Location camp in game.World.Locations)
            tenure[camp] = (camp.Player, 0, -1000);
    }

    IEnumerable<Character> Characters => game.World.AllCharacters
        .Concat(game.World.Players.SelectMany(player => player.Group))
        .Concat(game.World.Players.Select(player => player.Character)).Distinct();

    internal void BeforeAction()
    {
        actionHealth = Characters.Where(character => !character.IsDead && character.Location != null)
            .ToDictionary(character => character, character => (character.Health, character.Location, character.Player != null));
        actionFood = game.World.Locations.Where(camp => camp.Player != null && !camp.IsCity)
            .ToDictionary(camp => camp, camp => (camp.Player, CampEconomy.StoredFoodValue(camp)));
    }

    internal void AfterAction(int turn)
    {
        // AI-action damage is separate from daily starvation. Even an attack
        // that changes no ownership gives the affected camp a recovery grace.
        // Hunting unowned animals does not exempt a healthy camp.
        foreach (var entry in actionHealth.Where(entry => IsCombatDamage(entry.Value.Health, entry.Key.Health,
            entry.Value.Owned, entry.Key.Player != null)))
        {
            lastCombatDamage[entry.Key] = turn;
            Location camp = entry.Value.Location;
            if (tenure.TryGetValue(camp, out var old))
                tenure[camp] = (old.Owner, old.Since, turn);
        }
        ObserveOwnership(turn);
    }

    internal void AfterFoodCollection()
    {
        foreach (var entry in actionFood.Where(entry => entry.Key.Player == entry.Value.Owner))
            withdrawals[entry.Value.Owner] = withdrawals.GetValueOrDefault(entry.Value.Owner) +
                Math.Max(0, entry.Value.Food - CampEconomy.StoredFoodValue(entry.Key));
    }

    internal void BeforeDaily() => dailyHealth = Characters
        .Where(character => !character.IsDead && character.Player != null)
        .ToDictionary(character => character, character => (character.Health, character.Player,
            character == character.Player.Character,
            character.Location?.Danger != null && !game.RuleBook.PassesDailyHazardCheck(character)));

    void ObserveOwnership(int turn)
    {
        foreach (Location camp in game.World.Locations)
        {
            var old = tenure[camp];
            if (old.Owner != camp.Player)
                tenure[camp] = (camp.Player, turn, old.LastDamage);
        }
    }

    internal void RecordTurn(int turn)
    {
        ObserveOwnership(turn);
        var players = game.World.Players.Select(player =>
        {
            var supplyDamage = dailyHealth.Where(entry => entry.Value.Owner == player &&
                !entry.Value.Hazard && turn - lastCombatDamage.GetValueOrDefault(entry.Key, -1000) >= 20 &&
                IsSupplyDamage(entry.Value.Health, entry.Key.Health, entry.Key.Food, entry.Key.Water)).ToArray();
            var camps = game.World.Locations.Where(camp => camp.Player == player && !camp.IsCity)
                .Select(camp =>
                {
                    var age = tenure[camp];
                    Character[] guards = camp.CampNPC.Where(character => character.Player == player && !character.IsDead).ToArray();
                    return new
                    {
                        Id = camp.Id, HeldTurns = turn - age.Since, QuietTurns = turn - age.LastDamage,
                        Suitable = CampEconomy.HasFoodProductionPotential(camp) && (camp.Source?.Water ?? 0) > 0,
                        BasePotential = CampEconomy.IsWellEstablishedPotential(camp),
                        FoodPerDay = camp.GetFoodProductionRate().FoodPerDay,
                        WaterPerDay = camp.Source?.Water ?? 0, Guards = guards.Length,
                        FoodStock = CampEconomy.StoredFoodValue(camp),
                        WaterStock = CampEconomy.StoredWaterValue(camp),
                        Product = camp.Production?.Produce.ID,
                        ProductionTools = camp.Production == null ? 0 : CampEconomy.ProductionToolCount(camp, camp.Production),
                        GuardDistress = guards.Count(character => character.Health < 50 && (character.Food == 0 || character.Water == 0))
                    };
                }).ToArray();
            return new
            {
                Player = player.Index, Alive = !player.IsDead, Camps = camps,
                GroupSize = player.Group.Count(character => !character.IsDead),
                CampFoodWithdrawn = withdrawals.GetValueOrDefault(player),
                SupplyDamage = supplyDamage.Length,
                SupplyDeaths = supplyDamage.Count(entry => entry.Key.IsDead),
                BossSupplyDeath = supplyDamage.Any(entry => entry.Value.Boss && entry.Key.IsDead)
            };
        }).ToArray();
        turns.Add(new { Turn = turn, Players = players });
        withdrawals.Clear();
    }

    internal static bool IsCombatDamage(int beforeHealth, int afterHealth, bool ownedBefore, bool ownedAfter) =>
        beforeHealth > afterHealth && (ownedBefore || ownedAfter);

    // Daily recovery offsets the 25-point starvation/dehydration penalty by
    // 2 or 4 health. Include the final, smaller health drop of a dying NPC.
    internal static bool IsSupplyDamage(int beforeHealth, int afterHealth, int food, int water) =>
        beforeHealth > afterHealth && beforeHealth - afterHealth >= Math.Min(20, beforeHealth) &&
        (food == 0 || water == 0);

    internal void Write(string path, HeadlessSimulationOptions options)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, options.Seed, Difficulty = options.Difficulty,
            Rules = game.Rules.ToString().ToLowerInvariant(), RequestedTurns = options.Turns,
            Profiles = game.World.Players.Select(player => game.GetAiProfile(player).ToString().ToLowerInvariant()),
            Turns = turns
        }));
    }
}
