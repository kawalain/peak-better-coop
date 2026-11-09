using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The most any value from another client may hold. Photon lets any player
/// write room and player properties and send events, so a value is checked
/// against these before it is read further: a value past them is refused
/// whole, before it can cost every client time or memory.
/// </summary>
internal static class WireLimits
{
    /// <summary>The most players a value may name; far more than a room holds.</summary>
    public const int MaxScouts = 64;

    /// <summary>The most segment-and-Pair entries a Carry Record may hold.</summary>
    public const int MaxStretches = 2048;

    /// <summary>The most statuses a Handover entry may carry.</summary>
    public const int MaxStatuses = 64;

    /// <summary>The most Parted Pairs a room may remember.</summary>
    public const int MaxParted = 512;

    /// <summary>The longest user id or other key a value may hold.</summary>
    public const int MaxIdLength = 64;

    /// <summary>The longest short text, such as a version or a sound's name.</summary>
    public const int MaxTextLength = 128;

    /// <summary>The most letters of a player's name the mod shows.</summary>
    public const int MaxNameLetters = 24;

    public static bool IsId(string? value) => value is { Length: > 0 and <= MaxIdLength };

    public static bool IsText(string? value) => value is { Length: <= MaxTextLength };

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool AllFinite(float[] values)
    {
        foreach (float value in values)
        {
            if (!IsFinite(value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A player's name as the mod shows it: control characters dropped, the
    /// brackets of rich text tags swapped for look-alikes so a name cannot
    /// restyle the paper, and at most <see cref="MaxNameLetters"/> letters,
    /// cut with an ellipsis. An empty name shows as <paramref name="fallback"/>.
    /// </summary>
    public static string Name(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        // Far past what is shown; looked at no further.
        string text = name!.Length > MaxTextLength * 4 ? name.Substring(0, MaxTextLength * 4) : name;
        var clean = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.IsControl(c) || c == '​' || c == '‮' || c == '‭')
            {
                continue;
            }

            clean.Append(c switch
            {
                '<' => '‹',
                '>' => '›',
                _ => c,
            });
        }

        string trimmed = clean.ToString().Trim();
        if (trimmed.Length == 0)
        {
            return fallback;
        }

        var letters = new StringInfo(trimmed);
        return letters.LengthInTextElements <= MaxNameLetters
            ? trimmed
            : letters.SubstringByTextElements(0, MaxNameLetters - 1).TrimEnd() + "…";
    }
}

/// <summary>
/// Lets so many things through at once and then so many a second, as a
/// bucket that fills again over time: for anything another client can make
/// this one do again and again.
/// </summary>
internal sealed class RateGate
{
    private readonly double capacity;

    private readonly double perSecond;

    private double tokens;

    private double last = double.NegativeInfinity;

    public RateGate(double capacity, double perSecond)
    {
        this.capacity = capacity;
        this.perSecond = perSecond;
        tokens = capacity;
    }

    /// <summary>Whether one more may pass at <paramref name="now"/>, in seconds; takes its place if so.</summary>
    public bool Take(double now)
    {
        if (!double.IsNegativeInfinity(last) && now > last)
        {
            tokens = Math.Min(capacity, tokens + ((now - last) * perSecond));
        }

        last = Math.Max(last, now);
        if (tokens < 1)
        {
            return false;
        }

        tokens -= 1;
        return true;
    }
}

/// <summary>
/// Which notices go up on paper: the same words again within a few seconds
/// do not, nor a flood past a few at once, so nothing another client does
/// can bury the screen in paper. What is held back still goes to the log.
/// </summary>
internal sealed class NoticeGate
{
    public const double RepeatSeconds = 3;

    private readonly RateGate rate = new(capacity: 6, perSecond: 0.75);

    private readonly Dictionary<string, double> shown = new();

    public bool Allow(string message, double now)
    {
        if (shown.TryGetValue(message, out double at) && now - at < RepeatSeconds)
        {
            return false;
        }

        if (!rate.Take(now))
        {
            return false;
        }

        if (shown.Count > 64)
        {
            shown.Clear();
        }

        shown[message] = now;
        return true;
    }
}
