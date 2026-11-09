using System;
using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class WireLimitsTests
{
    [Theory]
    [InlineData("Alder", "Alder")]
    [InlineData("  Birch  ", "Birch")]
    [InlineData("<size=500%>Big</size>", "‹size=500%›Big‹/size›")]
    [InlineData("a\u0000b‮c", "abc")]
    [InlineData("", "#3")]
    [InlineData("   ", "#3")]
    [InlineData(null, "#3")]
    public void A_name_cannot_restyle_the_paper_or_hide_itself(string? name, string shown)
    {
        Assert.Equal(shown, WireLimits.Name(name, "#3"));
    }

    [Fact]
    public void A_long_name_is_cut_with_an_ellipsis_whole_letters_at_a_time()
    {
        string shown = WireLimits.Name(new string('가', 5000), "#1");

        Assert.Equal(WireLimits.MaxNameLetters, shown.Length);
        Assert.EndsWith("…", shown);
        Assert.Equal("👍👍", WireLimits.Name("👍👍", "#1"));
    }

    [Fact]
    public void A_rate_gate_lets_a_burst_through_then_so_many_a_second()
    {
        var gate = new RateGate(capacity: 3, perSecond: 2);

        Assert.Equal(3, Enumerable.Range(0, 10).Count(_ => gate.Take(0)));
        Assert.False(gate.Take(0.4));
        Assert.True(gate.Take(0.5));
        Assert.Equal(3, Enumerable.Range(0, 10).Count(_ => gate.Take(100)));
    }

    [Fact]
    public void The_same_notice_soon_after_or_a_flood_is_held_back()
    {
        var gate = new NoticeGate();

        Assert.True(gate.Allow("a", 0));
        Assert.False(gate.Allow("a", 1));
        Assert.True(gate.Allow("a", NoticeGate.RepeatSeconds + 0.1));
        int shown = Enumerable.Range(0, 100).Count(i => gate.Allow($"flood {i}", 5));
        Assert.InRange(shown, 1, 6);
    }

    [Fact]
    public void A_handover_plan_too_large_to_hold_is_refused_before_it_is_read()
    {
        // Counts whose product overflows to zero once let a small message ask for gigabytes.
        object huge = new object[] { 1, true, 65536, new int[65536], new int[65536], new float[65536 * 3], Array.Empty<float>(), new float[65536 * 2] };
        object notAPlace = new HandoverPlan(1, true, [new HandoverEntry(2, 1, [float.NaN, 0f, 0f], [], 0f, 0f)]).Encode();

        Assert.Null(HandoverPlan.Decode(huge));
        Assert.Null(HandoverPlan.Decode(notAPlace));
        Assert.NotNull(HandoverPlan.Decode(new HandoverPlan(1, true, [new HandoverEntry(2, 1, [1f, 2f, 3f], [0.1f], 0.5f, 0f)]).Encode()));
    }

    [Fact]
    public void A_carry_record_naming_more_scouts_than_a_room_holds_is_refused()
    {
        int many = WireLimits.MaxScouts + 1;
        object[] value =
        [
            Enumerable.Range(0, many).Select(i => $"id{i}").ToArray(),
            Enumerable.Range(0, many).Select(i => $"n{i}").ToArray(),
            new double[many], new double[many], new double[many],
            Array.Empty<int>(), Array.Empty<string>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<double>(),
        ];

        Assert.Null(CarryRecord.Decode(value));
    }

    [Fact]
    public void A_carry_record_shows_names_made_safe()
    {
        var record = new CarryRecord();
        record.Count([new PairSample("a", "<b>Alder</b>", "b", "Birch", true, 0)], 1, 0, "SHORE");

        CarryRecord read = CarryRecord.Decode(record.Encode())!;

        Assert.Equal("‹b›Alder‹/b›", read.Find("a")!.Name);
    }

    [Fact]
    public void Pairs_and_parted_Pairs_past_the_limits_are_refused()
    {
        Assert.Null(Arrangement.Decode(new object[] { 1, new int[(2 * WireLimits.MaxScouts) + 2] }, 1));
        Assert.Empty(PartedPairs.Decode(Enumerable.Repeat("x", (2 * WireLimits.MaxParted) + 2).ToArray()).Encode());
        Assert.Empty(PartedPairs.Decode(new[] { new string('x', WireLimits.MaxIdLength + 1), "y" }).Encode());
    }

    [Fact]
    public void A_Rider_look_that_is_not_a_number_is_refused()
    {
        byte[] good = new RiderActions { Sequence = 1, Yaw = 2f, Pitch = 3f }.Encode();
        byte[] bad = new RiderActions { Sequence = 1, Yaw = float.NaN, Pitch = 3f }.Encode();

        Assert.True(RiderActions.TryDecode(good, out _));
        Assert.False(RiderActions.TryDecode(bad, out _));
        Assert.Null(CarrierHud.Decode(new CarrierHud(CarrierHud.NoReticle, true, float.PositiveInfinity, 3).Encode()));
    }
}
