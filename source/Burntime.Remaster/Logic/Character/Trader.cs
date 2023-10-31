using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Remaster.Maps;

namespace Burntime.Remaster.Logic
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
        StateLink<Location> homeArea;

        int counter = 0;
        StateLinkList<TraderItemRefreshItem> itemRefreshs;
        int itemRefreshRange = 0;

        readonly int _maxStockItemCount = 7;

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

            for (int count = Platform.Math.Random.Next(3, 6); count >= 0; count--)
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
                    Location = HomeArea.Neighbors[Platform.Math.Random.Next(HomeArea.Neighbors.Count - 1)];
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
            // randomly swap 1 to 3 items
            int swapItems = System.Math.Min(Items.Count, Platform.Math.Random.Next(1, 3));
            int remove = swapItems;
            int add = swapItems;

            // keep total to 3 to max stock items
            const int MIN_STOCK_ITEMS = 2;
            const int MAX_STOCK_ITEMS = 6;
            int targetCount = Platform.Math.Random.Next(MIN_STOCK_ITEMS, MAX_STOCK_ITEMS);
            int projectedCount = Items.Count + add - remove;
            if (targetCount > projectedCount)
                add += targetCount - projectedCount;
            else
                remove += projectedCount - targetCount;

            // keep add/remove within 2 difference
            if (add > remove)
                add = System.Math.Min(remove + 1, add);
            else
                remove = System.Math.Min(add + 1, remove);

            for (int i = 0; i < remove && Items.Count > 0; i++)
            {
                int item = Platform.Math.Random.Next(Items.Count);

                Burntime.Platform.Log.Debug("trader remove: " + Items[item]);
                Items.Remove(Items[item]);
            }

            for (int i = 0; i < add && Items.Count < _maxStockItemCount; i++)
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
            var itemTypes = new List<string>();

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
            return game.ItemTypes[itemTypes[Platform.Math.Random.Next() % itemTypes.Count]];
        }
    }
}
