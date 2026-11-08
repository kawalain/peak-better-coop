namespace PeakCoop.Core;

/// <summary>
/// Replays a remote press counter on the Carrier's client, one press per
/// frame, so presses that arrive together are not merged into one.
/// </summary>
internal sealed class RelayedPresses
{
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
    }

    public void Reset()
    {
        latest = 0;
        applied = 0;
        initialized = false;
    }

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
