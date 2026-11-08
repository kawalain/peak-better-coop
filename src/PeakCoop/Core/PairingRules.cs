using System;
using System.Collections.Generic;
using System.Linq;

namespace PeakCoop.Core;

/// <summary>Two players bound for a Run, named by Photon actor number.</summary>
internal sealed class Pair
{
    public Pair(int carrier, int rider)
    {
        Carrier = carrier;
        Rider = rider;
    }

    public int Carrier { get; }

    public int Rider { get; }

    public bool Contains(int actor) => actor == Carrier || actor == Rider;

    public int PartnerOf(int actor) => actor == Carrier ? Rider : Carrier;
}

/// <summary>A player taking part in pairing.</summary>
internal sealed class PairCandidate
{
    public PairCandidate(int actor, RolePreference preference)
    {
        Actor = actor;
        Preference = preference;
    }

    public int Actor { get; }

    public RolePreference Preference { get; }
}

/// <summary>The Pairs of a Run and who was left out or reassigned.</summary>
internal sealed class PairingResult
{
    public PairingResult(IReadOnlyList<Pair> pairs, IReadOnlyList<int> unpaired, IReadOnlyList<int> preferenceOverridden)
    {
        Pairs = pairs;
        Unpaired = unpaired;
        PreferenceOverridden = preferenceOverridden;
    }

    public IReadOnlyList<Pair> Pairs { get; }

    public IReadOnlyList<int> Unpaired { get; }

    /// <summary>Players who got the role opposite to their Role Preference.</summary>
    public IReadOnlyList<int> PreferenceOverridden { get; }
}

/// <summary>Forms Pairs at random, honouring Role Preferences where possible.</summary>
internal static class PairingRules
{
    public static PairingResult Form(IReadOnlyList<PairCandidate> candidates, Random random) =>
        Form(candidates, random, []);

    /// <summary>Forms Pairs, leaving out someone not in <paramref name="satOut"/> when the count is odd.</summary>
    private static PairingResult Form(IReadOnlyList<PairCandidate> candidates, Random random, IReadOnlyCollection<int> satOut)
    {
        // A wish this version does not know counts as no wish.
        List<PairCandidate> players = Shuffle(
            candidates.Select(c => c.Preference is RolePreference.Carrier or RolePreference.Rider
                ? c
                : new PairCandidate(c.Actor, RolePreference.Random)),
            random);
        var unpaired = new List<int>();

        if (players.Count % 2 != 0)
        {
            // The odd one out comes from a wish more players hold than there are
            // Pairs, so leaving them out overrides one wish fewer; otherwise it
            // is someone without a wish. One of the two always exists.
            int slots = players.Count / 2;
            RolePreference leaveOut =
                players.Count(p => p.Preference == RolePreference.Carrier) > slots ? RolePreference.Carrier
                : players.Count(p => p.Preference == RolePreference.Rider) > slots ? RolePreference.Rider
                : RolePreference.Random;
            int index = players.FindIndex(p => p.Preference == leaveOut && !satOut.Contains(p.Actor));
            if (index < 0)
            {
                index = players.FindIndex(p => p.Preference == leaveOut);
            }

            unpaired.Add(players[index].Actor);
            players.RemoveAt(index);
        }

        int pairCount = players.Count / 2;
        var wantCarrier = players.Where(p => p.Preference == RolePreference.Carrier).ToList();
        var wantRider = players.Where(p => p.Preference == RolePreference.Rider).ToList();
        var anyRole = new Queue<PairCandidate>(players.Where(p => p.Preference == RolePreference.Random));

        var carriers = wantCarrier.Take(pairCount).ToList();
        var riders = wantRider.Take(pairCount).ToList();
        var extraCarrier = new Queue<PairCandidate>(wantCarrier.Skip(pairCount));
        var extraRider = new Queue<PairCandidate>(wantRider.Skip(pairCount));
        var overridden = new List<int>();

        while (carriers.Count < pairCount)
        {
            if (anyRole.Count > 0)
            {
                carriers.Add(anyRole.Dequeue());
            }
            else
            {
                PairCandidate moved = extraRider.Dequeue();
                overridden.Add(moved.Actor);
                carriers.Add(moved);
            }
        }

        while (riders.Count < pairCount)
        {
            if (anyRole.Count > 0)
            {
                riders.Add(anyRole.Dequeue());
            }
            else
            {
                PairCandidate moved = extraCarrier.Dequeue();
                overridden.Add(moved.Actor);
                riders.Add(moved);
            }
        }

        riders = Shuffle(riders, random);
        var pairs = new List<Pair>(pairCount);
        for (int i = 0; i < pairCount; i++)
        {
            pairs.Add(new Pair(carriers[i].Actor, riders[i].Actor));
        }

        return new PairingResult(pairs, unpaired, overridden);
    }

