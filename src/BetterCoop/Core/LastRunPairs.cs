using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// The Pairs the last Run ended with, named by user id so they outlast the
/// room, for the next Pair Plan to start from when the same scouts gather
/// again. Kept as text: a line per Pair, Carrier then Rider.
/// </summary>
internal static class LastRunPairs
{
    public static string Encode(IEnumerable<(string Carrier, string Rider)> pairs) =>
        string.Join("\n", pairs.Where(p => Valid(p.Carrier) && Valid(p.Rider)).Select(p => $"{p.Carrier} {p.Rider}"));

    public static List<(string Carrier, string Rider)> Decode(string? text)
    {
        var pairs = new List<(string Carrier, string Rider)>();
        foreach (string line in (text ?? string.Empty).Split('\n'))
        {
            string[] ids = line.Trim().Split(' ');
            if (ids.Length == 2 && Valid(ids[0]) && Valid(ids[1]) && ids[0] != ids[1])
            {
                pairs.Add((ids[0], ids[1]));
            }
        }

        return pairs;
    }

    /// <summary>The Pairs whose two members are both here now, as their actors, each player once.</summary>
    public static List<Pair> Map(IEnumerable<(string Carrier, string Rider)> pairs, Func<string, int?> actorOf) =>
        PairingRules.Distinct(pairs
            .Select(p => (Carrier: actorOf(p.Carrier), Rider: actorOf(p.Rider)))
            .Where(p => p.Carrier is int && p.Rider is int)
            .Select(p => new Pair(p.Carrier!.Value, p.Rider!.Value)));

    private static bool Valid(string id) => id.Length > 0 && !id.Any(char.IsWhiteSpace);
}
