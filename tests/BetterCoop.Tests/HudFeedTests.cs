using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class HudFeedTests
{
    private static CarrierHud Hud(sbyte reticle = 0, bool highlighted = false, float throwCharge = 0f, short rope = -1) =>
        new(reticle, highlighted, throwCharge, rope);

    [Fact]
    public void The_first_hud_goes_out_reliably()
    {
        var feed = new HudFeed();

        Assert.True(feed.ShouldSend(Hud(), 0f, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void An_unchanged_hud_is_not_sent_again()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(), 0f, out _);

        Assert.False(feed.ShouldSend(Hud(), 1f, out _));
    }

    [Fact]
    public void A_new_crosshair_goes_out_at_once_and_reliably()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(), 0f, out _);

        Assert.True(feed.ShouldSend(Hud(reticle: 3), 1f, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void A_crosshair_flickering_every_frame_goes_out_at_a_capped_rate()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(), 0f, out _);

        int sent = 0;
        for (int frame = 1; frame <= 240; frame++)
        {
            if (feed.ShouldSend(Hud(highlighted: frame % 2 == 0), frame / 240f, out bool reliable))
            {
                Assert.True(reliable);
                sent++;
            }
        }

        // One second at 240 frames: no more than one change per interval.
        Assert.InRange(sent, 1, (int)(1f / HudFeed.SendInterval) + 1);
    }

    [Fact]
    public void A_change_held_back_goes_out_once_the_interval_passes()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(), 0f, out _);
        feed.ShouldSend(Hud(reticle: 3), 1f, out _);

        Assert.False(feed.ShouldSend(Hud(reticle: 4), 1.001f, out _));
        Assert.True(feed.ShouldSend(Hud(reticle: 4), 1f + HudFeed.SendInterval, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void Throw_charge_is_sent_at_a_steady_rate()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(), 0f, out _);

        Assert.True(feed.ShouldSend(Hud(throwCharge: 0.1f), 0.1f, out bool reliable));
        Assert.False(reliable);
        Assert.False(feed.ShouldSend(Hud(throwCharge: 0.2f), 0.11f, out _));
        Assert.True(feed.ShouldSend(Hud(throwCharge: 0.3f), 0.1f + HudFeed.SendInterval, out _));
    }

    [Fact]
    public void The_end_of_a_throw_goes_out_at_once_and_reliably()
    {
        var feed = new HudFeed();
        feed.ShouldSend(Hud(throwCharge: 0.5f), 0f, out _);

        Assert.True(feed.ShouldSend(Hud(throwCharge: 0f), HudFeed.SendInterval, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void A_hud_survives_encoding()
    {
        CarrierHud hud = Hud(reticle: CarrierHud.NoReticle, highlighted: true, throwCharge: 0.4f, rope: 12);

        Assert.Equal(hud, CarrierHud.Decode(hud.Encode()));
        Assert.Null(CarrierHud.Decode(new object[] { 1L, true, 0f, (short)1 }));
        Assert.Null(CarrierHud.Decode(new object[] { 200, true, 0f, (short)1 }));
    }
}
