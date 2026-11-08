using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The printed marks of a paper form, as SVG drawings made for their own
/// size: a check box, the line to sign on, a rubber stamp, a passport
/// photo's frame, an empty place for a photo, and crayon doodles. What a pen
/// writes on the form is drawn stroke by stroke instead, see <see cref="PenPaths"/>. Printed parts
/// are in ink black, written ones in pen blue, stamps in stamp red. Every
/// colour is mixed with the paper beforehand and drawn opaque, so a mark
/// looks the same over the paper whatever its edges.
/// </summary>
internal static class FormDrawing
{
    /// <summary>The blue of an answer written by pen.</summary>
    public static readonly Ink Pen = new(0.110f, 0.200f, 0.580f);

    /// <summary>The red of a rubber stamp.</summary>
    public static readonly Ink StampRed = new(0.760f, 0.160f, 0.180f);

    /// <summary>The black of a post office's cancelling ink, a little blue.</summary>
    public static readonly Ink PostmarkInk = new(0.120f, 0.125f, 0.180f);

    private static Ink Paper => PaperDrawing.Paper;

    /// <summary>A printed check box; the tick in it is written by hand, see <see cref="PenPaths.Tick"/>.</summary>
    public static string CheckBox(float size)
    {
        var svg = Open(size, size);
        float box = PenPaths.CheckBox(size, out float x, out float y);
        svg.Append(F($"<rect x=\"{x}\" y=\"{y}\" width=\"{box}\" height=\"{box}\" rx=\"{box * 0.08f}\" fill=\"none\" stroke=\"{PaperDrawing.InkBlack.Hex}\" stroke-width=\"{size * 0.05f}\"/>"));
        return Close(svg);
    }

    /// <summary>A printed line to write or sign on.</summary>
    public static string Line(float width, float height)
    {
        var svg = Open(width, height);
        svg.Append(F($"<rect x=\"0\" y=\"{height - 1.6f}\" width=\"{width}\" height=\"1.6\" fill=\"{PaperDrawing.InkBlack.Hex}\"/>"));
        return Close(svg);
    }

