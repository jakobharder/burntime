using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework.States;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Production : StateObject
    {
        public int ID;

        public int[] ProductionPerDay;
        public int[] ProductionPerDay2Person;
        public int MaxCombination;
        protected StateLink<ItemType> produce;

        public ItemType Produce
        {
            get { return produce; }
            set { produce = value; }
        }

        public float GetInterval(int toolCount, int npcCount)
        {
            int ppd = GetProductionPerDay(toolCount, npcCount);
            ppd -= npcCount;
            if (ppd <= 0)
                return 0;

            return (float)Produce.FoodValue / (float)ppd;
        }

        public int GetProductionPerDay(int toolCount, int npcCount)
        {
            if (npcCount >= 2)
                return ProductionPerDay2Person[Math.Min(toolCount, MaxCombination)];
            else
                return ProductionPerDay[Math.Min(toolCount, MaxCombination)];
        }
    }
}
