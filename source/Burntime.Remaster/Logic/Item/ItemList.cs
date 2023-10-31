using Burntime.Data.BurnGfx;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Remaster
{
    public class ItemArgs : EventArgs
    {
        public Item Item
        {
            get { return item; }
        }

        protected Item item;

        public ItemArgs(Item item)
        {
            this.item = item;
        }
    }

    [Serializable]
    public class ItemList : StateObject, IItemCollection
    {
        StateLinkList<Item> list;
        int max = Infinite;

        [NonSerialized]
        public EventHandler<ItemArgs> RemovedItem;

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

        public bool Move(IItemCollection ItemList)
        {
            while (ItemList.Count != 0)
            {
                if (!Add(ItemList[0]))
                    return false;
                ItemList.Remove(ItemList[0]);
            }

            return true;
        }

        public bool MoveTo(IItemCollection ItemList)
        {
            while (Count != 0)
            {
                if (!ItemList.Add(list[0]))
                    return false;
                Remove(list[0]);
            }

            return true;
        }

        public void Remove(Item Item)
        {
            if (RemovedItem != null)
                RemovedItem.Invoke(this, new ItemArgs(list[0]));
            list.Remove(Item);
        }

        public const int Infinite = -1;
        public int MaxCount
        {
            get { return max; }
            set { max = value; }
        }

        public bool IsFull
        {
            get { return Count != Infinite && MaxCount <= Count; }
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

        // IEnumerable interface implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ItemCollectionEnumerator(this);
        }

        // for debug
        public override string ToString()
        {
            string text = "[" + list.Count + "]";
            foreach (var item in this)
                text += " \"" + item.ToString() + "\"";
            return text;
        }

        public Item? FindBestWeapon(Item? current = null)
        {
            foreach (Item item in this)
            {
                if (item.DamageValue > 0)
                {
                    if (current is null
                        || (current.DamageValue < item.DamageValue && !item.ConsumesAmmo))
                        current = item;
                }
            }
            return current;
        }

        public Item? FindBestDefense(Item? current = null)
        {
            foreach (Item item in this)
            {
                if (item.DefenseValue > 0)
                    current ??= item;
            }
            return current;
        }

        public Item? FindBestProtection(Item? current, string dangerType)
        {
            foreach (Item item in this)
            {
                var protection = item.Type.GetProtection(dangerType);
                if (protection is not null && protection.Rate > (current?.Type.GetProtection(dangerType)?.Rate ?? 0))
                    current = item;
            }
            return current;
        }
    }
}
