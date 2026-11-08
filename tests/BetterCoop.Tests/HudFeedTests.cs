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

        Assert.True(feed.ShouldSend(Hud(reticle: 3), 0.001f, out bool reliable));
        Assert.True(reliable);
        Assert.True(feed.ShouldSend(Hud(reticle: 3, highlighted: true), 0.002f, out reliable));
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

        Assert.True(feed.ShouldSend(Hud(throwCharge: 0f), 0.001f, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void A_hud_survives_encoding()
    {
        CarrierHud hud = Hud(reticle: CarrierHud.NoReticle, highlighted: true, throwCharge: 0.4f, rope: 12);

        Assert.Equal(hud, CarrierHud.Decode(hud.Encode()));
        Assert.Null(CarrierHud.Decode(new object[] { 1, true, 0f, (short)1 }));
    }
}
