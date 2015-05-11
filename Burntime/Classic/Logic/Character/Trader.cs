using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Classic.Maps;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class TraderItemRefreshItem : StateObject
    {
        public StateLink<ItemType> Type;
        public int Rate;
    };

    [Serializable]
    public class Trader : Character, ITestIF
    {
        static Random rnd = new Random();

        StateLink<Location> homeArea;

        int counter = 0;
        StateLinkList<TraderItemRefreshItem> itemRefreshs;
        int itemRefreshRange = 0;
        int itemCount = 10;
        protected int traderId;

        public Location HomeArea
        {
            get { return homeArea; }
            set { homeArea = value; }
        }

        public int TraderId
        {
            get { return traderId; }
            set { traderId = value; }
        }

        public override float AttackValue
        {
            get { return 55; }
        }

        public void AddRefreshItem(ItemType Type, int Rate)
        {
            itemRefreshRange += Rate;
            TraderItemRefreshItem item = container.Create<TraderItemRefreshItem>();
            item.Rate = Rate;
            item.Type = Type;
            itemRefreshs.Add(item);
        }

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            itemRefreshs = Container.CreateLinkList<TraderItemRefreshItem>();
        }

        // logic
        public override void Turn()
        {
            if (IsDead)
                return;

            // ignore food/water

            NextSellLocation();
            RefreshItems();

            //Dialog.Turn();
        }

        public void RandomizeInventory()
        {
            
            Items.Clear();

            for (int count = rnd.Next() % 1 + 6; count >= 0; count--)
            {
                ItemType type = GetNextItem();
                if (type == null)
                    break;
                Item item = type.Generate();
                if (item.Type != null)
                    Items.Add(item);
            }
        }

        // private logic
        protected virtual void NextSellLocation()
        {
            if (HomeArea == null)
                return;

            counter++;
            if (counter > 1)
            {
                counter = 0;
                if (Location == HomeArea)
                {
                    Location = HomeArea.Neighbors[rnd.Next(HomeArea.Neighbors.Count - 1)];
                    position = Location.EntryPoint;
                }
                else
                {
                    Location = HomeArea;
                    position = Location.EntryPoint;
                }
            }
        }

        protected virtual void RefreshItems()
        {
            int remove = rnd.Next(3);
            int add = rnd.Next(3);

            for (int i = 0; i < remove && Items.Count > 0; i++)
            {
                int item = rnd.Next(Items.Count);

                Burntime.Platform.Log.Debug("trader remove: " + Items[item]);
                Items.Remove(Items[item]);
            }

            for (int i = 0; i < add && Items.Count < itemCount; i++)
            {
                ItemType type = GetNextItem();
                if (type == null)
                    break;
                Item item = type.Generate();
                Items.Add(item);
                Burntime.Platform.Log.Debug("trader add: " + item);
            }
        }

        protected virtual ItemType GetNextItem()
        {
            List<string> itemTypes = new List<string>();

            // list up all item types not yet in inventory
            foreach (TraderItemRefreshItem item in itemRefreshs)
            {
                if (!Items.Contains(item.Type.Object.ID))
                {
                    itemTypes.Add(item.Type.Object.ID);
                }
            }

            ClassicGame game = container.Root as ClassicGame;

            // no more item types to choose from
            if (itemTypes.Count == 0)
                return null;
            // only one item type, skip randomizer
            else if (itemTypes.Count == 1)
            {
                return game.ItemTypes[itemTypes[0]];
            }

            // get random item type
            return game.ItemTypes[itemTypes[rnd.Next() % itemTypes.Count]];
        }
    }
}
