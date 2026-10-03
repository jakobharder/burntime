using System;
using Burntime.Platform;
using Burntime.Framework.GUI;

namespace Burntime.Remaster.GUI;

internal static class TouchChoiceRows
{
    // All rows share horizontal bounds; overlapping padding is divided by row centers.
    internal static int HitTest(Vector2 position, int left, int width,
        int top, int rowHeight, int rowSpacing, int count, Func<int, bool> isVisible)
    {
        if (count <= 0 || rowHeight <= 0 || rowSpacing <= 0)
            return -1;
        Rect horizontal = TouchHitTest.Expand(new Rect(left, top, width, rowHeight),
            TouchHitTest.MinimumSize);
        if (position.x < horizontal.Left || position.x >= horizontal.Left + horizontal.Width)
            return -1;

        int selected = -1;
        int distance = int.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!isVisible(i))
                continue;
            Rect row = TouchHitTest.Expand(
                new Rect(left, top + rowSpacing * i, width, rowHeight),
                TouchHitTest.MinimumSize);
            int candidateDistance = System.Math.Abs(position.y - row.Center.y);
            if (position.y >= row.Top && position.y < row.Top + row.Height &&
                candidateDistance < distance)
            {
                selected = i;
                distance = candidateDistance;
            }
        }
        return selected;
    }
}
