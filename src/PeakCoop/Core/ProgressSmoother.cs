using System;

namespace PeakCoop.Core;

/// <summary>
/// Shows the progress the Carrier sends at a fixed rate as a smooth bar on the
/// Rider's screen (ADR 0005): between updates the bar keeps moving at the last
/// observed rate, briefly, and eases toward each new value without going back;
/// a new use starts from its own value, and a stopped use clears at once.
/// </summary>
internal sealed class ProgressSmoother
{
    /// <summary>How long the bar trusts the last update without a new one.</summary>
    public const float StaleSeconds = 0.5f;

    /// <summary>How far past the last update the bar keeps moving on its own.</summary>
    private const float ExtrapolateSeconds = 0.15f;

    /// <summary>How quickly the bar closes on where it should be, per second.</summary>
    private const float Sharpness = 18f;

    private float lastValue;

    private float lastAt = float.NegativeInfinity;

    private float rate;

    private float shown;

    private float shownAt = float.NegativeInfinity;

    public void Receive(float value, float now)
    {
        if (value <= 0f)
        {
            Reset();
            return;
        }

        if (lastValue > 0f && value >= lastValue && now > lastAt)
        {
            float observed = (value - lastValue) / (now - lastAt);
            rate = rate <= 0f ? observed : (rate + observed) / 2f;
        }
        else
        {
            // A new use, or the first update of one.
            rate = 0f;
            shown = value;
            shownAt = now;
        }

        lastValue = value;
        lastAt = now;
    }

    /// <summary>Whether a use is under way, as far as the updates tell.</summary>
    public bool IsActive(float now) => lastValue > 0f && now - lastAt < StaleSeconds;

    /// <summary>The value to draw at <paramref name="now"/>; 0 hides the bar.</summary>
    public float Display(float now)
    {
        if (lastValue <= 0f || now - lastAt >= StaleSeconds)
        {
            return 0f;
        }

        float ahead = Math.Min(Math.Max(now - lastAt, 0f), ExtrapolateSeconds);
        float target = Math.Min(1f, lastValue + (rate * ahead));
        float elapsed = Math.Max(now - shownAt, 0f);
        shownAt = now;
        if (target > shown)
        {
            shown += (target - shown) * (1f - (float)Math.Exp(-Sharpness * elapsed));
        }

        return shown;
    }

    public void Reset()
    {
        lastValue = 0f;
        lastAt = float.NegativeInfinity;
        rate = 0f;
        shown = 0f;
        shownAt = float.NegativeInfinity;
    }
}
