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
            set { danger = value; }
        }

        public bool IsCity;

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

        public int[] NeighborIds;

        StateLinkList<Location> neighbors;
        public StateLinkList<Location> Neighbors
        {
            get { return neighbors; }
            set { neighbors = value; }
        }

        public int[] Ways;
        public int[] WayLengths;

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
            set { player = value; }
        }

        // for debug
        public string Title
        {
#warning // incorrect string
            get { return ResourceManager.GetString("burn?" + this.Id); }
        }

        public Item FindFood(out IItemCollection owner)
        {
            Item item = null;
            owner = null;

            for (int i = 0; i < Rooms.Count; i++)
            {
                for (int j = 0; j < Rooms[i].Items.Count; j++)
                {
                    if (Rooms[i].Items[j].FoodValue != 0)
                    {
                        if (item == null || item.FoodValue < Rooms[i].Items[j].FoodValue || item.Type == Production.Produce)
                        {
                            item = Rooms[i].Items[j];
                            owner = Rooms[i].Items;
                        }
                    }
                }
            }

            return item;
        }

        public Item FindWater()
        {
            Item item = null;

            for (int i = 0; i < Rooms.Count; i++)
            {
                for (int j = 0; j < Rooms[i].Items.Count; j++)
                {
                    if (Rooms[i].Items[j].WaterValue != 0 &&
                        (item == null || Rooms[i].Items[j].WaterValue > item.WaterValue))
                    {
                        item = Rooms[i].Items[j];
                    }
                }
            }

            return item;
        }

        // temporary
        [NonSerialized]
        public Maps.MapViewHoverInfo Hover;

        #region food
        const int MAX_STOCK_FOOD = 6;
        StateLink<Production> production;
        public int[] AvailableProducts;
        float productionState = 0;
        public int NPCFoodProduction;

        public Production Production
        {
            get => production;
            set => production = value;
        }

        public IEnumerable<Production> ValidProductions
        {
            get => AvailableProducts.Where(p => p >= 0).Select(p => ((ClassicGame)Container.Root).Productions[p]);
        }

        public Production.Rate GetFoodProductionRate(Production? production = null)
        {
            production ??= Production;

            if (Player is null || production is null)
                return new Production.Rate();

            int trapsInRooms = Rooms.Sum(room => room.Items.Where(item => item.Type.Production == production).Count());
            int trapsOnNPCs = CampNPC.Sum(npc => npc.Items.Where(item => item.Type.Production == production).Count());

            return production.GetRate(trapsInRooms + trapsOnNPCs, CampNPC.Count());
        }

        public Production.Rate AutoSelectFoodProduction(bool onlyIfStarving)
        {
            var info = GetFoodProductionRate();
            if (!info.IsCampStarving && onlyIfStarving)
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
            var production = AutoSelectFoodProduction(onlyIfStarving: true);
            NPCFoodProduction = production.FoodPerDay;
            if (production.ItemDropInterval > 0)
            {
                int alreadyInStock = Rooms.Sum(room => room.Items.GetCount(Production.Produce));
                if (alreadyInStock < MAX_STOCK_FOOD)
                {
                    productionState += 1;
                    if (productionState >= production.ItemDropInterval)
                    {
                        productionState -= production.ItemDropInterval;
                        StoreItem(Production.Produce.Generate());
                    }
                }
            }

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

        protected override void InitInstance(object[] parameter)
        {
            items = container.Create<DroppedItemList>();
            characters = container.CreateLinkList<Character>();
            source = container.Create<WaterSource>(this);
            neighbors = container.CreateLinkList<Location>();

            base.InitInstance(parameter);
        }

        // drop item at random position
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

        /// <summary>
        /// Insert item into room. If none is available drop it randomly.
        /// </summary>
        public void StoreItem(Item item, bool randomRoom = false)
        {
            var rooms = Rooms.Where(x => !x.Items.IsFull).ToList();
            if (rooms.Count == 0)
            {
                DropItemRandom(item);
                return;
            }

            int index = randomRoom ? Platform.Math.Random.Next(0, rooms.Count - 1) : 0;
            var room = rooms[index];

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
            character.Position = EntryPoint;
            character.Path.MoveTo = EntryPoint;
            character.Location = this;
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

        public void ClearItemsAfterTakeover()
        {
            if (container.Root is not ClassicGame root)
                return;

            foreach (var room in Rooms)
                room.Items.Clear();

            var list = new List<Item?>();

            // all levels
            if (root.World.Difficulty <= 2)
            {
                // random food
                list.Add(root.ItemTypes.GenerateClass(new string[] { "food" }, Array.Empty<string>(), 1));

                // 20% chance of a rare/special
                list.Add(root.ItemTypes.GenerateClass(new string[] { "rare", "special" }, new string[] { "nodrop" }, 0.2f));

                // 50% chance of a bottle or canteen
                list.Add(root.ItemTypes.GenerateClass(new string[] { "bottle" }, Array.Empty<string>(), 0.5f));

                // 0-2 random items
                list.AddRange(root.ItemTypes.GenerateClass(new string[] { "material", "useless" }, Array.Empty<string>(), 0, 2));
            }

            // level 2 and 1
            if (root.World.Difficulty <= 1)
            {
                // 50% chance of a food trap
                list.Add(root.ItemTypes.GenerateClass(new string[] { "trap" }, Array.Empty<string>(), 0.5f));

                // another 20% chance of a rare/special
                list.Add(root.ItemTypes.GenerateClass(new string[] { "rare", "special" }, Array.Empty<string>(), 0.2f));

                // 0-2 random items
                list.AddRange(root.ItemTypes.GenerateClass(new string[] { "material", "useless" }, Array.Empty<string>(), 0, 2));
            }

            // level 1 only
            if (root.World.Difficulty == 0)
            {
                // random food
                list.AddRange(root.ItemTypes.GenerateClass(new string[] { "food" }, Array.Empty<string>(), 1, 2));

                // another 50% chance of a food trap
                list.Add(root.ItemTypes.GenerateClass(new string[] { "trap" }, Array.Empty<string>(), 0.5f));

                // another 20% chance of a rare/special
                list.Add(root.ItemTypes.GenerateClass(new string[] { "rare", "special", "protection_parts" }, Array.Empty<string>(), 0.2f));

                // 1-2 random items
                list.AddRange(root.ItemTypes.GenerateClass(new string[] { "material", "useless" }, Array.Empty<string>(), 1, 2));
            }

            foreach (var item in list)
            {
                if (item is not null)
                    StoreItemRandom(item);
            }
        }
    }
}