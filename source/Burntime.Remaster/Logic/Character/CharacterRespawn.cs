using Burntime.Framework.States;
using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public sealed class CharacterRespawn : StateObject
{
    [Serializable]
    private sealed class RespawnObject : StateObject
    {
        readonly StateLink<Character> character;
        readonly StateLink<Location> location;
        int remainingTime;

        public Character Character => character;
        public Location Location => location;
        public int RemainingTime => remainingTime;

        public RespawnObject(Character character, int remainingTime, Location location)
        {
            this.character = character;
            this.remainingTime = remainingTime;
            this.location = location;
        }

        public void Turn() => remainingTime--;
    }

    StateLinkList<RespawnObject> respawnList;
    int npcRespawn;
    int traderRespawn;
    int mutantRespawn;
    int dogRespawn;

    public int TraderHealth { get; set; }
    public int MutantHealth { get; set; }
    public int DogHealth { get; set; }

    public int TraderAttack { get; set; }
    public int MutantAttack { get; set; }
    public int DogAttack { get; set; }

    public CharacterRespawn()
    {
        TraderHealth = 100;
        MutantHealth = 31;
        DogHealth = 31;

        TraderAttack = 60;
        MutantAttack = 40;
        DogAttack = 40;
    }

    protected override void InitInstance(object[] parameter)
    {
        if (parameter.Length != 4)
            throw new Burntime.Framework.BurntimeLogicException();

        respawnList = container.CreateLinkList<RespawnObject>();
        npcRespawn = (int)parameter[0];
        traderRespawn = (int)parameter[1];
        mutantRespawn = (int)parameter[2];
        dogRespawn = (int)parameter[3];

        base.InitInstance(parameter);
    }

    protected override void AfterDeserialization()
    {
        base.AfterDeserialization();

        if (TraderHealth == 0)
            TraderHealth = 100;
        if (MutantHealth == 0)
            MutantHealth = 31;
        if (DogHealth == 0)
            DogHealth = 31;
        if (TraderAttack == 0)
            TraderAttack = 60;
        if (MutantAttack == 0)
            MutantAttack = 40;
        if (DogAttack == 0)
            DogAttack = 40;
    }

    public void Respawn(Character character)
    {
        var timeToSpawn = character.Class switch
        {
            CharClass.Trader => traderRespawn,
            CharClass.Dog => dogRespawn,
            CharClass.Mutant => mutantRespawn,
            _ => npcRespawn,
        };

        // set for respawn in same location
        Location location = character.Location;

        // schedule for respawn
        respawnList.Add(container.Create(() => new RespawnObject(character, timeToSpawn, location)));
    }

    public void Turn()
    {
        // update respawn list
        for (int i = 0; i < respawnList.Count; i++)
        {
            respawnList[i].Turn();

            // if remaing time is zero, then respawn
            if (respawnList[i].RemainingTime == 0)
            {
                respawnList[i].Character.Revive();

                // enter specified location
                respawnList[i].Location.EnterLocation(respawnList[i].Character);

                // remove from list
                respawnList.Remove(respawnList[i]);
                i--;
            }
        }
    }
}
