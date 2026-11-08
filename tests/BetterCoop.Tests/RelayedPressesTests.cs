using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class RelayedPressesTests
{
    [Fact]
    public void Presses_before_the_first_snapshot_are_not_replayed()
    {
        var presses = new RelayedPresses();
        presses.Receive(12);

        Assert.False(presses.Step());
    }

    [Fact]
    public void Presses_arriving_together_play_one_per_frame()
    {
        var presses = new RelayedPresses();
        presses.Receive(0);
        presses.Receive(3);

        bool[] frames = Enumerable.Range(0, 5).Select(_ => presses.Step()).ToArray();

        Assert.Equal([true, true, true, false, false], frames);
    }

    [Fact]
    public void Discarded_presses_are_not_replayed()
    {
        var presses = new RelayedPresses();
        presses.Receive(0);
        presses.Receive(4);
        presses.Discard();

        Assert.False(presses.Step());
    }

    [Fact]
    public void Counter_survives_wraparound()
    {
        var presses = new RelayedPresses();
        presses.Receive(ushort.MaxValue);
        presses.Receive(0);

        Assert.True(presses.Step());
        Assert.False(presses.Step());
    }
}