    /// <summary>A small pen chevron pointing back (left) or forward (right), to step through answers.</summary>
    public static string Arrow(float width, float height, bool back)
    {
        var svg = Open(width, height);
        float stroke = Math.Max(1.6f, width * 0.14f);
        float near = width * 0.32f;
        float far = width * 0.68f;
        float tip = back ? near : far;
        float tail = back ? far : near;
        svg.Append(F($"<path d=\"M{tail} {height * 0.25f}L{tip} {height * 0.5f}L{tail} {height * 0.75f}\" fill=\"none\" stroke=\"{Pen.Hex}\" stroke-width=\"{stroke}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        return Close(svg);
    }

    /// <summary>
    /// A rubber stamp's impression: a double rounded border in stamp red,
    /// worn in places as a stamp is. Its words are set over it as text.
    /// </summary>
    public static string Stamp(float width, float height, int seed = 3, Ink? colour = null)
    {
        var svg = Open(width, height);
        string red = (colour ?? StampRed).Hex;
        float outer = Math.Max(2f, height * 0.07f);
        float inner = outer * 0.45f;
        float r = height * 0.18f;
        svg.Append(F($"<rect x=\"{outer / 2f}\" y=\"{outer / 2f}\" width=\"{width - outer}\" height=\"{height - outer}\" rx=\"{r}\" fill=\"none\" stroke=\"{red}\" stroke-width=\"{outer}\"/>"));
        float gap = outer * 2.2f;
        svg.Append(F($"<rect x=\"{gap}\" y=\"{gap}\" width=\"{width - 2f * gap}\" height=\"{height - 2f * gap}\" rx=\"{Math.Max(1f, r - gap / 2f)}\" fill=\"none\" stroke=\"{red}\" stroke-width=\"{inner}\"/>"));

        // Worn spots: specks of paper showing through the border.
        var random = new Random(seed);
        string paper = Paper.Hex;
        int specks = (int)((width + height) / 9f);
        for (int i = 0; i < specks; i++)
        {
            bool alongTop = random.NextDouble() < width / (width + height);
            float sx = alongTop ? Next(random, 0f, width) : (random.NextDouble() < 0.5 ? outer / 2f : width - outer / 2f);
            float sy = alongTop ? (random.NextDouble() < 0.5 ? outer / 2f : height - outer / 2f) : Next(random, 0f, height);
            float speck = Next(random, outer * 0.3f, outer * 0.9f);
            svg.Append(F($"<rect x=\"{sx - speck / 2f}\" y=\"{sy - speck / 2f}\" width=\"{speck}\" height=\"{speck * 0.8f}\" fill=\"{paper}\"/>"));
        }

        return Close(svg);
    }

    /// <summary>
    /// A postmark's impression: a double ring <paramref name="ring"/> across
    /// at the left, and wavy cancelling lines running from it to the right
    /// edge, unevenly inked. Its words are set over it as text.
    /// </summary>
    public static string Postmark(float width, float height, float ring, int seed = 11, Ink? colour = null)
    {
        var svg = Open(width, height);
        var hand = new Random(seed);
        string ink = (colour ?? PostmarkInk).Hex;
        float r = ring / 2f - 2f;
        float cx = ring / 2f;
        float cy = height / 2f;
        svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"none\" stroke=\"{ink}\" stroke-width=\"2.6\"/>"));
        svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r - 5f}\" fill=\"none\" stroke=\"{ink}\" stroke-width=\"1.2\"/>"));

        // The cancelling lines: five waves in step, from the ring's edge to the right.
        const int lines = 5;
        float gap = height * 0.11f;
        float start = cx + r + 3f;
        float wave = gap * 0.42f;
        float length = width - start - 3f;
        for (int line = 0; line < lines; line++)
        {
            float y = cy + (line - (lines - 1) / 2f) * gap;
            var path = new StringBuilder(F($"M{start} {y}"));
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                path.Append(F($"L{start + length * t} {y + (float)Math.Sin(t * Math.PI * 4) * wave}"));
            }

            svg.Append(F($"<path d=\"{path}\" fill=\"none\" stroke=\"{ink}\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        }

        // Where the ink ran thin: flecks of paper over the marks.
        string paper = Paper.Hex;
        for (int i = 0; i < 70; i++)
        {
            float x = Next(hand, 0f, width);
            float y = Next(hand, 0f, height);
            float fleck = Next(hand, 0.8f, 2.4f);
            svg.Append(F($"<rect x=\"{x}\" y=\"{y}\" width=\"{fleck}\" height=\"{fleck * 0.6f}\" fill=\"{paper}\"/>"));
        }

        return Close(svg);
    }

    /// <summary>
    /// The punched hole of a baggage tag, <paramref name="size"/> across: a
    /// card reinforcement ring round a hole that shows what lies behind.
    /// </summary>
    public static string TagHole(float size)
    {
        var svg = Open(size, size);
        float c = size / 2f;
        Ink ring = new(0.80f, 0.66f, 0.42f);
        svg.Append(F($"<circle cx=\"{c}\" cy=\"{c}\" r=\"{c - 1f}\" fill=\"{ring.Hex}\" stroke=\"{ring.Mix(PaperDrawing.InkBlack, 0.35f).Hex}\" stroke-width=\"1.4\"/>"));
        svg.Append(F($"<circle cx=\"{c}\" cy=\"{c}\" r=\"{c * 0.42f}\" fill=\"{PaperDrawing.InkBlack.Hex}\"/>"));
        return Close(svg);
    }

    /// <summary>
    /// An airmail envelope's border: red and blue bars slanting along a strip
    /// <paramref name="width"/> by <paramref name="height"/>, with paper between.
    /// </summary>
    public static string Airmail(float width, float height)
    {
        var svg = Open(width, height);
        Ink red = new(0.78f, 0.17f, 0.18f);
        Ink blue = new(0.13f, 0.25f, 0.62f);
        float bar = height * 1.4f;
        float slant = height * 0.8f;
        int i = 0;
        for (float x = -slant; x < width; x += bar * 1.6f, i++)
        {
            string fill = (i % 2 == 0 ? red : blue).Hex;
            svg.Append(F($"<path d=\"M{x + slant} 0H{x + slant + bar}L{x + bar} {height}H{x}Z\" fill=\"{fill}\"/>"));
        }

        return Close(svg);
    }

    /// <summary>The thin printed edge round a yearbook photo.</summary>
    public static string PhotoEdge(float width, float height)
    {
        var svg = Open(width, height);
        svg.Append(F($"<rect x=\"0.7\" y=\"0.7\" width=\"{width - 1.4f}\" height=\"{height - 1.4f}\" fill=\"none\" stroke=\"{Paper.Mix(PaperDrawing.InkBlack, 0.7f).Hex}\" stroke-width=\"1.4\"/>"));
        return Close(svg);
    }

    /// <summary>A card cut from lighter paper with a thin ink edge; lifted, it casts a soft shadow.</summary>
    public static string Card(float width, float height, bool lifted)
    {
        var svg = Open(width, height);
        float shadow = lifted ? 6f : 2.5f;
        float r = 8f;
        string card = Paper.Mix(new Ink(1f, 1f, 1f), 0.45f).Hex;
        svg.Append(F($"<rect x=\"{shadow}\" y=\"{shadow}\" width=\"{width - shadow - 1f}\" height=\"{height - shadow - 1f}\" rx=\"{r}\" fill=\"#000000\" fill-opacity=\"{(lifted ? 0.22f : 0.12f)}\"/>"));
        svg.Append(F($"<rect x=\"1\" y=\"1\" width=\"{width - shadow - 2f}\" height=\"{height - shadow - 2f}\" rx=\"{r}\" fill=\"{card}\" stroke=\"{PaperDrawing.InkBlack.Hex}\" stroke-width=\"2\"/>"));
        return Close(svg);
    }

    /// <summary>An empty place on the form for a card, outlined in dashes; marked, it is where a held card would land.</summary>
    public static string Slot(float width, float height, bool marked)
    {
        var svg = Open(width, height);
        string stroke = marked ? Pen.Hex : Paper.Mix(PaperDrawing.InkBlack, 0.4f).Hex;
        if (marked)
        {
            svg.Append(F($"<rect x=\"2\" y=\"2\" width=\"{width - 4f}\" height=\"{height - 4f}\" rx=\"8\" fill=\"{Paper.Mix(Pen, 0.1f).Hex}\"/>"));
        }

        // Dashes along each side, drawn as short bars so they need no dash pattern.
        const float dash = 9f;
        const float gap = 6f;
        float t = marked ? 2.4f : 1.6f;
        for (float x = 10f; x + dash <= width - 10f; x += dash + gap)
        {
            svg.Append(F($"<rect x=\"{x}\" y=\"2\" width=\"{dash}\" height=\"{t}\" fill=\"{stroke}\"/>"));
            svg.Append(F($"<rect x=\"{x}\" y=\"{height - 2f - t}\" width=\"{dash}\" height=\"{t}\" fill=\"{stroke}\"/>"));
        }

        for (float y = 10f; y + dash <= height - 10f; y += dash + gap)
        {
            svg.Append(F($"<rect x=\"2\" y=\"{y}\" width=\"{t}\" height=\"{dash}\" fill=\"{stroke}\"/>"));
            svg.Append(F($"<rect x=\"{width - 2f - t}\" y=\"{y}\" width=\"{t}\" height=\"{dash}\" fill=\"{stroke}\"/>"));
        }

        return Close(svg);
    }

    /// <summary>
    /// A scout's head and shoulders as a plain silhouette in their skin
    /// colour, for when no photo of them can be taken.
    /// </summary>
    public static string Silhouette(float width, float height, Ink skin)
    {
        var svg = Open(width, height);
        svg.Append(F($"<rect width=\"{width}\" height=\"{height}\" fill=\"{Paper.Mix(PaperDrawing.PassportBlue, 0.18f).Hex}\"/>"));
        float cx = width / 2f;
        string body = skin.Hex;
        string outline = PaperDrawing.InkBlack.Hex;
        // Shoulders, then the round head the scouts have.
        svg.Append(F($"<path d=\"M{width * 0.08f} {height}C{width * 0.1f} {height * 0.72f} {width * 0.3f} {height * 0.66f} {cx} {height * 0.66f}C{width * 0.7f} {height * 0.66f} {width * 0.9f} {height * 0.72f} {width * 0.92f} {height}Z\" fill=\"{body}\" stroke=\"{outline}\" stroke-width=\"2\"/>"));
        svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{height * 0.42f}\" rx=\"{width * 0.27f}\" ry=\"{height * 0.25f}\" fill=\"{body}\" stroke=\"{outline}\" stroke-width=\"2\"/>"));
        svg.Append(F($"<ellipse cx=\"{cx - width * 0.09f}\" cy=\"{height * 0.4f}\" rx=\"{width * 0.035f}\" ry=\"{height * 0.05f}\" fill=\"{outline}\"/>"));
        svg.Append(F($"<ellipse cx=\"{cx + width * 0.09f}\" cy=\"{height * 0.4f}\" rx=\"{width * 0.035f}\" ry=\"{height * 0.05f}\" fill=\"{outline}\"/>"));
        return Close(svg);
    }

    /// <summary>The doodles someone may leave by a signature.</summary>
    public enum DoodleKind
    {
        Sun,
        AngryFace,
        Peak,
    }

    /// <summary>The doodle left by the signature of <paramref name="signer"/>: the same one for the same name.</summary>
    public static DoodleKind DoodleFor(string signer) => (DoodleKind)(Hash(signer) % 3);

    /// <summary>
    /// A crayon doodle by the signature: a smiling sun, an angry face, or a
    /// peak with a flag on top. Crayon goes on in passes that never quite
    /// cover each other, and the paper's grain shows through the wax.
    /// </summary>
    public static string Doodle(float width, float height, string signer)
    {
        var svg = Open(width, height);
        var hand = new Random((int)(Hash(signer) & 0x7fffffff));
        float w = width;
        float h = height;
        switch (DoodleFor(signer))
        {
            case DoodleKind.Sun:
            {
                var orange = new Ink(0.95f, 0.52f, 0.12f);
                var yellow = new Ink(0.98f, 0.76f, 0.16f);
                var brown = new Ink(0.45f, 0.24f, 0.10f);
                float r = h * 0.24f;
                (float, float) centre = (w * 0.5f, h * 0.5f);
                Crayon(svg, hand, Round(centre, r, r, 0.0, 2.1, 30), orange, h);
                for (int i = 0; i < 9; i++)
                {
                    double angle = Math.PI * 2 * i / 9 + 0.2;
                    Crayon(svg, hand, [Polar(centre, r * 1.35f, angle), Polar(centre, r * 1.85f + Next(hand, -3f, 3f), angle)], yellow, h);
                }

                Crayon(svg, hand, [(w * 0.44f, h * 0.44f), (w * 0.44f, h * 0.49f)], brown, h);
                Crayon(svg, hand, [(w * 0.56f, h * 0.44f), (w * 0.56f, h * 0.49f)], brown, h);
                Crayon(svg, hand, Round((w * 0.5f, h * 0.50f), r * 0.45f, r * 0.3f, 0.15, 0.7, 9), brown, h);
                break;
            }

            case DoodleKind.AngryFace:
            {
                var red = new Ink(0.84f, 0.18f, 0.16f);
                var dark = new Ink(0.30f, 0.10f, 0.10f);
                float r = h * 0.34f;
                (float, float) centre = (w * 0.46f, h * 0.56f);
                Crayon(svg, hand, Round(centre, r * 1.05f, r, 0.3, 2.08, 32), red, h);
                Crayon(svg, hand, [(w * 0.30f, h * 0.42f), (w * 0.42f, h * 0.50f)], dark, h);
                Crayon(svg, hand, [(w * 0.62f, h * 0.42f), (w * 0.50f, h * 0.50f)], dark, h);
                Crayon(svg, hand, [(w * 0.38f, h * 0.55f), (w * 0.39f, h * 0.58f)], dark, h);
                Crayon(svg, hand, [(w * 0.54f, h * 0.55f), (w * 0.53f, h * 0.58f)], dark, h);
                Crayon(svg, hand, Round((w * 0.46f, h * 0.84f), r * 0.42f, r * 0.22f, 1.15, 0.7, 9), dark, h);

                // The vein that throbs on an angry forehead, as comics draw it.
                (float X, float Y) v = (w * 0.80f, h * 0.20f);
                float a = h * 0.07f;
                Crayon(svg, hand, [(v.X - a, v.Y - a * 0.3f), (v.X - a * 0.3f, v.Y - a * 0.3f), (v.X - a * 0.3f, v.Y - a)], red, h);
                Crayon(svg, hand, [(v.X + a, v.Y - a * 0.3f), (v.X + a * 0.3f, v.Y - a * 0.3f), (v.X + a * 0.3f, v.Y - a)], red, h);
                Crayon(svg, hand, [(v.X - a, v.Y + a * 0.3f), (v.X - a * 0.3f, v.Y + a * 0.3f), (v.X - a * 0.3f, v.Y + a)], red, h);
                Crayon(svg, hand, [(v.X + a, v.Y + a * 0.3f), (v.X + a * 0.3f, v.Y + a * 0.3f), (v.X + a * 0.3f, v.Y + a)], red, h);
                break;
            }

            default:
            {
                var green = new Ink(0.33f, 0.50f, 0.24f);
                var snow = new Ink(0.42f, 0.62f, 0.86f);
                var pole = new Ink(0.25f, 0.20f, 0.18f);
                var flag = new Ink(0.88f, 0.22f, 0.18f);
                Crayon(svg, hand, [(w * 0.06f, h * 0.92f), (w * 0.34f, h * 0.36f), (w * 0.47f, h * 0.56f), (w * 0.64f, h * 0.24f), (w * 0.94f, h * 0.92f)], green, h);
                Crayon(svg, hand, [(w * 0.52f, h * 0.46f), (w * 0.57f, h * 0.40f), (w * 0.61f, h * 0.45f), (w * 0.66f, h * 0.38f), (w * 0.72f, h * 0.44f)], snow, h);
                Crayon(svg, hand, [(w * 0.64f, h * 0.25f), (w * 0.645f, h * 0.04f)], pole, h);
                Crayon(svg, hand, [(w * 0.645f, h * 0.05f), (w * 0.80f, h * 0.10f), (w * 0.645f, h * 0.16f)], flag, h);
                break;
            }
        }

        return Close(svg);
    }

    /// <summary>
    /// One crayon line along <paramref name="points"/>: a few passes, each a
    /// little off the last and of a slightly different shade, then flecks of
    /// paper where the wax skipped.
    /// </summary>
    private static void Crayon(StringBuilder svg, Random hand, IReadOnlyList<(float X, float Y)> points, Ink colour, float size)
    {
        float width = Math.Max(3.6f, size * 0.05f);
        float[] passes = [1f, 0.8f, 0.6f];
        float[] shades = [0f, 0.22f, 0.1f];
        for (int pass = 0; pass < passes.Length; pass++)
        {
            var path = new StringBuilder();
            for (int i = 0; i < points.Count; i++)
            {
                float jitter = width * 0.35f;
                path.Append(F($"{(i == 0 ? 'M' : 'L')}{points[i].X + Next(hand, -jitter, jitter)} {points[i].Y + Next(hand, -jitter, jitter)}"));
            }

            svg.Append(F($"<path d=\"{path}\" fill=\"none\" stroke=\"{colour.Mix(Paper, shades[pass]).Hex}\" stroke-width=\"{width * passes[pass]}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        }

        string paper = Paper.Hex;
        for (int i = 1; i < points.Count; i++)
        {
            float length = PenPaths.Distance(points[i - 1], points[i]);
            for (float along = 0f; along < length; along += width * 0.9f)
            {
                if (hand.NextDouble() > 0.45)
                {
                    continue;
                }

                float t = along / length;
                float fleck = Next(hand, 0.5f, 1.1f);
                float x = points[i - 1].X + (points[i].X - points[i - 1].X) * t + Next(hand, -width * 0.4f, width * 0.4f);
                float y = points[i - 1].Y + (points[i].Y - points[i - 1].Y) * t + Next(hand, -width * 0.4f, width * 0.4f);
                svg.Append(F($"<rect x=\"{x}\" y=\"{y}\" width=\"{fleck}\" height=\"{fleck * 0.7f}\" fill=\"{paper}\"/>"));
            }
        }
    }

    /// <summary>Points round an ellipse from <paramref name="start"/> (in half turns) over <paramref name="sweep"/> half turns.</summary>
    private static List<(float X, float Y)> Round((float X, float Y) centre, float rx, float ry, double start, double sweep, int steps)
    {
        var points = new List<(float X, float Y)>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double angle = Math.PI * (start + sweep * i / steps);
            points.Add((centre.X + (float)Math.Cos(angle) * rx, centre.Y + (float)Math.Sin(angle) * ry));
        }

        return points;
    }

    private static (float X, float Y) Polar((float X, float Y) centre, float radius, double angle) =>
        (centre.X + (float)Math.Cos(angle) * radius, centre.Y + (float)Math.Sin(angle) * radius);

    /// <summary>A hash that is the same in every run, unlike a string's own.</summary>
    internal static uint Hash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash = (hash ^ c) * 16777619;
        }

        return hash;
    }

    private static StringBuilder Open(float width, float height) =>
        new StringBuilder(F($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">"));

    private static string Close(StringBuilder svg) => svg.Append("</svg>").ToString();

    private static float Next(Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
