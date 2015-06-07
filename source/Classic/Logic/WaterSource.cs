
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
using Burntime.Framework.States;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class WaterSource : StateObject
    {
        protected int capacity;
        protected int reserve;
        protected int water;

        protected StateLink<Location> location;

        public int Capacity
        {
            get { return capacity; }
            set { capacity = value; }
        }

        public int Reserve
        {
            get { return reserve; }
            set { reserve = value; }
        }

        public int Water
        {
            get 
            { 
                return water + GetBoost(); 
            }
            set 
            { 
                water = value - GetBoost(); 
            }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new Burntime.Framework.BurntimeLogicException();

            location = parameter[0] as Location;
        }

        public void BeginTurn()
        {
            Reserve += Water;
        }

        public void EndTurn()
        {
            if (Reserve > Capacity)
                Reserve = Capacity;
        }

        public int GetBoost()
        {
            int boost = 0;

            // at game start objects can be null
            if (location.Object == null || location.Object.Rooms == null)
                return 0;

            // find water source
            Room source = null;
            foreach (Room room in location.Object.Rooms)
            {
                if (room.IsWaterSource)
                {
                    source = room;
                    break;
                }
            }

            if (source == null)
                return 0;

            // find pumps at source
            bool handPump = null != source.Items.Find("item_hand_pump");
            bool pump = null != source.Items.Find("item_industrial_pump");

            if (pump)
            {
                if (water == 1)
                    boost = 5;
                else
                    boost = water / 2;
            }
            else if (handPump)
            {
                if (water == 1)
                    boost = 2;
                else
                    boost = water / 4;
            }

            return boost;
        }

        public bool RefillItem(Item item)
        {
            if (item.Type.Full != null && item.Type.Full.WaterValue != 0)
            {
                if (Reserve >= item.Type.Full.WaterValue)
                {
                    Reserve -= item.Type.Full.WaterValue;
                    item.MakeFull();
                    return true;
                }
            }

            return false;
        }
    }
}