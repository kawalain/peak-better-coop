using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PairDraftTests
{
    private static readonly int[] Players = [1, 2, 3, 4, 5];

    private static PairDraft Draft() => new([new Pair(1, 2), new Pair(3, 4)], Players);

    private static (int, int)[] PairsOf(PairDraft draft) => draft.Pairs.Select(p => (p.Carrier, p.Rider)).ToArray();

    [Fact]
    public void Starts_from_the_room_with_one_empty_row_for_a_new_pair()
    {
        PairDraft draft = Draft();

        Assert.Equal([(1, 2), (3, 4), (null, null)], draft.Rows.Select(r => (r.Carrier, r.Rider)));
        Assert.Equal([5], draft.Unpaired);
        Assert.False(draft.Changed);
    }

    [Fact]
    public void Trading_the_two_of_a_row_turns_it_round()
    {
        PairDraft draft = Draft();

        Assert.True(draft.Move(new Slot(SlotKind.Carrier, 0), new Slot(SlotKind.Rider, 0)));

        Assert.Equal([(2, 1), (3, 4)], PairsOf(draft));
        Assert.True(draft.Changed);
    }

    [Fact]
    public void Dropping_on_a_card_swaps_the_two_players()
    {
        PairDraft draft = Draft();

        draft.Move(new Slot(SlotKind.Rider, 0), new Slot(SlotKind.Rider, 1));

        Assert.Equal([(1, 4), (3, 2)], PairsOf(draft));
    }

    [Fact]
    public void An_unpaired_player_takes_a_place_and_the_one_there_is_unpaired()
    {
        PairDraft draft = Draft();

        draft.Move(new Slot(SlotKind.Unpaired, 0), new Slot(SlotKind.Carrier, 1));

        Assert.Equal([(1, 2), (5, 4)], PairsOf(draft));
        Assert.Equal([3], draft.Unpaired);
    }

    [Fact]
    public void A_new_pair_forms_in_the_empty_row_and_another_empty_row_follows()
    {
        PairDraft draft = Draft();

        draft.Move(new Slot(SlotKind.Unpaired, 0), new Slot(SlotKind.Carrier, 2));
        draft.Move(new Slot(SlotKind.Rider, 1), new Slot(SlotKind.Rider, 2));

        Assert.Equal([(1, 2), (5, 4)], PairsOf(draft));
        Assert.Equal([(1, 2), (3, null), (5, 4), (null, null)], draft.Rows.Select(r => (r.Carrier, r.Rider)));
        Assert.Empty(draft.Unpaired);
    }

    [Fact]
    public void A_player_put_among_the_unpaired_leaves_their_row_and_an_emptied_row_goes()
    {
        PairDraft draft = Draft();

        draft.Move(new Slot(SlotKind.Carrier, 0), new Slot(SlotKind.Unpaired, 1));
        draft.Move(new Slot(SlotKind.Rider, 0), new Slot(SlotKind.Unpaired, 2));

        Assert.Equal([(3, 4)], PairsOf(draft));
        Assert.Equal([5, 1, 2], draft.Unpaired);
        Assert.Equal(2, draft.Rows.Count);
    }

    [Fact]
    public void Nothing_moves_from_an_empty_slot_or_off_the_board()
    {
        PairDraft draft = Draft();

        Assert.False(draft.Move(new Slot(SlotKind.Carrier, 2), new Slot(SlotKind.Carrier, 0)));
        Assert.False(draft.Move(new Slot(SlotKind.Carrier, 0), new Slot(SlotKind.Rider, 9)));
        Assert.False(draft.Move(new Slot(SlotKind.Carrier, 0), new Slot(SlotKind.Carrier, 0)));
        Assert.False(draft.Changed);
    }

    [Fact]
    public void Finds_where_each_player_lies()
    {
        PairDraft draft = Draft();

        Assert.Equal(new Slot(SlotKind.Rider, 1), draft.Find(4));
        Assert.Equal(new Slot(SlotKind.Unpaired, 0), draft.Find(5));
        Assert.Null(draft.Find(9));
    }

    [Fact]
    public void Follows_players_coming_and_going_while_the_pairs_hold()
    {
        PairDraft draft = Draft();
        draft.Move(new Slot(SlotKind.Rider, 0), new Slot(SlotKind.Rider, 1));

        draft.Follow([new Pair(1, 2), new Pair(3, 4)], [1, 2, 3, 6]);

        Assert.Equal([(1, null), (3, 2), (null, null)], draft.Rows.Select(r => (r.Carrier, r.Rider)));
        Assert.Equal([(3, 2)], PairsOf(draft));
        Assert.Equal([6], draft.Unpaired);
    }

    [Fact]
    public void Starts_again_when_the_room_pairs_change()
    {
        PairDraft draft = Draft();
        draft.Move(new Slot(SlotKind.Rider, 0), new Slot(SlotKind.Rider, 1));

        draft.Follow([new Pair(2, 1), new Pair(3, 4)], Players);

        Assert.Equal([(2, 1), (3, 4)], PairsOf(draft));
        Assert.False(draft.Changed);
    }
}
