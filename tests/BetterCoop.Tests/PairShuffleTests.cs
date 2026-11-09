using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PairShuffleTests
{
    private static PairCandidate C(int actor, RolePreference preference = RolePreference.Random) => new(actor, preference);

    private static PairingResult Reshuffle(PairCandidate[] candidates, int seed, Pair[] previous, int[]? previousUnpaired = null) =>
        PairingRules.Reshuffle(candidates, new Random(seed), previous, previousUnpaired ?? []);

    private static bool SamePartners(Pair a, Pair b) => a.Contains(b.Carrier) && a.Contains(b.Rider);

    [Theory]
    [InlineData(RolePreference.Random, RolePreference.Random, RolePreference.Random, RolePreference.Random)]
    [InlineData(RolePreference.Carrier, RolePreference.Rider, RolePreference.Carrier, RolePreference.Rider)]
    [InlineData(RolePreference.Carrier, RolePreference.Rider, RolePreference.Random, RolePreference.Random)]
    [InlineData(RolePreference.Carrier, RolePreference.Carrier, RolePreference.Carrier, RolePreference.Rider)]
    public void A_shuffle_never_repeats_a_partner_when_there_are_two_pairs(
        RolePreference one, RolePreference two, RolePreference three, RolePreference four)
    {
        Pair[] previous = [new Pair(1, 2), new Pair(3, 4)];
        for (int seed = 0; seed < 200; seed++)
        {
            PairingResult result = Reshuffle([C(1, one), C(2, two), C(3, three), C(4, four)], seed, previous);

            Assert.Equal(2, result.Pairs.Count);
            Assert.DoesNotContain(result.Pairs, p => previous.Any(q => SamePartners(p, q)));
        }
    }

    [Fact]
    public void A_shuffle_keeps_role_preferences_while_it_changes_partners()
    {
        Pair[] previous = [new Pair(1, 2), new Pair(3, 4)];
        for (int seed = 0; seed < 200; seed++)
        {
            PairingResult result = Reshuffle(
                [C(1, RolePreference.Carrier), C(2, RolePreference.Rider), C(3, RolePreference.Carrier), C(4, RolePreference.Rider)],
                seed,
                previous);

            Assert.Equal(new[] { 1, 3 }, result.Pairs.Select(p => p.Carrier).OrderBy(a => a));
            Assert.Empty(result.PreferenceOverridden);
        }
    }

    [Theory]
    [InlineData(RolePreference.Random, RolePreference.Random)]
    [InlineData(RolePreference.Carrier, RolePreference.Rider)]
    public void Two_players_always_trade_roles(RolePreference first, RolePreference second)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            PairingResult result = Reshuffle([C(1, first), C(2, second)], seed, [new Pair(1, 2)]);

            Pair pair = Assert.Single(result.Pairs);
            Assert.Equal((2, 1), (pair.Carrier, pair.Rider));
        }
    }

    [Fact]
    public void Trading_roles_against_a_wish_is_reported()
    {
        PairingResult result = Reshuffle([C(1, RolePreference.Carrier), C(2, RolePreference.Rider)], 0, [new Pair(1, 2)]);

        Assert.Equal(new[] { 1, 2 }, result.PreferenceOverridden.OrderBy(a => a));
    }

    [Fact]
    public void Whoever_sat_out_gets_a_pair_next()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            PairingResult result = Reshuffle([C(1), C(2), C(3)], seed, [new Pair(1, 2)], [3]);

            Assert.Contains(result.Pairs, p => p.Contains(3));
            Assert.DoesNotContain(3, result.Unpaired);
        }
    }

    [Fact]
    public void No_pair_comes_back_unchanged_in_a_large_party()
    {
        Pair[] previous = [new Pair(1, 2), new Pair(3, 4), new Pair(5, 6), new Pair(7, 8)];
        PairCandidate[] candidates = Enumerable.Range(1, 8).Select(a => C(a)).ToArray();
        for (int seed = 0; seed < 200; seed++)
        {
            PairingResult result = Reshuffle(candidates, seed, previous);

            Assert.Equal(4, result.Pairs.Count);
            Assert.DoesNotContain(result.Pairs, p => previous.Any(q => SamePartners(p, q)));
        }
    }

    [Fact]
    public void Each_new_body_takes_over_the_former_body_its_carrier_came_from()
    {
        Pair[] before = [new Pair(1, 2), new Pair(3, 4)];
        Pair[] after = [new Pair(1, 3), new Pair(2, 4)];

        IReadOnlyDictionary<int, int> sources = BodyHandover.Sources(before, after);

        Assert.Equal(1, sources[1]);
        Assert.Equal(3, sources[2]);
    }

    [Fact]
    public void A_role_swap_hands_the_body_to_the_rider()
    {
        IReadOnlyDictionary<int, int> sources = BodyHandover.Sources([new Pair(1, 2)], [new Pair(2, 1)]);

        Assert.Equal(1, sources[2]);
        Assert.Equal(new Dictionary<int, int> { [2] = 1, [1] = 2 }, BodyHandover.InventoryMoves(sources));
    }

    [Fact]
    public void Every_former_body_is_taken_over_once()
    {
        Pair[] before = [new Pair(1, 2), new Pair(3, 4), new Pair(5, 6)];
        Pair[] after = [new Pair(2, 4), new Pair(6, 1), new Pair(3, 5)];

        IReadOnlyDictionary<int, int> sources = BodyHandover.Sources(before, after);

        Assert.Equal(3, sources.Count);
        Assert.Equal(new[] { 1, 3, 5 }, sources.Values.OrderBy(s => s));
    }

    [Fact]
    public void A_pair_of_newcomers_keeps_its_own_body_when_none_is_left()
    {
        IReadOnlyDictionary<int, int> sources = BodyHandover.Sources([new Pair(1, 2)], [new Pair(1, 2), new Pair(3, 4)]);

        Assert.Equal(1, sources[1]);
        Assert.False(sources.ContainsKey(3));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 3, 2, 4 })]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 2, 4, 6, 1, 3, 5 })]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 4, 1, 6, 3, 2, 5 })]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new[] { 2, 3, 4, 5, 6, 7, 8, 1 })]
    public void Inventories_move_as_a_permutation(int[] beforeFlat, int[] afterFlat)
    {
        Pair[] before = Pairs(beforeFlat);
        Pair[] after = Pairs(afterFlat);

        IReadOnlyDictionary<int, int> moves = BodyHandover.InventoryMoves(BodyHandover.Sources(before, after));

        // Nothing lost or copied: every inventory given is received exactly once.
        Assert.Equal(moves.Keys.OrderBy(a => a), moves.Values.OrderBy(a => a));
        foreach (KeyValuePair<int, int> move in BodyHandover.Sources(before, after))
        {
            if (move.Key != move.Value)
            {
                Assert.Equal(move.Value, moves[move.Key]);
            }
        }
    }

    [Fact]
    public void Handover_plan_survives_encoding()
    {
        var plan = new HandoverPlan(7, true, [
            new HandoverEntry(2, 1, [1f, 2f, 3f], [0.1f, 0.2f, 0f], 0.5f, 0.25f),
            new HandoverEntry(4, 3, [4f, 5f, 6f], [0f, 0f, 0.3f], 1f, 0f),
        ]);

        HandoverPlan? decoded = HandoverPlan.Decode(plan.Encode());

        Assert.NotNull(decoded);
        Assert.Equal(7, decoded!.Id);
        Assert.True(decoded.MovesPlaces);
        HandoverEntry second = decoded.For(4)!;
        Assert.Equal(3, second.Source);
        Assert.Equal([4f, 5f, 6f], second.Place);
        Assert.Equal([0f, 0f, 0.3f], second.Statuses);
        Assert.Equal(1f, second.Stamina);
        Assert.Null(decoded.For(9));
    }

    [Fact]
    public void A_place_only_entry_survives_encoding()
    {
        var plan = new HandoverPlan(3, true, [new HandoverEntry(2, 5, [1f, 80f, 3f], [], 0f, 0f)]);

        HandoverEntry entry = HandoverPlan.Decode(plan.Encode())!.For(2)!;

        Assert.False(entry.TakesStatuses);
        Assert.Equal([1f, 80f, 3f], entry.Place);
    }

    [Fact]
    public void A_malformed_plan_is_ignored()
    {
        Assert.Null(HandoverPlan.Decode(new object[] { 1, true, 2, new[] { 1 }, new[] { 1 }, new float[3], new float[1], new float[2] }));
        Assert.Null(HandoverPlan.Decode("plan"));
    }

    private static Pair[] Pairs(int[] flat) =>
        Enumerable.Range(0, flat.Length / 2).Select(i => new Pair(flat[i * 2], flat[(i * 2) + 1])).ToArray();
}

