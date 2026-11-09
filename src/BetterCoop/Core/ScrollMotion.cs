using System;

namespace BetterCoop.Core;

/// <summary>
/// Smooth scrolling with overscroll for a list: the wheel and the selection
/// set where the list should be, and the list glides there. Pushing past
/// either end stretches with growing resistance, and once the pushing stops
/// the list springs back to the end.
/// </summary>
/// <remarks>Positions are how far the list is scrolled down, in panel units: 0 at the top, <see cref="Max"/> at the bottom.</remarks>
internal sealed class ScrollMotion
{
    /// <summary>How far a push past an end reaches before it stops.</summary>
    public const float Stretch = 140f;

    /// <summary>How long the list waits after the last push past an end before it springs back.</summary>
    public const float HoldSeconds = 0.1f;

    private const float GlideSeconds = 0.09f;

    private const float SpringSeconds = 0.11f;

    private float target;

    private float velocity;

    private float sincePush = float.MaxValue;

    private float max;

    private float travelFrom;

    private float travelTo;

    private float travelSeconds;

    private float travelled = float.MaxValue;

    public float Position { get; private set; }

    /// <summary>Whether a <see cref="Travel"/> is under way.</summary>
    public bool Travelling => travelled < travelSeconds;

    /// <summary>How far the list scrolls; 0 when it fits.</summary>
    public float Max
    {
        get => max;
        set
        {
            max = Math.Max(0f, value);
            if (sincePush >= HoldSeconds)
            {
                target = Clamp(target, 0f, max);
            }
        }
    }

    /// <summary>How far past an end the list is: negative above the top, positive below the bottom.</summary>
    public float Overscroll => Position < 0f ? Position : Position > max ? Position - max : 0f;

    /// <summary>Scrolls by <paramref name="amount"/>, positive down; past an end the push meets resistance.</summary>
    public void Push(float amount)
    {
        float beyond = target < 0f ? -target : target > max ? target - max : 0f;
        bool outward = (amount < 0f && target <= 0f) || (amount > 0f && target >= max);
        if (outward)
        {
            float give = 1f - beyond / Stretch;
            amount *= give * give * 0.5f;
        }

        travelled = float.MaxValue;
        target = Clamp(target + amount, -Stretch, max + Stretch);
        sincePush = 0f;
    }

    /// <summary>Glides to a position within the list, as when the selection moves.</summary>
    public void GlideTo(float position)
    {
        travelled = float.MaxValue;
        target = Clamp(position, 0f, max);
    }

    /// <summary>
    /// Travels to a position over <paramref name="seconds"/>, easing out, as
    /// the page carrying the eye to something that just changed. A push or a
    /// glide stops it where it is.
    /// </summary>
    public void Travel(float position, float seconds)
    {
        travelFrom = Position;
        travelTo = Clamp(position, 0f, max);
        travelSeconds = Math.Max(seconds, 0.0001f);
        travelled = 0f;
    }

    /// <summary>Jumps to a position at once, without any motion.</summary>
    public void Reset(float position = 0f)
    {
        target = Clamp(position, 0f, max);
        Position = target;
        velocity = 0f;
        sincePush = float.MaxValue;
    }

    public void Step(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        sincePush += seconds;
        if (Travelling)
        {
            travelled += seconds;
            float t = Math.Min(1f, travelled / travelSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            Position = target = travelFrom + (Clamp(travelTo, 0f, max) - travelFrom) * eased;
            velocity = 0f;
            return;
        }

        bool springing = false;
        if (sincePush >= HoldSeconds && (target < 0f || target > max))
        {
            target = Clamp(target, 0f, max);
            springing = true;
        }

        springing |= Position < 0f || Position > max;
        Position = SmoothDamp(Position, target, ref velocity, springing ? SpringSeconds : GlideSeconds, seconds);
        if (Math.Abs(Position - target) < 0.01f && Math.Abs(velocity) < 0.01f)
        {
            Position = target;
            velocity = 0f;
        }
    }

    /// <summary>A critically damped spring toward the target, as Unity's SmoothDamp.</summary>
    private static float SmoothDamp(float current, float goal, ref float speed, float smoothTime, float deltaTime)
    {
        float omega = 2f / smoothTime;
        float x = omega * deltaTime;
        float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float change = current - goal;
        float temp = (speed + omega * change) * deltaTime;
        speed = (speed - omega * temp) * decay;
        return goal + (change + temp) * decay;
    }

    private static float Clamp(float value, float min, float maxValue) => Math.Max(min, Math.Min(maxValue, value));
}
