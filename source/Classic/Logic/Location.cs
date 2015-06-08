
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Classic.Logic
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

        StateLink<Production> production;
        public int[] AvailableProducts;
        float productionState = 0;
        public int NPCFoodProduction;

        public Production Production
        {
            get { return production; }
            set { production = value; }
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

        float GetFoodProductionInterval()
        {
            if (Player == null || Production == null)
                return 0;

            int count = 0;

            // check for tools/traps in all rooms
            foreach (Room room in Rooms)
            {
                foreach (Item item in room.Items)
                {
                    if (item.Type.Production == Production)
                    {
                        count++;
                    }
                }
            }

            // now check for tools/traps in camp npcs
            foreach (Character npc in CampNPC)
            {
                foreach (Item item in npc.Items)
                {
                    if (item.Type.Production == Production)
                    {
                        count++;
                    }
                }
            }

            return Production.GetInterval(count, CampNPC.Count);
        }

        public int GetFoodProductionValue()
        {
            if (Player == null || Production == null)
                return 0;

            int count = 0;

            // check for tools/traps in all rooms
            foreach (Room room in Rooms)
            {
                foreach (Item item in room.Items)
                {
                    if (item.Type.Production == Production)
                    {
                        count++;
                    }
                }
            }

            // now check for tools/traps in camp npcs
            foreach (Character npc in CampNPC)
            {
                foreach (Item item in npc.Items)
                {
                    if (item.Type.Production == Production)
                    {
                        count++;
                    }
                }
            }

            return Production.GetProductionPerDay(count, CampNPC.Count);
        }

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
            if (Player != null && Production != null)
            {
                NPCFoodProduction = GetFoodProductionValue();

                float interval = GetFoodProductionInterval();
                int count = 0;
                foreach (Room room in Rooms)
                    count += room.Items.GetCount(Production.Produce);

                if (count < 6 && interval > 0)
                {
                    productionState += 1;
                    if (productionState >= interval)
                    {
                        productionState -= interval;

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