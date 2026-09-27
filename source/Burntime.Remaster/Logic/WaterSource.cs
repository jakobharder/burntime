using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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

        internal int BaseWater => water;

        public int Water
        {
            get => Root.RuleBook.CalculateWaterOutput(water, HasHandPump, HasIndustrialPump);
            set => water = value;
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
            return Water - water;
        }

        Room? SourceRoom => location.Object?.Rooms?.FirstOrDefault(room => room.IsWaterSource);
        bool HasHandPump => SourceRoom?.Items.Find("item_hand_pump") != null;
        bool HasIndustrialPump => SourceRoom?.Items.Find("item_industrial_pump") != null;
        ClassicGame Root => (ClassicGame)Container.Root;

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
