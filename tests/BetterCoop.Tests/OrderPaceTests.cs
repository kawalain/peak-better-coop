using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class OrderPaceTests
{
    [Fact]
    public void An_order_pressed_again_at_once_waits_a_moment()
    {
        var pace = new OrderPace();
        Assert.True(pace.Ready("Shuffle", 0));
        pace.Went("Shuffle", 0);

        Assert.False(pace.Ready("Shuffle", 0.2));
        Assert.True(pace.Ready("Shuffle", OrderPace.Seconds));
    }

    [Fact]
    public void Each_kind_of_order_has_its_own_pace()
    {
        // Calling off right after ordering a shuffle still works.
        var pace = new OrderPace();
        pace.Went("Shuffle", 0);

        Assert.True(pace.Ready("CallOffShuffle", 0.1));
    }

    [Fact]
    public void An_order_that_did_not_go_through_does_not_count()
    {
        var pace = new OrderPace();
        Assert.True(pace.Ready("Arrange", 0));

        Assert.True(pace.Ready("Arrange", 0.1));
    }

    [Fact]
    public void Mashing_an_order_lets_through_one_a_second()
    {
        var pace = new OrderPace();
        int went = 0;
        for (int press = 0; press < 100; press++)
        {
            double now = press * 0.05;
            if (pace.Ready("Recover", now))
            {
                pace.Went("Recover", now);
                went++;
            }
        }

        Assert.Equal(5, went);
    }
}

public class SourceGateTests
{
    [Fact]
    public void One_player_asking_again_and_again_shows_one_notice_a_few_seconds()
    {
        var gate = new SourceGate();
        int shown = Enumerable.Range(0, 200).Count(i => gate.Allow(source: 2, now: i * 0.05));

        // Ten seconds of presses at 20 a second.
        Assert.InRange(shown, 1, 4);
    }

    [Fact]
    public void Several_players_share_a_small_budget()
    {
        var gate = new SourceGate();
        int shown = Enumerable.Range(0, 30).Count(i => gate.Allow(source: i, now: 0));

        Assert.Equal(3, shown);
    }
}
