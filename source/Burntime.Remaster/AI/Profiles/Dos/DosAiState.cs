using System;
using System.Linq;
using Burntime.Framework;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.AI;

/// <summary>
/// DOS AI identity and serialization boundary. Its recovered decision loop is
/// intentionally isolated here so it can evolve without changing Modern AI.
/// </summary>
[Serializable]
internal sealed class DosAiState : OriginalAiState
{
    protected override string ProfileLabel => "dos";
    int ConflictBudget => new[] { 2, 4, 6 }[Difficulty];

    protected override bool CanRecruit(Logic.Character candidate) => true;

    protected override void RecoverGroup(bool arrived)
    {
        if (Player.Location.Player != null && Player.Location.Player != Player)
            return;

        Player.Character.Water = 10;
        Player.Character.Food = Player.Location.IsCity ? 9 : 18;
        AiTelemetry.Report(Player, AiTelemetryEvent.DosMaintenanceCompleted);

        OriginalItemRecords.CleanupDos(Player.Location, Difficulty,
            item => RootGame.ItemTypes.GetOriginalTitleId(item.Type));
    }

    protected override void PrepareRecruit(Logic.Character recruit)
    {
        recruit.Food = 9;
        recruit.Water = 5;
    }

    public override void Turn()
    {
        if (Player.IsDead || Player.Character.IsDead || Player.IsTraveling)
            return;

        bool arrived = headedLocation != null && Current == headedLocation.Object;
        if (arrived)
            headedLocation = null;

        if (ResolveCurrentOpposition())
        {
            AiTelemetry.Report(Player, AiTelemetryEvent.DosMaintenanceBlockedByConflict);
            return;
        }

        // German 0x6704 resets the counter even when supplies are skipped.
        wait = ConflictBudget;

        // Winning a conflict enters the executable's unowned-location branch,
        // including its maintenance and class-priority recruitment calls.
        // German 0x661c/0x663c/0x6673: one maintenance call on the
        // eligible non-hostile branch, never two calls around opposition.
        if (CanMaintainCurrentLocation())
            RecoverGroup(arrived: false);
        else if (Player.Location.Player != null && Player.Location.Player != Player)
            AiTelemetry.Report(Player, AiTelemetryEvent.DosMaintenanceBlockedByConflict);
        if (RecruitOneAtCurrentLocation())
            return;

        Logic.Location? next = ChooseNextStep();
        if (next != null)
            StartTravel(next);
    }

    bool CanMaintainCurrentLocation() => Player.Location.IsCity ||
        Player.Location.Player == Player ||
        (Player.Location.Player == null && Player.Location.Source.BaseWater != 0);

    protected override bool RecruitOneAtCurrentLocation()
    {
        // DOS enters the three class-specific recruitment calls only through
        // the unowned-location branch. Recruits are assigned to that camp,
        // rather than joining the travelling party.
        if (Player.Location.IsCity || Player.Location.Player != null)
            return false;

        Logic.Character? recruit = new[]
            {
                CharClass.Mercenary,
                CharClass.Technician,
                CharClass.Doctor
            }
            .Select(characterClass => Player.Location.Characters.FirstOrDefault(character =>
                !character.IsDead && !character.IsHired && character.Class == characterClass))
            .FirstOrDefault(character => character != null);
        if (recruit == null)
            return false;

        recruit.Hire(Player, waivePayment: true);
        PrepareRecruit(recruit);
        recruit.JoinCamp();
        AiTelemetry.Report(Player, $"recruited and stationed {recruit.Name} for free");
        return true;
    }

    Logic.Location? ChooseNextStep()
    {
        Logic.Location current = Player.Location;
        // The DOS player record's FromCityId is used as a remembered route
        // after conflict and after an unproductive neutral visit. Dangerous
        // neutral locations explicitly take the random-route branch instead.
        if (current.Player != Player && !current.IsCity && current.Danger == null &&
            Player.PreviousLocation is Logic.Location previous &&
            Player.CanTravel(current, previous))
            return previous;

        // Existing Remaster route normalization: clamp to the last neighbor.
        // Original DOS instead idles on an empty trailing slot; that separate
        // fidelity correction remains pending.
        if (current.Neighbors.Count == 0)
            return null;
        int slot = System.Math.Min(
            Burntime.Platform.Math.Random.Next(4),
            current.Neighbors.Count - 1);
        Logic.Location candidate = current.Neighbors[slot];
        return Player.CanTravel(current, candidate) ? candidate : null;
    }

    protected override int StrategicDamage(Logic.Character attacker, Logic.Character defender)
    {
        int baseStrength = defender.SelectOriginalWeapon()?.ID switch
        {
            "item_knife" => 20,
            "item_axe" => 30,
            "item_pitchfork" => 40,
            "item_loaded_rifle" => 60,
            _ => 10
        };
        int pressure = RootGame.World.Day switch
        {
            < 60 => 20,
            < 100 => 30,
            < 140 => 40,
            < 160 => 50,
            < 200 => 60,
            < 240 => 70,
            _ => 80
        };
        int damage = Player.Character.Experience + pressure -
            (baseStrength + defender.Experience);
        return damage == 0 ? 1 : damage;
    }

    protected override bool ResolveCurrentOpposition()
    {
        Logic.Location current = Player.Location;
        if (current.Player == null || current.Player == Player)
            return false;

        Logic.Player owner = current.Player;
        // DOS 0x49c2–0x49d4 skips the strategic attack at either owner travel
        // endpoint. Reset the conflict budget and continue into normal routing.
        if (owner.Location == current || owner.Destination == current)
        {
            wait = ConflictBudget;
            AiTelemetry.Report(Player, "avoids strategic attack: camp owner present or approaching");
            return false;
        }

        Logic.Character[] defenders = AiStateOperations.GetCampDefenders(
            current, Player, new[] { owner }).ToArray();
        if (defenders.Length == 0)
        {
            current.Player = null;
            return false;
        }

        wait--;
        if (wait < 0)
        {
            wait = ConflictBudget;
            AiTelemetry.Report(Player, $"pauses conflict at {current.Title}");
            return false;
        }

        bool inflictedDamage = false;
        foreach (Logic.Character defender in defenders.Where(character => !character.IsDead))
        {
            int damage = StrategicDamage(Player.Character, defender);
            if (damage < 0)
                continue;
            defender.Health -= damage;
            inflictedDamage = true;
        }

        if (defenders.All(character => character.IsDead))
        {
            current.Player = null;
            wait = 0;
            AiTelemetry.Report(Player, $"defeated opposition at {current.Title}");
            return false;
        }

        if (!inflictedDamage)
        {
            wait = ConflictBudget;
            return false;
        }

        mode = Mode.WaitInterval;
        AiTelemetry.Report(Player, $"remains contested at {current.Title}");
        return true;
    }

}
