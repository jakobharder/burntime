using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Shared state and utilities of the original computer-player controllers.
/// DOS and Amiga each implement their own turn loop.
/// </summary>
[Serializable]
internal abstract class OriginalAiState : Burntime.Framework.States.AiState,
    Logic.Generation.IAiProfileState
{
    protected enum Mode
    {
        None,
        LookForNextCamp,
        HireNpc,
        WaitInterval
    }

    protected StateLink<Player> player;
    protected AiSettings settings;
    protected Mode mode;
    protected StateLink<Location> headedLocation;
    protected int wait;

    protected virtual int MaximumGroupSize => 5;
    protected virtual bool HasRecruitmentCapacity => Player.Group.Count < MaximumGroupSize;
    protected abstract bool CanRecruit(Character candidate);
    protected abstract void RecoverGroup(bool arrived);
    protected abstract void PrepareRecruit(Character recruit);
    protected virtual void HireRecruit(Character recruit)
    {
        recruit.Hire(Player, waivePayment: true);
        PrepareRecruit(recruit);
    }
    protected abstract int StrategicDamage(Character attacker, Character defender);

    protected override void InitInstance(object[] parameter)
    {
        base.InitInstance(parameter);
        if (parameter.Length != 2)
            throw new BurntimeLogicException();
        player = parameter[0] as Player;
        if (player == null)
            throw new BurntimeLogicException();
        settings = (AiSettings)parameter[1];
        mode = Mode.LookForNextCamp;
        wait = 0;
    }

    public Player Player => player;
    Logic.Generation.AiProfileId Logic.Generation.IAiProfileState.Profile => settings.Profile;
    internal int Difficulty => settings.Difficulty;
    internal ClassicGame RootGame => (ClassicGame)container.Root;
    internal Location Current => Player.Location;
    internal int OwnedCampCount => AiStateOperations.OwnedCampCount(RootGame, Player);

    internal virtual void InitAfterLoad()
    {
        // All persistent original-AI state is held in its player, settings,
        // mode, destination and wait fields. It has no Extended planner caches.
    }

    internal void InitializeNewGamePlayer(
        Burntime.Data.BurnGfx.Save.SaveGame source)
    {
        // The originals have two AI boss templates. Reuse them alternately so
        // an original profile receives an original AI start in any player slot.
        int templateSlot = 2 + Player.Index % 2;
        var character = source.Characters[templateSlot];
        Player.Character.Health = character.Health;
        int experience = RootGame.RuleBook.Settings.StartExperience;
        Player.Character.Experience = experience;
        Player.Character.Food = character.Food;
        Player.Character.Water = character.Water;
        Player.BaseExperience = experience;

        Player.Character.Items.Clear();
        foreach (var (info, index) in source.Items.Select((info, index) => (info, index)).Where(entry =>
            entry.info.OwnerType == Burntime.Data.BurnGfx.Save.ItemOwnerType.Character &&
            entry.info.OwnerId == templateSlot))
        {
            Item item = RootGame.ItemTypes[info.SpriteId].Generate();
            item.OriginalRecordSlot = index + 1;
            Player.Character.Items.Add(item);
        }
    }

    public abstract void Turn();

    protected string ProfileLabel => settings.Profile.ToString().ToLowerInvariant();

    protected abstract bool ResolveCurrentOpposition();

    protected IEnumerable<Character> GetDefenders(Location location) =>
        AiStateOperations.GetCampDefenders(location, Player, RootGame.World.Players);

    protected virtual bool RecruitOneAtCurrentLocation()
    {
        if (!HasRecruitmentCapacity)
            return false;
        Character? recruit = Player.Location.Characters.FirstOrDefault(character =>
            !character.IsDead && !character.IsHired && character.IsHuman &&
            !character.IsTrader && CanRecruit(character));
        if (recruit == null)
            return false;

        HireRecruit(recruit);
        AiTelemetry.Report(Player, $"recruited {recruit.Name} for free");
        return true;
    }

    protected void StartTravel(Location destination)
    {
        headedLocation = destination;
        mode = Mode.LookForNextCamp;
        Player.Travel(destination);
        AiTelemetry.Report(Player,
            $"travels toward {destination.Title} using original {ProfileLabel} AI");
    }
}
