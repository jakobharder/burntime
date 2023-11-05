using Burntime.Platform;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Remaster
{
    public class PickItemList : IItemCollection
    {
        float range;
        DroppedItemList list;
        int count;
        Vector2 position;

        public PickItemList(DroppedItemList list, Vector2 position, float range)
        {
            this.range = range;
            this.list = list;
            this.position = position;
            UpdateCount();
        }

        public bool Add(Item item)
        {
            bool res = list.Add(item);
            UpdateCount();
            return res;
        }

        public void Remove(Item item)
        {
            list.Remove(item);
            UpdateCount();
        }

        public int Count
        {
            get { return count; }
        }

        public Item this[int index]
        {
            get
            {
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if ((list.MapObjects[i].Position - position).Length <= range)
                    {
                        if (n == index)
                            return list[i];
                        n++;
                    }
                }

                return null;

            }
            set
            {
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if ((list.MapObjects[i].Position - position).Length <= range)
                    {
                        if (n == index)
                        {
                            list[i] = value;
                            return;
                        }
                        n++;
                    }
                }

            }
        }

        public bool Contains(Item item)
        {
            throw new NotSupportedException();
        }

        void UpdateCount()
        {
            count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list.MapObjects[i].Position - position).Length <= range)
                    count++;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => new ItemCollectionEnumerator(this);
        IEnumerator<Item> IEnumerable<Item>.GetEnumerator() => new ItemCollectionEnumerator(this);
    }
}