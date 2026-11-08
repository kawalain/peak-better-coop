using System.Collections.Generic;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class BarHidingTests
{
    /// <summary>A HUD laid out as paths: "group/bar" lies inside "group".</summary>
    private static bool IsInside(string piece, string outer) => piece.StartsWith(outer + "/");

    private static HashSet<string> Pick(params (string, string)[] pieces) =>
        BarHiding.Pick(pieces, character => character is "rider" or "carrier", IsInside);

    [Fact]
    public void A_riders_bar_is_hidden_and_others_stay() =>
        Assert.Equal(["group/bar2"], Pick(("group/bar1", "someone"), ("group/bar2", "rider")));

    [Fact]
    public void A_container_of_several_players_is_never_hidden()
    {
        // A list keeper that remembers whom the list centres on.
        HashSet<string> picked = Pick(
            ("group", "carrier"),
            ("group/bar1", "someone"),
            ("group/bar2", "carrier"));

        Assert.Equal(["group/bar2"], picked);
    }

    [Fact]
    public void A_bar_with_its_own_parts_is_hidden_whole()
    {
        HashSet<string> picked = Pick(
            ("group/bar", "rider"),
            ("group/bar/statuses", "rider"),
            ("group/bar/inventory", "rider"));

        Assert.Equal(["group/bar"], picked);
    }

    [Fact]
    public void Nothing_is_hidden_without_a_hidden_character() =>
        Assert.Empty(Pick(("group/bar1", "someone"), ("group/bar2", "another")));
}
