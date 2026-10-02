using System;
using System.Linq;

namespace Burntime.Remaster.Logic.Generation;

internal static class PlayerColorSetup
{
    internal static BurntimePlayerColor[] Assign(BurntimePlayerColor first, BurntimePlayerColor second)
    {
        var colors = Enum.GetValues<BurntimePlayerColor>();
        if (!colors.Contains(first)) first = BurntimePlayerColor.Green;
        if (!colors.Contains(second) || first == second)
            second = colors.First(color => color != first);
        return new[] { first, second }.Concat(colors.Where(color => color != first && color != second)).ToArray();
    }

    internal static BurntimePlayerColor OtherSelection(BurntimePlayerColor previous,
        BurntimePlayerColor selected, BurntimePlayerColor other) => selected == other ? previous : other;

    internal static int IconId(BurntimePlayerColor color) => color switch
    {
        BurntimePlayerColor.Red => 1,
        BurntimePlayerColor.Blue => 3,
        BurntimePlayerColor.Black => 2,
        _ => 0
    };

    internal static int FlagId(BurntimePlayerColor color) => color switch
    {
        BurntimePlayerColor.Red => 8,
        BurntimePlayerColor.Blue => 12,
        BurntimePlayerColor.Black => 4,
        _ => 0
    };

    internal static int BodyColorSet(BurntimePlayerColor color) => color switch
    {
        BurntimePlayerColor.Red => 0,
        BurntimePlayerColor.Blue => -1,
        BurntimePlayerColor.Black => 1,
        _ => 2
    };
}
