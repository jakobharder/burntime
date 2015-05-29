
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