using Burntime.Platform;

namespace Burntime.Framework.GUI;

/// <summary>Converts touch release velocity into a short, decaying scroll.</summary>
public sealed class KineticScroll
{
    const float MaximumSpeed = 500;
    const float StopSpeed = 4;
    const float HalfLife = 0.15f;

    float velocity;

    public bool IsActive => velocity != 0;

    public void Release(float releaseVelocity) =>
        velocity = System.Math.Clamp(releaseVelocity, -MaximumSpeed, MaximumSpeed);

    public void Stop() => velocity = 0;

    public float Update(float elapsed)
    {
        if (velocity == 0 || elapsed <= 0)
            return 0;

        float distance = velocity * elapsed;
        velocity *= (float)System.Math.Pow(0.5, elapsed / HalfLife);
        if (System.Math.Abs(velocity) < StopSpeed)
            velocity = 0;
        return distance;
    }
}
