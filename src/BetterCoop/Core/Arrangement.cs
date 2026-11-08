using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// The Pairs a Pair Assignment sets, as the room keeps them beside the
/// operation: the operation's id, then each Pair's Carrier and Rider in turn.
/// The id ties them to their operation, so a new Troop Leader never runs one
/// operation with another's Pairs.
/// </summary>
internal static class Arrangement
{
    public static object[] Encode(int id, IReadOnlyList<Pair> pairs) =>
        [id, pairs.SelectMany(p => new[] { p.Carrier, p.Rider }).ToArray()];

    /// <summary>The operation an encoded arrangement belongs to, or 0 for none.</summary>
    public static int IdOf(object? value) => value is object[] { Length: 2 } parts && parts[0] is int id ? id : 0;

    /// <summary>The Pairs set for operation <paramref name="id"/>, or null when the value is not theirs.</summary>
    public static IReadOnlyList<Pair>? Decode(object? value, int id)
    {
        if (value is not object[] { Length: 2 } parts
            || parts[0] is not int owner
            || owner != id
            || parts[1] is not int[] flat
            || flat.Length % 2 != 0)
        {
            return null;
        }

        var pairs = new List<Pair>();
        for (int i = 0; i < flat.Length; i += 2)
        {
            pairs.Add(new Pair(flat[i], flat[i + 1]));
        }

        return pairs;
    }

    /// <summary>The Pairs with these two made one, the Pairs they were in ended.</summary>
    public static IReadOnlyList<Pair> WithPair(IReadOnlyList<Pair> pairs, int carrier, int rider) =>
        pairs.Where(p => !p.Contains(carrier) && !p.Contains(rider)).Append(new Pair(carrier, rider)).ToList();
}
