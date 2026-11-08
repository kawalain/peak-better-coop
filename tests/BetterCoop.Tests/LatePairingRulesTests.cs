using System;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class LatePairingRulesTests
{
    private static LateCandidate Awake(int actor, RolePreference preference = RolePreference.Random, float height = 0f) =>
        new(actor, preference, awake: true, height);

    private static LateCandidate Down(int actor, RolePreference preference = RolePreference.Random, float height = 0f) =>
        new(actor, preference, awake: false, height);

    [Fact]
    public void A_lone_unpaired_player_waits() =>
        Assert.Null(LatePairingRules.PickTwo([Awake(1)]));

    [Fact]
    public void Two_who_are_down_wait() =>
        Assert.Null(LatePairingRules.PickTwo([Down(1), Down(2)]));

    [Fact]
    public void Someone_down_goes_first_with_someone_awake() =>
        Assert.Equal((3, 1), LatePairingRules.PickTwo([Awake(1), Awake(2), Down(3)]));

    [Fact]
    public void Two_awake_players_are_picked_in_join_order() =>
        Assert.Equal((2, 5), LatePairingRules.PickTwo([Awake(5), Awake(2), Awake(9)]));

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
