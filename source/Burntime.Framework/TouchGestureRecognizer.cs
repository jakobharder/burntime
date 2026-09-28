using Burntime.Platform;

namespace Burntime.Framework;

public enum TouchGestureKind { Tap, LongPress, Drag, HoldMove, HoldEnd, Cancel }

public readonly record struct TouchGesture(TouchGestureKind Kind, Vector2 Origin, Vector2 Position, Vector2 Delta, double Timestamp = 0);

/// <summary>Single-contact gestures. Coordinates/tolerance use the same units; time is monotonic seconds.</summary>
public sealed class TouchGestureRecognizer
{
    public double HoldSeconds { get; set; } = 0.5;
    public float MovementTolerance { get; set; } = 10;
    bool active, dragging, consumed;
    Vector2 origin, previous;
    double started;

    public void Begin(Vector2 position, double now)
    {
        active = true;
        dragging = consumed = false;
        origin = previous = position;
        started = now;
    }

    public TouchGesture? Move(Vector2 position, double now)
    {
        if (!active) return null;
        if (consumed)
        {
            var holdDelta = position - previous;
            previous = position;
            return holdDelta == Vector2.Zero ? null : new(TouchGestureKind.HoldMove, origin, position, holdDelta);
        }
        if (!dragging && (position - origin).Length > MovementTolerance)
            dragging = true;
        if (dragging)
        {
            var delta = position - previous;
            previous = position;
            return delta == Vector2.Zero ? null : new(TouchGestureKind.Drag, origin, position, delta);
        }
        // Retain the original position until a drag starts so its initial delta is complete.
        if (now - started < HoldSeconds) return null;
        consumed = true;
        previous = position;
        return new(TouchGestureKind.LongPress, origin, origin, Vector2.Zero);
    }

    public TouchGesture? End(Vector2 position, double now)
    {
        if (!active) return null;
        // A hold that wasn't displayed before release must not open a persistent
        // tooltip or execute an unseen action after a slow frame.
        if (consumed || (!dragging && now - started >= HoldSeconds &&
            (position - origin).Length <= MovementTolerance))
        {
            Cancel();
            return new(TouchGestureKind.HoldEnd, origin, position, Vector2.Zero);
        }
        var final = Move(position, now);
        if (final is null && !dragging && !consumed)
            final = new(TouchGestureKind.Tap, origin, position, Vector2.Zero);
        Cancel();
        return final;
    }

    public void Cancel() => active = dragging = consumed = false;
}
