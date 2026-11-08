using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class SharedLifeRulesTests
{
    private const float LongAgo = 100f;

    [Fact]
    public void A_live_pair_needs_nothing() =>
        Assert.Equal(RiderLifeAction.None, SharedLifeRules.Decide(false, false, false, LongAgo));

    [Fact]
    public void The_rider_dies_with_the_carrier() =>
        Assert.Equal(RiderLifeAction.Die, SharedLifeRules.Decide(true, false, false, LongAgo));

    [Fact]
    public void A_dead_pair_needs_nothing() =>
        Assert.Equal(RiderLifeAction.None, SharedLifeRules.Decide(true, true, false, LongAgo));

    [Fact]
    public void A_dead_rider_beside_a_live_carrier_comes_back()
    {
        // The Carrier's revival reached this client before the Carrier's death did.
        Assert.Equal(RiderLifeAction.Revive, SharedLifeRules.Decide(false, true, false, LongAgo));
    }

    [Fact]
    public void A_rider_revived_alone_waits_for_the_carrier()
    {
        Assert.Equal(RiderLifeAction.None, SharedLifeRules.Decide(true, false, false, 1f));
        Assert.Equal(
            RiderLifeAction.Die,
            SharedLifeRules.Decide(true, false, false, SharedLifeRules.RevivalGraceSeconds));
    }

    [Fact]
    public void A_passed_out_rider_beside_a_live_carrier_is_woken() =>
        Assert.Equal(RiderLifeAction.Wake, SharedLifeRules.Decide(false, false, true, LongAgo));
}
