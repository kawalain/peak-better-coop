using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

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
    public void Edges_arriving_together_play_one_per_frame()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        button.Receive(State(false, 2, 2));

        bool[] pressed = Enumerable.Range(0, 5).Select(_ => button.Step().WasPressed).ToArray();
        Assert.Equal([true, false, true, false, false], pressed);
    }

    [Fact]
    public void Clicks_while_the_carrier_cannot_take_input_are_dropped()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        // Two clicks arrive while the Carrier's menu is open.
        button.Receive(State(false, 2, 2));
        button.Discard();

        Assert.All(Enumerable.Range(0, 5).Select(_ => button.Step()), frame =>
        {
            Assert.False(frame.WasPressed);
            Assert.False(frame.IsPressed);
        });
    }

    [Fact]
    public void A_hold_through_a_menu_resumes_without_a_new_press()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        button.Receive(State(true, 1, 0));
        button.Discard();
        ButtonFrame frame = button.Step();

        Assert.False(frame.WasPressed);
        Assert.True(frame.IsPressed);
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

    [Fact]
    public void Clicks_past_the_backlog_are_dropped_and_the_rest_still_alternate()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 0, 0));
        button.Step();

        // A Rider clicking far faster than the Carrier's frames.
        button.Receive(State(false, 500, 500));

        ButtonFrame[] frames = Enumerable.Range(0, 10).Select(_ => button.Step()).ToArray();
        Assert.Equal(RelayedButton.MostWaiting, frames.Count(f => f.WasPressed));
        Assert.Equal(RelayedButton.MostWaiting, frames.Count(f => f.WasReleased));
        Assert.False(frames[^1].IsPressed);
    }

    [Fact]
    public void Counters_that_jump_back_never_replay_thousands_of_clicks()
    {
        var button = new RelayedButton();
        button.Receive(State(false, 500, 500));
        button.Step();

        button.Receive(State(false, 10, 10));

        int presses = Enumerable.Range(0, 1000).Count(_ => button.Step().WasPressed);
        Assert.True(presses <= RelayedButton.MostWaiting);
    }
}
