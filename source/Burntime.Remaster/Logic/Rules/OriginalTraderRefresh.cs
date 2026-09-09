using System;
using System.Linq;

namespace Burntime.Remaster.Logic.Rules;

internal static class OriginalTraderRefresh
{
    internal static void Dos(ItemList stock, ItemType[] assortment, int day,
        Func<ItemType, int> title, Func<int, int> random, Action<ItemType> add, ItemType meat)
    {
        Item[] Ordered() => stock.OrderBy(i => i.OriginalRecordSlot).ToArray();
        Item? food = Ordered().FirstOrDefault(i => title(i.Type) is >= 0x33 and <= 0x36);
        if (food != null) stock.Remove(food);
        int[] core = assortment.Take(4).Select(title).ToArray();
        Item? other = Ordered().FirstOrDefault(i => !core.Contains(title(i.Type)));
        if (other != null) stock.Remove(other);

        foreach (ItemType type in assortment.Take(6))
        {
            Item? existing = Ordered().FirstOrDefault(i => title(i.Type) == title(type));
            if (existing != null)
            {
                if (random(8) == 0) stock.Remove(existing);
            }
            else if (title(type) % 4 == day % 4)
                add(type);
        }
        if (day % 2 == 0) add(meat);
    }

    internal static void Amiga(ItemList stock, ItemType[] assortment, int day,
        Func<ItemType, int> title, Func<int, int> random, Action<ItemType> add, ItemType meat)
    {
        AmigaRemove(new[] { stock }, day, title, random);
        AmigaRestock(assortment, day, title, add, meat);
    }

    internal static void AmigaRemove(ItemList[] stocks, int day,
        Func<ItemType, int> title, Func<int, int> random)
    {
        // 0x6ffc: both passes cover every trader before any trader restocks.
        foreach (int offset in new[] { 5, 0 })
            foreach (var entry in stocks.SelectMany(stock => stock.Select(item => (stock, item)))
                .OrderBy(e => e.item.OriginalRecordSlot).ToArray())
            {
                int id = title(entry.item.Type);
                if (id < 0x37 || (id % 8 == (day + offset) % 8 && random(4) != 0))
                    entry.stock.Remove(entry.item);
            }
    }

    internal static void AmigaRestock(ItemType[] assortment, int day,
        Func<ItemType, int> title, Action<ItemType> add, ItemType meat)
    {
        foreach (ItemType type in assortment.Take(6))
            if (title(type) % 8 == day % 8) add(type);
        if (day % 4 == 0) add(meat);
    }
}
