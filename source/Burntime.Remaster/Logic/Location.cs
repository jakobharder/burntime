using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Logic;

[Serializable]
public class Location : StateObject, IUpdateable, ITurnable
{
    #region Map

    [DiagnosticOnly]
    internal string Title => ResourceManager.GetString($"burn?{Id}");

    public int Id;
    public static implicit operator int(Location right)
    {
        return right.Id;
    }

    DataID<Interaction.Danger> danger;
    public Interaction.Danger? Danger
    {
        get => danger;
        set => danger = value == null ? default : new DataID<Interaction.Danger>(value);
    }

    public bool IsCity;

    Vector2 entryPoint;
    public Vector2 EntryPoint
    {
        get => new Vector2(entryPoint);
        internal set => entryPoint = value;
    }

    StateLink<Map> map = null!;
    public Map Map
    {
        get => map;
        internal set => map = value;
    }
    public int[] NeighborIds = [];

    StateLinkList<Location> neighbors = null!;
    public StateLinkList<Location> Neighbors => neighbors;

    public int[] Ways = [];
    public int[] WayLengths = [];

    [NonSerialized]
    public Maps.MapViewHoverInfo? Hover;

    #endregion

    #region Characters

    static readonly Vector2[] entryOffsets =
    {
        new(0, 0),
        new(0, -8),
        new(8, 0),
        new(-8, 0),
        new(0, 8)
    };

    StateLinkList<Character> characters = null!;
    public StateLinkList<Character> Characters => characters;
    public IEnumerable<Character> CampNPC => characters.Where(chr => chr.Player != null);

    public StateLink<Trader>? LocalTrader;

    protected StateLink<Player>? player = null;
    public Player? Player
    {
        get => player?.Object;
        set
        {
            Player? previous = Player;
            if (previous == value)
                return;
            player = value;
            (Container.Root as ClassicGame)?.NotifyCampOwnershipChanged(this, previous, value);
        }
    }

    public Player? ControllingPlayer
    {
        get
        {
            if (!IsCity || neighbors == null || neighbors.Count == 0)
                return null;

            Player? controller = neighbors[0].Player;
            if (controller == null)
                return null;

            for (int i = 1; i < neighbors.Count; i++)
                if (neighbors[i].Player != controller)
                    return null;

            return controller;
        }
    }

    public bool AreEntrancesBlockedFor(Player visitor) => Player != null && Player != visitor;

    internal Vector2 GetEntryPosition(int groupIndex) =>
        EntryPoint + entryOffsets[groupIndex];

    [NonSerialized]
    public Character? HoverCharacter;

    public void EnterLocation(Character character)
    {
        character.Position = EntryPoint;
        character.Path.MoveTo = EntryPoint;
        character.Location = this;
    }

    internal void PlaceUnpositionedResidents()
    {
        bool AwayFromEntry(Vector2 position) => (position - EntryPoint).Length > 20;
        var mask = Map.Mask;
        Vector2[] npcPositions = Characters
            .Select(character => character.Position)
            .Where(AwayFromEntry)
            .ToArray();
        Vector2[] entryNeighbors = entryOffsets
            .Skip(1)
            .Select(offset => EntryPoint + offset)
            .Where(mask.IsWalkableMapPosition)
            .ToArray();

        int fallbackIndex = 0;
        foreach (Character character in Characters.Where(character =>
            !character.IsWithBoss && !character.IsPlayerCharacter &&
            character.Position == EntryPoint).ToArray())
        {
            Vector2 position = npcPositions.Length > 0
                ? npcPositions[Platform.Math.Random.Next(npcPositions.Length)]
                : entryNeighbors.Length > 0
                    ? entryNeighbors[fallbackIndex++ % entryNeighbors.Length]
                    : EntryPoint;
            character.Position = position;
            character.Path.Stop(position);
        }
    }

    #endregion

    #region Storage

    public StateLinkList<Room> Rooms = null!;
    public bool IsFullStorage => !Rooms.Any(room => !room.Items.IsFull);

    StateLink<DroppedItemList> items = null!;
    public DroppedItemList Items => items;

    /// <summary>
    /// Drop item at random position
    /// </summary>
    public void DropItemRandom(Item item)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 position = new(
                Platform.Math.Random.Next(Map.Mask.Width),
                Platform.Math.Random.Next(Map.Mask.Height));
            if (!Map.Mask[position])
                continue;

