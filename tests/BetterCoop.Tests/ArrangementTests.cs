using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class ArrangementTests
{
    [Fact]
    public void Pairs_survive_the_room_and_belong_to_their_operation()
    {
        Pair[] pairs = [new Pair(1, 2), new Pair(5, 3)];
        object[] encoded = Arrangement.Encode(7, pairs);

        Assert.Equal(7, Arrangement.IdOf(encoded));
        Assert.Equal([(1, 2), (5, 3)], Arrangement.Decode(encoded, 7)!.Select(p => (p.Carrier, p.Rider)));
        Assert.Null(Arrangement.Decode(encoded, 8));
    }

    [Fact]
    public void Anything_else_is_not_an_arrangement()
    {
        Assert.Null(Arrangement.Decode(null, 1));
        Assert.Null(Arrangement.Decode(new object[] { 1, new[] { 1, 2, 3 } }, 1));
        Assert.Null(Arrangement.Decode(new object[] { 1, "pairs" }, 1));
        Assert.Equal(0, Arrangement.IdOf(null));
    }

    [Fact]
    public void An_empty_arrangement_ends_every_pair() =>
        Assert.Empty(Arrangement.Decode(Arrangement.Encode(2, []), 2)!);
}
