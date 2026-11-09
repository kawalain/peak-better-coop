using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class ScrollMotionTests
{
    private static ScrollMotion List(float max = 500f)
    {
        var motion = new ScrollMotion { Max = max };
        motion.Reset();
        return motion;
    }

    private static void Run(ScrollMotion motion, float seconds)
    {
        for (float t = 0f; t < seconds; t += 1f / 60f)
        {
            motion.Step(1f / 60f);
        }
    }

    [Fact]
    public void Glides_to_where_the_wheel_sends_it_rather_than_jumping()
    {
        ScrollMotion motion = List();
        motion.Push(100f);
        motion.Step(1f / 60f);

        Assert.InRange(motion.Position, 1f, 99f);

        Run(motion, 1f);
        Assert.Equal(100f, motion.Position, 1);
    }

    [Fact]
    public void A_travel_arrives_in_its_time_easing_out()
    {
        ScrollMotion motion = List();
        motion.Travel(300f, 0.45f);
        Run(motion, 0.1f);

        Assert.InRange(motion.Position, 100f, 299f);
        Run(motion, 0.4f);
        Assert.Equal(300f, motion.Position, 1);
        Assert.False(motion.Travelling);
    }

    [Fact]
    public void A_push_stops_a_travel_where_it_is()
    {
        ScrollMotion motion = List();
        motion.Travel(400f, 0.45f);
        Run(motion, 0.1f);
        float stopped = motion.Position;
        motion.Push(-10f);
        Run(motion, 1f);

        Assert.False(motion.Travelling);
        Assert.Equal(stopped - 10f, motion.Position, 1);
    }

    [Fact]
    public void A_push_past_the_end_meets_resistance()
    {
        ScrollMotion motion = List(max: 0f);
        motion.Push(100f);
        motion.Step(1f / 60f);
        Run(motion, 0.05f);

        Assert.InRange(motion.Overscroll, 1f, 60f);
    }

    [Fact]
    public void Springs_back_once_the_pushing_stops()
    {
        ScrollMotion motion = List();
        motion.Push(-200f);
        Run(motion, 0.05f);
        Assert.True(motion.Overscroll < 0f);

        Run(motion, 1.5f);
        Assert.Equal(0f, motion.Position, 1);
    }

    [Fact]
    public void Never_stretches_past_its_limit()
    {
        ScrollMotion motion = List();
        for (int i = 0; i < 50; i++)
        {
            motion.Push(1000f);
            motion.Step(1f / 60f);
        }

        Assert.InRange(motion.Position, 500f, 500f + ScrollMotion.Stretch);
    }

    [Fact]
    public void A_selection_glides_only_within_the_list()
    {
        ScrollMotion motion = List();
        motion.GlideTo(900f);
        Run(motion, 1f);

        Assert.Equal(500f, motion.Position, 1);
    }

    [Fact]
    public void A_shorter_list_brings_the_position_back_within_it()
    {
        ScrollMotion motion = List();
        motion.GlideTo(400f);
        Run(motion, 1f);
        motion.Max = 100f;
        Run(motion, 1f);

        Assert.Equal(100f, motion.Position, 1);
    }
}
