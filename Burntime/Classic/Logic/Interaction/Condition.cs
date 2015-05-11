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
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Condition : StateObject
    {
        StateLink<ItemType> requiredItem = null;
        float maxDistanceOnMap = 0;
        Rect regionOnMap;
        bool hasRegionOnMap = false;
        bool removeItem = false;

        public ItemType RequiredItem
        {
            get { return requiredItem == null ? null : requiredItem; }
            set { requiredItem = value; }
        }

        public float MaxDistanceOnMap
        {
            get { return maxDistanceOnMap; }
            set { maxDistanceOnMap = value; }
        }

        public Rect RegionOnMap
        {
            get { return regionOnMap; }
            set { regionOnMap = value; }
        }

        public bool HasRegionOnMap
        {
            get { return hasRegionOnMap; }
            set { hasRegionOnMap = value; }
        }

        public bool RemoveItem
        {
            get { return removeItem; }
            set { removeItem = value; }
        }

        public bool Process(Character character)
        {
            if (maxDistanceOnMap > 0 && hasRegionOnMap)
            {
                if (maxDistanceOnMap < regionOnMap.Distance(character.Position))
                    return false;
            }

            if (RequiredItem != null)
            {
                Item item = character.Items.Find(RequiredItem);
                if (item != null)
                {
                    if (removeItem)
                        character.Items.Remove(item);

                    return true;
                }
                else
                    return false;
            }

            return true;
        }
    }
}
