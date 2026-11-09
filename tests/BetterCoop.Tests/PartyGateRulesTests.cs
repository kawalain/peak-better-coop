using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PartyGateRulesTests
{
    private static PartyMember Modded(string name) => new(name, canPair: true);

    private static PartyMember Unmodded(string name) => new(name, canPair: false);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Even_party_with_matching_mod_departs(int count)
    {
        var members = Enumerable.Range(0, count).Select(i => Modded($"P{i}")).ToList();

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, allowOddPlayerCount: false);

        Assert.True(verdict.CanDepart);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Odd_party_is_blocked_unless_allowed(int count)
    {
        var members = Enumerable.Range(0, count).Select(i => Modded($"P{i}")).ToList();

        Assert.False(PartyGateRules.Evaluate(members, allowOddPlayerCount: false).CanDepart);
        Assert.True(PartyGateRules.Evaluate(members, allowOddPlayerCount: true).CanDepart);
    }

    [Fact]
    public void Empty_room_never_departs()
    {
        PartyGateVerdict verdict = PartyGateRules.Evaluate([], allowOddPlayerCount: true);

        Assert.False(verdict.CanDepart);
    }

    [Fact]
    public void Unmodded_players_depart_with_the_party_and_are_named()
    {
        var members = new[] { Modded("Host"), Modded("Guest"), Unmodded("Plain") };

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, allowOddPlayerCount: false);

        Assert.True(verdict.CanDepart);
        Assert.Equal(2, verdict.PairableCount);
        Assert.Equal(["Plain"], verdict.Unmodded);
    }

    [Fact]
    public void Only_players_who_can_pair_count_towards_an_even_number()
    {
        var members = new[] { Modded("Host"), Modded("A"), Modded("B"), Unmodded("Plain") };

        PartyGateVerdict verdict = PartyGateRules.Evaluate(members, allowOddPlayerCount: false);

        Assert.True(verdict.OddCountBlocked);
        Assert.Equal(3, verdict.PairableCount);
        Assert.Equal(["Plain"], verdict.Unmodded);
        Assert.True(PartyGateRules.Evaluate(members, allowOddPlayerCount: true).CanDepart);
    }
}
