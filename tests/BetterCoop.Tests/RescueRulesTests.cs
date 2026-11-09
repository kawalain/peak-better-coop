using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class RescueRulesTests
{
    private static readonly RescueHold ByPair = new((1, 2), null);

    [Fact]
    public void A_passed_out_unpaired_scout_can_be_rescued() =>
        Assert.True(RescueRules.CanBeRescued(fullyPassedOut: true, dead: false, carried: false, RescueStanding.Unpaired));

    [Fact]
    public void A_passed_out_shared_body_can_be_rescued() =>
        Assert.True(RescueRules.CanBeRescued(fullyPassedOut: true, dead: false, carried: false, RescueStanding.Carrier));

    [Fact]
    public void A_rider_is_never_rescued_alone() =>
        Assert.False(RescueRules.CanBeRescued(true, false, false, RescueStanding.Rider));

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void Only_a_passed_out_live_uncarried_scout_can_be_rescued(bool passedOut, bool dead, bool carried) =>
        Assert.False(RescueRules.CanBeRescued(passedOut, dead, carried, RescueStanding.Unpaired));

    [Fact]
    public void A_rescue_holds_while_nothing_changes() =>
        Assert.True(RescueRules.Keeps(ByPair, ByPair, ruleOn: true, sharedBodyUp: true, pairsChanging: false));

    [Fact]
    public void The_rule_turned_off_puts_the_rescued_down() =>
        Assert.False(RescueRules.Keeps(ByPair, ByPair, ruleOn: false, sharedBodyUp: true, pairsChanging: false));

    [Fact]
    public void A_shared_body_down_puts_the_rescued_down() =>
        Assert.False(RescueRules.Keeps(ByPair, ByPair, ruleOn: true, sharedBodyUp: false, pairsChanging: false));

    [Fact]
    public void A_coming_pair_change_puts_the_rescued_down() =>
        Assert.False(RescueRules.Keeps(ByPair, ByPair, ruleOn: true, sharedBodyUp: true, pairsChanging: true));

    [Fact]
    public void A_rescuer_whose_pair_changed_puts_the_rescued_down() =>
        Assert.False(RescueRules.Keeps(ByPair, new RescueHold((2, 1), null), true, true, false));

    [Fact]
    public void A_rescued_scout_who_gained_a_pair_is_put_down() =>
        Assert.False(RescueRules.Keeps(ByPair, new RescueHold((1, 2), (3, 4)), true, true, false));

    [Fact]
    public void An_unpaired_rescuer_who_gained_a_pair_puts_the_rescued_down() =>
        Assert.False(RescueRules.Keeps(new RescueHold(null, (3, 4)), new RescueHold((5, 6), (3, 4)), true, true, false));
}