public class OperationTests
{
    [Fact]
    public void An_operation_is_announced_with_a_countdown()
    {
        var queue = new OperationQueue();

        Operation op = queue.Request(null, OperationKind.TimedShuffle, 100)!;

        Assert.Equal(1, op.Id);
        Assert.Equal(103, op.At);
        Assert.Equal(3, op.SecondsLeft(100));
        Assert.Equal(2, op.SecondsLeft(101.5));
        Assert.Equal(1, op.SecondsLeft(102.9));
        Assert.Equal(0, op.SecondsLeft(103));
        Assert.False(op.Locks(102.4));
        Assert.True(op.Locks(102.6));
        Assert.False(op.Finished().Locks(104));
    }

    [Fact]
    public void Shuffles_that_come_together_merge()
    {
        var queue = new OperationQueue();
        Operation op = queue.Request(null, OperationKind.CampfireShuffle, 0)!;

        Assert.Null(queue.Request(op, OperationKind.TimedShuffle, 1));
        Assert.Null(queue.After(op.Finished(), 4));
    }

    [Fact]
    public void A_shuffle_during_a_role_swap_waits_for_it()
    {
        var queue = new OperationQueue();
        Operation swap = queue.Request(null, OperationKind.RoleSwap, 0, 1, 2)!;

        Assert.Null(queue.Request(swap, OperationKind.CampfireShuffle, 1));
        Assert.Null(queue.Request(swap, OperationKind.TimedShuffle, 2));
        Operation next = queue.After(swap.Finished(), 4)!;

        Assert.Equal(OperationKind.CampfireShuffle, next.Kind);
        Assert.Equal(2, next.Id);
        Assert.Null(queue.After(next.Finished(), 8));
    }

