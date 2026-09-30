using Burntime.Platform;

namespace Burntime.Framework;

public enum TouchGestureKind { Press, Tap, LongPress, Drag, DragEnd, HoldMove, HoldEnd, Cancel }

public readonly record struct TouchGesture(TouchGestureKind Kind, Vector2 Origin,
    Vector2 Position, Vector2 Delta, double Timestamp = 0,
    Vector2f Velocity = default);

/// <summary>Single-contact gestures. Coordinates/tolerance use the same units; time is monotonic seconds.</summary>
public sealed class TouchGestureRecognizer
{
    public double HoldSeconds { get; set; } = 0.5;
    public float MovementTolerance { get; set; } = 10;
    bool active, dragging, consumed;
    Vector2 origin, previous;
    Vector2f velocity;
    double started, previousSampleTime, lastMovementTime;

    const double VelocitySmoothingSeconds = 0.06;
    const double ReleasePauseSeconds = 0.08;

    public TouchGesture Begin(Vector2 position, double now)
    {
        active = true;
        dragging = consumed = false;
        origin = previous = position;
        velocity = Vector2f.Zero;
        started = previousSampleTime = lastMovementTime = now;
        return new(TouchGestureKind.Press, position, position, Vector2.Zero, now);
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
            UpdateVelocity(delta, now);
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
        if (!dragging && (position - origin).Length > MovementTolerance)
            dragging = true;

        TouchGesture final;
        if (dragging)
        {
            Vector2 delta = position - previous;
            UpdateVelocity(delta, now);
            if (now - lastMovementTime > ReleasePauseSeconds)
                velocity = Vector2f.Zero;
            final = new(TouchGestureKind.DragEnd, origin, position, delta, now, velocity);
        }
        else
            final = new(TouchGestureKind.Tap, origin, position, Vector2.Zero);
        Cancel();
        return final;
    }

    void UpdateVelocity(Vector2 delta, double now)
    {
        if (delta == Vector2.Zero)
            return;

        double elapsed = now - previousSampleTime;
        if (elapsed > 0)
        {
            Vector2f instantaneous = (Vector2f)delta / (float)elapsed;
            float weight = velocity == Vector2f.Zero
                ? 1
                : 1 - (float)System.Math.Exp(-elapsed / VelocitySmoothingSeconds);
            velocity = velocity * (1 - weight) + instantaneous * weight;
        }
        previousSampleTime = lastMovementTime = now;
    }

    public void Cancel() => active = dragging = consumed = false;
}
