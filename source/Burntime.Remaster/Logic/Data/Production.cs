using Burntime.Framework.States;
using System;

namespace Burntime.Remaster.Logic;

[Serializable]
public sealed class Production : StateObject
{
    public struct Rate
    {
        public float ItemDropInterval;
        public int FoodPerDay;
        public bool IsCampStarving;
    }

    readonly public int ID;

    readonly int[] ProductionPerDay;
    readonly int[] ProductionPerDay2Person;
    readonly int MaxCombination;
    readonly StateLink<ItemType> produce;

    public ItemType Produce => produce;

    public Production(int maxCombi, int[] perDay, int[] perDayDouble, ItemType produce, int id)
    {
        MaxCombination = maxCombi;
        ProductionPerDay = perDay;
        ProductionPerDay2Person = perDayDouble;
        if (ProductionPerDay2Person.Length == 0)
            ProductionPerDay2Person = ProductionPerDay;
        this.produce = produce;
        ID = id;
    }

    public Rate GetRate(int toolCount, int npcCount)
    {
        int trapCount = Math.Min(toolCount, MaxCombination);

        var info = new Rate()
        {
            FoodPerDay = (npcCount >= 2) ? ProductionPerDay2Person[trapCount] : ProductionPerDay[trapCount]
        };
        int remainingPerDay = info.FoodPerDay - npcCount;
        if (remainingPerDay > 0)
            info.ItemDropInterval = info.FoodPerDay > npcCount ? Produce.FoodValue / (float)remainingPerDay : 0;
        else if (remainingPerDay < 0)
            info.IsCampStarving = true;
        return info;
    }
}
