using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class RoomWardTests
{
    [Fact]
    public void A_value_the_Troop_Leader_wrote_is_its_own()
    {
        var ward = new RoomWard();
        ward.Wrote("Pairs", new object[] { new[] { 1, 2 }, new int[0] }, now: 0);

        Assert.True(ward.IsOwn("Pairs", new object[] { new[] { 1, 2 }, new int[0] }, now: 1));
        Assert.False(ward.IsOwn("Pairs", new object[] { new[] { 2, 1 }, new int[0] }, now: 1));
    }

    [Fact]
    public void An_earlier_write_echoing_back_late_is_still_its_own()
    {
        var ward = new RoomWard();
        ward.Wrote("Rules", 5, now: 0);
        ward.Wrote("Rules", 7, now: 0.2);

        Assert.True(ward.IsOwn("Rules", 5, now: 0.3));
        Assert.False(ward.IsOwn("Rules", 5, now: RoomWard.EchoSeconds + 1));
        Assert.True(ward.IsOwn("Rules", 7, now: RoomWard.EchoSeconds + 1));
    }

    [Fact]
    public void Another_players_value_is_put_back_to_the_last_one_written()
    {
        var ward = new RoomWard();
        ward.Wrote("ShuffleClock", 100.0, now: 0);

        Assert.False(ward.IsOwn("ShuffleClock", 99999.0, now: 1));
        Assert.Equal(100.0, ward.Restore("ShuffleClock"));
    }

    [Fact]
    public void A_key_never_written_stays_empty()
    {
        var ward = new RoomWard();

        Assert.True(ward.IsOwn("Plan", null, now: 0));
        Assert.False(ward.IsOwn("Plan", new[] { 1, 2 }, now: 0));
        Assert.Null(ward.Restore("Plan"));
    }

    [Fact]
    public void A_new_Troop_Leader_trusts_what_the_room_holds()
    {
        var ward = new RoomWard();
        ward.Trust([new("Rules", (object?)9)], now: 0);

        Assert.True(ward.IsOwn("Rules", 9, now: 60));
    }

    [Fact]
    public void Values_of_other_types_print_apart()
    {
        Assert.NotEqual(RoomWard.Print(1), RoomWard.Print(1.0));
        Assert.NotEqual(RoomWard.Print(new[] { 1, 2 }), RoomWard.Print(new[] { 12 }));
        Assert.Equal(RoomWard.Print(new[] { "a", "b" }), RoomWard.Print(new[] { "a", "b" }));
    }
}
