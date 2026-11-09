using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>How the room took an order from the Roster.</summary>
internal enum OrderOutcome
{
    /// <summary>It runs, or has run.</summary>
    Done,

    /// <summary>It waits for the Pair change under way and runs after it.</summary>
    Queued,

    /// <summary>Another Pair change is under way.</summary>
    Busy,

    /// <summary>It cannot run now, for a reason of its own.</summary>
    Refused,

    /// <summary>The same order went through a moment ago.</summary>
    TooSoon,
}

/// <summary>
/// Each kind of order goes through at most once every <see cref="Seconds"/>:
/// every order reaches every client, so pressing again and again cannot
/// flood the room. An order that did not go through does not count.
/// </summary>
internal sealed class OrderPace
{
    public const double Seconds = 1;

    private readonly Dictionary<string, double> last = new();

    /// <summary>Whether <paramref name="order"/> may go through at <paramref name="now"/>.</summary>
    public bool Ready(string order, double now) =>
        !last.TryGetValue(order, out double at) || now - at >= Seconds || now < at;

    /// <summary>Notes that <paramref name="order"/> went through at <paramref name="now"/>.</summary>
    public void Went(string order, double now) => last[order] = now;
}
