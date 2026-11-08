using System;

namespace BetterCoop.Core;

/// <summary>
/// One slot's remaining uses on the Rider's HUD, drawn from the values the
/// Carrier sends a few times a second: between sends the bar keeps moving at
/// the sent rate for a short while, and the drawn value eases toward where
/// the bar should be instead of jumping.
/// </summary>
internal sealed class UsesSmoother
{
    /// <summary>How far past the last send the bar keeps moving on its own.</summary>
    public const float ExtrapolateSeconds = 0.3f;

    /// <summary>How quickly the drawn bar closes on its target, per second.</summary>
    private const float Sharpness = 12f;

    private float? value;

    private float rate;

    private float receivedAt;

    private float shown;

    private float shownAt;

    private bool hasShown;

    public void Receive(float? newValue, float newRate, float now)
    {
        if (newValue is not float received)
        {
            Reset();
            return;
        }

        value = received;
        rate = newRate;
        receivedAt = now;
        if (!hasShown)
        {
            Snap(now);
        }
    }

    /// <summary>The fraction to draw at <paramref name="now"/>, or null when nothing was received.</summary>
    public float? Display(float now)
    {
        if (value == null)
        {
            return null;
        }

        float target = Target(now);
        float elapsed = Math.Max(now - shownAt, 0f);
        shownAt = now;
        shown += (target - shown) * (1f - (float)Math.Exp(-Sharpness * elapsed));
        return shown;
    }

    /// <summary>Draws the target at once, as when another item comes into the slot.</summary>
    public void Snap(float now)
    {
        if (value == null)
        {
            return;
        }

        shown = Target(now);
        shownAt = now;
        hasShown = true;
    }

    public void Reset()
    {
        value = null;
        rate = 0f;
        hasShown = false;
    }

    private float Target(float now)
    {
        float ahead = Math.Min(Math.Max(now - receivedAt, 0f), ExtrapolateSeconds);
        return Math.Min(Math.Max(value!.Value + (rate * ahead), 0f), 1f);
    }
}
