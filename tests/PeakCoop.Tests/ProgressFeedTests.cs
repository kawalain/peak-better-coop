using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class ProgressFeedTests
{
    [Fact]
    public void Nothing_is_sent_while_idle()
    {
        var feed = new ProgressFeed();

        Assert.False(feed.ShouldSend(0f, 1f, out _));
        Assert.False(feed.ShouldSend(0f, 2f, out _));
    }

    [Fact]
    public void Progress_is_sent_at_once_then_at_the_send_rate()
    {
        var feed = new ProgressFeed();

        Assert.True(feed.ShouldSend(0.1f, 1f, out bool reliable));
        Assert.False(reliable);
        Assert.False(feed.ShouldSend(0.2f, 1f + (ProgressFeed.SendInterval / 2f), out _));
        Assert.True(feed.ShouldSend(0.3f, 1f + ProgressFeed.SendInterval, out _));
    }

    [Fact]
    public void Stopping_is_sent_once_and_reliably()
    {
        var feed = new ProgressFeed();
        feed.ShouldSend(0.5f, 1f, out _);

        Assert.True(feed.ShouldSend(0f, 1.001f, out bool reliable));
        Assert.True(reliable);
        Assert.False(feed.ShouldSend(0f, 2f, out _));
    }

    [Fact]
    public void A_new_action_after_a_stop_is_sent_at_once()
    {
        var feed = new ProgressFeed();
        feed.ShouldSend(0.5f, 1f, out _);
        feed.ShouldSend(0f, 1.001f, out _);

        Assert.True(feed.ShouldSend(0.05f, 1.002f, out _));
    }
}
