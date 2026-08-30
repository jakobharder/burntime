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
        public void Reset(int time) => remainingTime = time;
    }

    StateLinkList<RespawnObject> respawnList;
    int npcRespawn;
    int traderRespawn;
    int mutantRespawn;
    int dogRespawn;

    [System.Runtime.Serialization.OptionalField]
    float mutantDropChance;
    [System.Runtime.Serialization.OptionalField]
    string[] mutantDropType;

    public int TraderHealth { get; set; }
    public int MutantHealth { get; set; }
    public int DogHealth { get; set; }

    public int TraderAttack { get; set; }
    public int MutantAttack { get; set; }
    public int DogAttack { get; set; }

    public float MutantDropChance
    {
        get => mutantDropChance;
        set => mutantDropChance = Math.Clamp(value, 0.0f, 1.0f);
    }

    public string[] MutantDropType
    {
        get => mutantDropType ?? Array.Empty<string>();
        set => mutantDropType = value ?? Array.Empty<string>();
    }

    public CharacterRespawn()
    {
        TraderHealth = 100;
        MutantHealth = 31;
        DogHealth = 31;

        TraderAttack = 60;
        MutantAttack = 40;
        DogAttack = 30;

        mutantDropChance = 0.0f;
        mutantDropType = Array.Empty<string>();
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

        mutantDropChance = Math.Clamp(mutantDropChance, 0.0f, 1.0f);
        mutantDropType ??= Array.Empty<string>();
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

        if (timeToSpawn <= 0)
            return;

        // set for respawn in same location
        Location location = character.Location;

        // schedule for respawn
        respawnList.Add(container.Create(() => new RespawnObject(character, timeToSpawn, location)));
    }

    public void Turn()
    {
        // Update all timers before respawning so resetting another character below
        // always starts a full interval on the next turn.
        for (int i = 0; i < respawnList.Count; i++)
            respawnList[i].Turn();

        for (int i = 0; i < respawnList.Count; i++)
        {
            RespawnObject respawn = respawnList[i];
            if (respawn.RemainingTime > 0)
                continue;

            Character character = respawn.Character;
            Location location = respawn.Location;
            bool spawnAtDeathLocation = respawn.Character.Location == location;
            Platform.Vector2 deathPosition = character.Position;

            character.Revive();
            location.EnterLocation(character);

            if (spawnAtDeathLocation)
            {
                character.Position = deathPosition;
            }

            respawnList.Remove(respawn);
            i--;

            int campRespawnTime = character.Class switch
            {
                CharClass.Dog => dogRespawn,
                CharClass.Mutant => mutantRespawn,
                _ => 0,
            };

            if (campRespawnTime <= 0)
                continue;

            // Camps restore dogs and mutants one at a time. Spawning one starts
            // a new full interval for all other dead characters of that class.
            for (int pendingIndex = 0; pendingIndex < respawnList.Count; pendingIndex++)
            {
                RespawnObject pending = respawnList[pendingIndex];
                if (pending.Location == location && pending.Character.Class == character.Class)
                    pending.Reset(campRespawnTime);
            }
        }
    }
}
