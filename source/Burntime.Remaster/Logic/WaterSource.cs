using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Remaster.Logic
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
                boost = System.Math.Max(5, water / 2);
            }
            else if (handPump)
            {
                boost = System.Math.Max(2, water / 4);
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