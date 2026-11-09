using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// Handover: when Pairs change, each new Shared Body takes over one
/// former Shared Body, and the items of the bodies move round so that
/// nothing is lost or copied.
/// </summary>
internal static class BodyHandover
{
    /// <summary>
    /// The former Shared Body (its Carrier's actor) that each new Carrier
    /// takes over. A new Pair takes the former Pair it shares the most with,
    /// its Carrier counting above its Rider; Pairs sharing nobody take the
    /// former bodies left over, so every former body is taken while there are
    /// enough new Pairs.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Sources(IReadOnlyList<Pair> before, IReadOnlyList<Pair> after)
    {
        var edges =
            from next in after
            from former in before
            let score = (former.Contains(next.Carrier) ? 2 : 0) + (former.Contains(next.Rider) ? 1 : 0)
            where score > 0
            orderby score descending, next.Carrier, former.Carrier
            select (next.Carrier, former.Carrier);

        var sources = new Dictionary<int, int>();
        var taken = new HashSet<int>();
        foreach ((int carrier, int formerCarrier) in edges)
        {
            if (!sources.ContainsKey(carrier) && taken.Add(formerCarrier))
            {
                sources[carrier] = formerCarrier;
            }
        }

        var leftOver = new Queue<int>(before.Select(p => p.Carrier).Where(c => !taken.Contains(c)));
        foreach (Pair next in after.Where(p => !sources.ContainsKey(p.Carrier)))
        {
            if (leftOver.Count == 0)
            {
                break;
            }

            sources[next.Carrier] = leftOver.Dequeue();
        }

        return sources;
    }

    /// <summary>
    /// Whose inventory each player receives (receiver to giver), for players
    /// whose inventory changes. Each new Carrier receives their source's; a
    /// source left without one receives the inventory freed further along its
    /// chain, so the moves form a permutation.
    /// </summary>
    public static IReadOnlyDictionary<int, int> InventoryMoves(IReadOnlyDictionary<int, int> sources)
    {
        var receives = sources.Where(s => s.Key != s.Value).ToDictionary(s => s.Key, s => s.Value);
        var takerOf = receives.ToDictionary(r => r.Value, r => r.Key);
        var moves = new Dictionary<int, int>(receives);
        foreach (int giver in takerOf.Keys.Where(g => !receives.ContainsKey(g)).ToList())
        {
            // Follow who took this giver's inventory until someone whose own
            // inventory nobody took: that inventory comes back to the giver.
            int free = takerOf[giver];
            while (takerOf.TryGetValue(free, out int next))
            {
                free = next;
            }

            moves[giver] = free;
        }

        return moves;
    }
}
