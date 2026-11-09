using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class TypewriterTests
{
    [Fact]
    public void A_letter_lands_the_same_way_every_time()
    {
        Assert.Equal(Typewriter.Strike(3, 'A', 7), Typewriter.Strike(3, 'A', 7));
    }

    [Fact]
    public void Letters_land_a_little_off_the_line_and_lean_a_little()
    {
        var strikes = Enumerable.Range(0, 200).Select(i => Typewriter.Strike(i, (char)('a' + i % 26))).ToArray();

        Assert.All(strikes, s =>
        {
            Assert.InRange(s.Rise, -Typewriter.Drift, Typewriter.Drift);
            Assert.InRange(s.Lean, -Typewriter.Lean, Typewriter.Lean);
            Assert.InRange(s.Ink, Typewriter.Faintest, 1f);
        });
        Assert.True(strikes.Select(s => s.Rise).Distinct().Count() > 100);
    }

    [Fact]
    public void Most_letters_take_nearly_full_ink()
    {
        int heavy = Enumerable.Range(0, 400).Count(i => Typewriter.Strike(i, 'x').Ink > 0.9f);

        Assert.True(heavy > 200, $"{heavy}");
    }

    [Fact]
    public void A_short_line_is_typed_a_letter_at_a_time()
    {
        Assert.Equal(0, Typewriter.Typed(-0.1f, 5));
        Assert.Equal(1, Typewriter.Typed(0f, 5));
        Assert.Equal(3, Typewriter.Typed(Typewriter.LetterSeconds * 2.5f, 5));
        Assert.Equal(5, Typewriter.Typed(1f, 5));
    }

    [Fact]
    public void A_long_line_never_takes_longer_than_a_line_does()
    {
        Assert.Equal(200, Typewriter.Typed(Typewriter.LineSeconds, 200));
        Assert.True(Typewriter.Typed(Typewriter.LineSeconds / 2f, 200) < 200);
        // A teleprinter's longer message may take longer.
        Assert.True(Typewriter.Typed(Typewriter.LineSeconds, 200, longest: 1.2f) < 200);
        Assert.Equal(200, Typewriter.Typed(1.2f, 200, longest: 1.2f));
    }
}
