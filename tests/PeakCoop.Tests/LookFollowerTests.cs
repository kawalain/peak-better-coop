using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class LookFollowerTests
{
    [Theory]
    [InlineData(0f, 90f, 90f)]
    [InlineData(350f, 10f, 20f)]
    [InlineData(10f, 350f, -20f)]
    [InlineData(1000f, 10f, 90f)]
    [InlineData(-725f, 0f, 5f)]
    [InlineData(0f, 180f, 180f)]
    [InlineData(0f, -180f, 180f)]
    public void Delta_angle_is_the_shortest_turn(float from, float to, float expected)
    {
        Assert.Equal(expected, LookFollower.DeltaAngle(from, to), 3);
    }

    [Fact]
    public void Nothing_moves_before_the_first_snapshot()
    {
        var follower = new LookFollower();
        float yaw = 5f, pitch = 6f;

        Assert.False(follower.Step(0.016f, ref yaw, ref pitch));
        Assert.Equal((5f, 6f), (yaw, pitch));
    }

    [Fact]
    public void First_snapshot_turns_to_it_at_once()
    {
        var follower = new LookFollower();
        follower.Receive(30f, -10f);
        float yaw = 0f, pitch = 0f;

        follower.Step(0.016f, ref yaw, ref pitch);

        Assert.Equal(30f, yaw, 3);
        Assert.Equal(-10f, pitch, 3);
    }

    [Fact]
    public void A_rewritten_yaw_turns_the_short_way_round()
    {
        // The game rewrote the body's unbounded yaw (1000, i.e. 280) into 10.
        var follower = new LookFollower();
        follower.Receive(1000f, 0f);
        float yaw = 1000f, pitch = 0f;
        follower.Step(0.016f, ref yaw, ref pitch);
        yaw = 10f;

        float before = yaw;
        for (int i = 0; i < 120; i++)
        {
            follower.Step(0.016f, ref yaw, ref pitch);
            Assert.InRange(yaw, before - 90f - 0.01f, before + 0.01f);
        }

        Assert.Equal(0f, LookFollower.DeltaAngle(yaw, 1000f), 2);
    }
}
