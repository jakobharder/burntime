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

        public List<Character> CampNPC
        {
            get
            {
                List<Character> l = new List<Character>();
                foreach (Character ch in characters)
                {
                    if (ch.Player != null)
                        l.Add(ch);
                }

                return l;
            }
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
        public Player Player
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

        public void RemoveItem(Item item)
        {

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
            get => AvailableProducts.Select(p => ((ClassicGame)Container.Root).Productions[p]);
        }

        public Production.Rate GetFoodProductionRate(Production? production = null)
        {
            production ??= Production;

            if (Player is null || production is null)
                return new Production.Rate();

            int trapsInRooms = Rooms.Sum(room => room.Items.Where(item => item.Type.Production == production).Count());
            int trapsOnNPCs = CampNPC.Sum(npc => npc.Items.Where(item => item.Type.Production == production).Count());

            return production.GetRate(trapsInRooms + trapsOnNPCs, CampNPC.Count);
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

                        int i = 0;
                        while (!Rooms[i].Items.Add(Production.Produce.Generate()))
                        {
                            // try next room if full
                            i++;
                        }
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

        // insert item in random room
        public void StoreItemRandom(Item item)
        {
            if (Rooms.Count == 0)
            {
                DropItemRandom(item);
                return;
            }

            Room room;
            do
            {
                room = this.Rooms[Burntime.Platform.Math.Random.Next() % Rooms.Count];
            } while (room.Items.MaxCount != ItemList.Infinite && room.Items.MaxCount < room.Items.Count);

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
    }
}