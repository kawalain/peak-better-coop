using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class TicketMotionTests
{
    [Fact]
    public void Rises_from_a_full_travel_below_and_rests_in_place()
    {
        Assert.Equal(-1f, TicketMotion.RiseOffset(0f), 3);
        Assert.Equal(0f, TicketMotion.RiseOffset(TicketMotion.RiseSeconds), 3);
        Assert.Equal(0f, TicketMotion.RiseTilt(TicketMotion.RiseSeconds), 3);
        Assert.Equal(1f, TicketMotion.RiseAlpha(TicketMotion.RiseSeconds), 3);
    }

    [Fact]
    public void Passes_its_place_a_little_before_settling()
    {
        float highest = 0f;
        for (float t = 0f; t < TicketMotion.RiseSeconds; t += 0.005f)
        {
            highest = System.Math.Max(highest, TicketMotion.RiseOffset(t));
        }

        Assert.InRange(highest, 0.02f, 0.06f);
    }

    [Fact]
    public void The_rise_has_no_jumps()
    {
        for (float t = 0f; t < TicketMotion.RiseSeconds; t += 0.01f)
        {
            Assert.InRange(TicketMotion.RiseOffset(t + 0.01f) - TicketMotion.RiseOffset(t), -0.01f, 0.2f);
            Assert.InRange(System.Math.Abs(TicketMotion.RiseTilt(t + 0.01f) - TicketMotion.RiseTilt(t)), 0f, 0.3f);
        }
    }

    [Fact]
    public void Tips_over_while_rising()
    {
        Assert.Equal(TicketMotion.RiseTiltDegrees, TicketMotion.RiseTilt(1f / 3f), 2);
        Assert.True(TicketMotion.RiseTilt(0.49f) < 0f);
    }

    [Fact]
    public void Sinks_with_a_small_lift_then_drops_away_and_fades()
    {
        Assert.Equal(0f, TicketMotion.SinkOffset(0f), 3);
        Assert.True(TicketMotion.SinkOffset(TicketMotion.SinkSeconds * 0.3f) > 0f);
        Assert.Equal(-1f, TicketMotion.SinkOffset(TicketMotion.SinkSeconds), 3);
        Assert.Equal(1f, TicketMotion.SinkAlpha(0f), 3);
        Assert.Equal(0f, TicketMotion.SinkAlpha(TicketMotion.SinkSeconds), 3);
    }
}
