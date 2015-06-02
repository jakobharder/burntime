
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
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Framework.States;
using Burntime.Classic.Maps;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Room : StateObject, IMapObject
    {
        StateLink<Condition> entryCondition;
        StateLink<ItemList> items;
        bool isWaterSource;
        string titleId;

        public string TitleId
        {
            get { return titleId; }
            set { titleId = value; }
        }

        public ItemList Items
        {
            get { return items; }
            set { items = value; }
        }

        public bool IsWaterSource
        {
            get { return isWaterSource; }
            set { isWaterSource = value; }
        }

        public Condition EntryCondition
        {
            get { return entryCondition == null ? null : entryCondition; }
            set { entryCondition = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            items = container.Create<ItemList>();
            entryCondition = container.Create<Condition>();

            base.InitInstance(parameter);
        }

        public Vector2 MapPosition
        {
            get { return EntryCondition.RegionOnMap.Center; }
        }

        public String GetTitle(ResourceManager resourceManager)
        {
            return resourceManager.GetString(titleId);
        }
    }
}
