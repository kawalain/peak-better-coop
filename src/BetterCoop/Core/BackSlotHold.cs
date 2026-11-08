namespace BetterCoop.Core;

/// <summary>
/// Tells a tap of the back-slot key, which brings the back item to hand as
/// in the game, from a hold, which opens the Carrier's own backpack wheel.
/// A tap only counts once the key is released, since until then it may still
/// become a hold.
/// </summary>
internal sealed class BackSlotHold
{
    /// <summary>The game's hold to open a backpack worn by someone else.</summary>
    public const float OpenAfter = 0.25f;

    private float pressedAt;

    private bool opened;

    internal enum Outcome
    {
        None,
        Tap,
        Open,
    }

    /// <summary>Whether a press is being held back to see whether it becomes a hold.</summary>
    public bool Tracking { get; private set; }

    public void Begin(float now)
    {
        Tracking = true;
        pressedAt = now;
        opened = false;
    }

    /// <summary>One frame of the key while tracking: <paramref name="down"/> is whether it is still held.</summary>
    public Outcome Step(bool down, float now)
    {
        if (!Tracking)
        {
            return Outcome.None;
        }

        if (down)
        {
            if (opened || now - pressedAt < OpenAfter)
            {
                return Outcome.None;
            }

            opened = true;
            return Outcome.Open;
        }

        Tracking = false;
        return opened ? Outcome.None : Outcome.Tap;
    }

    public void Reset()
    {
        Tracking = false;
        opened = false;
    }
}
