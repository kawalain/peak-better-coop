using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.DevTools;
using Xunit;

namespace BetterCoop.Tests;

public class LastRunPairsTests
{
    [Fact]
    public void The_last_Runs_Pairs_come_back_as_they_were_kept()
    {
        var pairs = new List<(string, string)> { ("765611", "765612"), ("765613", "765614") };

        Assert.Equal(pairs, LastRunPairs.Decode(LastRunPairs.Encode(pairs)));
    }

    [Fact]
    public void Broken_lines_are_skipped()
    {
        var pairs = LastRunPairs.Decode("765611 765612\n\nonly-one\n765613 765613\na b c\n765615 765616\n");

        Assert.Equal([("765611", "765612"), ("765615", "765616")], pairs);
    }

    [Fact]
    public void Only_Pairs_whose_two_members_are_here_now_are_planned()
    {
        var actors = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 3, ["e"] = 5 };

        List<Pair> planned = LastRunPairs.Map(
            [("a", "b"), ("c", "d"), ("e", "a")],
            id => actors.TryGetValue(id, out int actor) ? actor : null);

        Assert.Equal([(1, 2)], planned.Select(p => (p.Carrier, p.Rider)));
    }

    [Fact]
    public void An_airport_room_offers_its_scouts_last_Run()
    {
        var room = new FakeRoom(4, troopLeader: true, formed: false);

        Assert.True(PairingRules.IsPlan(room.LastRunPairs, room.Players));
        Assert.NotEmpty(room.LastRunPairs);
    }
}
