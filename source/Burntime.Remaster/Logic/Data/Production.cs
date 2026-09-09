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

    int[] ProductionPerDay;
    int[] ProductionPerDay2Person;
    int MaxCombination;
    readonly StateLink<ItemType> produce;

    public ItemType Produce => produce;
    public int MaxToolCount => MaxCombination;
    [System.Runtime.Serialization.OptionalField]
    bool allowInventory;
    public bool AllowInventory => allowInventory;

    public Production(int maxCombi, int[] perDay, int[] perDayDouble, ItemType produce, int id, bool allowInventory = false)
    {
        ApplySettings(maxCombi, perDay, perDayDouble, allowInventory);
        this.produce = produce;
        ID = id;
    }

    // Keep serialized field names and object identities for existing saves.
    internal void ApplySettings(int maxCombi, int[] perDay, int[] perDayDouble, bool allowInventory = false)
    {
        this.allowInventory = allowInventory;
        MaxCombination = maxCombi;
        ProductionPerDay = perDay;
        ProductionPerDay2Person = perDayDouble.Length == 0 ? perDay : perDayDouble;
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
