using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class BackSlotHoldTests
{
    [Fact]
    public void A_short_press_is_a_tap_on_release()
    {
        var hold = new BackSlotHold();
        hold.Begin(1f);

        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(true, 1.1f));
        Assert.Equal(BackSlotHold.Outcome.Tap, hold.Step(false, 1.2f));
        Assert.False(hold.Tracking);
    }

    [Fact]
    public void Holding_opens_once_and_its_release_is_no_tap()
    {
        var hold = new BackSlotHold();
        hold.Begin(1f);

        Assert.Equal(BackSlotHold.Outcome.Open, hold.Step(true, 1f + BackSlotHold.OpenAfter));
        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(true, 2f));
        Assert.True(hold.Tracking);
        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(false, 3f));
        Assert.False(hold.Tracking);
    }

    [Fact]
    public void Nothing_happens_without_a_press()
    {
        var hold = new BackSlotHold();

        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(true, 5f));
        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(false, 6f));
    }

    [Fact]
    public void A_reset_drops_the_press()
    {
        var hold = new BackSlotHold();
        hold.Begin(1f);
        hold.Reset();

        Assert.False(hold.Tracking);
        Assert.Equal(BackSlotHold.Outcome.None, hold.Step(false, 1.1f));
    }

    [Fact]
    public void A_new_press_after_an_open_can_open_again()
    {
        var hold = new BackSlotHold();
        hold.Begin(1f);
        hold.Step(true, 2f);
        hold.Step(false, 3f);

        hold.Begin(4f);
        Assert.Equal(BackSlotHold.Outcome.Open, hold.Step(true, 4f + BackSlotHold.OpenAfter));
    }
}
