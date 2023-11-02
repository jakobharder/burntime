using Burntime.Framework.States;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Remaster
{
    [Serializable]
    public class RoomItemList : StateObject, IItemCollection
    {
        StateLinkList<Item> list;
        int max = Infinite;

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            list = Container.CreateLinkList<Item>();
        }

        public bool Add(Item Item)
        {
            if (max != Infinite && list.Count >= max)
                return false;

            list.Add(Item);
            return true;
        }

        public void Remove(Item Item)
        {
            list.Remove(Item);
        }

        public const int Infinite = -1;
        public int MaxCount
        {
            get { return max; }
            set { max = value; }
        }

        public int Count
        {
            get { return list.Count; }
        }

        public Item this[int Index]
        {
            get { return list[Index]; }
            set { list[Index] = value; }
        }

        public bool Contains(Item item)
        {
            return list.Contains(item);
        }

        IEnumerator IEnumerable.GetEnumerator() => new ItemCollectionEnumerator(this);
        IEnumerator<Item> IEnumerable<Item>.GetEnumerator() => new ItemCollectionEnumerator(this);
    }
}
