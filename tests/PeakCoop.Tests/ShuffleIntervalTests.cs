using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

public class ShuffleIntervalTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(60, 60)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(61, 60)]
    [InlineData(255, 60)]
    public void The_interval_stays_between_one_and_sixty_minutes(int minutes, int expected)
    {
        Assert.Equal(expected, ShuffleInterval.Clamp(minutes));
    }
}
