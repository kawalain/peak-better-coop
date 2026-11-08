using PeakCoop.Core;
using Xunit;

namespace PeakCoop.Tests;

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
    public void A_lone_player_stays_unpaired()
    {
        PairingResult result = Form(0, C(1, RolePreference.Carrier));

        Assert.Empty(result.Pairs);
        Assert.Equal([1], result.Unpaired);
    }
}
