using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class ProgressSmootherTests
{
    [Fact]
    public void The_bar_keeps_moving_between_updates()
    {
        var smoother = new ProgressSmoother();
        smoother.Receive(0.1f, 0f);
        smoother.Receive(0.2f, 0.1f);

        float atSample = Settle(smoother, 0.1f, 0.12f);
        float between = Settle(smoother, 0.12f, 0.18f);

        Assert.True(between > atSample, $"{between} should pass {atSample}");
        Assert.True(between <= 0.29f);
    }

    [Fact]
    public void Uneven_updates_still_move_the_bar_forward_every_frame()
    {
        var smoother = new ProgressSmoother();
        float[] arrivals = [0f, 0.02f, 0.11f, 0.12f, 0.25f, 0.27f, 0.4f];
        float last = 0f;
        int next = 0;
        for (float now = 0f; now <= 0.4f; now += 1f / 60f)
        {
            while (next < arrivals.Length && arrivals[next] <= now)
            {
                smoother.Receive(arrivals[next] * 0.5f, arrivals[next]);
                next++;
            }

            float shown = smoother.Display(now);
            if (now > 0.05f)
            {
                Assert.True(shown >= last, $"went back from {last} to {shown} at {now}");
            }

            last = shown;
        }

        Assert.InRange(last, 0.15f, 0.21f);
    }

    [Fact]
    public void A_stopped_use_clears_at_once()
    {
        var smoother = new ProgressSmoother();
        smoother.Receive(0.5f, 0f);
        smoother.Receive(0.6f, 0.1f);
        Settle(smoother, 0.1f, 0.2f);

        smoother.Receive(0f, 0.2f);

        Assert.Equal(0f, smoother.Display(0.2f));
    }

    [Fact]
    public void A_new_use_starts_from_its_own_progress()
    {
        var smoother = new ProgressSmoother();
        smoother.Receive(0.8f, 0f);
        smoother.Receive(0.9f, 0.1f);
        Settle(smoother, 0.1f, 0.15f);

        smoother.Receive(0.05f, 0.2f);

        Assert.InRange(smoother.Display(0.2f), 0.04f, 0.06f);
    }

    [Fact]
    public void The_bar_never_runs_past_full_or_far_ahead()
    {
        var smoother = new ProgressSmoother();
        smoother.Receive(0.8f, 0f);
        smoother.Receive(0.95f, 0.05f);

        float shown = Settle(smoother, 0.05f, 0.4f);

        Assert.True(shown <= 1f);
    }

    [Fact]
    public void Silence_hides_the_bar()
    {
        var smoother = new ProgressSmoother();
        smoother.Receive(0.3f, 0f);

        Assert.True(smoother.Display(0.1f) > 0f);
        Assert.Equal(0f, smoother.Display(0.1f + ProgressSmoother.StaleSeconds));
    }

    /// <summary>Displays frame by frame from <paramref name="from"/> to <paramref name="to"/>; returns the last value.</summary>
    private static float Settle(ProgressSmoother smoother, float from, float to)
    {
        float shown = 0f;
        for (float now = from; now <= to + 1e-4f; now += 1f / 60f)
        {
            shown = smoother.Display(now);
        }

        return shown;
    }
}
