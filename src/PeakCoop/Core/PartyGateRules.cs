using System.Collections.Generic;

namespace PeakCoop.Core;

/// <summary>A player in the room as seen by the party gate.</summary>
internal sealed class PartyMember
{
    public PartyMember(string name, string? modVersion)
    {
        Name = name;
        ModVersion = modVersion;
    }

    public string Name { get; }

    /// <summary>The mod version the player published, or null without the mod.</summary>
    public string? ModVersion { get; }
}

/// <summary>Why a Run may or may not depart.</summary>
internal sealed class PartyGateVerdict
{
    public PartyGateVerdict(
        int playerCount,
        bool oddCountBlocked,
        IReadOnlyList<string> playersWithoutMod,
        IReadOnlyList<PartyMember> playersWithOtherVersion)
    {
        PlayerCount = playerCount;
        OddCountBlocked = oddCountBlocked;
        PlayersWithoutMod = playersWithoutMod;
        PlayersWithOtherVersion = playersWithOtherVersion;
    }

    public int PlayerCount { get; }

    public bool OddCountBlocked { get; }

    public IReadOnlyList<string> PlayersWithoutMod { get; }

    public IReadOnlyList<PartyMember> PlayersWithOtherVersion { get; }

    public bool CanDepart =>
        PlayerCount > 0
        && !OddCountBlocked
        && PlayersWithoutMod.Count == 0
        && PlayersWithOtherVersion.Count == 0;
}

/// <summary>Decides whether the party may leave the Airport on a Run.</summary>
internal static class PartyGateRules
{
    public static PartyGateVerdict Evaluate(
        IReadOnlyList<PartyMember> members,
        string requiredVersion,
        bool allowOddPlayerCount)
    {
        var withoutMod = new List<string>();
        var otherVersion = new List<PartyMember>();

        foreach (PartyMember member in members)
        {
            if (member.ModVersion == null)
            {
                withoutMod.Add(member.Name);
            }
            else if (member.ModVersion != requiredVersion)
            {
                otherVersion.Add(member);
            }
        }

        bool oddBlocked = members.Count % 2 != 0 && !allowOddPlayerCount;
        return new PartyGateVerdict(members.Count, oddBlocked, withoutMod, otherVersion);
    }
}
