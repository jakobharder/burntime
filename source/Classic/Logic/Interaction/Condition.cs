
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
