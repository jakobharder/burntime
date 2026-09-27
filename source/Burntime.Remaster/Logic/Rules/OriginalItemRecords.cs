using System;
using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.Logic.Rules;

// Approximate original item rules using deterministic local collection order.
internal static class OriginalItemRecords
{
    internal static void AddTraderStock(Trader trader, ItemType type)
    {
        Item item = type.Generate();
        // Direct original allocation bypasses the human carrying limit.
        int limit = trader.Items.MaxCount;
        try
        {
            trader.Items.MaxCount = ItemList.Infinite;
            trader.Items.Add(item);
        }
        finally { trader.Items.MaxCount = limit; }
    }

    internal static void CleanupDos(Location location, int difficulty, Func<Item, int> title)
    {
        if (difficulty == 0) return;
        var candidates = location.Items.Select(i => (Owner: (IItemCollection)location.Items, Item: i))
            .Concat(location.Rooms.SelectMany(r => r.Items
                .Where(i => difficulty >= 2 && (title(i) < 0x37 || title(i) > 0x47))
                .Select(i => (Owner: (IItemCollection)r.Items, Item: i))))
            .ToArray();
        if (candidates.Length != 0)
            candidates[0].Owner.Remove(candidates[0].Item);
    }

    internal static void CleanupAmiga(Location location, Character[] party, Func<Item, int> title,
        int productionTool)
    {
        Sweep(location.Items);
        if (location.Player == null || location.Player == party.FirstOrDefault()?.Player)
        {
            // Room owner encoding covers all rooms at a location in the original.
            var roomItems = location.Rooms.SelectMany(r => r.Items.Select(i =>
                (Owner: (IItemCollection)r.Items, Item: i)));
            SweepRecords(roomItems);
        }

        void Sweep(IItemCollection owner) =>
            SweepRecords(owner.Select(i => (Owner: owner, Item: i)));

        void SweepRecords(IEnumerable<(IItemCollection Owner, Item Item)> source)
        {
            var records = source.ToArray();
            int cursor = 0;
            bool discardedOther = false;
            foreach (Character character in party)
            {
                int free = Math.Max(0, 6 - character.Items.Count);
                while (free > 0 && cursor < records.Length)
                {
                    var entry = records[cursor++];
                    int id = title(entry.Item);
                    if (id is >= 0x33 and < 0x37)
                    {
                        entry.Owner.Remove(entry.Item); // Food does not spend a carrying slot.
                        continue;
                    }
                    if (id == productionTool) continue;
                    if (id is >= 0x3d and < 0x42)
                    {
                        if (!character.Items.Add(entry.Item)) continue;
                        entry.Owner.Remove(entry.Item);
                    }
                    else if (id is >= 0x43 and <= 0x45 || !discardedOther)
                        entry.Owner.Remove(entry.Item);
                    else continue;
                    discardedOther = true;
                    free--;
                }
            }
        }
    }

}
