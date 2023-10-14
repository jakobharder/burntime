using System.Collections;

namespace Burntime.Remaster
{
    #region public IItemCollection interface
    /// <summary>
    /// Item collection interface.
    /// </summary>
    public interface IItemCollection : IEnumerable
    {
        /// <summary>
        /// Item count.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Items.
        /// </summary>
        /// <param name="index">index</param>
        /// <returns>item at index</returns>
        Item this[int index] { get; set; }
        
        /// <summary>
        /// Add item to collection.
        /// </summary>
        /// <param name="item">item to add</param>
        /// <returns>true if item was added</returns>
        bool Add(Item item);

        /// <summary>
        /// Remove item from collection.
        /// </summary>
        /// <param name="item">item to remove</param>
        void Remove(Item item);

        /// <summary>
        /// Check if item is in collection.
        /// </summary>
        /// <param name="item">item to check</param>
        /// <returns>true if contained</returns>
        bool Contains(Item item);
    }
    #endregion

    #region public IItemCollection extensions
    /// <summary>
    /// Extention class for IItemCollection.
    /// </summary>
    public static class ItemCollectionExtensions
    {
        /// <summary>
        /// Add all items from collection.
        /// </summary>
        /// <param name="me">collection to add items to</param>
        /// <param name="collection">collection to add items from</param>
        /// <returns>true if all items were added</returns>
        public static bool Add(this IItemCollection me, IItemCollection collection)
        {
            foreach (Item item in collection)
            {
                if (!me.Add(item))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Remove all items contained in both collections.
        /// </summary>
        /// <param name="me">collection to remove items from</param>
        /// <param name="collection">collection with items to remove from me</param>
        public static void Remove(this IItemCollection me, IItemCollection collection)
        {
            for (int i = 0; i < collection.Count; i++)
                me.Remove(collection[i]);
        }

        /// <summary>
        /// Remove all items.
        /// </summary>
        /// <param name="me">collection to remove items from</param>
        public static void Clear(this IItemCollection me)
        {
            while (me.Count > 0)
                me.Remove(me[0]);
        }

        /// <summary>
        /// Check wether collection contains a specific item type.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <param name="type">item type id</param>
        /// <returns>true if contained</returns>
        public static bool Contains(this IItemCollection me, string type)
        {
            for (int i = 0; i < me.Count; i++)
            {
                if (me[i].Type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get item with specific item type.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <param name="type">item type id</param>
        /// <returns>item or null</returns>
        public static Item Find(this IItemCollection me, string type)
        {
            for (int i = 0; i < me.Count; i++)
            {
                if (me[i].Type == type)
                    return me[i];
            }

            return null;
        }

        /// <summary>
        /// Get count of item type in collection.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <param name="type">item type id</param>
        /// <returns>count</returns>
        public static int GetCount(this IItemCollection me, string type)
        {
            int count = 0;

            for (int i = 0; i < me.Count; i++)
            {
                if (me[i].Type.ID == type)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Eat value for restaurants.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <returns>eat value</returns>
        public static int GetEatValue(this IItemCollection me)
        {
            float v = 0;
            foreach (Item item in me)
                v += item.EatValue;
            return (int)v;
        }

        /// <summary>
        /// Drink value for pubs
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <returns>drink value</returns>
        public static int GetDrinkValue(this IItemCollection me)
        {
            float v = 0;
            foreach (Item item in me)
                v += item.DrinkValue;
            return (int)v;
        }

        /// <summary>
        /// Trade value.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <returns>trade value</returns>
        public static int GetTradeValue(this IItemCollection me)
        {
            float v = 0;
            foreach (Item item in me)
                v += item.TradeValue;
            return (int)v;
        }

        /// <summary>
        /// Heal value for doctors.
        /// </summary>
        /// <param name="me">collection of items</param>
        /// <returns>heal value</returns>
        public static int GetHealValue(this IItemCollection me)
        {
            float v = 0;
            foreach (Item item in me)
                v += item.HealValue;
            return (int)v;
        }
    }
    #endregion

    #region internal IItemCollection enumerator
    /// <summary>
    /// IItemCollection enumerator.
    /// </summary>
    internal sealed class ItemCollectionEnumerator : IEnumerator
    {
        IItemCollection collection;
        int current;

        public ItemCollectionEnumerator(IItemCollection collection)
        {
            this.collection = collection;
            Reset();
        }

        public void Reset()
        {
            current = -1;
        }

        public bool MoveNext()
        {
            current++;
            return (current < collection.Count);
        }

        public object Current
        {
            get { return collection[current]; }
        }
    }
    #endregion
}