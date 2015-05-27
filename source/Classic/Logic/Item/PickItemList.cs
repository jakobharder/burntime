using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;

using Burntime.Classic.Logic;

namespace Burntime.Classic
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

        // IEnumerable interface implementation
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new ItemCollectionEnumerator(this);
        }
    }
}