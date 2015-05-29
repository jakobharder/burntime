
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
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;

using Burntime.Classic.Logic;

namespace Burntime.Classic
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
