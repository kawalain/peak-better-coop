using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class WheelSignalTests
{
    [Fact]
    public void An_open_survives_the_wire()
    {
        WheelSignal sent = WheelSignal.Open(7, true, 1234, 4, 3);

        Assert.True(WheelSignal.TryDecode(sent.Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Open, got.Kind);
        Assert.Equal(7, got.Session);
        Assert.True(got.Equipped);
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
    public void A_close_survives_the_wire()
    {
        Assert.True(WheelSignal.TryDecode(WheelSignal.Close(3).Encode(), out WheelSignal got));
        Assert.Equal(WheelSignalKind.Close, got.Kind);
        Assert.Equal(3, got.Session);
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
    public void A_slot_beyond_a_byte_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Pick, 1, 0, 256, 0, 0 }, out _));

    [Fact]
    public void An_open_with_a_bad_slot_count_is_refused() =>
        Assert.False(WheelSignal.TryDecode(new[] { (int)WheelSignalKind.Open, 1, 1, 5, -1, 1 }, out _));
}
