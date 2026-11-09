using System;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The figure wheels of a hand numbering machine, as printed serials and a
/// post office's dater set their figures: each figure stands on a wheel of
/// ten. When the figures change, every wheel whose figure differs turns
/// forward a figure at a time, at one pace, and the wheels come to rest from
/// left to right, the last with the machine's clunk. Anything else in the
/// text, letters, dots, and spaces, is type that never moves.
/// </summary>
internal static class NumberingMachine
{
    /// <summary>How long a wheel takes to turn one figure on.</summary>
    public const float StepSeconds = 0.04f;

    /// <summary>How long after one turning wheel comes to rest the next one does.</summary>
    public const float GapSeconds = 0.07f;

    /// <summary>
    /// The text the machine shows <paramref name="elapsed"/> seconds after
    /// its figures were set from <paramref name="from"/> to <paramref name="to"/>.
    /// The first turning wheel comes to rest once the wheel with the most to
    /// turn could have turned it all. With <paramref name="fullTurn"/>, a
    /// wheel already on its figure turns a full round as well, as when a form
    /// is first numbered. Text of another layout than <paramref name="from"/>
    /// has no wheels to turn and shows at once.
    /// </summary>
    public static string Shown(string from, string to, float elapsed, bool fullTurn = false)
    {
        if (!SameLayout(from, to))
        {
            return to;
        }

        float lead = Lead(from, to, fullTurn);
        var shown = new StringBuilder(to.Length);
        int turning = 0;
        for (int i = 0; i < to.Length; i++)
        {
            int turns = IsFigure(to[i]) ? Turns(from[i], to[i], fullTurn) : 0;
            if (turns == 0)
            {
                shown.Append(to[i]);
                continue;
            }

            int left = Math.Min(turns, Remaining(lead + turning++ * GapSeconds, elapsed));
            shown.Append((char)('0' + (((to[i] - '0') - left) % 10 + 10) % 10));
        }

        return shown.ToString();
    }

    /// <summary>How long the wheels of <paramref name="to"/> take to come to rest; zero when none turns.</summary>
    public static float Seconds(string from, string to, bool fullTurn = false)
    {
        if (!SameLayout(from, to))
        {
            return 0f;
        }

        int turning = 0;
        for (int i = 0; i < to.Length; i++)
        {
            if (IsFigure(to[i]) && Turns(from[i], to[i], fullTurn) > 0)
            {
                turning++;
            }
        }

        return turning == 0 ? 0f : Lead(from, to, fullTurn) + (turning - 1) * GapSeconds;
    }

    /// <summary>The same text with every figure set to nought, as a machine stands before it is set.</summary>
    public static string Noughts(string text)
    {
        var noughts = new StringBuilder(text.Length);
        foreach (char letter in text)
        {
            noughts.Append(IsFigure(letter) ? '0' : letter);
        }

        return noughts.ToString();
    }

    private static bool IsFigure(char letter) => letter is >= '0' and <= '9';

    /// <summary>Whether <paramref name="to"/> stands on the same wheels as <paramref name="from"/>: figures where it has figures, the same type elsewhere.</summary>
    public static bool SameLayout(string from, string to)
    {
        if (from.Length != to.Length)
        {
            return false;
        }

        for (int i = 0; i < to.Length; i++)
        {
            if (IsFigure(to[i]) != IsFigure(from[i]) || (!IsFigure(to[i]) && to[i] != from[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int Turns(char from, char to, bool fullTurn)
    {
        int turns = ((to - from) % 10 + 10) % 10;
        return turns == 0 && fullTurn ? 10 : turns;
    }

    /// <summary>When the first turning wheel comes to rest: once the one with the most to turn has turned it.</summary>
    private static float Lead(string from, string to, bool fullTurn)
    {
        int most = 0;
        for (int i = 0; i < to.Length; i++)
        {
            if (IsFigure(to[i]))
            {
                most = Math.Max(most, Turns(from[i], to[i], fullTurn));
            }
        }

        return most * StepSeconds;
    }

    /// <summary>How many figures a wheel resting at <paramref name="restsAt"/> still has to turn, turning at one pace.</summary>
    private static int Remaining(float restsAt, float elapsed)
    {
        float before = restsAt - elapsed;
        return before <= 0f ? 0 : (int)Math.Ceiling(before / StepSeconds - 1e-4f);
    }
}
