namespace PeakCoop.Core;

/// <summary>
/// Decides when the Carrier sends the progress of an action the Rider
/// started (eating, building, setting a piton): steadily while it runs, and
/// once, reliably, when it stops, so the Rider's bar never sticks.
/// </summary>
internal sealed class ProgressFeed
{
    public const float SendInterval = 1f / 30f;

    private float lastSent;

    private float nextSend;

    /// <summary>True when <paramref name="progress"/> should be sent now.</summary>
    public bool ShouldSend(float progress, float now, out bool reliable)
    {
        reliable = false;
        if (progress <= 0f)
        {
            if (lastSent <= 0f)
            {
                return false;
            }

            lastSent = 0f;
            reliable = true;
            return true;
        }

        if (lastSent > 0f && now < nextSend)
        {
            return false;
        }

        lastSent = progress;
        nextSend = now + SendInterval;
        return true;
    }

    public void Reset()
    {
        lastSent = 0f;
        nextSend = 0f;
    }
}
