using System;
using System.Collections.Generic;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The trail signs a scout lays with stones, sticks, and grass, as the
/// Boy Scout Handbook of 1959 pictures them; the paper marks its sections
/// with them, and the last of them ends the form.
/// </summary>
internal enum TrailSign
{
    /// <summary>A small stone on a big one: "This is the way."</summary>
    ThisWay,

    /// <summary>Sticks laid as an arrow: "Go this way."</summary>
    Arrow,

    /// <summary>A stone stacked on another with a third to its right: "Turn right."</summary>
    TurnRight,

    /// <summary>A tuft of grass tied and bent over: "This way," where there are no stones.</summary>
    Grass,

    /// <summary>A ring of stones round one stone: "I have gone home," the trail's end.</summary>
    GoneHome,
}

internal static partial class FormDrawing
{
    /// <summary>The trail signs that mark a form's sections, in turn.</summary>
    public static readonly TrailSign[] SectionSigns = [TrailSign.ThisWay, TrailSign.Arrow, TrailSign.TurnRight, TrailSign.Grass];

    /// <summary>The paler print of the form's second colour, for stones, sticks, and rope.</summary>
    public static Ink PrintTint => Paper.Mix(PaperDrawing.PassportBlue, 0.32f);

    /// <summary>The form's second printing colour at strength, for its rules.</summary>
    public static Ink PrintBlue => Paper.Mix(PaperDrawing.PassportBlue, 0.85f);

    /// <summary>The trail sign <paramref name="sign"/> as a printed cut <paramref name="size"/> square, in black and the form's second colour.</summary>
    public static string Sign(TrailSign sign, float size)
    {
        var svg = Open(size, size);
        float s = size;
        string outline = PaperDrawing.InkBlack.Hex;
        string fill = PrintTint.Hex;
        float line = Math.Max(1.4f, s * 0.055f);
        int stones = 0;
        void stone(float cx, float cy, float rx, float ry) =>
            svg.Append(F($"<path d=\"{Stone(cx * s, cy * s, rx * s, ry * s, stones++)}\" fill=\"{fill}\" stroke=\"{outline}\" stroke-width=\"{line}\" stroke-linejoin=\"round\"/>"));
        void stick(float x1, float y1, float x2, float y2)
        {
            svg.Append(F($"<path d=\"M{x1 * s} {y1 * s}L{x2 * s} {y2 * s}\" stroke=\"{outline}\" stroke-width=\"{s * 0.13f}\" stroke-linecap=\"round\" fill=\"none\"/>"));
            svg.Append(F($"<path d=\"M{x1 * s} {y1 * s}L{x2 * s} {y2 * s}\" stroke=\"{fill}\" stroke-width=\"{s * 0.13f - 2f * line}\" stroke-linecap=\"round\" fill=\"none\"/>"));
        }

        switch (sign)
        {
            case TrailSign.ThisWay:
                stone(0.5f, 0.76f, 0.4f, 0.14f);
                stone(0.46f, 0.53f, 0.27f, 0.11f);
                stone(0.53f, 0.33f, 0.15f, 0.09f);
                break;
            case TrailSign.Arrow:
                stick(0.1f, 0.62f, 0.86f, 0.5f);
                stick(0.86f, 0.5f, 0.62f, 0.3f);
                stick(0.86f, 0.5f, 0.66f, 0.74f);
                break;
            case TrailSign.TurnRight:
                stone(0.36f, 0.72f, 0.3f, 0.14f);
                stone(0.38f, 0.48f, 0.19f, 0.1f);
                stone(0.83f, 0.78f, 0.13f, 0.08f);
                break;
            case TrailSign.Grass:
                // The blades rise from the ground, are tied a little way up, and bend over to point the way.
                for (int blade = -2; blade <= 2; blade++)
                {
                    float foot = 0.5f + blade * 0.06f;
                    string d = F($"M{foot * s} {0.92f * s}Q{(0.5f + blade * 0.02f) * s} {0.55f * s} {0.5f * s} {0.4f * s}Q{(0.62f + blade * 0.02f) * s} {(0.14f + blade * 0.02f) * s} {(0.86f + blade * 0.015f) * s} {(0.24f + blade * 0.03f) * s}");
                    svg.Append(F($"<path d=\"{d}\" stroke=\"{outline}\" stroke-width=\"{line * 2.4f}\" fill=\"none\" stroke-linecap=\"round\"/>"));
                    svg.Append(F($"<path d=\"{d}\" stroke=\"{fill}\" stroke-width=\"{line * 0.9f}\" fill=\"none\" stroke-linecap=\"round\"/>"));
                }

                svg.Append(F($"<rect x=\"{0.36f * s}\" y=\"{0.52f * s}\" width=\"{0.28f * s}\" height=\"{0.09f * s}\" rx=\"{0.03f * s}\" fill=\"{outline}\"/>"));
                break;
            case TrailSign.GoneHome:
                const int ring = 9;
                for (int i = 0; i < ring; i++)
                {
                    double angle = -Math.PI / 2 + i * 2 * Math.PI / ring;
                    stone(0.5f + 0.37f * (float)Math.Cos(angle), 0.5f + 0.37f * (float)Math.Sin(angle), 0.085f, 0.07f);
                }

                stone(0.5f, 0.5f, 0.11f, 0.09f);
                break;
        }

        return Close(svg);
    }

