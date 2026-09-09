using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    [DebuggerDisplay("{Title}")]
    public class Location : StateObject, IUpdateable, ITurnable
    {
        public int Id;
        public static implicit operator int(Location right)
        {
            return right.Id;
        }

        DataID<Interaction.Danger> danger;
        public Interaction.Danger Danger
        {
            get { return danger; }
            set { danger = value == null ? default(DataID<Interaction.Danger>) : new DataID<Interaction.Danger>(value); }
        }

        public bool IsCity;

        [System.Runtime.Serialization.OptionalField]
        internal byte OriginalDosFoodFlag;

        Vector2 entryPoint;
        public Vector2 EntryPoint
        {
            get { return new Vector2(entryPoint); }
            set { entryPoint = value; }
        }

        StateLink<Map> map;
        public Map Map
        {
            get { return map; }
            set { map = value; }
        }
        public StateLinkList<Room> Rooms;

        StateLinkList<Character> characters;
        public StateLinkList<Character> Characters
        {
            get { return characters; }
            set { characters = value; }
        }

        public IEnumerable<Character> CampNPC
        {
            get => characters.Where(chr => chr.Player != null);
        }

        StateLink<DroppedItemList> items;
        public DroppedItemList Items
        {
            get { return items; }
            set { items = value; }
        }

        //StateList CampCharacters;
        public StateLink<Trader> LocalTrader;

        #region Neighbors
        public int[] NeighborIds;

        StateLinkList<Location> neighbors;
        public StateLinkList<Location> Neighbors
        {
            get { return neighbors; }
            set { neighbors = value; }
        }

        public int[] Ways;
        public int[] WayLengths;
        #endregion

        StateLink<WaterSource> source;
        public WaterSource Source
        {
            get { return source; }
            set { source = value; }
        }

        protected StateLink<Player> player;
        public Player? Player
        {
            get { return (player != null) ? player : null; }
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

        public bool AreEntrancesBlockedFor(Player visitor)
        {
            return Player != null && Player != visitor;
        }

        // for debug
        public string Title
        {
#warning // incorrect string
            get { return ResourceManager.GetString("burn?" + this.Id); }
        }

        /// <summary>
        /// Find food. Prefer currently produced food, then highest value.
        /// </summary>
        public Item? FindFood(out IItemCollection? owner)
        {
            Item? foundItem = null;
            owner = null;

            foreach (var room in Rooms)
            {
                foreach (var item in room.Items)
                {
                    if (item.FoodValue == 0)
                        continue;
                    
                    if (foundItem == null
                        || (Production is not null && foundItem.Type == Production.Produce)
                        || foundItem.FoodValue < item.FoodValue)
                    {
                        foundItem = item;
                        owner = room.Items;
                    }
                }
            }

            return foundItem;
        }

        /// <summary>
        /// Find water item with highest value.
        /// </summary>
        public Item? FindWater()
        {
            Item? foundItem = null;

            foreach (var room in Rooms)
            {
                foreach (var item in room.Items)
                {
                    if (item.WaterValue == 0)
                        continue;

                    if (foundItem == null
                        || item.WaterValue > foundItem.WaterValue)
                    {
                        foundItem = item;
                    }
                }
            }

            return foundItem;
        }

        // temporary
        [NonSerialized]
        public Maps.MapViewHoverInfo Hover;
        [NonSerialized]
        public Character HoverCharacter;

        #region food
        public const int MaxStockFood = 6;
        StateLink<Production> production;
        public int[] AvailableProducts;
        float productionState = 0;
        public int NPCFoodProduction;

        public Production? Production
        {
            get => production;
            set => production = value;
        }

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

        internal void ConsumeExcessFoodStock(int maximumItems)
        {
            IEnumerable<(IItemCollection Owner, Item Item)> roomFood = Rooms
                .SelectMany(room => room.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => ((IItemCollection)room.Items, item)));
            IEnumerable<(IItemCollection Owner, Item Item)> garrisonFood = CampNPC
                .Where(npc => npc.Player == Player && !npc.IsDead)
                .SelectMany(npc => npc.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => ((IItemCollection)npc.Items, item)));
            var stored = roomFood
                .Concat(garrisonFood)
                .OrderBy(entry => entry.Item.FoodValue)
                .ThenBy(entry => entry.Item.TradeValue)
                .ThenBy(entry => entry.Item.ID)
                .ToArray();
            int excess = stored.Length - maximumItems;
            foreach (var entry in stored.Take(System.Math.Max(0, excess)))
                entry.Owner.Remove(entry.Item);
        }

        public Production.Rate GetFoodProductionRate(Production? production = null)
        {
            production ??= Production;

            if (Player is null || production is null)
                return new Production.Rate();

            return production.GetRate(GetProductionToolCount(production), CampNPC.Count());
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

            foreach (var production in ValidProductions)
            {
                var candidate = GetFoodProductionRate(production);
                if (candidate.FoodPerDay > info.FoodPerDay)
                {
                    Production = production;
                    info = candidate;
                }
            }

            return info;
        }
        #endregion

        // logic
        public virtual void Update(float elapsed)
        {
            //Time -= 0.5f * elapsed;
            //if (Time < 0)
            //    Time = 0;

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
            var production = AutoSelectFoodProduction(onlyIfCurrentProducesNothing: true);
            ((ClassicGame)Container.Root).RuleBook.ProcessFoodProduction(this, production);

            // turn npcs
            foreach (Character npc in Characters)
                npc.Turn();

            // fill up bottles
            foreach (Room room in Rooms)
            {
                if (room.IsWaterSource)
                {
                    foreach (Item item in room.Items)
                    {
                        if (item.Type.Full != null && item.Type.Full.WaterValue != 0)
                        {
                            if (Source.Reserve >= item.Type.Full.WaterValue)
                            {
                                Source.Reserve -= item.Type.Full.WaterValue;
                                item.MakeFull();
                            }
                        }
                    }
                }
            }

            Source.EndTurn();
        }

        internal void ProcessExtendedFoodProduction(Production.Rate production)
        {
            NPCFoodProduction = production.FoodPerDay;
            if (production.ItemDropInterval > 0)
            {
                int alreadyInStock = GetCurrentProductionStockCount();
                // Like DOS, only the selected product counts toward the cap.
                // Food from an older selection remains available for normal use.
                if (alreadyInStock < MaxStockFood && Rooms.Any(room => !room.Items.IsFull) &&
                    ((ClassicGame)Container.Root).RuleBook.CanCreateItem(
                        (ClassicGame)Container.Root))
                {
                    productionState += 1;
                    if (productionState >= production.ItemDropInterval)
                    {
                        productionState -= production.ItemDropInterval;
                        Room trapRoom = Rooms.FirstOrDefault(room => room.Items
                            .Any(item => item.Type.Production == Production));
                        StoreItem(Production.Produce.Generate(), preferredRoom: trapRoom);
                    }
                }
            }
        }

        internal void AccumulateOriginalFood(int points)
        {
            NPCFoodProduction = 0;
            if (points <= 0 || Production == null ||
                GetCurrentProductionStockCount() >= MaxStockFood ||
                !Rooms.Any(room => !room.Items.IsFull) ||
                !((ClassicGame)Container.Root).RuleBook.CanCreateItem(
                    (ClassicGame)Container.Root))
                return;

            productionState += points;
            while (productionState >= Production.Produce.FoodValue &&
                GetCurrentProductionStockCount() < MaxStockFood &&
                Rooms.Any(room => !room.Items.IsFull) &&
                ((ClassicGame)Container.Root).RuleBook.CanCreateItem(
                    (ClassicGame)Container.Root))
            {
                productionState -= Production.Produce.FoodValue;
                Room trapRoom = Rooms.FirstOrDefault(room => room.Items
                    .Any(item => item.Type.Production == Production));
                StoreItem(Production.Produce.Generate(), preferredRoom: trapRoom);
            }
        }

        protected override void InitInstance(object[] parameter)
        {
            items = container.Create<DroppedItemList>();
            characters = container.CreateLinkList<Character>();
            source = container.Create<WaterSource>(this);
            neighbors = container.CreateLinkList<Location>();

            base.InitInstance(parameter);
        }

        /// <summary>
        /// Drop item at random position
        /// </summary>
        public void DropItemRandom(Item item)
        {
            Vector2 pos;
            do
            {
                pos.x = Burntime.Platform.Math.Random.Next() % Map.Mask.Width;
                pos.y = Burntime.Platform.Math.Random.Next() % Map.Mask.Height;
            } while (!Map.Mask[pos]);

            Items.DropAt(item, pos * Map.Mask.Resolution);
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
        public void StoreItem(Item item, bool randomRoom = false, Room preferredRoom = null)
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
                int index = randomRoom ? Platform.Math.Random.Next(0, rooms.Count - 1) : 0;
                room = rooms[index];
            }

            // fill up empty bottles
            if (room.IsWaterSource && item.Type.Full != null && Source.Reserve >= item.Type.Full.WaterValue)
            {
                item.MakeFull();
                Source.Reserve -= item.WaterValue;
            }

            room.Items.Add(item);
        }

        // add character to this location
        public void EnterLocation(Character character)
        {
            Vector2 position = character.IsWithBoss || character.IsPlayerCharacter
                ? EntryPoint
                : GetResidentPosition(character);
            character.Position = position;
            character.Path.MoveTo = position;
            character.Location = this;
        }

        [System.Runtime.Serialization.OptionalField]
        bool legacyEntryPositionsRepaired;

        internal void RepairLegacyEntryPositions()
        {
            if (legacyEntryPositionsRepaired)
                return;

            // Older arrivals used the entrance itself or a 20-pixel radius.
            // Exclude that whole cluster from both local anchors and fallback.
            bool AwayFromEntry(Vector2 position) => (position - EntryPoint).Length > 20;
            foreach (Character resident in Characters.Where(character =>
                !character.IsDead && !character.IsWithBoss && !character.IsPlayerCharacter &&
                !AwayFromEntry(character.Position)).ToArray())
                resident.Position = GetResidentPosition(resident, AwayFromEntry);

            legacyEntryPositionsRepaired = true;
        }

        // Residents inherit an established local position instead of remaining
        // in an arriving party's entrance formation. Shared by all editions.
        public Vector2 GetResidentPosition(Character arrivingCharacter, Func<Vector2, bool>? allowedPosition = null)
        {
            var mask = Map.Mask;
            var positions = Characters
                .Where(other => other != arrivingCharacter && !other.IsWithBoss &&
                    !other.IsPlayerCharacter && other.Location == this)
                .Select(other => other.Position)
                .Where(mask.IsWalkableMapPosition)
                .Where(position => allowedPosition == null || allowedPosition(position))
                .Distinct().ToArray();
            if (positions.Length > 0)
                return positions[Platform.Math.Random.Next(positions.Length)];

            // Empty camps have no position to copy. Choose a walkable map cell
            // rather than making the entrance the default resident position.
            var walkable = new List<Vector2>();
            for (int y = 0; y < mask.Height; y++)
                for (int x = 0; x < mask.Width; x++)
                    if (mask[x, y])
                    {
                        Vector2 position = new Vector2(x, y) * mask.Resolution + mask.Resolution / 2;
                        if (allowedPosition == null || allowedPosition(position))
                            walkable.Add(position);
                    }
            return walkable.Count > 0
                ? walkable[Platform.Math.Random.Next(walkable.Count)]
                : EntryPoint;
        }

        public Vector2 GetRandomNpcEntryPosition(Character? arrivingCharacter = null,
            int radius = 20)
        {
            var mask = Map.Mask;
            int resolution = mask.Resolution;
            List<Vector2> validPositions = new();
            List<Vector2> unoccupiedPositions = new();

            int minimumX = System.Math.Max(0, (EntryPoint.x - radius) / resolution);
            int maximumX = System.Math.Min(mask.Width - 1,
                (EntryPoint.x + radius) / resolution);
            int minimumY = System.Math.Max(0, (EntryPoint.y - radius) / resolution);
            int maximumY = System.Math.Min(mask.Height - 1,
                (EntryPoint.y + radius) / resolution);
            int radiusSquared = radius * radius;

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    if (!mask[x, y])
                        continue;

                    Vector2 candidate = new Vector2(x, y) * resolution + resolution / 2;
                    Vector2 difference = candidate - EntryPoint;
                    if (difference.x * difference.x + difference.y * difference.y > radiusSquared)
                        continue;

                    validPositions.Add(candidate);
                    bool occupied = IsNpcSpawnPositionOccupied(candidate,
                        arrivingCharacter, resolution);
                    if (!occupied)
                        unoccupiedPositions.Add(candidate);
                }
            }

            List<Vector2> choices = unoccupiedPositions.Count > 0
                ? unoccupiedPositions
                : validPositions;
            if (choices.Count > 0)
                return choices[Platform.Math.Random.Next(choices.Count)];

            // Entry points should normally have nearby walkable cells. If legacy
            // data does not, validity is more important than keeping the radius.
            Vector2 nearest = EntryPoint;
            int nearestDistanceSquared = int.MaxValue;
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (!mask[x, y])
                        continue;

                    Vector2 candidate = new Vector2(x, y) * resolution + resolution / 2;
                    Vector2 difference = candidate - EntryPoint;
                    int distanceSquared = difference.x * difference.x +
                        difference.y * difference.y;
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = candidate;
                        nearestDistanceSquared = distanceSquared;
                    }
                }
            }
            return nearest;
        }

        bool IsNpcSpawnPositionOccupied(Vector2 candidate, Character? arrivingCharacter,
            int minimumDistance)
        {
            bool IsTooClose(Character character) =>
                character != arrivingCharacter && !character.IsDead &&
                (character.Position - candidate).Length < minimumDistance;

            if (Characters.Any(IsTooClose))
                return true;

            if (Container.Root is not ClassicGame game)
                return false;

            foreach (Player player in game.World.Players)
            {
                if (player.Location != this)
                    continue;
                foreach (Character character in player.Group)
                    if (IsTooClose(character))
                        return true;
            }

            return false;
        }

        #region get helpers
        public Room GetSourceRoom()
        {
            foreach (Room room in Rooms)
                if (room.IsWaterSource)
                    return room;
            return null;
        }
        #endregion

        protected override void AfterResolving()
        {
            base.AfterResolving();

            // fix saves before 1.0
            if (Source is not null && Source.Water > 0 &&
                Title == "New Village" &&
                Rooms is not null && Rooms.Count > 3 && !Rooms.Any(x => x.IsWaterSource))
            {
                Rooms[3].IsWaterSource = true;
            }
        }
    }
}
