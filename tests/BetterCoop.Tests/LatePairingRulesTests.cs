using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class LatePairingRulesTests
{
    private static LateCandidate Awake(int actor, RolePreference preference = RolePreference.Random, float height = 0f) =>
        new(actor, preference, awake: true, height);

    private static LateCandidate Down(int actor, RolePreference preference = RolePreference.Random, float height = 0f) =>
        new(actor, preference, awake: false, height);

    private static List<(int Carrier, int Rider)> All(IReadOnlyList<LateCandidate> candidates, IReadOnlyList<Pair>? former = null) =>
        LatePairingRules.FormAll(candidates, new Random(0), former).Select(f => (f.Pair.Carrier, f.Pair.Rider)).ToList();

    [Fact]
    public void A_lone_unpaired_player_waits() =>
        Assert.Empty(All([Awake(1)]));

    [Fact]
    public void Two_who_are_down_wait() =>
        Assert.Empty(All([Down(1), Down(2)]));

    [Fact]
    public void Someone_down_goes_first_with_someone_awake() =>
        Assert.Equal((1, 3), All([Awake(1), Awake(2), Down(3)])[0]);

    [Fact]
    public void Everyone_who_can_pair_is_paired_at_once()
    {
        // A party of thirty joins together: fifteen Pairs in one Late Pairing.
        var candidates = Enumerable.Range(1, 30).Select(a => Awake(a)).ToList();

        var formed = All(candidates);

        Assert.Equal(15, formed.Count);
        Assert.Equal(30, formed.SelectMany(p => new[] { p.Carrier, p.Rider }).Distinct().Count());
    }

    [Fact]
    public void An_odd_one_out_waits_for_the_next()
    {
        var formed = All([Awake(1), Awake(2), Awake(3)]);

        Assert.Single(formed);
    }

    [Fact]
    public void Wishes_to_carry_meet_wishes_to_ride_first()
    {
        var formed = All([
            Awake(1, RolePreference.Carrier),
            Awake(2, RolePreference.Carrier),
            Awake(3, RolePreference.Rider),
            Awake(4, RolePreference.Rider),
        ]);

        Assert.Equal([(1, 3), (2, 4)], formed);
        Assert.All(LatePairingRules.FormAll([
            Awake(1, RolePreference.Carrier),
            Awake(2, RolePreference.Carrier),
            Awake(3, RolePreference.Rider),
            Awake(4, RolePreference.Rider),
        ], new Random(0)), f => Assert.Empty(f.PreferenceOverridden));
    }

    [Fact]
    public void Each_who_is_down_rides_with_someone_awake_and_two_down_wait()
    {
        var formed = All([Down(1), Down(2), Down(3), Awake(4), Awake(5)]);

        Assert.Equal([(4, 1), (5, 2)], formed);
    }

    [Fact]
    public void Two_who_are_down_form_no_pair() =>
        Assert.Null(LatePairingRules.Form(Down(1), Down(2), new Random(0)));

    [Fact]
    public void Whoever_is_down_rides_whatever_they_wish()
    {
        LatePairing pairing = LatePairingRules.Form(Down(1, RolePreference.Carrier), Awake(2, RolePreference.Rider), new Random(0))!;

        Assert.Equal(2, pairing.Pair.Carrier);
        Assert.Equal(1, pairing.Pair.Rider);
        Assert.Equal([2, 1], pairing.PreferenceOverridden);
    }

    [Fact]
    public void A_rider_who_is_down_comes_to_the_carrier_even_from_higher()
    {
        LatePairing pairing = LatePairingRules.Form(Down(1, height: 500f), Awake(2, height: 10f), new Random(0))!;

        Assert.False(pairing.CarrierMovesToRider);
    }

    [Fact]
    public void Role_preferences_decide_between_two_awake_players()
    {
        LatePairing pairing = LatePairingRules.Form(Awake(1, RolePreference.Rider), Awake(2), new Random(0))!;

        Assert.Equal(2, pairing.Pair.Carrier);
        Assert.Empty(pairing.PreferenceOverridden);
    }

    [Fact]
    public void Matching_wishes_leave_one_overridden()
    {
        LatePairing pairing = LatePairingRules.Form(
            Awake(1, RolePreference.Carrier),
            Awake(2, RolePreference.Carrier),
            new Random(0))!;

        Assert.Equal([pairing.Pair.Rider], pairing.PreferenceOverridden);
    }

    [Fact]
    public void Two_without_a_wish_get_either_role()
    {
        bool firstCarried = false;
        bool secondCarried = false;
        var random = new Random(1);
        for (int i = 0; i < 50; i++)
        {
            LatePairing pairing = LatePairingRules.Form(Awake(1), Awake(2), random)!;
            firstCarried |= pairing.Pair.Carrier == 1;
            secondCarried |= pairing.Pair.Carrier == 2;
            Assert.Empty(pairing.PreferenceOverridden);
        }

        Assert.True(firstCarried && secondCarried);
    }

    [Fact]
    public void The_shared_body_starts_at_the_higher_place()
    {
        LatePairing higherRider = LatePairingRules.Form(
            Awake(1, RolePreference.Carrier, height: 10f),
            Awake(2, RolePreference.Rider, height: 300f),
            new Random(0))!;
        LatePairing higherCarrier = LatePairingRules.Form(
            Awake(1, RolePreference.Carrier, height: 300f),
            Awake(2, RolePreference.Rider, height: 10f),
            new Random(0))!;

        Assert.True(higherRider.CarrierMovesToRider);
        Assert.False(higherCarrier.CarrierMovesToRider);
    }

    [Fact]
    public void Former_partners_pair_up_before_anyone_else() =>
        Assert.Equal((4, 2), All([Awake(1), Awake(2), Down(3), Awake(4)], [new Pair(4, 2)])[0]);

    [Fact]
    public void Former_partners_who_are_both_down_do_not_hold_up_others() =>
        Assert.Equal((1, 3), All([Awake(1), Awake(2), Down(3), Down(4)], [new Pair(3, 4)])[0]);

    [Fact]
    public void An_announced_pair_keeps_its_roles_as_it_runs()
    {
        LatePairing confirmed = LatePairingRules.Confirm(new Pair(1, 2), [Awake(1, RolePreference.Rider), Awake(2, RolePreference.Carrier)])!;

        Assert.Equal((1, 2), (confirmed.Pair.Carrier, confirmed.Pair.Rider));
        Assert.Equal([1, 2], confirmed.PreferenceOverridden);
    }

    [Fact]
    public void An_announced_carrier_gone_down_rides_instead()
    {
        LatePairing confirmed = LatePairingRules.Confirm(new Pair(1, 2), [Down(1), Awake(2)])!;

        Assert.Equal((2, 1), (confirmed.Pair.Carrier, confirmed.Pair.Rider));
    }

    [Fact]
    public void An_announced_pair_with_someone_gone_or_both_down_does_not_form()
    {
        Assert.Null(LatePairingRules.Confirm(new Pair(1, 2), [Awake(1)]));
        Assert.Null(LatePairingRules.Confirm(new Pair(1, 2), [Down(1), Down(2)]));
    }

    [Fact]
    public void Former_partners_take_their_former_roles_over_their_wishes()
    {
        LatePairing pairing = LatePairingRules.Form(
            Awake(1, RolePreference.Carrier),
            Awake(2, RolePreference.Rider),
            new Random(0),
            [new Pair(2, 1)])!;

        Assert.Equal(2, pairing.Pair.Carrier);
        Assert.Equal(1, pairing.Pair.Rider);
    }

    [Fact]
    public void A_former_carrier_who_is_down_rides_this_time()
    {
        LatePairing pairing = LatePairingRules.Form(Down(1), Awake(2), new Random(0), [new Pair(1, 2)])!;

        Assert.Equal(2, pairing.Pair.Carrier);
    }

    [Fact]
    public void A_parted_pair_is_found_again_by_user_id()
    {
        PartedPairs parted = PartedPairs.Empty;
        parted.Add("carrier-id", "rider-id");

        PartedPairs decoded = PartedPairs.Decode(parted.Encode());
        IReadOnlyList<Pair> resolved = decoded.Resolve(id => id == "carrier-id" ? 9 : id == "rider-id" ? 3 : null);

        Pair pair = Assert.Single(resolved);
        Assert.Equal((9, 3), (pair.Carrier, pair.Rider));
    }

    [Fact]
    public void A_player_keeps_only_their_latest_former_partner()
    {
        PartedPairs parted = PartedPairs.Empty;
        parted.Add("a", "b");
        parted.Add("c", "a");

        Assert.Equal([("c", "a")], parted.Entries);
        Assert.True(parted.Remove("a", "c"));
        Assert.Empty(parted.Entries);
    }

    [Fact]
    public void A_parted_pair_with_someone_absent_is_not_resolved()
    {
        PartedPairs parted = PartedPairs.Empty;
        parted.Add("a", "b");

        Assert.Empty(parted.Resolve(id => id == "a" ? 1 : null));
        Assert.Empty(PartedPairs.Decode("junk").Entries);
    }

    [Fact]
    public void A_new_character_settles_before_it_can_be_paired()
    {
        var clock = new ArrivalClock();

        Assert.False(clock.Settled(1, 100, 0f));
        Assert.False(clock.Settled(1, 100, ArrivalClock.SettleSeconds - 0.1f));
        Assert.True(clock.Settled(1, 100, ArrivalClock.SettleSeconds));

        // A character spawned anew, as on a respawn, settles again.
        Assert.False(clock.Settled(1, 101, 10f));
        Assert.True(clock.Settled(1, 101, 10f + ArrivalClock.SettleSeconds));
    }
}
