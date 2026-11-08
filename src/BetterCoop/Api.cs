using System;
using System.Diagnostics.CodeAnalysis;
using BetterCoop.Core;
using BetterCoop.Features;

namespace BetterCoop;

/// <summary>
/// Read-only access to BetterCoop's Pairs for other mods (ADR 0008).
/// Reference BetterCoop as a soft dependency and call this only when the
/// plugin <c>in.kawala.bettercoop</c> is loaded. Every client sees the same
/// Pairs. Calls never throw: a failure answers as if the character were not
/// paired, and an exception in an event handler stays with that handler.
/// </summary>
public static class Api
{
    /// <summary>Rises only on a change that breaks callers; added members leave it as it is.</summary>
    public const int ApiVersion = 1;

    /// <summary>A character's place in a Pair.</summary>
    public enum Role
    {
        /// <summary>Not in a Pair, or BetterCoop is disabled: plays as in unmodded PEAK.</summary>
        None,

        /// <summary>Carries the Rider; this character is the Pair's Shared Body.</summary>
        Carrier,

        /// <summary>Carried by the Carrier; this character is idle while the Pair lasts, with its statuses kept clear.</summary>
        Rider,
    }

    /// <summary>What changes the Pairs.</summary>
    public enum PairChange
    {
        /// <summary>A Pair Shuffle: every Pair changes.</summary>
        Shuffle,

        /// <summary>A Role Swap: one Pair trades roles.</summary>
        RoleSwap,

        /// <summary>A Late Pairing: two Unpaired Players become a Pair.</summary>
        LatePairing,

        /// <summary>A Pair Assignment: the Troop Leader sets the Pairs as they choose.</summary>
        Assignment,
    }

    /// <summary>False when BetterCoop turned itself off; every character then reads as unpaired.</summary>
    public static bool Enabled => Plugin.Patched;

    /// <summary>Raised on every client when the Pairs change, including when they form and when they clear at the Airport.</summary>
    public static event Action? PairsChanged;

    /// <summary>Raised on every client when a Pair change is announced, a few seconds before it runs.</summary>
    public static event Action<PairChange>? PairChangeStarting;

    /// <summary>Raised on every client when an announced Pair change has run.</summary>
    public static event Action<PairChange>? PairChangeFinished;

    /// <summary>The character's role in the current Pairs.</summary>
    public static Role RoleOf(Character? character)
    {
        try
        {
            return !Enabled
                ? Role.None
                : PairRoster.RoleOf(character) switch
                {
                    PairRole.Carrier => Role.Carrier,
                    PairRole.Rider => Role.Rider,
                    _ => Role.None,
                };
        }
        catch (Exception exception)
        {
            Guard.Report("Api.RoleOf", exception);
            return Role.None;
        }
    }

    /// <summary>The other member of the character's Pair, when both are in the room.</summary>
    public static bool TryGetPartner(Character? character, [NotNullWhen(true)] out Character? partner)
    {
        partner = null;
        try
        {
            if (Enabled && PairRoster.TryGetPartner(character, out Character found))
            {
                partner = found;
                return true;
            }
        }
        catch (Exception exception)
        {
            Guard.Report("Api.TryGetPartner", exception);
        }

        return false;
    }

    /// <summary>
    /// The body the character's player plays through: their Carrier's for a
    /// Rider, their own otherwise. False only for no character, or a Rider
    /// whose Carrier is not in the room.
    /// </summary>
    public static bool TryGetSharedBody(Character? character, [NotNullWhen(true)] out Character? body)
    {
        body = null;
        if (character == null)
        {
            return false;
        }

        if (RoleOf(character) != Role.Rider)
        {
            body = character;
            return true;
        }

        return TryGetPartner(character, out body);
    }

    internal static void RaisePairsChanged() => Raise(PairsChanged, nameof(PairsChanged));

    internal static void RaisePairChangeStarting(OperationKind kind) =>
        Raise(PairChangeStarting, ChangeOf(kind), nameof(PairChangeStarting));

    internal static void RaisePairChangeFinished(OperationKind kind) =>
        Raise(PairChangeFinished, ChangeOf(kind), nameof(PairChangeFinished));

    private static PairChange ChangeOf(OperationKind kind) => kind switch
    {
        OperationKind.RoleSwap => PairChange.RoleSwap,
        OperationKind.LatePairing => PairChange.LatePairing,
        OperationKind.PairAssignment => PairChange.Assignment,
        _ => PairChange.Shuffle,
    };

    private static void Raise(Action? handlers, string name)
    {
        foreach (Delegate handler in handlers?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"A handler of Api.{name} threw: {exception}");
            }
        }
    }

    private static void Raise(Action<PairChange>? handlers, PairChange change, string name)
    {
        foreach (Delegate handler in handlers?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action<PairChange>)handler)(change);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"A handler of Api.{name} threw: {exception}");
            }
        }
    }
}
