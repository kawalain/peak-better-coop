using System;

namespace BetterCoop.Core;

/// <summary>One frame of a button as the game reads it.</summary>
internal readonly struct ButtonFrame
{
    public ButtonFrame(bool wasPressed, bool isPressed, bool wasReleased)
    {
        WasPressed = wasPressed;
        IsPressed = isPressed;
        WasReleased = wasReleased;
    }

    public bool WasPressed { get; }

    public bool IsPressed { get; }

    public bool WasReleased { get; }

    public override string ToString() => $"pressed={WasPressed} held={IsPressed} released={WasReleased}";
}

/// <summary>
/// Replays a remote button on the Carrier's client, one edge per frame, so a
/// quick click still shows as a press and then a release. Between snapshots
/// the button keeps its last known state: a packet gap never lets go of a
/// held climbing grip (ADR 0002). At most <see cref="MostWaiting"/> clicks
/// wait to play: a Rider clicking faster than the Carrier's frames play
/// them, or counters that jump, never leave the Shared Body clicking on
/// after the Rider has stopped.
/// </summary>
internal sealed class RelayedButton
{
    /// <summary>The most presses waiting to play; older clicks are dropped whole.</summary>
    public const int MostWaiting = 2;

    private ButtonCounters latest;

    private ushort appliedPresses;

    private ushort appliedReleases;

    private bool held;

    private bool initialized;

    public void Receive(ButtonCounters counters)
    {
        if (!initialized)
        {
            appliedPresses = counters.Presses;
            appliedReleases = counters.Releases;
            initialized = true;
        }

        latest = counters;
        int waiting = (ushort)(latest.Presses - appliedPresses);
        if (waiting > MostWaiting)
        {
            // Drop the oldest clicks whole, a press with its release, so the edges still alternate.
            int dropped = waiting - MostWaiting;
            appliedPresses = (ushort)(appliedPresses + dropped);
            appliedReleases = (ushort)(appliedReleases + Math.Min(dropped, (ushort)(latest.Releases - appliedReleases)));
        }
    }

    public void Reset()
    {
        latest = default;
        appliedPresses = 0;
        appliedReleases = 0;
        held = false;
        initialized = false;
    }

    /// <summary>
    /// Drops the edges not yet played and takes the latest held state, as the
    /// game drops a player's own input while a menu blocks it.
    /// </summary>
    public void Discard()
    {
        appliedPresses = latest.Presses;
        appliedReleases = latest.Releases;
        held = latest.Down;
    }

    /// <summary>Advances one game frame.</summary>
    public ButtonFrame Step()
    {
        int pendingPresses = (ushort)(latest.Presses - appliedPresses);
        int pendingReleases = (ushort)(latest.Releases - appliedReleases);

        if (!held && pendingPresses > 0)
        {
            appliedPresses++;
            held = true;
            return new ButtonFrame(true, true, false);
        }

        if (held && pendingReleases > 0)
        {
            appliedReleases++;
            held = false;
            return new ButtonFrame(false, false, true);
        }

        // Counters spent, or out of step with our state: settle on the latest state.
        appliedPresses = latest.Presses;
        appliedReleases = latest.Releases;
        if (held != latest.Down)
        {
            held = latest.Down;
            return held ? new ButtonFrame(true, true, false) : new ButtonFrame(false, false, true);
        }

        return new ButtonFrame(false, held, false);
    }
}
