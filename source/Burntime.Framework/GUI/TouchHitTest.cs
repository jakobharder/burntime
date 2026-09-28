using Burntime.Platform;

namespace Burntime.Framework.GUI;

public static class TouchHitTest
{
    public const int MinimumSize = 25;

    public static Rect Expand(Rect bounds, int minimum)
    {
        var size = new Vector2(System.Math.Max(bounds.Width, minimum), System.Math.Max(bounds.Height, minimum));
        return new Rect(bounds.Center - size / 2, size);
    }

    internal static Window? Prefer(Window? current, Window? candidate, Vector2 position)
    {
        if (candidate == null) return current;
        if (current == null) return candidate;
        Rect currentBounds = Bounds(current), candidateBounds = Bounds(candidate);
        bool currentExact = currentBounds.PointInside(position), candidateExact = candidateBounds.PointInside(position);
        if (currentExact != candidateExact) return candidateExact ? candidate : current;
        // Keep the topmost exact target (particularly overlapping inventory items).
        if (currentExact) return current;
        return (position - candidateBounds.Center).Length < (position - currentBounds.Center).Length ? candidate : current;
    }

    internal static Rect Bounds(Window window) =>
        window.Boundings + (window.PositionOnScreen - window.Position);
}
