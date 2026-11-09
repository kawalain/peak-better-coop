using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class FakeRoomTests
{
    private static bool EveryoneOnce(FakeRoom room) =>
        room.Pairs.SelectMany(p => new[] { p.Carrier, p.Rider }).Concat(room.Unpaired())
            .OrderBy(a => a)
            .SequenceEqual(room.Players);

    [Fact]
    public void Before_Pair_Formation_everyone_is_unpaired()
    {
        var room = new FakeRoom(6, troopLeader: true, formed: false);

        Assert.False(room.Formed);
        Assert.Empty(room.Pairs);
        Assert.Equal(6, room.Unpaired().Count());
        Assert.False(room.Shuffle());
    }

    [Fact]
    public void Pair_Formation_pairs_everyone_and_an_odd_party_leaves_one_out()
    {
        var even = new FakeRoom(8, troopLeader: true, formed: true);
        var odd = new FakeRoom(7, troopLeader: true, formed: true);

        Assert.Equal(4, even.Pairs.Count);
        Assert.True(EveryoneOnce(even));
        Assert.Equal(3, odd.Pairs.Count);
        Assert.Single(odd.Unpaired());
        Assert.True(odd.Rule(RunRule.AllowOddPlayerCount));
    }

    [Fact]
    public void A_shuffle_changes_every_partner()
    {
        var room = new FakeRoom(8, troopLeader: true, formed: true);
        var before = room.Pairs.Select(p => (p.Carrier, p.Rider)).ToList();

        Assert.True(room.Shuffle());

        Assert.True(EveryoneOnce(room));
        Assert.DoesNotContain(room.Pairs, p => before.Any(b =>
            (b.Carrier == p.Carrier && b.Rider == p.Rider) || (b.Carrier == p.Rider && b.Rider == p.Carrier)));
    }

    [Fact]
    public void Without_Late_Pairing_those_an_assignment_leaves_out_stay_unpaired()
    {
        var room = new FakeRoom(8, troopLeader: true, formed: true);
        room.SetRule(RunRule.LatePairing, false);
        Pair first = room.Pairs[0];
        Pair second = room.Pairs[1];

        Assert.True(room.Arrange(room.Pairs.Skip(2).Append(new Pair(first.Carrier, second.Carrier)).ToList()));

        Assert.Equal(3, room.Pairs.Count);
        Assert.Equal(2, room.Unpaired().Count());
        Assert.True(EveryoneOnce(room));
    }

    [Fact]
    public void Swapping_places_waits_on_the_rule()
    {
        var room = new FakeRoom(4, troopLeader: false, formed: true);
        Pair mine = room.PairOf(room.Me)!;
        room.SetRule(RunRule.AllowSwapRequests, false);

        Assert.False(room.CanAskSwap);
        room.ToggleSwap();
        Assert.Equal(mine.Carrier, room.PairOf(room.Me)!.Carrier);

        room.SetRule(RunRule.AllowSwapRequests, true);
        Assert.True(room.CanAskSwap);
        room.ToggleSwap();
        Assert.Equal(mine.Rider, room.PairOf(room.Me)!.Carrier);
    }

    [Fact]
    public void Every_rule_starts_out_leaving_players_free()
    {
        var room = new FakeRoom(4, troopLeader: true, formed: false);

        Assert.True(room.Rule(RunRule.FollowRolePreferences));
        Assert.True(room.Rule(RunRule.AllowSwapRequests));
        Assert.True(room.Rule(RunRule.LatePairing));
    }

    [Fact]
    public void An_assignment_pairs_the_two_and_their_former_partners_pair_up()
    {
        var room = new FakeRoom(8, troopLeader: true, formed: true);
        Pair first = room.Pairs[0];
        Pair second = room.Pairs[1];

        Assert.True(room.Arrange(room.Pairs.Skip(2).Append(new Pair(first.Carrier, second.Carrier)).ToList()));

        Assert.Contains(room.Pairs, p => p.Carrier == first.Carrier && p.Rider == second.Carrier);
        Assert.Contains(room.Pairs, p => p.Contains(first.Rider) && p.Contains(second.Rider));
        Assert.Empty(room.Unpaired());
        Assert.True(EveryoneOnce(room));
    }

    [Fact]
    public void An_assignment_needs_two_different_players_after_Pair_Formation()
    {
        var formed = new FakeRoom(4, troopLeader: true, formed: true);
        Assert.False(formed.Arrange([new Pair(2, 2)]));
        Assert.False(new FakeRoom(4, troopLeader: true, formed: false).Arrange([new Pair(1, 2)]));
    }

    [Fact]
    public void A_whole_arrangement_applies_at_once()
    {
        var room = new FakeRoom(6, troopLeader: true, formed: true);

        Assert.True(room.Arrange([new Pair(1, 2), new Pair(3, 4), new Pair(6, 5)]));

        Assert.Equal([(1, 2), (3, 4), (6, 5)], room.Pairs.Select(p => (p.Carrier, p.Rider)));
    }

    [Fact]
    public void A_role_swap_trades_the_two_roles()
    {
        var room = new FakeRoom(4, troopLeader: false, formed: true);
        Pair mine = room.PairOf(room.Me)!;

        room.ToggleSwap();

        Pair after = room.PairOf(room.Me)!;
        Assert.Equal((mine.Rider, mine.Carrier), (after.Carrier, after.Rider));
    }

    [Fact]
    public void A_Timed_Shuffle_comes_when_its_interval_runs_out()
    {
        var room = new FakeRoom(4, troopLeader: true, formed: true);
        Assert.Null(room.SecondsToTimedShuffle);

        room.SetRule(RunRule.TimedShuffle, true);
        room.ShuffleMinutes = 1;
        var before = room.Pairs.Select(p => (p.Carrier, p.Rider)).ToList();
        room.Tick(61f);

        Assert.NotEqual(before, room.Pairs.Select(p => (p.Carrier, p.Rider)).ToList());
        Assert.True(room.SecondsToTimedShuffle > 59);
    }

    [Fact]
    public void The_menu_shape_changes_with_the_Pairs()
    {
        var room = new FakeRoom(4, troopLeader: true, formed: true);
        string before = room.Shape();

        room.Shuffle();

        Assert.NotEqual(before, room.Shape());
    }
}