    /// <summary>A field stone round (<paramref name="cx"/>, <paramref name="cy"/>): flat-bottomed and lumpy, never quite an oval.</summary>
    private static string Stone(float cx, float cy, float rx, float ry, int which)
    {
        // How far out each of eight points round the stone lies, a few stones' worth in turn.
        float[] lumps = [1.0f, 0.86f, 1.05f, 0.93f, 0.98f, 1.08f, 0.9f, 1.02f, 0.95f, 1.1f, 0.88f];
        const int corners = 8;
        var points = new (float X, float Y)[corners];
        for (int i = 0; i < corners; i++)
        {
            double angle = i * 2 * Math.PI / corners;
            float reach = lumps[(i + which * 3) % lumps.Length];
            float y = (float)Math.Sin(angle);
            // The underside sits flat on the ground or the stone below.
            points[i] = (cx + rx * reach * (float)Math.Cos(angle), cy + ry * reach * (y > 0f ? y * 0.75f : y));
        }

        // Round the corners: a curve through each point's midpoints with the point as its handle.
        var path = new StringBuilder();
        for (int i = 0; i <= corners; i++)
        {
            (float X, float Y) a = points[i % corners];
            (float X, float Y) b = points[(i + 1) % corners];
            var mid = ((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
            path.Append(i == 0 ? F($"M{mid.Item1} {mid.Item2}") : F($"Q{a.X} {a.Y} {mid.Item1} {mid.Item2}"));
        }

        return path.Append('Z').ToString();
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

    /// <summary>
    /// A length of rope across <paramref name="width"/>, tied in its middle
    /// with a square knot, the first knot a scout learns: two bights linked
    /// through each other, each rope's end lying beside its own standing part.
    /// <paramref name="tight"/>, 0 to 1, is how far the knot has been pulled
    /// up: loose, its bights hang wide apart; tight, they close round each other.
    /// </summary>
    public static string Rope(float width, float height, float tight)
    {
        tight = Math.Max(0f, Math.Min(1f, tight));
        var svg = Open(width, height);
        float thick = Math.Min(height * 0.26f, 8f);
        float c = width / 2f;
        float y = height / 2f;
        float spread = Lerp(24f, 11f, tight);
        float rx = Lerp(24f, 19f, tight);
        float ry = Math.Min(height / 2f - thick / 2f - 1f, Lerp(11f, 9f, tight));
        float slack = Lerp(2.5f, 0f, tight);
        float end = thick;

        // The loops: each rope's bight, left and right of the middle.
        List<(float X, float Y)> Bight(float cx, int from, int to)
        {
            var points = new List<(float X, float Y)>();
            for (int i = from; i <= to; i++)
            {
                double angle = i * Math.PI / 24;
                points.Add((cx + rx * (float)Math.Cos(angle), y + ry * (float)Math.Sin(angle)));
            }

            return points;
        }

        // A standing part sags a little while the knot is loose.
        List<(float X, float Y)> Standing(float from, float to)
        {
            var points = new List<(float X, float Y)>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                points.Add((Lerp(from, to, t), y + slack * (float)Math.Sin(t * Math.PI)));
            }

            return points;
        }

        float left = c - spread;
        float right = c + spread;
        // Seen from the front, angles run clockwise from the right, y down: the lower half is 0 to 24.
        Strand(svg, Standing(end, left - rx), thick);
        Strand(svg, Bight(left, 0, 24), thick);
        Strand(svg, Bight(right, -24, 24), thick);
        Strand(svg, Standing(right + rx, width - end), thick);
        // Each end lies along its own side, as a square knot's ends do.
        Strand(svg, [(right + rx * 0.3f, y + ry * 0.95f), (right + rx + 12f, y + ry + 1f)], thick);
        Strand(svg, Bight(left, -24, 0), thick);
        Strand(svg, [(left - rx * 0.3f, y - ry * 0.95f), (left - rx - 12f, y - ry - 1f)], thick);
        return Close(svg);
    }

    /// <summary>One stretch of rope along <paramref name="points"/>: outlined, filled, and laid with the twist of its strands.</summary>
    private static void Strand(StringBuilder svg, IReadOnlyList<(float X, float Y)> points, float thick)
    {
        var path = new StringBuilder();
        for (int i = 0; i < points.Count; i++)
        {
            path.Append(F($"{(i == 0 ? "M" : "L")}{points[i].X} {points[i].Y}"));
        }

        string d = path.ToString();
        float inner = thick - 2.4f;
        svg.Append(F($"<path d=\"{d}\" fill=\"none\" stroke=\"{PaperDrawing.InkBlack.Hex}\" stroke-width=\"{thick}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        svg.Append(F($"<path d=\"{d}\" fill=\"none\" stroke=\"{PrintTint.Hex}\" stroke-width=\"{inner}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));

        // The strands' twist: short slanted marks across the rope at even steps along it.
        string twist = Paper.Mix(PaperDrawing.PassportBlue, 0.7f).Hex;
        float half = inner / 2f;
        float step = thick * 0.62f;
        float carried = step / 2f;
        for (int i = 1; i < points.Count; i++)
        {
            float dx = points[i].X - points[i - 1].X;
            float dy = points[i].Y - points[i - 1].Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0f)
            {
                continue;
            }

            float ux = dx / length;
            float uy = dy / length;
            for (; carried <= length; carried += step)
            {
                float px = points[i - 1].X + ux * carried;
                float py = points[i - 1].Y + uy * carried;
                // Across the rope (the normal), leaning along it, as a twisted strand lies.
                float ax = px - uy * half - ux * half * 0.7f;
                float ay = py + ux * half - uy * half * 0.7f;
                float bx = px + uy * half + ux * half * 0.7f;
                float by = py - ux * half + uy * half * 0.7f;
                svg.Append(F($"<path d=\"M{ax} {ay}L{bx} {by}\" stroke=\"{twist}\" stroke-width=\"1.1\" stroke-linecap=\"round\"/>"));
            }

            carried -= length;
        }
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}
