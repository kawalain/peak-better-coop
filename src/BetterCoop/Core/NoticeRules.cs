using System;
using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>What a notice says, which decides the paper it comes on.</summary>
internal enum NoticeKind
{
    /// <summary>News from the Troop Leader or the mod, on a yellow telegram.</summary>
    Telegram,

    /// <summary>Word between scouts, such as a Role Swap asked for, on a pen-written memo.</summary>
    Memo,

    /// <summary>What happened to someone's things, on a manila baggage tag.</summary>
    Tag,

    /// <summary>Something wrong with the party, under a red rubber stamp.</summary>
    Caution,
}

/// <summary>How long notices stay and how many show at once.</summary>
internal static class NoticeTiming
{
    /// <summary>The most notices shown at once; a new one sends the oldest away early.</summary>
    public const int MostShown = 3;

    /// <summary>
    /// How long a notice stays up: long enough to read, a little longer the
    /// more it says, and longest for a caution.
    /// </summary>
    public static float Seconds(NoticeKind kind, int length)
    {
        float reading = Math.Min(4f, Math.Max(0, length) * 0.05f);
        return kind switch
        {
            NoticeKind.Caution => 8f + reading,
            NoticeKind.Memo => 5f + reading,
            _ => 4.5f + reading,
        };
    }

    /// <summary>How many of <paramref name="shown"/> notices go early so a new one fits.</summary>
    public static int Overflow(int shown) => Math.Max(0, shown + 1 - MostShown);
}

/// <summary>Who the Pair letter shows a player before an operation runs.</summary>
internal static class PairLetterPlan
{
    /// <summary>
    /// The partner <paramref name="me"/> will have and whether they carry,
    /// as far as known before <paramref name="kind"/> runs: a Role Swap turns
    /// the Pair round, a Late Pairing names both but not who carries, a Pair
    /// Assignment says it all, and a Pair Shuffle is a surprise. A null
    /// partner with a false role means the Assignment leaves them unpaired.
    /// </summary>
    public static (int? Partner, bool? Carries) Preview(
        OperationKind kind, int me, int swapCarrier, int swapRider, IReadOnlyList<Pair>? assignment)
    {
        switch (kind)
        {
            case OperationKind.RoleSwap when me == swapCarrier:
                return (swapRider, false);
            case OperationKind.RoleSwap when me == swapRider:
                return (swapCarrier, true);
            case OperationKind.LatePairing when me == swapCarrier:
                return (swapRider, null);
            case OperationKind.LatePairing when me == swapRider:
                return (swapCarrier, null);
            case OperationKind.PairAssignment when assignment != null:
                foreach (Pair pair in assignment)
                {
                    if (pair.Contains(me))
                    {
                        return (pair.PartnerOf(me), pair.Carrier == me);
                    }
                }

                return (null, false);
            default:
                return (null, null);
        }
    }
}
