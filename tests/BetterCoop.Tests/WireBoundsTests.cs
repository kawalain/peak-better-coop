using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

/// <summary>
/// The Troop Leader never writes a value its readers refuse whole: whatever
/// a Run grows to, what it encodes decodes again.
/// </summary>
public class WireBoundsTests
{
    private static PairSample Sample(string carrier, string rider) =>
        new(carrier, carrier.ToUpperInvariant(), rider, rider.ToUpperInvariant(), alive: true, height: 0);

    [Fact]
    public void A_Carry_Record_past_every_limit_still_decodes()
    {
        // 100 scouts paired every way across seven segments: far past both limits.
        var record = new CarryRecord();
        string[] ids = Enumerable.Range(0, 100).Select(i => $"s{i}").ToArray();
        for (int segment = 0; segment < 7; segment++)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                for (int step = 1; step <= 5; step++)
                {
                    record.Count([Sample(ids[i], ids[(i + step) % ids.Length])], seconds: 1 + i, segment, "SEGMENT");
                }
            }
        }

        Assert.True(record.Scouts.Count > WireLimits.MaxScouts);
        Assert.True(record.Stretches.Count > WireLimits.MaxStretches);

        CarryRecord? shared = CarryRecord.Decode(record.Encode());

        Assert.NotNull(shared);
        Assert.Equal(WireLimits.MaxScouts, shared!.Scouts.Count);
        Assert.True(shared.Stretches.Count <= WireLimits.MaxStretches);
        // The scouts with the most time in Pairs are the ones shared.
        static double Total(CarryRecord.Scout s) => s.Carried + s.Ridden + s.Rescuing;
        double leastShared = shared.Scouts.Min(Total);
        Assert.All(record.Scouts.Where(s => shared.Find(s.Id) == null), s => Assert.True(Total(s) <= leastShared));
        Assert.All(shared.Stretches, s => Assert.True(shared.Find(s.Carrier) != null && shared.Find(s.Rider) != null));
    }

    [Fact]
    public void A_Carry_Record_with_too_many_stretches_shares_the_longest()
    {
        var record = new CarryRecord();
        // 50 scouts, each Pair one way round once per segment: 7 × 50 × 49 stretches.
        string[] ids = Enumerable.Range(0, 50).Select(i => $"s{i}").ToArray();
        int n = 0;
        for (int segment = 0; segment < 7; segment++)
        {
            foreach (string carrier in ids)
            {
                foreach (string rider in ids.Where(r => r != carrier))
                {
                    record.Count([Sample(carrier, rider)], seconds: ++n, segment, "SEGMENT");
                }
            }
        }

        CarryRecord? shared = CarryRecord.Decode(record.Encode());

        Assert.NotNull(shared);
        Assert.Equal(WireLimits.MaxStretches, shared!.Stretches.Count);
        double shortestShared = shared.Stretches.Min(s => s.Seconds);
        Assert.Equal(n - WireLimits.MaxStretches + 1, shortestShared);
    }

    [Fact]
    public void Parted_Pairs_keep_the_latest_within_the_limit()
    {
        var parted = PartedPairs.Empty;
        for (int i = 0; i < WireLimits.MaxParted + 100; i++)
        {
            parted.Add($"c{i}", $"r{i}");
        }

        PartedPairs shared = PartedPairs.Decode(parted.Encode());

        Assert.Equal(WireLimits.MaxParted, shared.Entries.Count);
        Assert.Equal(($"c{WireLimits.MaxParted + 99}", $"r{WireLimits.MaxParted + 99}"), shared.Entries[^1]);
    }

    [Fact]
    public void Pairs_with_more_players_than_the_limit_still_decode()
    {
        var pairs = Enumerable.Range(0, 50).Select(i => new Pair(2 * i, (2 * i) + 1)).ToList();
        var result = new PairingResult(pairs, Enumerable.Range(100, 80).ToList(), Enumerable.Range(0, 80).ToList());

        PairingResult? shared = PairingResult.Decode(result.Encode());

        Assert.NotNull(shared);
        Assert.Equal(50, shared!.Pairs.Count);
    }

    [Fact]
    public void Players_who_left_while_unpaired_leave_the_list()
    {
        // Players come and go while unpaired: the list keeps only those still here.
        var before = new PairingResult([new Pair(1, 2)], [3, 4, 5], [4]);
        var present = new HashSet<int> { 1, 2, 3 };

        Assert.True(PairingRules.TryDissolveAbsent(before, present.Contains, out PairingResult after));

        Assert.Single(after.Pairs);
        Assert.Equal([3], after.Unpaired);
        Assert.Empty(after.PreferenceOverridden);
        Assert.False(PairingRules.TryDissolveAbsent(after, present.Contains, out _));
    }
}
