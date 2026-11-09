using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// Keeps the room's shared values the Troop Leader's: any player can write a
/// room property, so the Troop Leader remembers what it wrote under each key
/// and puts back any value it did not write. Its own writes come back as echoes
/// out of order, so every value it wrote in the last few seconds counts as its
/// own, and the latest is what it restores.
/// </summary>
internal sealed class RoomWard
{
    /// <summary>How long a value this client wrote still counts as its own when it comes back.</summary>
    public const double EchoSeconds = 10;

    private readonly Dictionary<string, List<(string Print, double At)>> recent = new();

    private readonly Dictionary<string, object?> latest = new();

    /// <summary>Notes a value this client writes now, as Troop Leader.</summary>
    public void Wrote(string key, object? value, double now)
    {
        if (!recent.TryGetValue(key, out List<(string Print, double At)>? prints))
        {
            prints = new List<(string Print, double At)>();
            recent[key] = prints;
        }

        prints.RemoveAll(p => now - p.At > EchoSeconds || p.At > now);
        prints.Add((Print(value), now));
        latest[key] = value;
    }

    /// <summary>
    /// Takes the room's values as this client's own, as a new Troop Leader who
    /// can know no better; what it has written itself still counts.
    /// </summary>
    public void Trust(IEnumerable<KeyValuePair<string, object?>> values, double now)
    {
        foreach (KeyValuePair<string, object?> pair in values)
        {
            Wrote(pair.Key, pair.Value, now);
        }
    }

    /// <summary>Forgets everything, as on leaving the room.</summary>
    public void Clear()
    {
        recent.Clear();
        latest.Clear();
    }

    /// <summary>Whether a value now under <paramref name="key"/> is one this client wrote.</summary>
    public bool IsOwn(string key, object? value, double now)
    {
        string print = Print(value);
        if (recent.TryGetValue(key, out List<(string Print, double At)>? prints)
            && prints.Any(p => p.Print == print && now - p.At <= EchoSeconds))
        {
            return true;
        }

        // A key this client never wrote stays empty.
        return latest.TryGetValue(key, out object? last) ? Print(last) == print : value == null;
    }

    /// <summary>What to put back under <paramref name="key"/>: the last value this client wrote, or nothing.</summary>
    public object? Restore(string key) => latest.TryGetValue(key, out object? last) ? last : null;

    /// <summary>A text standing for a room property value, equal for equal values.</summary>
    public static string Print(object? value)
    {
        var text = new StringBuilder();
        Append(text, value);
        return text.ToString();
    }

    private static void Append(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append('_');
                break;
            case string s:
                text.Append('"').Append(s.Replace("\"", "\"\"")).Append('"');
                break;
            case IFormattable number and not IEnumerable:
                text.Append(value.GetType().Name[0]).Append(number.ToString(value is double or float ? "R" : null, CultureInfo.InvariantCulture));
                break;
            case IEnumerable items:
                text.Append('[');
                foreach (object? item in items)
                {
                    Append(text, item);
                    text.Append(',');
                }

                text.Append(']');
                break;
            default:
                text.Append(value.GetType().Name).Append(':').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
