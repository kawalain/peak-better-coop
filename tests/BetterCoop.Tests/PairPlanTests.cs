using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.DevTools;
using Xunit;

namespace BetterCoop.Tests;

public class PairPlanTests
{
    private static List<PairCandidate> Candidates(params int[] actors) =>
        actors.Select(a => new PairCandidate(a, RolePreference.Random)).ToList();

    [Fact]
    public void Planned_Pairs_whose_members_are_both_there_are_kept_as_planned()
    {
        var plan = new List<Pair> { new(3, 1), new(2, 4) };

        PairingResult formed = PairingRules.FormWithPlan(Candidates(1, 2, 3, 4), plan, new Random(5));

        Assert.Equal(2, formed.Pairs.Count);
        Assert.Contains(formed.Pairs, p => p.Carrier == 3 && p.Rider == 1);
        Assert.Contains(formed.Pairs, p => p.Carrier == 2 && p.Rider == 4);
        Assert.Empty(formed.Unpaired);
    }

    [Fact]
    public void Planned_Pairs_override_Role_Preferences()
    {
        var candidates = new List<PairCandidate> { new(1, RolePreference.Carrier), new(2, RolePreference.Carrier) };

        PairingResult formed = PairingRules.FormWithPlan(candidates, [new Pair(2, 1)], new Random(1));

        Pair pair = Assert.Single(formed.Pairs);
        Assert.Equal(2, pair.Carrier);
        Assert.Empty(formed.PreferenceOverridden);
    }

    [Fact]
    public void A_planned_Pair_missing_a_member_is_dropped_and_everyone_else_is_paired_at_random()
    {
        // 5 left before departure; 6 joined after the plan was made.
        var plan = new List<Pair> { new(1, 2), new(3, 5) };

        PairingResult formed = PairingRules.FormWithPlan(Candidates(1, 2, 3, 4, 6), plan, new Random(2));

        Assert.Contains(formed.Pairs, p => p.Carrier == 1 && p.Rider == 2);
        Assert.Equal(2, formed.Pairs.Count);
        Assert.Single(formed.Unpaired);
        var everyone = formed.Pairs.SelectMany(p => new[] { p.Carrier, p.Rider }).Concat(formed.Unpaired).OrderBy(a => a);
        Assert.Equal(new[] { 1, 2, 3, 4, 6 }, everyone);
    }

    [Fact]
    public void Without_a_plan_the_formation_is_the_usual_one()
    {
        PairingResult planned = PairingRules.FormWithPlan(Candidates(1, 2, 3, 4), [], new Random(9));
        PairingResult usual = PairingRules.Form(Candidates(1, 2, 3, 4), new Random(9));

        Assert.Equal(usual.Pairs.Select(p => (p.Carrier, p.Rider)), planned.Pairs.Select(p => (p.Carrier, p.Rider)));
    }

    [Fact]
    public void A_plan_names_only_players_in_the_room_once_each()
    {
        int[] players = [1, 2, 3, 4];

        Assert.True(PairingRules.IsPlan([new Pair(1, 2)], players));
        Assert.True(PairingRules.IsPlan([], players));
        Assert.False(PairingRules.IsPlan([new Pair(1, 9)], players));
        Assert.False(PairingRules.IsPlan([new Pair(1, 2), new Pair(2, 3)], players));
        Assert.False(PairingRules.IsPlan([new Pair(4, 4)], players));
    }

    [Fact]
    public void A_drawn_plan_is_laid_out_as_an_unsent_change()
    {
        int[] players = [1, 2, 3, 4];
        var draft = new PairDraft([], players);

        draft.Lay([new Pair(1, 2), new Pair(3, 4)], players);

        Assert.True(draft.Changed);
        Assert.Equal(2, draft.Pairs.Count);
        Assert.Empty(draft.Unpaired);
    }

    [Fact]
    public void An_airport_room_plans_and_its_formation_follows_the_plan()
    {
        var room = new FakeRoom(4, troopLeader: true, formed: false);
        Assert.True(room.Planning);

        Assert.Equal(OrderOutcome.Done, room.SetPlan([new Pair(4, 1), new Pair(2, 3)]));
        room.Form();

        Assert.False(room.Planning);
        Assert.Contains(room.Pairs, p => p.Carrier == 4 && p.Rider == 1);
        Assert.Contains(room.Pairs, p => p.Carrier == 2 && p.Rider == 3);
        Assert.Empty(room.Plan);
    }

    [Fact]
    public void Only_an_airport_room_takes_a_plan()
    {
        var formed = new FakeRoom(4, troopLeader: true, formed: true);
        var member = new FakeRoom(4, troopLeader: false, formed: false);

        Assert.Equal(OrderOutcome.Refused, formed.SetPlan([new Pair(1, 2)]));
        Assert.Equal(OrderOutcome.Refused, member.SetPlan([new Pair(1, 2)]));
        Assert.Empty(member.Plan);
    }
}
