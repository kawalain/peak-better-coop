using System;

namespace BetterCoop.Core;

/// <summary>How things on the paper ease from one state to the next, by a fraction 0 to 1 of the way.</summary>
internal static class Easing
{
    /// <summary>Fast at first, settling at the end.</summary>
    public static float Out(float t)
    {
        float u = 1f - Clamp(t);
        return 1f - (u * u * u);
    }

    /// <summary>Slow at both ends, as a hand starting and finishing a stroke.</summary>
    public static float InOut(float t)
    {
        t = Clamp(t);
        return t < 0.5f ? 4f * t * t * t : 1f - ((float)Math.Pow(-2f * t + 2f, 3) / 2f);
    }

    /// <summary>Springs out past the end and settles back, as a letter popping out from the edge.</summary>
    public static float Pop(float t)
    {
        const float back = 1.70158f;
        float u = Clamp(t) - 1f;
        return 1f + ((back + 1f) * u * u * u) + (back * u * u);
    }

    /// <summary>
    /// Moves <paramref name="value"/> towards <paramref name="target"/> as a
    /// spring that settles in about <paramref name="seconds"/>, the same
    /// whatever the frame rate.
    /// </summary>
    public static float Towards(float value, float target, float seconds, float elapsed) =>
        target + ((value - target) * (float)Math.Exp(-elapsed * 4.6f / Math.Max(1e-4f, seconds)));

    private static float Clamp(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
}
