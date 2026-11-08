using System;

namespace BetterCoop.Core;

/// <summary>What the Troop Leader runs on the Pairs.</summary>
internal enum OperationKind : byte
{
    None = 0,
    CampfireShuffle = 1,
    TimedShuffle = 2,
    RoleSwap = 3,
}

/// <summary>
/// A Pair Shuffle or Role Swap the Troop Leader announced, published as a
/// room property: every client counts down to <see cref="At"/> (server
/// time), and a new Troop Leader picks up one that is not done.
/// </summary>
internal sealed class Operation
{
    /// <summary>How long the countdown before an operation lasts.</summary>
    public const double CountdownSeconds = 3;

    /// <summary>How long before the operation hands are emptied and pickups refused.</summary>
    public const double LockLeadSeconds = 0.5;

    public Operation(int id, OperationKind kind, double at, int swapCarrier, int swapRider, bool done)
    {
        Id = id;
        Kind = kind;
        At = at;
        SwapCarrier = swapCarrier;
        SwapRider = swapRider;
        Done = done;
    }

    public int Id { get; }

    public OperationKind Kind { get; }

    /// <summary>Server time the operation runs at.</summary>
    public double At { get; }

    /// <summary>The Carrier of the Pair a Role Swap turns round, else -1.</summary>
    public int SwapCarrier { get; }

    /// <summary>The Rider of the Pair a Role Swap turns round, else -1.</summary>
    public int SwapRider { get; }

    public bool Done { get; }

    public bool IsShuffle => Kind is OperationKind.CampfireShuffle or OperationKind.TimedShuffle;

    /// <summary>Whether the operation concerns this player: everyone for a Pair Shuffle.</summary>
    public bool Involves(int actor) => IsShuffle || actor == SwapCarrier || actor == SwapRider;

    /// <summary>Whether hands stay empty now: from just before the run until it is done.</summary>
    public bool Locks(double now) => !Done && now >= At - LockLeadSeconds;

    public Operation Finished() => new(Id, Kind, At, SwapCarrier, SwapRider, true);

    public object[] Encode() => [Id, (byte)Kind, At, SwapCarrier, SwapRider, Done];

    public static Operation? Decode(object? value)
    {
        if (value is not object[] { Length: 6 } parts
            || parts[0] is not int id
            || parts[1] is not byte kind
            || parts[2] is not double at
            || parts[3] is not int swapCarrier
            || parts[4] is not int swapRider
            || parts[5] is not bool done
            || !Enum.IsDefined(typeof(OperationKind), kind))
        {
            return null;
        }

        return new Operation(id, (OperationKind)kind, at, swapCarrier, swapRider, done);
    }

    /// <summary>The whole seconds left to announce in the countdown, or 0 outside it.</summary>
    public int SecondsLeft(double now)
    {
        double left = At - now;
        return Done || left <= 0 || left > CountdownSeconds ? 0 : (int)Math.Ceiling(left);
    }
}

/// <summary>
/// The Troop Leader's rule of one operation at a time. A Pair Shuffle that
/// comes during a Role Swap waits for it; one that comes during another
/// Pair Shuffle merges into it; a Role Swap that comes during either is refused.
/// </summary>
internal sealed class OperationQueue
{
    private OperationKind waiting;

    /// <summary>The operation to announce now, or null when it waits or is refused.</summary>
    public Operation? Request(Operation? current, OperationKind kind, double now, int swapCarrier = -1, int swapRider = -1)
    {
        if (current is { Done: false })
        {
            bool shuffle = kind is OperationKind.CampfireShuffle or OperationKind.TimedShuffle;
            if (shuffle && current.Kind == OperationKind.RoleSwap && waiting == OperationKind.None)
            {
                waiting = kind;
            }

            return null;
        }

        return new Operation((current?.Id ?? 0) + 1, kind, now + Operation.CountdownSeconds, swapCarrier, swapRider, false);
    }

    /// <summary>The Pair Shuffle that waited for <paramref name="finished"/>, if any.</summary>
    public Operation? After(Operation finished, double now)
    {
        if (waiting == OperationKind.None)
        {
            return null;
        }

        OperationKind kind = waiting;
        waiting = OperationKind.None;
        return new Operation(finished.Id + 1, kind, now + Operation.CountdownSeconds, -1, -1, false);
    }

    public void Clear() => waiting = OperationKind.None;
}
