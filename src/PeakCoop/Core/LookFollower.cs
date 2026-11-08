using System;

namespace PeakCoop.Core;

/// <summary>
/// Moves the Carrier's look smoothly toward the latest look the Rider sent,
/// so the Carrier's view glides between network snapshots. Yaw is an
/// unbounded angle that the game sometimes rewrites into [0, 360), so it
/// turns the short way round rather than unwinding whole turns.
/// </summary>
internal sealed class LookFollower
{
    /// <summary>How quickly the look closes the gap, per second.</summary>
    public const float Sharpness = 20f;

    private float targetYaw;

    private float targetPitch;

    private bool hasTarget;

    private bool snapped;

    public void Receive(float yaw, float pitch)
    {
        targetYaw = yaw;
        targetPitch = pitch;
        hasTarget = true;
    }

    public void Reset()
    {
        hasTarget = false;
        snapped = false;
    }

    /// <summary>
    /// Makes the next step reach the target at once, for a frame whose action
    /// must aim where the Rider aimed rather than where the smoothing has got to.
    /// </summary>
    public void SnapNext() => snapped = false;

    /// <summary>Moves the look toward the target; false while nothing has arrived.</summary>
    public bool Step(float deltaTime, ref float yaw, ref float pitch)
    {
        if (!hasTarget)
        {
            return false;
        }

        float t = snapped ? 1f - (float)Math.Exp(-Sharpness * deltaTime) : 1f;
        snapped = true;
        yaw += DeltaAngle(yaw, targetYaw) * t;
        pitch += (targetPitch - pitch) * t;
        return true;
    }

    /// <summary>The shortest signed turn from one angle to another, in (-180, 180].</summary>
    public static float DeltaAngle(float from, float to)
    {
        float delta = (to - from) % 360f;
        if (delta > 180f)
        {
            delta -= 360f;
        }
        else if (delta <= -180f)
        {
            delta += 360f;
        }

        return delta;
    }
}
