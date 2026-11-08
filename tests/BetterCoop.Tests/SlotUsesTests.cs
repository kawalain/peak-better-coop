using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class SlotUsesTests
{
    [Fact]
    public void Slots_start_without_a_uses_bar()
    {
        var uses = new SlotUses(0);

        for (int slot = 0; slot < SlotUses.Count; slot++)
        {
            Assert.Null(uses[slot]);
        }
    }

    [Fact]
    public void Each_slot_keeps_its_own_fraction()
    {
        SlotUses uses = new SlotUses(0).With(0, 1f).With(1, 0f).With(SlotUses.BackSlot, 0.5f);

        Assert.Equal(1f, uses[0]);
        Assert.Equal(0f, uses[1]);
        Assert.Null(uses[2]);
        Assert.Equal(0.5f, uses[SlotUses.BackSlot]!.Value, 2);
    }

    [Fact]
    public void A_slot_can_lose_its_uses_bar_again()
    {
        SlotUses uses = new SlotUses(0).With(SlotUses.BackSlot, 0.3f).With(SlotUses.BackSlot, null);

        Assert.Null(uses[SlotUses.BackSlot]);
        Assert.Equal(new SlotUses(0), uses);
    }

    [Fact]
    public void Fractions_are_kept_to_within_one_step()
    {
        SlotUses uses = new SlotUses(0).With(2, 0.3333f);

        Assert.InRange(uses[2]!.Value, 0.3333f - 1f / 254, 0.3333f + 1f / 254);
        Assert.True(uses.Shows(2, 0.3334f));
        Assert.False(uses.Shows(2, 0.5f));
    }

    [Fact]
    public void Out_of_range_fractions_are_clamped()
    {
        SlotUses uses = new SlotUses(0).With(0, 1.5f).With(1, -0.2f);

        Assert.Equal(1f, uses[0]);
        Assert.Equal(0f, uses[1]);
    }

    [Fact]
    public void A_change_in_uses_goes_out_at_once_and_reliably()
    {
        var feed = new HudFeed();
        feed.ShouldSend(new CarrierHud(0, false, 0f, -1, new SlotUses(0).With(0, 1f)), 0f, out _);

        Assert.True(feed.ShouldSend(new CarrierHud(0, false, 0f, -1, new SlotUses(0).With(0, 0.9f)), 0.001f, out bool reliable));
        Assert.True(reliable);
    }

    [Fact]
    public void Uses_survive_the_wire()
    {
        var hud = new CarrierHud(1, false, 0f, -1, new SlotUses(0).With(0, 0.25f).With(SlotUses.BackSlot, 1f));

        Assert.Equal(hud, CarrierHud.Decode(hud.Encode()));
    }
}
