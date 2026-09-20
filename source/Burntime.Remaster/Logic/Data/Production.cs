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
        public int MaintenanceBonus;
        public bool IsCampStarving;
    }

    readonly public int ID;

    int[] ProductionPerDay = [0];
    int[] ProductionPerDay2Person = [0];
    int MaxCombination = 0;
    readonly StateLink<ItemType> produce;

    public ItemType Produce => produce;
    public int MaxToolCount => MaxCombination;
    [field: NonSerialized]
    public bool AllowInventory { get; private set; }

    public Production(int maxCombi, int[] perDay, int[] perDayDouble, ItemType produce, int id, bool allowInventory = false)
    {
        ApplySettings(maxCombi, perDay, perDayDouble, allowInventory);
        this.produce = produce;
        ID = id;
    }

    // Keep serialized field names and object identities for existing saves.
    internal void ApplySettings(int maxCombi, int[] perDay, int[] perDayDouble, bool allowInventory = false)
    {
        AllowInventory = allowInventory;
        MaxCombination = maxCombi;
        ProductionPerDay = perDay;
        ProductionPerDay2Person = perDayDouble.Length == 0 ? perDay : perDayDouble;
    }

    public Rate GetRate(int toolCount, int npcCount, int maintenanceBonus = 0)
    {
        int trapCount = Math.Min(toolCount, MaxCombination);
        int foodPerDay = (npcCount >= 2) ? ProductionPerDay2Person[trapCount] : ProductionPerDay[trapCount];

        var info = new Rate()
        {
            MaintenanceBonus = foodPerDay > 0 ? Math.Max(0, maintenanceBonus) : 0
        };
        info.FoodPerDay = foodPerDay + info.MaintenanceBonus;

        int remainingPerDay = info.FoodPerDay - npcCount;
        if (remainingPerDay > 0)
            info.ItemDropInterval = info.FoodPerDay > npcCount ? Produce.FoodValue / (float)remainingPerDay : 0;
        else if (remainingPerDay < 0)
            info.IsCampStarving = true;
        return info;
    }
}
