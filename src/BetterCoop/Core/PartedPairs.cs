using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// Pairs that ended because a member left the room, named by the players'
/// stable user ids: a player who reconnects gets a new actor number, and a
/// Late Pairing puts them back with their former partner in their former roles.
/// </summary>
internal sealed class PartedPairs
{
    private readonly List<(string Carrier, string Rider)> entries;

    private PartedPairs(List<(string Carrier, string Rider)> entries)
    {
        this.entries = entries;
    }

    public static PartedPairs Empty => new([]);

    public IReadOnlyList<(string Carrier, string Rider)> Entries => entries;

    /// <summary>Records a parted Pair; each player keeps only their latest former partner.</summary>
    public void Add(string carrier, string rider)
    {
        entries.RemoveAll(e => e.Carrier == carrier || e.Rider == carrier || e.Carrier == rider || e.Rider == rider);
        entries.Add((carrier, rider));
    }

    /// <summary>Forgets the record of these two, in either role.</summary>
    public bool Remove(string one, string other) =>
        entries.RemoveAll(e => (e.Carrier == one && e.Rider == other) || (e.Carrier == other && e.Rider == one)) > 0;

    /// <summary>The records whose two players are both in the room now, as Pairs of their current actors.</summary>
    public IReadOnlyList<Pair> Resolve(Func<string, int?> actorOf)
    {
        var pairs = new List<Pair>();
        foreach ((string carrier, string rider) in entries)
        {
            if (actorOf(carrier) is int carrierActor && actorOf(rider) is int riderActor && carrierActor != riderActor)
            {
                pairs.Add(new Pair(carrierActor, riderActor));
            }
        }

        return pairs;
    }

    public string[] Encode() => entries.SelectMany(e => new[] { e.Carrier, e.Rider }).ToArray();

    public static PartedPairs Decode(object? value)
    {
        var entries = new List<(string, string)>();
        if (value is string[] flat && flat.Length % 2 == 0)
        {
            for (int i = 0; i < flat.Length; i += 2)
            {
                if (!string.IsNullOrEmpty(flat[i]) && !string.IsNullOrEmpty(flat[i + 1]))
                {
                    entries.Add((flat[i], flat[i + 1]));
                }
            }
        }

        return new PartedPairs(entries);
    }
}
