using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>An Unpaired Player who may take part in a Late Pairing.</summary>
internal sealed class LateCandidate
{
    public LateCandidate(int actor, RolePreference preference, bool awake, float height)
    {
        Actor = actor;
        Preference = preference;
        Awake = awake;
        Height = height;
    }

    public int Actor { get; }

    public RolePreference Preference { get; }

    /// <summary>Alive and not passed out.</summary>
    public bool Awake { get; }

    /// <summary>How high the player's character stands.</summary>
    public float Height { get; }
}

/// <summary>The Pair a Late Pairing forms.</summary>
internal sealed class LatePairing
{
    public LatePairing(Pair pair, bool carrierMovesToRider, IReadOnlyList<int> preferenceOverridden)
    {
        Pair = pair;
        CarrierMovesToRider = carrierMovesToRider;
        PreferenceOverridden = preferenceOverridden;
    }

    public Pair Pair { get; }

    /// <summary>Whether the Shared Body starts at the Rider's place, the higher of the two.</summary>
    public bool CarrierMovesToRider { get; }

    /// <summary>Members who got the role opposite to their Role Preference.</summary>
    public IReadOnlyList<int> PreferenceOverridden { get; }
}

/// <summary>
/// Late Pairing: two Unpaired Players form a Pair in the middle of a Run,
/// and every other Pair stays as it is.
/// </summary>
internal static class LatePairingRules
{
    /// <summary>
    /// The two Unpaired Players to pair next, or null when no two can be:
    /// former partners first (one of them reconnected), then someone who is
    /// down with someone awake, so nobody waits dead longer than needed;
    /// two who are down wait for one to wake.
    /// </summary>
    public static (int First, int Second)? PickTwo(IReadOnlyList<LateCandidate> candidates, IReadOnlyList<Pair>? former = null)
    {
        foreach (Pair pair in former ?? [])
        {
            LateCandidate? carrier = candidates.FirstOrDefault(c => c.Actor == pair.Carrier);
            LateCandidate? rider = candidates.FirstOrDefault(c => c.Actor == pair.Rider);
            if (carrier != null && rider != null && (carrier.Awake || rider.Awake))
            {
                return (pair.Carrier, pair.Rider);
            }
        }

        var awake = candidates.Where(c => c.Awake).OrderBy(c => c.Actor).ToList();
        var down = candidates.Where(c => !c.Awake).OrderBy(c => c.Actor).ToList();
        if (awake.Count == 0)
        {
            return null;
        }

        if (down.Count > 0)
        {
            return (down[0].Actor, awake[0].Actor);
        }

        return awake.Count >= 2 ? (awake[0].Actor, awake[1].Actor) : null;
    }

    /// <summary>
    /// Pairs two Unpaired Players: one who is down rides and comes back on
    /// the Carrier's back; otherwise former partners take their former roles,
    /// and anyone else's Role Preferences decide. The Shared Body starts
    /// where the higher of the two stands. Null when both are down.
    /// </summary>
    public static LatePairing? Form(LateCandidate first, LateCandidate second, Random random, IReadOnlyList<Pair>? former = null)
    {
        Pair? before = (former ?? []).FirstOrDefault(p => p.Contains(first.Actor) && p.Contains(second.Actor));
        if (!first.Awake && !second.Awake)
        {
            return null;
        }

        bool firstCarries = first.Awake != second.Awake
            ? first.Awake
            : before != null
                ? before.Carrier == first.Actor
            : CarrierWish(first) != CarrierWish(second)
                ? CarrierWish(first) > CarrierWish(second)
                : random.Next(2) == 0;
        LateCandidate carrier = firstCarries ? first : second;
        LateCandidate rider = firstCarries ? second : first;

        var overridden = new List<int>();
        if (carrier.Preference == RolePreference.Rider)
        {
            overridden.Add(carrier.Actor);
        }

        if (rider.Preference == RolePreference.Carrier)
        {
            overridden.Add(rider.Actor);
        }

        bool moves = rider.Awake && rider.Height > carrier.Height;
        return new LatePairing(new Pair(carrier.Actor, rider.Actor), moves, overridden);
    }

    /// <summary>How much a player wants to carry; a wish this version does not know counts as none.</summary>
    private static int CarrierWish(LateCandidate candidate) => candidate.Preference switch
    {
        RolePreference.Carrier => 1,
        RolePreference.Rider => -1,
        _ => 0,
    };
}
