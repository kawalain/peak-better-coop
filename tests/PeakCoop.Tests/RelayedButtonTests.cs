using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class RelayedButtonTests
{
    private static ButtonCounters State(bool down, ushort presses, ushort releases) => new(down, presses, releases);

    [Fact]
    public void Idle_button_stays_up()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));

        ButtonFrame frame = button.Step();

        Assert.False(frame.WasPressed);
        Assert.False(frame.IsPressed);
        Assert.False(frame.WasReleased);
    }

    [Fact]
    public void Quick_click_in_one_snapshot_plays_as_press_then_release()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        button.Receive(State(false, 1, 1));
        ButtonFrame press = button.Step();
        ButtonFrame release = button.Step();
        ButtonFrame after = button.Step();

        Assert.True(press.WasPressed);
        Assert.True(press.IsPressed);
        Assert.True(release.WasReleased);
        Assert.False(release.IsPressed);
        Assert.False(after.IsPressed);
        Assert.False(after.WasPressed);
    }

    [Fact]
    public void Held_button_stays_held_through_a_packet_gap()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();
        button.Receive(State(true, 1, 0));
        button.Step();

        // No snapshot for many frames.
        for (int i = 0; i < 100; i++)
        {
            ButtonFrame frame = button.Step();
            Assert.True(frame.IsPressed);
            Assert.False(frame.WasReleased);
        }
    }

    [Fact]
    public void Button_already_held_at_first_snapshot_is_pressed()
    {
        var button = new RelayedButton();
        button.Receive(State(true, 5, 4));

        ButtonFrame frame = button.Step();

        Assert.True(frame.WasPressed);
        Assert.True(frame.IsPressed);
    }

    [Fact]
    public void Edges_wait_while_the_carrier_cannot_take_input()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        // Two clicks arrive while the Carrier's menu is open (no Step).
        button.Receive(State(false, 2, 2));

        bool[] pressed = Enumerable.Range(0, 5).Select(_ => button.Step().WasPressed).ToArray();
        Assert.Equal([true, false, true, false, false], pressed);
    }

    [Fact]
    public void Lost_history_settles_on_the_latest_state()
    {
        var button = new RelayedButton();
        button.Receive(State(true, 1, 0));
        button.Step();

        // Inconsistent counters (more presses than releases, yet up): replay, then trust the state.
        button.Receive(State(false, 3, 1));
        ButtonFrame first = button.Step();
        Assert.True(first.WasReleased);

        ButtonFrame[] rest = Enumerable.Range(0, 4).Select(_ => button.Step()).ToArray();
        Assert.False(rest[^1].IsPressed);
        Assert.False(rest[^1].WasPressed);
    }

    [Fact]
    public void Counters_survive_wraparound()
    {
        var button = new RelayedButton();
        button.Receive(State(false, ushort.MaxValue, ushort.MaxValue));
        button.Step();

        button.Receive(State(true, 0, ushort.MaxValue));
        ButtonFrame frame = button.Step();

        Assert.True(frame.WasPressed);
        Assert.True(frame.IsPressed);
    }
}