    [Fact]
    public void A_shuffle_during_a_late_pairing_waits_for_it()
    {
        var queue = new OperationQueue();
        Operation pairing = queue.Request(null, OperationKind.LatePairing, 0, 5, 6)!;

        Assert.Null(queue.Request(pairing, OperationKind.CampfireShuffle, 1));
        Assert.Null(queue.Request(pairing, OperationKind.LatePairing, 1, 7, 8));
        Operation next = queue.After(pairing.Finished(), 4)!;

        Assert.Equal(OperationKind.CampfireShuffle, next.Kind);
    }

    [Fact]
    public void An_ordered_shuffle_is_a_shuffle_for_everyone()
    {
        var queue = new OperationQueue();
        Operation swap = queue.Request(null, OperationKind.RoleSwap, 0, 1, 2)!;

        Assert.Null(queue.Request(swap, OperationKind.OrderedShuffle, 1));
        Operation next = queue.After(swap.Finished(), 4)!;

        Assert.Equal(OperationKind.OrderedShuffle, next.Kind);
        Assert.True(next.IsShuffle);
        Assert.True(next.Involves(9));
        Assert.Equal(OperationKind.OrderedShuffle, Operation.Decode(next.Encode())!.Kind);
    }

    [Fact]
    public void A_late_pairing_involves_only_its_two()
    {
        var pairing = new Operation(1, OperationKind.LatePairing, 0, 5, 6, false);

        Assert.True(pairing.Involves(5));
        Assert.True(pairing.Involves(6));
        Assert.False(pairing.Involves(1));
    }

    [Fact]
    public void A_role_swap_during_a_shuffle_is_refused()
    {
        var queue = new OperationQueue();
        Operation shuffle = queue.Request(null, OperationKind.TimedShuffle, 0)!;

        Assert.Null(queue.Request(shuffle, OperationKind.RoleSwap, 1, 1, 2));
        Assert.Null(queue.After(shuffle.Finished(), 4));
    }

    [Fact]
    public void A_role_swap_involves_only_its_pair()
    {
        var swap = new Operation(1, OperationKind.RoleSwap, 0, 1, 2, false);

        Assert.True(swap.Involves(2));
        Assert.False(swap.Involves(3));
        Assert.True(new Operation(1, OperationKind.TimedShuffle, 0, -1, -1, false).Involves(3));
    }

    [Fact]
    public void An_operation_survives_encoding()
    {
        var op = new Operation(4, OperationKind.RoleSwap, 12.5, 7, 8, false);

        Operation decoded = Operation.Decode(op.Encode())!;

        Assert.Equal((4, OperationKind.RoleSwap, 12.5, 7, 8, false), (decoded.Id, decoded.Kind, decoded.At, decoded.SwapCarrier, decoded.SwapRider, decoded.Done));
        Assert.Null(Operation.Decode(new object[] { 1, (byte)9, 0.0, -1, -1, false, false }));
    }

    [Fact]
    public void A_shuffle_counting_down_is_called_off_with_nothing_changed()
    {
        var coming = new Operation(3, OperationKind.CampfireShuffle, 10, -1, -1, false);

        Operation called = Operation.CallOff(coming, timedComing: false, now: 8)!;

        Assert.Equal(3, called.Id);
        Assert.True(called.Done);
        Assert.True(called.CalledOff);
        Assert.True(Operation.Decode(called.Encode())!.CalledOff);
    }

    [Fact]
    public void Only_a_shuffle_not_yet_run_can_be_called_off()
    {
        Assert.Null(Operation.CallOff(new Operation(3, OperationKind.TimedShuffle, 10, -1, -1, false), false, now: 11));
        Assert.Null(Operation.CallOff(new Operation(3, OperationKind.RoleSwap, 10, 1, 2, false), true, now: 8));
        Assert.Null(Operation.CallOff(new Operation(3, OperationKind.TimedShuffle, 10, -1, -1, true), timedComing: false, now: 20));
    }

    [Fact]
    public void A_coming_Timed_Shuffle_is_called_off_as_a_new_operation_already_done()
    {
        Operation called = Operation.CallOff(new Operation(3, OperationKind.TimedShuffle, 10, -1, -1, true), timedComing: true, now: 50)!;

        Assert.Equal(4, called.Id);
        Assert.True(called.Done && called.CalledOff);
    }
}
