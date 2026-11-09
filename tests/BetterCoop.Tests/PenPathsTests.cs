using System;
using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PenPathsTests
{
    [Fact]
    public void A_ring_stays_inside_its_box_and_overshoots_its_start()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var ring = PenPaths.Ring(120f, 40f, 2.8f, new Random(seed));

            Assert.All(ring, p =>
            {
                Assert.InRange(p.X, 0f, 120f);
                Assert.InRange(p.Y, 0f, 40f);
            });
            Assert.True(PenPaths.Length(ring) > Math.PI * 2 * 20);
        }
    }

    [Fact]
    public void No_two_marks_are_drawn_quite_the_same()
    {
        var hand = new Random(1);

        Assert.NotEqual(PenPaths.Tick(46f, hand), PenPaths.Tick(46f, hand));
        Assert.NotEqual(PenPaths.Ring(120f, 40f, 2.8f, hand), PenPaths.Ring(120f, 40f, 2.8f, hand));
    }

    [Fact]
    public void A_tick_starts_in_the_box_dips_and_runs_past_its_top_right()
    {
        float box = PenPaths.CheckBox(46f, out float x, out float y);
        var tick = PenPaths.Tick(46f, new Random(2));

        Assert.InRange(tick[0].X, x, x + box / 2f);
        Assert.True(tick.Max(p => p.Y) > y + box * 0.8f);
        Assert.True(tick[^1].X > x + box && tick[^1].Y < y);
    }

    [Fact]
    public void Length_adds_up_the_segments() =>
        Assert.Equal(7f, PenPaths.Length([(0f, 0f), (3f, 4f), (5f, 4f)]), 4);
}

public class EasingTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public void Eases_from_none_to_all(float t, float expected)
    {
        Assert.Equal(expected, Easing.Out(t), 4);
        Assert.Equal(expected, Easing.InOut(t), 4);
        Assert.Equal(expected, Easing.Pop(t), 4);
    }

    [Fact]
    public void A_pop_springs_past_the_end_before_settling() =>
        Assert.True(Easing.Pop(0.7f) > 1f);

    [Fact]
    public void Out_is_ahead_and_in_out_is_even_at_half()
    {
        Assert.True(Easing.Out(0.5f) > 0.8f);
        Assert.Equal(0.5f, Easing.InOut(0.5f), 4);
    }

    [Fact]
    public void Towards_settles_the_same_whatever_the_frame_rate()
    {
        float coarse = 10f;
        float fine = 10f;
        for (int i = 0; i < 10; i++)
        {
            coarse = Easing.Towards(coarse, 0f, 0.3f, 0.03f);
        }

        for (int i = 0; i < 30; i++)
        {
            fine = Easing.Towards(fine, 0f, 0.3f, 0.01f);
        }

        Assert.Equal(coarse, fine, 3);
        Assert.True(coarse < 0.2f);
    }
}