    /// <summary>
    /// Forms the Pairs anew for a Pair Shuffle so that every Pair changes:
    /// no partners repeat while there are two Pairs or more, and a lone Pair
    /// of the same two trades roles, whatever their Role Preferences. When
    /// the count is odd, someone who did not sit out last time sits out.
    /// </summary>
    public static PairingResult Reshuffle(
        IReadOnlyList<PairCandidate> candidates,
        Random random,
        IReadOnlyList<Pair> previous,
        IReadOnlyCollection<int> previousUnpaired)
    {
        PairingResult formed = Form(candidates, random, previousUnpaired);
        var before = new HashSet<(int, int)>(previous.Select(PartnersOf));
        var pairs = formed.Pairs.ToList();
        var overridden = new HashSet<int>(formed.PreferenceOverridden);

        // Each player had one partner before, so trading Riders with any
        // other Pair parts a repeated Pair without making a new repeat, and
        // keeps every role.
        for (int i = 0; i < pairs.Count && pairs.Count > 1; i++)
        {
            if (!before.Contains(PartnersOf(pairs[i])))
            {
                continue;
            }

            int other = pairs.FindIndex(i + 1, p => before.Contains(PartnersOf(p)));
            if (other < 0)
            {
                other = random.Next(pairs.Count - 1);
                other += other >= i ? 1 : 0;
            }

            (pairs[i], pairs[other]) = (new Pair(pairs[i].Carrier, pairs[other].Rider), new Pair(pairs[other].Carrier, pairs[i].Rider));
        }

        if (pairs.Count == 1 && previous.Any(p => p.Carrier == pairs[0].Carrier && p.Rider == pairs[0].Rider))
        {
            Pair lone = pairs[0];
            pairs[0] = new Pair(lone.Rider, lone.Carrier);
            foreach (PairCandidate candidate in candidates.Where(c => lone.Contains(c.Actor)))
            {
                bool wantsOther = candidate.Preference == (candidate.Actor == lone.Carrier ? RolePreference.Carrier : RolePreference.Rider);
                if (wantsOther)
                {
                    overridden.Add(candidate.Actor);
                }
                else
                {
                    overridden.Remove(candidate.Actor);
                }
            }
        }

        return new PairingResult(pairs, formed.Unpaired, overridden.ToList());
    }

    private static (int, int) PartnersOf(Pair pair) =>
        pair.Carrier < pair.Rider ? (pair.Carrier, pair.Rider) : (pair.Rider, pair.Carrier);

    /// <summary>
    /// Dissolves every Pair with a member no longer in the room; the partner
    /// left behind becomes unpaired. Idempotent, so the host may apply it to a
    /// stale copy of the Pairs and still converge.
    /// </summary>
    /// <returns>False when every Pair is complete.</returns>
    public static bool TryDissolveAbsent(
        PairingResult result,
        Func<int, bool> isPresent,
        out PairingResult reconciled)
    {
        reconciled = result;
        var kept = result.Pairs.Where(p => isPresent(p.Carrier) && isPresent(p.Rider)).ToList();
        if (kept.Count == result.Pairs.Count)
        {
            return false;
        }

        var leftBehind = result.Pairs
            .Except(kept)
            .SelectMany(p => new[] { p.Carrier, p.Rider })
            .Where(isPresent);
        reconciled = new PairingResult(
            kept,
            result.Unpaired.Concat(leftBehind).Distinct().ToList(),
            result.PreferenceOverridden);
        return true;
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
