using System;

namespace PeakCoop.Core;

/// <summary>
/// Moves the Carrier's look smoothly toward the latest look the Rider sent,
/// so the Carrier's view glides between network snapshots.
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

    /// <summary>Moves the look toward the target; false while nothing has arrived.</summary>
    public bool Step(float deltaTime, ref float yaw, ref float pitch)
    {
        if (!hasTarget)
        {
            return false;
        }

        if (!snapped)
        {
            yaw = targetYaw;
            pitch = targetPitch;
            snapped = true;
            return true;
        }

        float t = 1f - (float)Math.Exp(-Sharpness * deltaTime);
        yaw += (targetYaw - yaw) * t;
        pitch += (targetPitch - pitch) * t;
        return true;
    }
}
