using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>Where a card lies on the Pair Assignment board.</summary>
internal enum SlotKind
{
    Carrier,
    Rider,
    Unpaired,
}

/// <summary>
/// A place for one card: the Carrier or Rider of a row, or a place among the
/// Unpaired Players; an index past the last Unpaired Player is the empty
/// place after them.
/// </summary>
internal readonly struct Slot : System.IEquatable<Slot>
{
    public Slot(SlotKind kind, int index)
    {
        Kind = kind;
        Index = index;
    }

    public SlotKind Kind { get; }

    public int Index { get; }

    public static bool operator ==(Slot a, Slot b) => a.Equals(b);

    public static bool operator !=(Slot a, Slot b) => !a.Equals(b);

    public bool Equals(Slot other) => Kind == other.Kind && Index == other.Index;

    public override bool Equals(object? obj) => obj is Slot other && Equals(other);

    public override int GetHashCode() => ((int)Kind * 397) ^ Index;

    public override string ToString() => $"{Kind} {Index}";
}

/// <summary>
/// The Troop Leader's draft of a Pair Assignment, as cards on a board: a
/// row per Pair with a Carrier and a Rider slot, and the Unpaired Players
/// apart. Moving a card onto another trades their places, so trading the two
/// of one row turns it round. One empty row stays last for a new Pair, and a
/// row left with one player sets no Pair: that player is Unpaired when the
/// draft is approved.
/// </summary>
internal sealed class PairDraft
{
    private readonly List<(int? Carrier, int? Rider)> rows = new();

    private readonly List<int> unpaired = new();

    private List<Pair> basis = new();

    /// <summary>Starts from the room's Pairs and players.</summary>
    public PairDraft(IReadOnlyList<Pair> pairs, IReadOnlyList<int> players) => Reset(pairs, players);

    public IReadOnlyList<(int? Carrier, int? Rider)> Rows => rows;

    public IReadOnlyList<int> Unpaired => unpaired;

    /// <summary>The Pairs approving the draft sets: every full row.</summary>
    public IReadOnlyList<Pair> Pairs =>
        rows.Where(r => r.Carrier.HasValue && r.Rider.HasValue).Select(r => new Pair(r.Carrier!.Value, r.Rider!.Value)).ToList();

    /// <summary>Whether approving the draft would change the Pairs it started from.</summary>
    public bool Changed => !Same(Pairs, basis);

    public void Reset(IReadOnlyList<Pair> pairs, IReadOnlyList<int> players)
    {
        basis = pairs.ToList();
        Lay(pairs, players);
    }

    /// <summary>Lays <paramref name="pairs"/> out on the board as an unsent change, as a shuffle drawn into the draft.</summary>
    public void Lay(IReadOnlyList<Pair> pairs, IReadOnlyList<int> players)
    {
        rows.Clear();
        rows.AddRange(pairs.Select(p => ((int?)p.Carrier, (int?)p.Rider)));
        unpaired.Clear();
        unpaired.AddRange(players.Where(a => !pairs.Any(p => p.Contains(a))));
        Tidy();
    }

    /// <summary>
    /// Follows the room: while its Pairs are those the draft started from,
    /// the draft is kept, players who left taken off and newcomers added as
    /// Unpaired; once they change, the draft starts again from them.
    /// </summary>
    public void Follow(IReadOnlyList<Pair> pairs, IReadOnlyList<int> players)
    {
        if (!Same(pairs, basis))
        {
            Reset(pairs, players);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            (int? carrier, int? rider) = rows[i];
            rows[i] = (Present(carrier), Present(rider));
        }

        unpaired.RemoveAll(a => !players.Contains(a));
        unpaired.AddRange(players.Where(a => !rows.Any(r => r.Carrier == a || r.Rider == a) && !unpaired.Contains(a)));
        Tidy();

        int? Present(int? actor) => actor is int a && players.Contains(a) ? a : null;
    }

    /// <summary>Who lies in a slot, or null when it is empty.</summary>
    public int? At(Slot slot) => slot.Kind switch
    {
        SlotKind.Carrier when slot.Index >= 0 && slot.Index < rows.Count => rows[slot.Index].Carrier,
        SlotKind.Rider when slot.Index >= 0 && slot.Index < rows.Count => rows[slot.Index].Rider,
        SlotKind.Unpaired when slot.Index >= 0 && slot.Index < unpaired.Count => unpaired[slot.Index],
        _ => null,
    };

    /// <summary>The slot a player lies in.</summary>
    public Slot? Find(int actor)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Carrier == actor)
            {
                return new Slot(SlotKind.Carrier, i);
            }

            if (rows[i].Rider == actor)
            {
                return new Slot(SlotKind.Rider, i);
            }
        }

        int index = unpaired.IndexOf(actor);
        return index >= 0 ? new Slot(SlotKind.Unpaired, index) : null;
    }

    /// <summary>
    /// Moves the card in <paramref name="from"/> onto <paramref name="to"/>:
    /// a card already there takes the moved card's place. False when there is
    /// no card to move or the slot is not on the board.
    /// </summary>
    public bool Move(Slot from, Slot to)
    {
        if (At(from) is not int moved || !OnBoard(to) || from == to)
        {
            return false;
        }

        int? other = At(to);
        if (from.Kind == SlotKind.Unpaired && to.Kind == SlotKind.Unpaired)
        {
            // Among the Unpaired, order is all there is to change.
            unpaired.RemoveAt(from.Index);
            unpaired.Insert(System.Math.Min(to.Index, unpaired.Count), moved);
            return true;
        }

        if (to.Kind == SlotKind.Unpaired)
        {
            Put(from, other);
            if (other.HasValue)
            {
                unpaired[to.Index] = moved;
            }
            else
            {
                unpaired.Add(moved);
            }
        }
        else if (from.Kind == SlotKind.Unpaired)
        {
            Put(to, moved);
            if (other is int taken)
            {
                unpaired[from.Index] = taken;
            }
            else
            {
                unpaired.RemoveAt(from.Index);
            }
        }
        else
        {
            Put(to, moved);
            Put(from, other);
        }

        Tidy();
        return true;
    }

    private bool OnBoard(Slot slot) => slot.Kind switch
    {
        SlotKind.Unpaired => slot.Index >= 0 && slot.Index <= unpaired.Count,
        _ => slot.Index >= 0 && slot.Index < rows.Count,
    };

    private void Put(Slot slot, int? actor)
    {
        (int? carrier, int? rider) = rows[slot.Index];
        rows[slot.Index] = slot.Kind == SlotKind.Carrier ? (actor, rider) : (carrier, actor);
    }

    /// <summary>Drops empty rows and keeps one empty row last, for a new Pair.</summary>
    private void Tidy()
    {
        rows.RemoveAll(r => r.Carrier == null && r.Rider == null);
        rows.Add((null, null));
    }

    private static bool Same(IReadOnlyList<Pair> a, IReadOnlyList<Pair> b) =>
        a.Count == b.Count && a.All(p => b.Any(q => q.Carrier == p.Carrier && q.Rider == p.Rider));
}
