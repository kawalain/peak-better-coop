using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class ServerClockTests
{
    private const double JustBeforeWrap = ServerClock.Span - 1;

    [Fact]
    public void Times_away_from_the_wrap_subtract_plainly()
    {
        Assert.Equal(2.5, ServerClock.Since(102.5, 100), 6);
        Assert.Equal(-2.5, ServerClock.Since(100, 102.5), 6);
    }

    [Fact]
    public void Time_keeps_counting_across_the_wrap()
    {
        Assert.Equal(3, ServerClock.Since(2, JustBeforeWrap), 6);
        Assert.Equal(-3, ServerClock.Since(JustBeforeWrap, 2), 6);
    }

    [Fact]
    public void The_later_time_is_the_one_after_the_wrap()
    {
        Assert.Equal(2, ServerClock.Later(JustBeforeWrap, 2));
        Assert.Equal(2, ServerClock.Later(2, JustBeforeWrap));
        Assert.Equal(5, ServerClock.Later(null, 5));
        Assert.Null(ServerClock.Later((double?)null, null));
    }

    [Fact]
    public void A_countdown_runs_across_the_wrap()
    {
        // Announced one second before the wrap, so it runs at 2 s after it.
        var op = new Operation(1, OperationKind.RoleSwap, ServerClock.Span + 2, 1, 2, false);

        Assert.Equal(3, op.SecondsLeft(JustBeforeWrap));
        Assert.Equal(1, op.SecondsLeft(1.5));
        Assert.Equal(0, op.SecondsLeft(2));
        Assert.True(op.Locks(1.6));
        Assert.False(op.Locks(JustBeforeWrap));
    }

    [Fact]
    public void A_received_operation_needs_a_real_time()
    {
        var op = new Operation(1, OperationKind.RoleSwap, double.NaN, 1, 2, false);

        Assert.Null(Operation.Decode(op.Encode()));
    }
}
