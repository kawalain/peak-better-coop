namespace BetterCoop.Core;

/// <summary>
/// Replays a remote press counter on the Carrier's client, one press per
/// frame, so presses that arrive together are not merged into one. At most
/// <see cref="MostWaiting"/> presses wait: a Rider pressing faster than the
/// Carrier's frames play them, or a counter that jumps, never leaves the
/// Shared Body pressing on after the Rider has stopped.
/// </summary>
internal sealed class RelayedPresses
{
    /// <summary>The most presses waiting to play; older ones are dropped.</summary>
    public const int MostWaiting = 3;

    private ushort latest;

    private ushort applied;

    private bool initialized;

    public void Receive(ushort presses)
    {
        if (!initialized)
        {
            applied = presses;
            initialized = true;
        }

        latest = presses;
        if ((ushort)(latest - applied) > MostWaiting)
        {
            applied = (ushort)(latest - MostWaiting);
        }
    }

    public void Reset()
    {
        latest = 0;
        applied = 0;
        initialized = false;
    }

    /// <summary>Drops the presses not yet played.</summary>
    public void Discard() => applied = latest;

    /// <summary>Advances one game frame; true when a press plays this frame.</summary>
    public bool Step()
    {
        if (latest == applied)
        {
            return false;
        }

        applied++;
        return true;
    }
}
