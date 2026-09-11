using Burntime.Framework.States;
using System;
using System.Linq;

namespace Burntime.Remaster.Logic;

public enum RespawnMethod
{
    Timer,
    PlayerCycle,
    LocationCycle,
}

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

    [NonSerialized]
    RespawnMethod spawnMethod;
    [NonSerialized]
    int citySpawnThreshold;

    internal RespawnMethod SpawnMethod => spawnMethod;

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

    internal void ApplySettings(Generation.GameSettings settings)
    {
        spawnMethod = settings.Respawn.Method;
        npcRespawn = settings.Respawn.NPC;
        citySpawnThreshold = settings.Respawn.CitySpawnThreshold;
        traderRespawn = settings.Respawn.Trader;
        mutantRespawn = settings.Respawn.Mutant;
        dogRespawn = settings.Respawn.Dog;

        TraderHealth = settings.ClassStats.TraderHealth;
        MutantHealth = settings.ClassStats.MutantHealth;
        DogHealth = settings.ClassStats.DogHealth;
        TraderAttack = settings.ClassStats.TraderAttack;
        MutantAttack = settings.ClassStats.MutantAttack;
        DogAttack = settings.ClassStats.DogAttack;
        MutantDropChance = settings.MutantDropChance;
        MutantDropType = settings.MutantDropType;
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
        bool IsOrdinaryRespawn(RespawnObject respawn) =>
            IsOrdinaryNpc(respawn.Character);

        // Non-NPC classes retain their individual death timers. Timer mode also
        // retains the legacy behavior for custom rulesets without spawn_method.
        for (int i = 0; i < respawnList.Count; i++)
            if (spawnMethod == RespawnMethod.Timer || !IsOrdinaryRespawn(respawnList[i]))
                respawnList[i].Turn();

        switch (spawnMethod)
        {
            case RespawnMethod.PlayerCycle:
                TurnPlayerCycle(IsOrdinaryRespawn);
                break;
            case RespawnMethod.LocationCycle:
                TurnLocationCycle(IsOrdinaryRespawn);
                break;
        }

        for (int i = 0; i < respawnList.Count; i++)
        {
            RespawnObject respawn = respawnList[i];
            if (respawn.RemainingTime > 0 ||
                spawnMethod != RespawnMethod.Timer && IsOrdinaryRespawn(respawn))
                continue;

            if (Spawn(respawn))
                i--;
        }
    }

    void TurnPlayerCycle(Func<RespawnObject, bool> isOrdinaryNpc)
    {
        World world = ((ClassicGame)container.Root).World;
        if (world.Players.Count == 0)
            return;

        int playerIndex = world.Day % world.Players.Count;
        Player player = world.Players[playerIndex];

        if (player.IsTraveling || player.Location is null)
            return;

        RespawnObject? pending = respawnList.FirstOrDefault(respawn =>
            isOrdinaryNpc(respawn) && respawn.Location == player.Location);
        if (pending is not null)
            Spawn(pending);
    }

    void TurnLocationCycle(Func<RespawnObject, bool> isOrdinaryNpc)
    {
        if (npcRespawn <= 0)
            return;

        World world = ((ClassicGame)container.Root).World;
        foreach (Location location in world.Locations)
        {
            if (!IsLocationCycleDue(world.Day, location.Id, npcRespawn))
                continue;

            RespawnObject? pending = respawnList.FirstOrDefault(respawn =>
                isOrdinaryNpc(respawn) && respawn.Location == location);
            if (pending is not null)
                Spawn(pending);
        }
    }

    internal static bool IsLocationCycleDue(int day, int locationId, int interval) =>
        interval > 0 && (day + locationId) % interval == 0;

    static bool IsOrdinaryNpc(Character character) =>
        character.Class is CharClass.Mercenary or CharClass.Technician or CharClass.Doctor;

    bool RefreshIdentity(RespawnObject respawn)
    {
        RespawnObject? nameDonor = respawnList.FirstOrDefault(candidate =>
            candidate != respawn && IsOrdinaryNpc(candidate.Character));
        if (nameDonor is null)
            return false;

        Character character = respawn.Character;
        (character.NameId, nameDonor.Character.NameId) =
            (nameDonor.Character.NameId, character.NameId);

        // Face 10 wears a gas mask, so neither replace it nor assign it randomly.
        if (character.FaceID != 10)
        {
            int face = Burntime.Platform.Math.Random.Next(7, 28);
            character.FaceID = face >= 10 ? face + 1 : face;
        }

        character.SetBodyId = Helper.GetSetBodyId(character.Class);
        if (character.SetBodyId >= 0)
            character.Body = Helper.GetCharacterBody(character.SetBodyId,
                Burntime.Platform.Math.Random.Next(0, 3));

        return true;
    }

    Location GetSpawnLocation(Character character, Location deathLocation)
    {
        ClassicGame game = (ClassicGame)container.Root;
        if (game.Rules != Generation.RuleSetId.Extended || citySpawnThreshold <= 0 ||
            !IsOrdinaryNpc(character))
            return deathLocation;

        Location? destination = null;
        int lowestPopulation = int.MaxValue;
        foreach (Location location in game.World.Locations)
        {
            if (!location.IsCity)
                continue;

            int population = location.Characters.Count(candidate =>
                IsOrdinaryNpc(candidate) && !candidate.IsDead && candidate.Player is null);
            if (population < citySpawnThreshold && population < lowestPopulation)
            {
                destination = location;
                lowestPopulation = population;
            }
        }

        return destination ?? deathLocation;
    }

    bool Spawn(RespawnObject respawn)
    {
        Character character = respawn.Character;
        bool ordinaryNpc = IsOrdinaryNpc(character);
        if (ordinaryNpc && !RefreshIdentity(respawn))
            return false;

        Location location = GetSpawnLocation(character, respawn.Location);
        character.Revive();
        location.EnterLocation(character);

        respawnList.Remove(respawn);

        int campRespawnTime = character.Class switch
        {
            CharClass.Dog => dogRespawn,
            CharClass.Mutant => mutantRespawn,
            _ => 0,
        };

        if (campRespawnTime <= 0)
            return true;

        // Camps restore dogs and mutants one at a time. Spawning one starts
        // a new full interval for all other dead characters of that class.
        for (int pendingIndex = 0; pendingIndex < respawnList.Count; pendingIndex++)
        {
            RespawnObject pending = respawnList[pendingIndex];
            if (pending.Location == location && pending.Character.Class == character.Class)
                pending.Reset(campRespawnTime);
        }

        return true;
    }
}
