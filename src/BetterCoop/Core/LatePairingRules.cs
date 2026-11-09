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
/// Late Pairing: Unpaired Players form Pairs in the middle of a Run, every
/// two that can pair at once, and every other Pair stays as it is.
/// </summary>
internal static class LatePairingRules
{
    /// <summary>
    /// Every Pair a Late Pairing forms now from <paramref name="candidates"/>:
    /// former partners first (one of them reconnected), then each who is down
    /// with someone awake, so nobody waits dead longer than needed, then the
    /// rest by Role Preference, those wishing to carry with those wishing to
    /// ride first. Two who are down wait for one to wake, and an odd one out
    /// waits for the next.
    /// </summary>
    public static IReadOnlyList<LatePairing> FormAll(IReadOnlyList<LateCandidate> candidates, Random random, IReadOnlyList<Pair>? former = null)
    {
        var left = candidates.GroupBy(c => c.Actor).Select(g => g.First()).OrderBy(c => c.Actor).ToList();
        var formed = new List<LatePairing>();

        bool Take(LateCandidate first, LateCandidate second)
        {
            if (Form(first, second, random, former) is not LatePairing pairing)
            {
                return false;
            }

            formed.Add(pairing);
            left.Remove(first);
            left.Remove(second);
            return true;
        }

        foreach (Pair pair in former ?? [])
        {
            LateCandidate? carrier = left.FirstOrDefault(c => c.Actor == pair.Carrier);
            LateCandidate? rider = left.FirstOrDefault(c => c.Actor == pair.Rider);
            if (carrier != null && rider != null && (carrier.Awake || rider.Awake))
            {
                Take(carrier, rider);
            }
        }

        foreach (LateCandidate down in left.Where(c => !c.Awake).ToList())
        {
            if (left.FirstOrDefault(c => c.Awake) is LateCandidate awake)
            {
                Take(down, awake);
            }
        }

        List<LateCandidate> Wishing(RolePreference preference) => left.Where(c => c.Awake && c.Preference == preference).ToList();

        void Match(List<LateCandidate> ones, List<LateCandidate> others)
        {
            while (ones.Count > 0 && others.Count > 0)
            {
                LateCandidate one = ones[0];
                LateCandidate other = others[0];
                if (one == other)
                {
                    // The same list: take the next one as the other.
                    if (ones.Count < 2)
                    {
                        return;
                    }

                    other = ones[1];
                }

                if (!Take(one, other))
                {
                    return;
                }

                ones.Remove(one);
                ones.Remove(other);
                others.Remove(one);
                others.Remove(other);
            }
        }

        Match(Wishing(RolePreference.Carrier), Wishing(RolePreference.Rider));
        Match(Wishing(RolePreference.Carrier), Wishing(RolePreference.Random));
        Match(Wishing(RolePreference.Random), Wishing(RolePreference.Rider));
        var anyone = left.Where(c => c.Awake).ToList();
        Match(anyone, anyone);
        return formed;
    }

    /// <summary>
    /// A Pair a Late Pairing announced, checked again as it runs: both must
    /// still be Unpaired Players and one awake. The announced roles hold,
    /// unless the Carrier has gone down while the Rider is awake. Null when
    /// it can no longer form.
    /// </summary>
    public static LatePairing? Confirm(Pair announced, IReadOnlyList<LateCandidate> candidates)
    {
        LateCandidate? carrier = candidates.FirstOrDefault(c => c.Actor == announced.Carrier);
        LateCandidate? rider = candidates.FirstOrDefault(c => c.Actor == announced.Rider);
        if (carrier == null || rider == null || announced.Carrier == announced.Rider)
        {
            return null;
        }

        // The announced roles as the former roles: Form keeps them unless the Carrier is down.
        return Form(carrier, rider, new Random(0), [new Pair(announced.Carrier, announced.Rider)]);
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
