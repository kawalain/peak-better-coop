using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class DittoTests
{
    [Fact]
    public void Nothing_is_printed_before_the_drum_turns_and_all_of_it_after()
    {
        foreach (float depth in new[] { 0f, 0.5f, 1f })
        {
            Assert.Equal(0f, Ditto.At(depth, 0f).Printed);
            Assert.Equal((1f, 0f), Ditto.At(depth, Ditto.TurnSeconds));
            Assert.True(Ditto.Done(Ditto.TurnSeconds));
        }
    }

    [Fact]
    public void The_top_of_the_sheet_prints_before_its_foot()
    {
        for (int i = 1; i < 20; i++)
        {
            float elapsed = i * Ditto.TurnSeconds / 20f;
            Assert.True(Ditto.At(0.1f, elapsed).Printed >= Ditto.At(0.9f, elapsed).Printed);
        }

        float halfway = Ditto.TurnSeconds / 2f;
        Assert.True(Ditto.At(0.1f, halfway).Printed > Ditto.At(0.9f, halfway).Printed);
    }

    [Fact]
    public void Fresh_words_are_violet_and_settle_into_their_own_ink()
    {
        float elapsed = Ditto.TurnSeconds * 0.5f;
        (float printed, float violet) = Ditto.At(0.45f, elapsed);

        Assert.True(printed > 0.5f);
        Assert.True(violet > 0.3f);
        Assert.True(Ditto.At(0.05f, elapsed).Violet < violet);
    }
}
