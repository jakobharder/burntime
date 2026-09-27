using Burntime.Framework.States;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;

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

        public override int BaseAttackValue => Root.World.Respawn.Object.TraderAttack;

        public void AddRefreshItem(ItemType Type, int Rate)
        {
            Rate = System.Math.Max(1, Rate);
            itemRefreshRange += Rate;
            TraderItemRefreshItem item = container.Create<TraderItemRefreshItem>();
            item.Rate = Rate;
            item.Type = Type;
            itemRefreshs.Add(item);
        }

        internal void RefreshAssortment(IEnumerable<(ItemType Type, int Rate)> types)
        {
            (ItemType Type, int Rate)[] assortment = types
                .Select(entry => (entry.Type, System.Math.Max(1, entry.Rate)))
                .ToArray();
            TraderItemRefreshItem[] saved = itemRefreshs.ToArray();
            itemRefreshRange = 0;
            for (int i = 0; i < assortment.Length; i++)
            {
                if (i < saved.Length)
                {
                    saved[i].Type = assortment[i].Type;
                    saved[i].Rate = assortment[i].Rate;
                    itemRefreshRange += assortment[i].Rate;
                }
                else
                {
                    AddRefreshItem(assortment[i].Type, assortment[i].Rate);
                }
            }
            foreach (var removed in saved.Skip(assortment.Length))
                itemRefreshs.Remove(removed);
        }

        public IEnumerable<ItemType> GetAssortment() => itemRefreshs.Select(item => item.Type.Object);

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            itemRefreshs = Container.CreateLinkList<TraderItemRefreshItem>();
        }

        public override void Revive()
        {
            base.Revive();

            Health = Root.World.Respawn.Object.TraderHealth;
        }

        // logic
        public override void Turn()
        {
            if (IsDead)
                return;

            Root.RuleBook.TurnTrader(this);

            RestoreTraderHealth();
        }

        // Shared by ordinary turns and Amiga's world-wide stock passes.
        internal void RestoreTraderHealth() => Health = Root.World.Respawn.Object.TraderHealth;

        internal void TurnExtendedTrader()
        {
            NextSellLocation();
            RefreshItems();
        }

        internal void MoveToNextSellLocation() => NextSellLocation();

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
                    Position = Location.EntryPoint;
                    Path.Stop(Position);
                }
                else
                {
                    Location = HomeArea;
                    Position = Location.EntryPoint;
                    Path.Stop(Position);
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
                Item[] removable = Items
                    .Where(CanRemoveFromStock)
                    .ToArray();
                if (removable.Length == 0)
                    break;
                Item item = removable[Platform.Math.Random.Next(removable.Length)];

                Burntime.Platform.Log.Debug("trader remove: " + item);
                Items.Remove(item);
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
            var itemTypes = new List<TraderItemRefreshItem>();

            // list up all item types not yet in inventory
            foreach (TraderItemRefreshItem item in itemRefreshs)
            {
                if (!Items.Contains(item.Type.Object.ID) &&
                    !(Root.Rules == Burntime.Remaster.Logic.Generation.RuleSet.Extended &&
                      AI.AiItemPool.IsFirearm(item.Type.Object) &&
                      Items.Any(stock => AI.AiItemPool.IsFirearm(stock.Type))) &&
                    IsBelowTraderWorldLimit(item.Type.Object, Root.World.AllItems))
                {
                    itemTypes.Add(item);
                }
            }

            // no more item types to choose from
            if (itemTypes.Count == 0)
                return null;
            // only one item type, skip randomizer
            else if (itemTypes.Count == 1)
            {
                return itemTypes[0].Type.Object;
            }

            int totalRate = itemTypes.Sum(item => System.Math.Max(1, item.Rate));
            int index = SelectWeightedIndex(
                itemTypes.Select(item => item.Rate).ToArray(),
                Platform.Math.Random.Next(totalRate));
            return itemTypes[index].Type.Object;
        }

        internal static int SelectWeightedIndex(IReadOnlyList<int> rates, int selection)
        {
            for (int i = 0; i < rates.Count; i++)
            {
                selection -= System.Math.Max(1, rates[i]);
                if (selection < 0)
                    return i;
            }
            return System.Math.Max(0, rates.Count - 1);
        }

        internal static bool IsBelowTraderWorldLimit(ItemType type, IEnumerable<Item> allItems)
        {
            if (type.TraderWorldLimit <= 0)
                return true;

            string group = string.IsNullOrEmpty(type.TraderWorldGroup)
                ? type.ID
                : type.TraderWorldGroup;
            int count = allItems.Count(item =>
                (string.IsNullOrEmpty(item.Type.TraderWorldGroup)
                    ? item.Type.ID
                    : item.Type.TraderWorldGroup) == group);
            return count < type.TraderWorldLimit;
        }

        internal static bool CanRemoveFromStock(Item item) =>
            item.Type.TraderWorldLimit <= 0;

        private ClassicGame Root => (ClassicGame)Container.Root;
    }
}
