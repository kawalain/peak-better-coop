using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class WheelSignalTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void An_open_survives_the_wire(bool equipped, bool riderPicks)
    {
        WheelSignal sent = WheelSignal.Open(7, equipped, riderPicks, 1234, 4, 3);

        Assert.True(WheelSignal.TryDecode(sent.Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Open, got.Kind);
        Assert.Equal(7, got.Session);
        Assert.Equal(equipped, got.Equipped);
        Assert.Equal(riderPicks, got.RiderPicks);
        Assert.Equal(1234, got.ViewId);
        Assert.Equal(4, got.SlotCount);
        Assert.Equal(3, got.BackpackType);
    }

    [Fact]
    public void A_pick_survives_the_wire()
    {
        WheelSignal sent = WheelSignal.Pick(9, WheelSlice.Slot, 2);

        Assert.True(WheelSignal.TryDecode(sent.Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Pick, got.Kind);
        Assert.Equal(9, got.Session);
        Assert.Equal(WheelSlice.Slot, got.Slice);
        Assert.Equal(2, got.SlotId);
    }

    [Fact]
    public void A_hover_survives_the_wire()
    {
        Assert.True(WheelSignal.TryDecode(WheelSignal.Hover(4, WheelSlice.Fuel, 0).Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Hover, got.Kind);
        Assert.Equal(4, got.Session);
        Assert.True(got.HasSlice);
        Assert.Equal(WheelSlice.Fuel, got.Slice);
    }

    [Fact]
    public void Pointing_at_nothing_survives_the_wire()
    {
        Assert.True(WheelSignal.TryDecode(WheelSignal.NoHover(4).Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Hover, got.Kind);
        Assert.False(got.HasSlice);
    }

    [Fact]
    public void A_close_survives_the_wire()
    {
        Assert.True(WheelSignal.TryDecode(WheelSignal.Close(3).Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Close, got.Kind);
        Assert.Equal(3, got.Session);
    }

    [Fact]
    public void Hover_keys_tell_slices_apart()
    {
        Assert.Equal(-1, WheelSignal.HoverKey(null, 0));
        Assert.NotEqual(WheelSignal.HoverKey(WheelSlice.Slot, 1), WheelSignal.HoverKey(WheelSlice.Slot, 2));
        Assert.NotEqual(WheelSignal.HoverKey(WheelSlice.Slot, 0), WheelSignal.HoverKey(WheelSlice.Wear, 0));
        Assert.NotEqual(-1, WheelSignal.HoverKey(WheelSlice.Slot, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("open")]
    public void Anything_but_an_int_array_is_refused(object? payload) =>
        Assert.False(WheelSignal.TryDecode(payload, out _));

    [Fact]
    public void A_wrong_length_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { 1, 2, 3 }, out _));

    [Fact]
    public void An_unknown_kind_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { 9, 1, 0, 0, 0, 0 }, out _));

    [Fact]
    public void An_unknown_slice_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Pick, 1, 4, 0, 0, 0 }, out _));

    [Fact]
    public void A_pick_of_no_slice_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Pick, 1, -1, 0, 0, 0 }, out _));

    [Fact]
    public void A_slot_beyond_a_byte_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Pick, 1, 0, 256, 0, 0 }, out _));

    [Fact]
    public void An_open_with_a_bad_slot_count_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Open, 1, 1, 5, -1, 1 }, out _));

    [Fact]
    public void An_open_with_unknown_flags_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Open, 1, 4, 5, 4, 1 }, out _));
}
