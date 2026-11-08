using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class RiderActionsTests
{
    [Fact]
    public void Snapshot_round_trips()
    {
        var sent = new RiderActions
        {
            Sequence = 1234,
            Yaw = 3601.25f,
            Pitch = -42.5f,
            Primary = new ButtonCounters(true, 7, 6),
            Secondary = new ButtonCounters(false, 2, 2),
            ScrollForward = new ButtonCounters(true, 9, 0),
            ScrollBackward = new ButtonCounters(false, 1, 0),
            ScrollTotal = -3.5f,
        };

        Assert.True(RiderActions.TryDecode(sent.Encode(), out RiderActions received));

        Assert.Equal(sent.Sequence, received.Sequence);
        Assert.Equal(sent.Yaw, received.Yaw);
        Assert.Equal(sent.Pitch, received.Pitch);
        Assert.Equal(sent.Primary, received.Primary);
        Assert.Equal(sent.Secondary, received.Secondary);
        Assert.Equal(sent.ScrollForward, received.ScrollForward);
        Assert.Equal(sent.ScrollBackward, received.ScrollBackward);
        Assert.Equal(sent.ScrollTotal, received.ScrollTotal);
    }

    [Fact]
    public void Foreign_payloads_are_rejected()
    {
        Assert.False(RiderActions.TryDecode(null, out _));
        Assert.False(RiderActions.TryDecode(new byte[3], out _));
        Assert.False(RiderActions.TryDecode("text", out _));
    }

    [Fact]
    public void Look_follower_snaps_then_glides()
    {
        var follower = new LookFollower();
        float yaw = 0f, pitch = 0f;
        Assert.False(follower.Step(0.016f, ref yaw, ref pitch));

        follower.Receive(90f, 10f);
        Assert.True(follower.Step(0.016f, ref yaw, ref pitch));
        Assert.Equal(90f, yaw);

        follower.Receive(100f, 10f);
        follower.Step(0.016f, ref yaw, ref pitch);
        Assert.InRange(yaw, 90.1f, 99.9f);
    }
}