            Items.DropAt(item, position * Map.Mask.Resolution);
            return;
        }

        Items.DropAt(item, EntryPoint);
    }

    public void StoreItemRandom(Item item) => StoreItem(item, randomRoom: true);

    public void StoreItemsRandom(IEnumerable<Item> items)
    {
        foreach (var item in items)
            StoreItemRandom(item);
    }

    /// <summary>
    /// Insert item into room. If none is available drop it randomly.
    /// </summary>
    public void StoreItem(Item item, bool randomRoom = false, Room? preferredRoom = null)
    {
        var rooms = Rooms.Where(x => !x.Items.IsFull).ToList();
        if (rooms.Count == 0)
        {
            DropItemRandom(item);
            return;
        }

        Room room;
        if (preferredRoom != null && rooms.Contains(preferredRoom))
        {
            room = preferredRoom;
        }
        else
        {
            int index = randomRoom ? Platform.Math.Random.Next(0, rooms.Count) : 0;
            room = rooms[index];
        }

        if (room.IsWaterSource)
            Source.RefillItem(item);

        room.Items.Add(item);
    }

    void CreateRooms()
    {
        foreach (var entrance in Map.Entrances)
        {
            RoomType type = entrance.RoomType;
            Room room = container.Create<Room>();
            room.IsWaterSource = type == RoomType.WaterSource;
            room.Items.MaxCount = type switch
            {
                RoomType.Normal or RoomType.Rope => 32,
                RoomType.WaterSource => 8,
                _ => 0
            };
            room.EntryCondition.MaxDistanceOnMap = type == RoomType.Rope ? 75 : 15;
            if (type == RoomType.Rope)
                room.EntryCondition.RequiredItem =
                    ((ClassicGame)container.Root).ItemTypes["item_rope"];
            room.EntryCondition.RegionOnMap = entrance.Area;
            room.EntryCondition.HasRegionOnMap = true;
            room.TitleId = entrance.TitleId;
            Rooms.Add(room);
        }
    }

    #endregion

    #region Water

    StateLink<WaterSource> source = null!;
    public WaterSource Source => source;
    public Room? SourceRoom => Rooms.FirstOrDefault(x => x.IsWaterSource);

    /// <summary>
    /// Find water item with lowest value first.
    /// </summary>
    public Item? FindWater()
    {
        Item? best = null;

        foreach (var room in Rooms)
        foreach (var item in room.Items)
        {
            if (item.WaterValue > 0 &&
                (best == null || item.WaterValue < best.WaterValue))
            {
                best = item;
            }
        }

        return best;
    }

    #endregion

    #region Food
    public const int MaxStockFood = 6;
    StateLink<Production>? production;
    public int[] AvailableProducts = [];
    float productionState = 0; // accumulated food value
    public int NPCFoodProduction; // [unused]

    // Missing in older saves means automatic selection, which is also the default.
    [OptionalField]
    bool manualProductionSelection;

    public bool IsProductionAutomatic => !manualProductionSelection;

    public Production? Production
    {
        get => production?.Object;
        set => production = value;
    }

    public void SelectProduction(Production production)
    {
        Production = production;
        manualProductionSelection = true;
    }

    public Production.Rate SelectAutomaticFoodProduction()
    {
        manualProductionSelection = false;
        return AutoSelectFoodProduction(onlyIfCurrentProducesNothing: false);
    }

    /// <summary>
    /// Find the stored food item with the lowest positive food value.
    /// </summary>
    public Item? FindFood(out IItemCollection? owner)
    {
        Item? best = null;
        owner = null;

        foreach (var room in Rooms)
        foreach (var item in room.Items)
        {
            if (item.FoodValue > 0 &&
                (best == null || item.FoodValue < best.FoodValue))
            {
                best = item;
                owner = room.Items;
            }
        }

        return best;
    }

    public Room? TrapRoom => Rooms.FirstOrDefault(room => room.Items.Any(item => item.Type.Production == Production));

    public IEnumerable<Production> ValidProductions
    {
        get => AvailableProducts.Where(p => p >= 0).Select(p => ((ClassicGame)Container.Root).Productions[p]);
    }

    public int GetCurrentProductionStockCount()
    {
        if (Production == null)
            return 0;
        return Rooms.Sum(room => room.Items.GetCount(Production.Produce));
    }

    public Production.Rate GetFoodProductionRate(Production? production = null)
    {
        production ??= Production;

        if (Player is null || production is null)
            return new Production.Rate();

        int maintenanceBonus = !IsCity && CampNPC.Any(character =>
            character.Player == Player && character.IsStationed && !character.IsDead &&
            character.Class == CharClass.Technician)
            ? ((ClassicGame)Container.Root).RuleBook.Settings.TechnicianFoodBonus
            : 0;
        return production.GetRate(GetProductionToolCount(production), CampNPC.Count(),
            maintenanceBonus);
    }

    public int GetProductionToolCount(Production production) => Rooms
        .SelectMany(room => room.Items)
        .Concat(production.AllowInventory ? CampNPC.SelectMany(npc => npc.Items) : Enumerable.Empty<Item>())
        .Count(item => item.Type.Production == production);

    public Production.Rate AutoSelectFoodProduction(bool onlyIfCurrentProducesNothing)
    {
        var info = GetFoodProductionRate();
        if (info.FoodPerDay > 0 && onlyIfCurrentProducesNothing)
            return info;

        Production = GetAutomaticFoodProduction();
        return GetFoodProductionRate();
    }

    public Production? GetAutomaticFoodProduction()
    {
        Production? selected = Production;
        var info = GetFoodProductionRate();
        foreach (var production in ValidProductions)
        {
            var candidate = GetFoodProductionRate(production);
            if (candidate.FoodPerDay > info.FoodPerDay ||
                candidate.FoodPerDay == info.FoodPerDay && candidate.FoodPerDay > 0 &&
                (selected == null || production.ID > selected.ID))
            {
                selected = production;
                info = candidate;
            }
        }

        return selected;
    }

    public Production.Rate RefreshFoodProductionSelection()
    {
        Production.Rate current = GetFoodProductionRate();
        if (manualProductionSelection && current.FoodPerDay > 0)
            return current;

        return SelectAutomaticFoodProduction();
    }

    internal void ProduceFood(int foodValue)
    {
        int stockedFood = GetCurrentProductionStockCount();

        if (foodValue <= 0 || Production == null ||
            stockedFood >= MaxStockFood || IsFullStorage)
        {
            // reset state until we have room and tools to produce
            productionState = 0;
            return;
        }

        productionState += foodValue;
        if (productionState >= Production.Produce.FoodValue)
        {
            productionState -= Production.Produce.FoodValue;
            StoreItem(Production.Produce.Generate(), preferredRoom: TrapRoom);
        }
    }
    #endregion

    #region Lifecycle

    internal sealed record Params(
        int Id,
        Map Map,
        int Water,
        int WaterReserve,
        int WaterCapacity,
        Production? Production,
        int[] AvailableProducts,
        Interaction.Danger? Danger,
        bool IsCity,
        Vector2 EntryPoint,
        int[] Ways,
        int[] WayLengths,
        int[] NeighborIds);

    internal static Location Create(StateManager container, Params parameters) =>
        container.Create<Location>(parameters);

    public virtual void Update(float elapsed)
    {
        ((ClassicGame)Container.Root).UpdateCreatureAttackCooldown(elapsed);

        for (int i = 0; i < characters.Count; i++)
        {
            characters[i].Update(elapsed);
        }
    }

    public virtual void Turn()
    {
        // refresh water
        Source.BeginTurn();

        // produce food
        var production = RefreshFoodProductionSelection();
        ((ClassicGame)Container.Root).RuleBook.ProcessFoodProduction(this, production);

        // turn npcs
        foreach (Character npc in Characters)
            npc.Turn();

        // fill up bottles
        foreach (Room room in Rooms)
        {
            if (room.IsWaterSource)
                foreach (Item item in room.Items)
                    Source.RefillItem(item);
        }

        // Per-location cycle keeps cleanup deterministic without adding save state.
        ClassicGame game = (ClassicGame)Container.Root;
        game.RuleBook.ProcessDroppedFoodDecay(this, game.World.Day);

        Source.EndTurn();
    }

    protected override void InitInstance(object[] parameter)
    {
        items = container.Create<DroppedItemList>();
        characters = container.CreateLinkList<Character>();
        source = container.Create<WaterSource>(this);
        neighbors = container.CreateLinkList<Location>();
        Rooms = container.CreateLinkList<Room>();

        base.InitInstance(parameter);

        // Parameterless construction remains available for focused tests.
        if (parameter == null || parameter.Length == 0)
            return;
        if (parameter.Length != 1 || parameter[0] is not Params init)
            throw new InvalidStateObjectConstruction(this);

        Id = init.Id;
        map = init.Map;
        Source.Water = init.Water;
        Source.Reserve = init.WaterReserve;
        Source.Capacity = init.WaterCapacity;
        production = init.Production;
        AvailableProducts = init.AvailableProducts;
        danger = init.Danger == null
            ? default
            : new DataID<Interaction.Danger>(init.Danger);
        IsCity = init.IsCity;
        entryPoint = init.EntryPoint;
        Ways = init.Ways;
        WayLengths = init.WayLengths;
        NeighborIds = init.NeighborIds;

        CreateRooms();
    }

    #endregion

}
