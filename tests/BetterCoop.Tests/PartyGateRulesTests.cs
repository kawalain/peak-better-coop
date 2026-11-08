using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PartyGateRulesTests
{
    private const string Version = "0.1.0";

    private static PartyMember Modded(string name) => new(name, Version);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Even_party_with_matching_mod_departs(int count)
    {
        var members = Enumerable.Range(0, count).Select(i => Modded($"P{i}")).ToList();

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, Version, allowOddPlayerCount: false);

        Assert.True(verdict.CanDepart);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Odd_party_is_blocked_unless_allowed(int count)
    {
        var members = Enumerable.Range(0, count).Select(i => Modded($"P{i}")).ToList();

        Assert.False(PartyGateRules.Evaluate(members, Version, allowOddPlayerCount: false).CanDepart);
        Assert.True(PartyGateRules.Evaluate(members, Version, allowOddPlayerCount: true).CanDepart);
    }

    [Fact]
    public void Empty_room_never_departs()
    {
        PartyGateVerdict verdict = PartyGateRules.Evaluate([], Version, allowOddPlayerCount: true);

        Assert.False(verdict.CanDepart);
    }

    [Fact]
    public void Players_without_the_mod_are_named()
    {
        var members = new[] { Modded("Host"), new PartyMember("Guest", null) };

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, Version, allowOddPlayerCount: false);

        Assert.False(verdict.CanDepart);
        Assert.Equal(["Guest"], verdict.PlayersWithoutMod);
    }

    [Fact]
    public void Players_with_another_version_are_named_with_their_version()
    {
        var members = new[] { Modded("Host"), new PartyMember("Guest", "0.0.9") };

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, Version, allowOddPlayerCount: false);

        Assert.False(verdict.CanDepart);
        PartyMember mismatch = Assert.Single(verdict.PlayersWithOtherVersion);
        Assert.Equal("Guest", mismatch.Name);
        Assert.Equal("0.0.9", mismatch.ModVersion);
    }
}
