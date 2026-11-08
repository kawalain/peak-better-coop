using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PairingRulesTests
{
    private static PairCandidate C(int actor, RolePreference preference = RolePreference.Random) => new(actor, preference);

    private static PairingResult Form(int seed, params PairCandidate[] candidates) =>
        PairingRules.Form(candidates, new Random(seed));

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Everyone_lands_in_exactly_one_pair(int count)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Form(seed, Enumerable.Range(1, count).Select(a => C(a)).ToArray());

            int[] members = result.Pairs.SelectMany(p => new[] { p.Carrier, p.Rider }).ToArray();
            Assert.Equal(Enumerable.Range(1, count), members.OrderBy(a => a));
            Assert.Empty(result.Unpaired);
            Assert.Empty(result.PreferenceOverridden);
        }
    }

    [Fact]
    public void Compatible_preferences_are_honoured()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Form(
                seed,
                C(1, RolePreference.Carrier),
                C(2, RolePreference.Rider),
                C(3, RolePreference.Carrier),
                C(4));

            Assert.Contains(result.Pairs, p => p.Carrier == 1);
            Assert.Contains(result.Pairs, p => p.Carrier == 3);
            Assert.Contains(result.Pairs, p => p.Rider == 2);
            Assert.Contains(result.Pairs, p => p.Rider == 4);
            Assert.Empty(result.PreferenceOverridden);
        }
    }

    [Fact]
    public void Two_would_be_carriers_split_and_one_is_reported()
    {
        PairingResult result = Form(7, C(1, RolePreference.Carrier), C(2, RolePreference.Carrier));

        Pair pair = Assert.Single(result.Pairs);
        int overridden = Assert.Single(result.PreferenceOverridden);
        Assert.Equal(pair.Rider, overridden);
    }

    [Fact]
    public void Two_would_be_riders_split_and_one_is_reported()
    {
        PairingResult result = Form(7, C(1, RolePreference.Rider), C(2, RolePreference.Rider));

        Pair pair = Assert.Single(result.Pairs);
        int overridden = Assert.Single(result.PreferenceOverridden);
        Assert.Equal(pair.Carrier, overridden);
    }

    [Fact]
    public void Odd_one_out_prefers_a_player_without_a_wish()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Form(
                seed,
                C(1, RolePreference.Carrier),
                C(2, RolePreference.Rider),
                C(3));

            Assert.Equal([3], result.Unpaired);
            Pair pair = Assert.Single(result.Pairs);
            Assert.Equal(1, pair.Carrier);
            Assert.Equal(2, pair.Rider);
        }
    }

    [Fact]
    public void Odd_one_out_comes_from_an_oversubscribed_wish()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Form(
                seed,
                C(1, RolePreference.Carrier),
                C(2, RolePreference.Carrier),
                C(3));

            int left = Assert.Single(result.Unpaired);
            Assert.Contains(left, new[] { 1, 2 });
            Pair pair = Assert.Single(result.Pairs);
            Assert.Equal(3 - left, pair.Carrier);
            Assert.Equal(3, pair.Rider);
            Assert.Empty(result.PreferenceOverridden);
        }
    }

    [Fact]
    public void An_unknown_wish_counts_as_no_wish()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Form(
                seed,
                C(1, RolePreference.Carrier),
                C(2, (RolePreference)7),
                C(3, RolePreference.Rider),
                C(4, (RolePreference)200));

            Assert.Equal(2, result.Pairs.Count);
            Assert.Contains(result.Pairs, p => p.Carrier == 1);
            Assert.Contains(result.Pairs, p => p.Rider == 3);
            Assert.Empty(result.PreferenceOverridden);
        }
    }

    [Fact]
    public void Complete_pairs_need_no_dissolving()
    {
        var pairs = new PairingResult([new Pair(1, 2), new Pair(3, 4)], [5], []);

        Assert.False(PairingRules.TryDissolveAbsent(pairs, _ => true, out PairingResult same));
        Assert.Same(pairs, same);
    }

    [Fact]
    public void A_pair_with_a_member_gone_is_dissolved_and_the_partner_left_unpaired()
    {
        var pairs = new PairingResult([new Pair(1, 2), new Pair(3, 4)], [5], [3]);

        Assert.True(PairingRules.TryDissolveAbsent(pairs, a => a != 1, out PairingResult result));

        Pair kept = Assert.Single(result.Pairs);
        Assert.Equal((3, 4), (kept.Carrier, kept.Rider));
        Assert.Equal([5, 2], result.Unpaired);
        Assert.Equal([3], result.PreferenceOverridden);
    }

    [Fact]
    public void Dissolving_from_a_stale_copy_never_revives_a_pair()
    {
        // Two players leave within one round trip: the second dissolve still
        // reads the Pairs from before the first one was written.
        var stale = new PairingResult([new Pair(1, 2), new Pair(3, 4)], [], []);
        var present = new HashSet<int> { 2, 4 };

        PairingRules.TryDissolveAbsent(stale, present.Contains, out PairingResult result);

        Assert.Empty(result.Pairs);
        Assert.Equal([2, 4], result.Unpaired);
    }

    [Fact]
    public void A_pair_with_both_members_gone_leaves_nobody_behind()
    {
        var pairs = new PairingResult([new Pair(1, 2)], [], []);

        PairingRules.TryDissolveAbsent(pairs, _ => false, out PairingResult result);

        Assert.Empty(result.Pairs);
        Assert.Empty(result.Unpaired);
    }

    [Fact]
    public void A_lone_player_stays_unpaired()
    {
        PairingResult result = Form(0, C(1, RolePreference.Carrier));

        Assert.Empty(result.Pairs);
        Assert.Equal([1], result.Unpaired);
    }

    [Fact]
    public void An_assignment_parts_both_former_pairs()
    {
        var before = new PairingResult([new Pair(1, 2), new Pair(3, 4), new Pair(5, 6)], [7], [2, 5]);

        PairingResult after = PairingRules.Assign(before, 1, 4, [1, 2, 3, 4, 5, 6, 7]);

        Assert.Equal([(5, 6), (1, 4)], after.Pairs.Select(p => (p.Carrier, p.Rider)));
        Assert.Equal([2, 3, 7], after.Unpaired);
        Assert.Equal([5], after.PreferenceOverridden);
    }

    [Fact]
    public void An_assignment_can_turn_a_pair_round()
    {
        var before = new PairingResult([new Pair(1, 2)], [], []);

        PairingResult after = PairingRules.Assign(before, 2, 1, [1, 2]);

        Pair pair = Assert.Single(after.Pairs);
        Assert.Equal((2, 1), (pair.Carrier, pair.Rider));
        Assert.Empty(after.Unpaired);
    }
}
