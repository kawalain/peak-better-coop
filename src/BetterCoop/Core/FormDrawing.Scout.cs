using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>The ruling a 1959 Boy Scout Handbook form is printed with.</summary>
internal static partial class FormDrawing
{
    /// <summary>The form's second printing colour at strength, for its rules.</summary>
    public static Ink PrintBlue => Paper.Mix(PaperDrawing.PassportBlue, 0.85f);

    /// <summary>
    /// The ruling of a troop's advancement chart, <paramref name="width"/> by
    /// <paramref name="height"/>: a printed line down at each of
    /// <paramref name="at"/>, between the columns.
    /// </summary>
    public static string ColumnRules(float width, float height, IReadOnlyList<float> at)
    {
        var svg = Open(width, height);
        string blue = Paper.Mix(PaperDrawing.PassportBlue, 0.5f).Hex;
        foreach (float x in at)
        {
            if (x > 0f && x < width)
            {
                svg.Append(F($"<rect x=\"{x - 0.6f}\" y=\"0\" width=\"1.2\" height=\"{height}\" fill=\"{blue}\"/>"));
            }
        }

        return Close(svg);
    }

    /// <summary>A printed rule under a heading, a heavy line over a hairline, in the form's second colour.</summary>
    public static string HeadingRule(float width, float height)
    {
        var svg = Open(width, height);
        string blue = PrintBlue.Hex;
        svg.Append(F($"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height * 0.45f}\" fill=\"{blue}\"/>"));
        svg.Append(F($"<rect x=\"0\" y=\"{height * 0.75f}\" width=\"{width}\" height=\"{height * 0.25f}\" fill=\"{blue}\"/>"));
        return Close(svg);
    }
}
