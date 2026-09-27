using System.Collections.Generic;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Tests;

using static Program;

static class TraderDistributionTests
{
    internal static IEnumerable<Case<int>> DistributionCases()
    {
        yield return Int("assortment weights parse with backwards-compatible default", 0, () =>
        {
            TraderAssortmentSetting weighted = TraderAssortmentSetting.Parse("item_meat:6");
            Equal("item_meat", weighted.ItemId, "weighted item");
            Equal(6, weighted.Rate, "weighted rate");

            TraderAssortmentSetting legacy = TraderAssortmentSetting.Parse("item_knife");
            Equal("item_knife", legacy.ItemId, "legacy item");
            Equal(1, legacy.Rate, "legacy rate");
            return 0;
        });

        yield return Int("weighted selection honors rate boundaries", 0, () =>
        {
            int[] rates = { 5, 2, 1 };
            Equal(0, Trader.SelectWeightedIndex(rates, 0), "first start");
            Equal(0, Trader.SelectWeightedIndex(rates, 4), "first end");
            Equal(1, Trader.SelectWeightedIndex(rates, 5), "second start");
            Equal(1, Trader.SelectWeightedIndex(rates, 6), "second end");
            Equal(2, Trader.SelectWeightedIndex(rates, 7), "third");
            return 0;
        });

        yield return Int("world-limited assortment waits for scarcity", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            Item existing = TestItem(manager, "item_mine_detector",
                traderWorldGroup: "metal_detector", traderWorldLimit: 1);
            Item candidate = TestItem(manager, "item_defective_mine_detector",
                traderWorldGroup: "metal_detector", traderWorldLimit: 1);
            Equal(false, Trader.IsBelowTraderWorldLimit(candidate.Type, new[] { existing }),
                "limited stock while group exists");
            Equal(true, Trader.IsBelowTraderWorldLimit(candidate.Type, System.Array.Empty<Item>()),
                "stock returns below limit");
            return 0;
        });

        yield return Int("world-limited trader stock survives refresh", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            Item limited = TestItem(manager, "item_bible",
                traderWorldGroup: "bible", traderWorldLimit: 2);
            Item ordinary = TestItem(manager, "item_meat");
            Equal(false, Trader.CanRemoveFromStock(limited), "limited item retained");
            Equal(true, Trader.CanRemoveFromStock(ordinary), "ordinary item rotates");
            return 0;
        });
    }
}
