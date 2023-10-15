using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;

using Burntime.Remaster.Logic;

namespace Burntime.Remaster
{
    [Serializable]
    public class DroppedItemList : StateObject, IItemCollection
    {
        StateLinkList<DroppedItem> list;

        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            list = Container.CreateLinkList<DroppedItem>();
        }

        Vector2 insertPosition = Vector2.Zero;
        public Vector2 DropPosition
        {
            get { return insertPosition; }
            set { insertPosition = value; }
        }

        public void DropAt(Item Item, Vector2 Position)
        {
            DroppedItem item = Container.Create<DroppedItem>();
            item.Position = Position;
            item.Item = Item;
            list.Add(item);
        }

        public bool Add(Item Item)
        {
            DroppedItem item = Container.Create<DroppedItem>();
            item.Position = DropPosition;
            item.Item = Item;
            list.Add(item);
            return true;
        }

        public void Remove(Item Item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if ((Item)list[i].Item == Item)
                {
                    list.Remove(list[i]);
                    break;
                }
            }
        }

        public int Count
        {
            get { return list.Count; }
        }

        public Item this[int Index]
        {
            get { return list[Index].Item; }
            set { list[Index].Item = value; }
        }

        public bool Contains(Item item)
        {
            throw new NotSupportedException();
        }

        public StateLinkList<DroppedItem> MapObjects
        {
            get { return list; }
        }

        // IEnumerable interface implementation
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new ItemCollectionEnumerator(this);
        }
    }
}
