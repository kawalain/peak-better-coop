using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>A player in the room as seen by the party gate.</summary>
internal sealed class PartyMember
{
    public PartyMember(string name, bool canPair)
    {
        Name = name;
        CanPair = canPair;
    }

    public string Name { get; }

    /// <summary>Whether the player's mod works with the Troop Leader's; false for an Unmodded Player.</summary>
    public bool CanPair { get; }
}

/// <summary>Why a Run may or may not depart.</summary>
internal sealed class PartyGateVerdict
{
    public PartyGateVerdict(int pairableCount, bool oddCountBlocked, IReadOnlyList<string> unmodded)
    {
        PairableCount = pairableCount;
        OddCountBlocked = oddCountBlocked;
        Unmodded = unmodded;
    }

    /// <summary>The players who can be paired, the Troop Leader among them.</summary>
    public int PairableCount { get; }

    public bool OddCountBlocked { get; }

    /// <summary>The Unmodded Players, who depart with the party and climb unpaired.</summary>
    public IReadOnlyList<string> Unmodded { get; }

    public bool CanDepart => PairableCount > 0 && !OddCountBlocked;
}

/// <summary>
/// Decides whether the party may leave the Airport on a Run. Unmodded Players
/// never hold it back (ADR 0009); only those who can be paired count towards
/// an even number.
/// </summary>
internal static class PartyGateRules
{
    public static PartyGateVerdict Evaluate(IReadOnlyList<PartyMember> members, bool allowOddPlayerCount)
    {
        int pairable = members.Count(m => m.CanPair);
        bool oddBlocked = pairable % 2 != 0 && !allowOddPlayerCount;
        return new PartyGateVerdict(pairable, oddBlocked, members.Where(m => !m.CanPair).Select(m => m.Name).ToList());
    }
}
