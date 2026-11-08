using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class NoticeRulesTests
{
    [Fact]
    public void A_caution_stays_longest_and_a_longer_notice_stays_longer()
    {
        Assert.True(NoticeTiming.Seconds(NoticeKind.Caution, 20) > NoticeTiming.Seconds(NoticeKind.Telegram, 20));
        Assert.True(NoticeTiming.Seconds(NoticeKind.Telegram, 80) > NoticeTiming.Seconds(NoticeKind.Telegram, 20));
        Assert.Equal(NoticeTiming.Seconds(NoticeKind.Telegram, 500), NoticeTiming.Seconds(NoticeKind.Telegram, 5000));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 3)]
    public void A_new_notice_sends_the_oldest_away_when_the_stack_is_full(int shown, int leaving) =>
        Assert.Equal(leaving, NoticeTiming.Overflow(shown));

    [Fact]
    public void A_role_swap_shows_the_partner_and_the_turned_role()
    {
        Assert.Equal((2, false), PairLetterPlan.Preview(OperationKind.RoleSwap, 1, 1, 2, null));
        Assert.Equal((1, true), PairLetterPlan.Preview(OperationKind.RoleSwap, 2, 1, 2, null));
    }

    [Fact]
    public void A_late_pairing_names_the_partner_but_not_who_carries() =>
        Assert.Equal((5, (bool?)null), PairLetterPlan.Preview(OperationKind.LatePairing, 4, 4, 5, null));

    [Fact]
    public void An_assignment_shows_the_Pair_it_gives_or_none()
    {
        Pair[] pairs = [new(3, 1), new(2, 4)];

        Assert.Equal((3, false), PairLetterPlan.Preview(OperationKind.PairAssignment, 1, -1, -1, pairs));
        Assert.Equal((4, true), PairLetterPlan.Preview(OperationKind.PairAssignment, 2, -1, -1, pairs));
        Assert.Equal(((int?)null, false), PairLetterPlan.Preview(OperationKind.PairAssignment, 9, -1, -1, pairs));
    }

    [Fact]
    public void A_shuffle_keeps_the_partner_a_surprise() =>
        Assert.Equal(((int?)null, (bool?)null), PairLetterPlan.Preview(OperationKind.TimedShuffle, 1, -1, -1, null));
}
