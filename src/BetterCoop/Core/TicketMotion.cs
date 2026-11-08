using System;

namespace BetterCoop.Core;

/// <summary>
/// How the Roster's paper rises and sinks, like a ticket out of the
/// airport kiosk: it shoots up from below, passes its place a little while
/// it tips, and settles; it leaves with a small lift before it drops away.
/// Offsets are fractions of the travel, negative below the panel's place;
/// tilts are degrees, positive anticlockwise.
/// </summary>
internal static class TicketMotion
{
    public const float RiseSeconds = 0.57f;

    public const float SinkSeconds = 0.3f;

    /// <summary>The largest tilt on the way up.</summary>
    public const float RiseTiltDegrees = 2f;

    private const float SinkTiltDegrees = -1.5f;

    /// <summary>Under the travel, then above it a little, then at rest by half a second.</summary>
    public static float RiseOffset(float seconds)
    {
        if (seconds <= 0f)
        {
            return -1f;
        }

        if (seconds < 1f / 3f)
        {
            float t = seconds;
            return -1f + 22.5f * t * t - 40.5f * t * t * t;
        }

        if (seconds < 0.5f)
        {
            float u = seconds - 1f / 3f;
            return 1.5f * u - 18f * u * u + 54f * u * u * u;
        }

        return 0f;
    }

    /// <summary>Tips over while rising, swings back past upright, and settles.</summary>
    public static float RiseTilt(float seconds)
    {
        float shape;
        if (seconds <= 0f || seconds >= RiseSeconds)
        {
            shape = 0f;
        }
        else if (seconds < 1f / 3f)
        {
            float t = seconds;
            shape = 94.5f * t * t - 189f * t * t * t;
        }
        else if (seconds < 0.45f)
        {
            float u = seconds - 1f / 3f;
            shape = 3.5f - 642.857f * u * u + 3306.124f * u * u * u;
        }
        else
        {
            float u = seconds - 0.45f;
            shape = -15f * u + 257.143f * u * u - 1102.041f * u * u * u;
        }

        return RiseTiltDegrees * shape / 3.5f;
    }

    /// <summary>Shows at once as the paper starts to rise.</summary>
    public static float RiseAlpha(float seconds) => Clamp01(seconds / 0.12f);

    /// <summary>A small lift, then a drop that speeds up.</summary>
    public static float SinkOffset(float seconds)
    {
        float t = Clamp01(seconds / SinkSeconds);
        return -(1.8f * t * t * t - 0.8f * t * t);
    }

    public static float SinkTilt(float seconds)
    {
        float t = Clamp01(seconds / SinkSeconds);
        return SinkTiltDegrees * t * t;
    }

    /// <summary>Fades over the drop's last part.</summary>
    public static float SinkAlpha(float seconds)
    {
        float t = Clamp01((seconds / SinkSeconds - 0.55f) / 0.45f);
        return 1f - t * t * (3f - 2f * t);
    }

    private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
}
