/*
 *  Burntime Classic
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

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
        int titleId;

        public int TitleId
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
            return resourceManager.GetString("burn?" + titleId);
        }
    }
}
