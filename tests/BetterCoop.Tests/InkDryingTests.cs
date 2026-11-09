using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class InkDryingTests
{
    private static readonly float[] Wetnesses = Enumerable.Range(0, 21).Select(i => i / 20f).ToArray();

    [Fact]
    public void Freshly_struck_every_spot_is_full_of_ink()
    {
        Assert.All(Wetnesses, w => Assert.Equal(1f, InkDrying.Left(0f, w), 3));
    }

    [Fact]
    public void Dried_no_spot_keeps_any_ink()
    {
        Assert.All(Wetnesses, w => Assert.Equal(0f, InkDrying.Left(1f, w), 3));
    }

    [Fact]
    public void A_spot_only_ever_loses_ink_as_it_dries()
    {
        foreach (float w in Wetnesses)
        {
            float before = 1f;
            for (int step = 0; step <= 50; step++)
            {
                float left = InkDrying.Left(step / 50f, w);
                Assert.True(left <= before + 1e-5f, $"wetness {w} at {step}");
                before = left;
            }
        }
    }

    [Fact]
    public void Halfway_the_drier_spots_have_gone_and_the_wetter_remain()
    {
        Assert.True(InkDrying.Left(0.5f, 0.2f) < 0.05f);
        Assert.True(InkDrying.Left(0.5f, 0.8f) > 0.95f);
    }

    [Fact]
    public void Wetness_stays_in_range_and_repeats_for_a_seed()
    {
        for (int x = 0; x < 140; x += 7)
        {
            for (int y = 0; y < 60; y += 5)
            {
                float w = InkDrying.Wetness(x, y, 140f, 60f, 3);
                Assert.InRange(w, 0f, 1f);
                Assert.Equal(w, InkDrying.Wetness(x, y, 140f, 60f, 3));
            }
        }
    }

    [Fact]
    public void Another_seed_soaks_in_another_pattern()
    {
        int differ = Enumerable.Range(0, 40)
            .Count(i => System.Math.Abs(InkDrying.Wetness(i * 3.1f, i * 1.7f, 140f, 60f, 1) - InkDrying.Wetness(i * 3.1f, i * 1.7f, 140f, 60f, 2)) > 0.05f);
        Assert.True(differ > 20, $"{differ}");
    }

    [Fact]
    public void The_rim_dries_before_the_middle_on_the_whole()
    {
        float middle = 0f;
        float rim = 0f;
        for (int seed = 0; seed < 40; seed++)
        {
            middle += InkDrying.Wetness(50f, 50f, 100f, 100f, seed);
            rim += InkDrying.Wetness(2f, 50f, 100f, 100f, seed);
        }

        Assert.True(rim < middle, $"rim {rim}, middle {middle}");
    }

    [Fact]
    public void The_noise_spreads_over_most_of_its_range()
    {
        float[] values = Enumerable.Range(0, 2000).Select(i => InkDrying.Noise(i * 0.37f, i * 0.53f, 9)).ToArray();
        Assert.True(values.Min() < 0.15f, $"{values.Min()}");
        Assert.True(values.Max() > 0.85f, $"{values.Max()}");
    }
}
