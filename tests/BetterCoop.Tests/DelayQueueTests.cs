using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class DelayQueueTests
{
    [Fact]
    public void Item_waits_for_the_full_delay()
    {
        var queue = new DelayQueue<int>();
        queue.Enqueue(1, now: 10f);

        Assert.False(queue.TryDequeue(10.05f, delay: 0.1f, out _));
        Assert.True(queue.TryDequeue(10.1f, delay: 0.1f, out int item));
        Assert.Equal(1, item);
    }

    [Fact]
    public void Items_leave_in_order()
    {
        var queue = new DelayQueue<int>();
        queue.Enqueue(1, now: 0f);
        queue.Enqueue(2, now: 0.01f);
        queue.Enqueue(3, now: 0.5f);

        var released = new List<int>();
        while (queue.TryDequeue(0.2f, delay: 0.1f, out int item))
        {
            released.Add(item);
        }

        Assert.Equal([1, 2], released);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void Zero_delay_releases_at_once()
    {
        var queue = new DelayQueue<string>();
        queue.Enqueue("a", now: 3f);

        Assert.True(queue.TryDequeue(3f, delay: 0f, out string item));
        Assert.Equal("a", item);
    }
}
