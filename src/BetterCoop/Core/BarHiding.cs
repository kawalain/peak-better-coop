using System;
using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>
/// Picks which pieces of another mod's HUD are bars for a character that
/// should not show. A piece counts as one bar when it points at a hidden
/// character and holds no piece that points at someone else, so a container
/// of several players' bars is never taken for one of them.
/// </summary>
internal static class BarHiding
{
    /// <param name="pieces">Each HUD piece with the character it points at.</param>
    /// <param name="hidden">Whether a character's bars should not show.</param>
    /// <param name="isInside">Whether the first piece lies inside the second, not counting itself.</param>
    public static HashSet<TPiece> Pick<TPiece, TCharacter>(
        IReadOnlyList<(TPiece Piece, TCharacter Character)> pieces,
        Func<TCharacter, bool> hidden,
        Func<TPiece, TPiece, bool> isInside)
        where TPiece : notnull
        where TCharacter : notnull
    {
        var picked = new HashSet<TPiece>();
        foreach ((TPiece piece, TCharacter character) in pieces)
        {
            if (!hidden(character) || picked.Contains(piece))
            {
                continue;
            }

            bool holdsOthers = false;
            foreach ((TPiece other, TCharacter otherCharacter) in pieces)
            {
                if (!Equals(otherCharacter, character) && isInside(other, piece))
                {
                    holdsOthers = true;
                    break;
                }
            }

            if (!holdsOthers)
            {
                picked.Add(piece);
            }
        }

        // The same character can point from one piece and from another inside
        // it; hiding the outer one covers both.
        picked.RemoveWhere(piece => Contains(picked, piece, isInside));
        return picked;
    }

    private static bool Contains<TPiece>(HashSet<TPiece> picked, TPiece piece, Func<TPiece, TPiece, bool> isInside)
    {
        foreach (TPiece outer in picked)
        {
            if (isInside(piece, outer))
            {
                return true;
            }
        }

        return false;
    }
}
