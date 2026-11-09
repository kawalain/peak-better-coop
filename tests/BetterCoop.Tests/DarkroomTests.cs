using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class DarkroomTests
{
    [Fact]
    public void A_print_starts_blank_and_ends_clear()
    {
        Assert.Equal(1f, Darkroom.Blank(0f));
        Assert.Equal(0f, Darkroom.Blank(Darkroom.DevelopSeconds));
        Assert.Equal(0f, Darkroom.Blank(10f));
    }

    [Fact]
    public void It_only_ever_comes_up_never_fades_back()
    {
        float before = 1f;
        for (int i = 0; i <= 30; i++)
        {
            float now = Darkroom.Blank(i * Darkroom.DevelopSeconds / 30f);
            Assert.True(now <= before);
            before = now;
        }
    }

    [Fact]
    public void It_stays_warm_while_the_picture_comes_up_and_clears_by_the_end()
    {
        Assert.Equal(1f, Darkroom.Warmth(Darkroom.DevelopSeconds * 0.3f));
        Assert.True(Darkroom.Warmth(Darkroom.DevelopSeconds * 0.7f) is > 0f and < 1f);
        Assert.Equal(0f, Darkroom.Warmth(Darkroom.DevelopSeconds));
    }
}
