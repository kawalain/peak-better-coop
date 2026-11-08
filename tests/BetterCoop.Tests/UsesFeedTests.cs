using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class UsesFeedTests
{
    private const float N = SlotUses.None;

    private static float[] Slots(float hand0, float back = N) => [hand0, N, N, back, N];

    [Fact]
    public void The_first_values_go_out_at_once_without_a_rate()
    {
        var feed = new UsesFeed();

        Assert.True(feed.ShouldSend(Slots(1f), 0f, out SlotUses message));
        Assert.Equal(1f, message.Values[0]);
        Assert.All(message.Rates, rate => Assert.Equal(0f, rate));
    }

    [Fact]
    public void Unchanged_values_are_not_sent_again()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);

        Assert.False(feed.ShouldSend(Slots(1f), 1f, out _));
    }

    [Fact]
    public void Changes_go_out_at_most_every_interval()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);

        Assert.False(feed.ShouldSend(Slots(0.9f), 0.05f, out _));
        Assert.True(feed.ShouldSend(Slots(0.9f), 0.1f, out _));
    }

    [Fact]
    public void A_running_use_carries_its_rate()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);

        Assert.True(feed.ShouldSend(Slots(0.95f), 0.1f, out SlotUses message));
        Assert.Equal(-0.5f, message.Rates[0], 3);
    }

    [Fact]
    public void A_use_that_stops_sends_once_more_with_no_rate()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);
        feed.ShouldSend(Slots(0.95f), 0.1f, out _);

        Assert.True(feed.ShouldSend(Slots(0.95f), 0.2f, out SlotUses message));
        Assert.Equal(0f, message.Rates[0]);
        Assert.False(feed.ShouldSend(Slots(0.95f), 0.3f, out _));
    }

    [Fact]
    public void Another_item_in_the_slot_jumps_without_a_rate()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(0.1f), 0f, out _);

        Assert.True(feed.ShouldSend(Slots(1f), 0.1f, out SlotUses message));
        Assert.Equal(0f, message.Rates[0]);
    }

    [Fact]
    public void A_change_after_a_pause_has_no_rate()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);

        Assert.True(feed.ShouldSend(Slots(0.8f), 2f, out SlotUses message));
        Assert.Equal(0f, message.Rates[0]);
    }

    [Fact]
    public void A_slot_gaining_or_losing_its_bar_is_sent()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);

        Assert.True(feed.ShouldSend(Slots(1f, back: 0.5f), 0.1f, out SlotUses gained));
        Assert.Equal(0.5f, gained.Values[SlotUses.BackSlot]);
        Assert.Equal(0f, gained.Rates[SlotUses.BackSlot]);
        Assert.True(feed.ShouldSend(Slots(N, back: 0.5f), 0.2f, out SlotUses lost));
        Assert.Equal(N, lost.Values[0]);
    }

    [Fact]
    public void A_new_partner_gets_the_values_at_once()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(1f), 0f, out _);
        feed.Reset();

        Assert.True(feed.ShouldSend(Slots(1f), 0.01f, out _));
    }

    [Fact]
    public void Values_survive_the_wire()
    {
        var feed = new UsesFeed();
        feed.ShouldSend(Slots(0.25f, back: 1f), 0f, out SlotUses message);

        SlotUses? decoded = SlotUses.Decode(message.Encode());

        Assert.NotNull(decoded);
        Assert.Equal(message.Values, decoded.Value.Values);
        Assert.Equal(message.Rates, decoded.Value.Rates);
    }

    [Fact]
    public void Bad_payloads_are_refused()
    {
        float[] rates = new float[SlotUses.Count];

        Assert.Null(SlotUses.Decode(null));
        Assert.Null(SlotUses.Decode(new object[] { new float[3], rates }));
        Assert.Null(SlotUses.Decode(new object[] { Slots(1.5f), rates }));
        Assert.Null(SlotUses.Decode(new object[] { Slots(float.NaN), rates }));
        Assert.Null(SlotUses.Decode(new object[] { Slots(1f), new[] { float.PositiveInfinity, 0f, 0f, 0f, 0f } }));
    }
}

public class UsesSmootherTests
{
    [Fact]
    public void Nothing_is_drawn_before_a_value_arrives()
    {
        Assert.Null(new UsesSmoother().Display(0f));
    }

    [Fact]
    public void The_first_value_is_drawn_at_once()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(0.8f, 0f, 0f);

        Assert.Equal(0.8f, smoother.Display(0f));
    }

    [Fact]
    public void The_bar_keeps_moving_at_the_sent_rate_then_holds()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(1f, -1f, 0f);

        float early = smoother.Display(0.1f)!.Value;
        float settled = 0f;
        for (float now = 0.2f; now < 3f; now += 0.05f)
        {
            settled = smoother.Display(now)!.Value;
        }

        Assert.True(early < 1f);
        Assert.Equal(1f - UsesSmoother.ExtrapolateSeconds, settled, 2);
    }

    [Fact]
    public void A_new_value_is_eased_into()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(1f, 0f, 0f);
        smoother.Display(0f);
        smoother.Receive(0.5f, 0f, 0f);

        float step = smoother.Display(0.05f)!.Value;

        Assert.InRange(step, 0.5f, 0.99f);
    }

    [Fact]
    public void Snap_draws_the_target_at_once()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(1f, 0f, 0f);
        smoother.Display(0f);
        smoother.Receive(0.2f, 0f, 0f);
        smoother.Snap(0f);

        Assert.Equal(0.2f, smoother.Display(0f));
    }

    [Fact]
    public void A_slot_without_a_bar_clears_the_value()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(1f, 0f, 0f);
        smoother.Receive(null, 0f, 1f);

        Assert.Null(smoother.Display(1f));
    }

    [Fact]
    public void The_bar_stays_within_empty_and_full()
    {
        var smoother = new UsesSmoother();
        smoother.Receive(0.05f, -10f, 0f);
        smoother.Snap(1f);

        Assert.Equal(0f, smoother.Display(1f));
    }
}
