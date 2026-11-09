using System;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The shapes of stamp a form can carry, after the office and post office
/// hand stamps of mid-century America.
/// </summary>
internal enum StampShape
{
    /// <summary>An office rubber stamp: a double rounded border round its word.</summary>
    Rubber,

    /// <summary>A circular date stamp: one ring, the office round its rim, the word inside.</summary>
    DateRing,

    /// <summary>A double-ring date stamp: the office between two rings, the word inside the inner one.</summary>
    DoubleRing,

    /// <summary>A duplex canceller: a date ring with four thick killer bars beside it.</summary>
    Duplex,

    /// <summary>A mute oval, as registered mail was struck: two ovals round the word.</summary>
    MuteOval,

    /// <summary>A target cancel: rings within rings, the word on a band across them.</summary>
    Target,

    /// <summary>A fancy cancel: a star cut in a ring, the word on a band across it.</summary>
    Star,

    /// <summary>A meat inspector's mark: a heavy ring with the inspection round its rim.</summary>
    Inspection,

    /// <summary>
    /// A notary's seal: a milled edge, a ring inside, the office round its
    /// rim, pressed into the paper by an embosser without ink, so it shows
    /// only by its light and shadow.
    /// </summary>
    Notary,
}

/// <summary>Where a stamp's words go: its size, and the ring its rim letters follow, if any.</summary>
internal readonly struct StampLayout
{
    public StampLayout(float width, float height, float ringX, float ring, float letters, float word)
    {
        Width = width;
        Height = height;
        RingX = ringX;
        Ring = ring;
        Letters = letters;
        Word = word;
    }

    /// <summary>The impression's width and height, in UI units.</summary>
    public float Width { get; }

    public float Height { get; }

    /// <summary>The centre of the ring from the left edge; the ring is centred vertically.</summary>
    public float RingX { get; }

    /// <summary>The ring's diameter, or 0 for a stamp without one.</summary>
    public float Ring { get; }

    /// <summary>The radius the rim letters stand on, or 0 for none.</summary>
    public float Letters { get; }

    /// <summary>The width the word may take across the middle.</summary>
    public float Word { get; }
}

internal static partial class FormDrawing
{
    /// <summary>A blue stamp pad's ink.</summary>
    public static readonly Ink StampBlue = new(0.160f, 0.270f, 0.600f);

    /// <summary>The violet of a registry office's or an inspector's stamp.</summary>
    public static readonly Ink StampViolet = new(0.430f, 0.200f, 0.540f);

    /// <summary>The shadow beside a ridge pressed up from the paper, the light coming from the top left.</summary>
    public static readonly Ink EmbossShade = PaperDrawing.Paper.Mix(PaperDrawing.InkBlack, 0.32f);

    /// <summary>The light catching a ridge pressed up from the paper.</summary>
    public static readonly Ink EmbossLight = PaperDrawing.Paper.Mix(new Ink(1f, 1f, 1f), 0.75f);

    /// <summary>How the words sit on a stamp of <paramref name="shape"/>; a rubber stamp's width follows its word.</summary>
    public static StampLayout LayoutOf(StampShape shape) => shape switch
    {
        StampShape.DateRing => new StampLayout(92f, 92f, 46f, 92f, 33f, 56f),
        StampShape.DoubleRing => new StampLayout(98f, 98f, 49f, 98f, 39f, 50f),
        StampShape.Duplex => new StampLayout(172f, 92f, 46f, 92f, 33f, 56f),
        StampShape.MuteOval => new StampLayout(136f, 76f, 68f, 0f, 0f, 92f),
        StampShape.Target => new StampLayout(94f, 94f, 47f, 94f, 0f, 84f),
        StampShape.Star => new StampLayout(94f, 94f, 47f, 94f, 0f, 84f),
        StampShape.Inspection => new StampLayout(98f, 98f, 49f, 98f, 37f, 54f),
        StampShape.Notary => new StampLayout(104f, 104f, 52f, 104f, 37f, 54f),
        _ => new StampLayout(0f, 56f, 0f, 0f, 0f, 0f),
    };

    /// <summary>
    /// A stamp's impression, <paramref name="width"/> by <paramref name="height"/>:
    /// its outline only, or, <paramref name="inked"/>, struck solid so its
    /// words show in the paper's colour. Inked as a rubber stamp inks: thin in
    /// patches where the pad was dry, lighter where the rubber met the paper
    /// less firmly, and heavier along a solid's edge, where the ink is
    /// squeezed out. A notary's seal takes no ink: it is embossed, deeper when
    /// struck. Its words are set over it as text.
    /// </summary>
    public static string Seal(StampShape shape, float width, float height, Ink ink, bool inked, int seed = 5)
    {
        if (shape == StampShape.Rubber)
        {
            return inked ? StampInked(width, height, seed, ink) : Stamp(width, height, seed, ink);
        }

        if (shape == StampShape.Notary)
        {
            return Embossed(width, height, inked);
        }

        var svg = Open(width, height);
        string on = ink.Hex;
        string off = Paper.Hex;
        StampLayout layout = LayoutOf(shape);
        float cx = layout.RingX;
        float cy = height / 2f;
        float r = Math.Min(layout.Ring, height) / 2f - 2f;
        switch (shape)
        {
            case StampShape.DateRing:
                Disc(svg, cx, cy, r, 3f, ink, inked, seed);
                break;
            case StampShape.DoubleRing:
                Disc(svg, cx, cy, r, 2.6f, ink, inked, seed);
                Ring(svg, cx, cy, r - 19f, 1.6f, inked ? off : on);
                break;
            case StampShape.Duplex:
                Disc(svg, cx, cy, r, 3f, ink, inked, seed);
                KillerBars(svg, cx + r + 7f, width - 2f, height, on);
                break;
            case StampShape.MuteOval:
                float rx = width / 2f - 2f;
                float ry = height / 2f - 2f;
                if (inked)
                {
                    svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx - 1.1f}\" ry=\"{ry - 1.1f}\" fill=\"{Body(ink)}\" stroke=\"{on}\" stroke-width=\"2.2\"/>"));
                    Pressure(svg, cx - rx, cy - ry, rx * 2f, ry * 2f, (x, y) => Sq((x - cx) / (rx - 4f)) + Sq((y - cy) / (ry - 4f)) < 1f, ink, seed);
                }
                else
                {
                    svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx - 1.5f}\" ry=\"{ry - 1.5f}\" fill=\"none\" stroke=\"{on}\" stroke-width=\"3\"/>"));
                }

                svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx - 9f}\" ry=\"{ry - 9f}\" fill=\"none\" stroke=\"{(inked ? off : on)}\" stroke-width=\"1.6\"/>"));
                break;
            case StampShape.Target:
                Disc(svg, cx, cy, r, 3f, ink, inked, seed);
                for (int ring = 1; ring <= 3; ring++)
                {
                    Ring(svg, cx, cy, r - ring * 10f, 3f, inked ? off : on);
                }

                Band(svg, cx, cy, r, inked ? on : off, inked ? off : on);
                break;
            case StampShape.Star:
                Disc(svg, cx, cy, r, 3f, ink, inked, seed);
                var star = new StringBuilder();
                for (int point = 0; point < 10; point++)
                {
                    double angle = -Math.PI / 2 + point * Math.PI / 5;
                    float reach = point % 2 == 0 ? r - 6f : (r - 6f) * 0.42f;
                    star.Append(F($"{(point == 0 ? "M" : "L")}{cx + (float)Math.Cos(angle) * reach} {cy + (float)Math.Sin(angle) * reach}"));
                }

                svg.Append(F($"<path d=\"{star}Z\" fill=\"{(inked ? off : on)}\"/>"));
                Band(svg, cx, cy, r, inked ? on : off, inked ? off : on);
                break;
            case StampShape.Inspection:
                Disc(svg, cx, cy, r, 4.4f, ink, inked, seed);
                Ring(svg, cx, cy, r - 21f, 1.6f, inked ? off : on);
                break;
        }

        Wear(svg, width, height, seed, inked ? 60 : 80);
        return Close(svg);
    }

    /// <summary>A rubber stamp struck solid: the border filled in, ink squeezed to its edge, its inner rule in the paper's colour.</summary>
    private static string StampInked(float width, float height, int seed, Ink ink)
    {
        var svg = Open(width, height);
        float outer = Math.Max(2f, height * 0.07f);
        float r = height * 0.18f;
        float corner = r + outer / 2f;
        svg.Append(F($"<rect x=\"1.1\" y=\"1.1\" width=\"{width - 2.2f}\" height=\"{height - 2.2f}\" rx=\"{corner - 1.1f}\" fill=\"{Body(ink)}\" stroke=\"{ink.Hex}\" stroke-width=\"2.2\"/>"));
        Pressure(svg, 0f, 0f, width, height, (x, y) => x > corner * 0.6f && x < width - corner * 0.6f && y > 4f && y < height - 4f, ink, seed);
        float gap = outer * 2.2f;
        svg.Append(F($"<rect x=\"{gap}\" y=\"{gap}\" width=\"{width - 2f * gap}\" height=\"{height - 2f * gap}\" rx=\"{Math.Max(1f, r - gap / 2f)}\" fill=\"none\" stroke=\"{Paper.Hex}\" stroke-width=\"{outer * 0.45f}\"/>"));
        Wear(svg, width, height, seed, (int)((width + height) / 4f));
        return Close(svg);
    }

    /// <summary>
    /// A notary's seal pressed into the paper without ink: its milled edge
    /// and rings stand up from the sheet, lit from the top left, so each shows
    /// as a light line beside a shadow. Struck, it is pressed deeper: the
    /// relief stands higher and the band of the office's words sinks into shade.
    /// </summary>
    private static string Embossed(float width, float height, bool deep)
    {
        var svg = Open(width, height);
        StampLayout layout = LayoutOf(StampShape.Notary);
        float cx = layout.RingX;
        float cy = height / 2f;
        float r = Math.Min(layout.Ring, height) / 2f - 3f;
        float depth = deep ? 1.5f : 0.85f;
        var edge = new StringBuilder();
        const int teeth = 44;
        for (int tooth = 0; tooth < teeth * 2; tooth++)
        {
            double angle = tooth * Math.PI / teeth;
            float reach = tooth % 2 == 0 ? r : r - 4f;
            edge.Append(F($"{(tooth == 0 ? "M" : "L")}{cx + (float)Math.Cos(angle) * reach} {cy + (float)Math.Sin(angle) * reach}"));
        }

        if (deep)
        {
            svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r - 15.5f}\" fill=\"none\" stroke=\"{Paper.Mix(EmbossShade, 0.3f).Hex}\" stroke-width=\"14\"/>"));
        }

        foreach ((float shift, Ink tone) in new[] { (depth, EmbossShade), (-depth * 0.8f, EmbossLight) })
        {
            string colour = tone.Hex;
            svg.Append(F($"<path transform=\"translate({shift} {shift})\" d=\"{edge}Z\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"1.7\" stroke-linejoin=\"round\"/>"));
            Ring(svg, cx + shift, cy + shift, r - 8f, 1.4f, colour);
            Ring(svg, cx + shift, cy + shift, r - 23f, 1.4f, colour);
        }

        // The raised ridges themselves are the sheet's own colour, between their light and shadow.
        svg.Append(F($"<path d=\"{edge}Z\" fill=\"none\" stroke=\"{Paper.Hex}\" stroke-width=\"0.9\" stroke-linejoin=\"round\"/>"));
        Ring(svg, cx, cy, r - 8f, 0.7f, Paper.Hex);
        Ring(svg, cx, cy, r - 23f, 0.7f, Paper.Hex);
        return Close(svg);
    }

    /// <summary>
    /// A duplex canceller's killer: an oval of thick bars between
    /// <paramref name="left"/> and <paramref name="right"/>, the middle bars
    /// longest, as the post office's obliterators struck them.
    /// </summary>
    private static void KillerBars(StringBuilder svg, float left, float right, float height, string ink)
    {
        const int bars = 8;
        const float thick = 6.4f;
        float half = (right - left) / 2f;
        float middle = (left + right) / 2f;
        float span = height - 6f;
        float step = span / bars;
        for (int bar = 0; bar < bars; bar++)
        {
            float y = 3f + step * (bar + 0.5f);
            float across = (y - height / 2f) / (span / 2f);
            float reach = Math.Max(thick, half * (float)Math.Sqrt(Math.Max(0f, 1f - across * across)));
            svg.Append(F($"<rect x=\"{middle - reach}\" y=\"{y - thick / 2f}\" width=\"{reach * 2f}\" height=\"{thick}\" rx=\"{thick / 2f}\" fill=\"{ink}\"/>"));
        }
    }

    /// <summary>A ring, or struck solid, a disc of ink, lighter in its body than at its edge.</summary>
    private static void Disc(StringBuilder svg, float cx, float cy, float r, float stroke, Ink ink, bool inked, int seed)
    {
        if (inked)
        {
            svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r - 1.1f}\" fill=\"{Body(ink)}\" stroke=\"{ink.Hex}\" stroke-width=\"2.2\"/>"));
            Pressure(svg, cx - r, cy - r, r * 2f, r * 2f, (x, y) => Sq(x - cx) + Sq(y - cy) < Sq(r - 4f), ink, seed);
            Ring(svg, cx, cy, r - stroke - 1.6f, 1f, Paper.Hex);
        }
        else
        {
            Ring(svg, cx, cy, r - stroke / 2f, stroke, ink.Hex);
        }
    }

    /// <summary>The body of a solid impression: a little lighter than the ink squeezed to its edge.</summary>
    private static string Body(Ink ink) => ink.Mix(Paper, 0.14f).Hex;

    /// <summary>
    /// The uneven pressure inside a solid impression, in the box from
    /// (<paramref name="x0"/>, <paramref name="y0"/>): lighter patches where
    /// the rubber met the paper less firmly, only where <paramref name="inside"/>.
    /// </summary>
    private static void Pressure(StringBuilder svg, float x0, float y0, float width, float height, Func<float, float, bool> inside, Ink ink, int seed)
    {
        var hand = new Random(seed * 31 + 7);
        string light = ink.Mix(Paper, 0.32f).Hex;
        int tries = (int)(width * height / 30f);
        for (int i = 0; i < tries; i++)
        {
            float x = x0 + Next(hand, 0f, width);
            float y = y0 + Next(hand, 0f, height);
            if (InkDrying.Noise(x / 8f, y / 8f, seed + 101) > 0.3f || !inside(x, y))
            {
                continue;
            }

            Blot(svg, hand, x, y, Next(hand, 1.4f, 3f), light);
        }
    }

    private static void Ring(StringBuilder svg, float cx, float cy, float r, float stroke, string colour) =>
        svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{stroke}\"/>"));

    /// <summary>A band across a ring for the word, ruled above and below.</summary>
    private static void Band(StringBuilder svg, float cx, float cy, float r, string fill, string rule)
    {
        const float half = 13f;
        float reach = r - 2f;
        svg.Append(F($"<rect x=\"{cx - reach}\" y=\"{cy - half}\" width=\"{reach * 2f}\" height=\"{half * 2f}\" fill=\"{fill}\"/>"));
        svg.Append(F($"<rect x=\"{cx - reach}\" y=\"{cy - half}\" width=\"{reach * 2f}\" height=\"1.6\" fill=\"{rule}\"/>"));
        svg.Append(F($"<rect x=\"{cx - reach}\" y=\"{cy + half - 1.6f}\" width=\"{reach * 2f}\" height=\"1.6\" fill=\"{rule}\"/>"));
    }

    /// <summary>
    /// Where the ink ran thin: the paper showing through in ragged voids,
    /// gathered where the pad was dry rather than spread evenly; about
    /// <paramref name="flecks"/> of them.
    /// </summary>
    private static void Wear(StringBuilder svg, float width, float height, int seed, int flecks)
    {
        var hand = new Random(seed);
        string paper = Paper.Hex;
        int tries = flecks * 3;
        for (int i = 0; i < tries; i++)
        {
            float x = Next(hand, 0f, width);
            float y = Next(hand, 0f, height);
            float dry = InkDrying.Noise(x / 11f, y / 11f, seed);
            if (dry > 0.42f)
            {
                continue;
            }

            Blot(svg, hand, x, y, Next(hand, 0.6f, 1f) + (0.42f - dry) * 3.2f, paper);
        }
    }

    /// <summary>A ragged blot about <paramref name="size"/> across round (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static void Blot(StringBuilder svg, Random hand, float x, float y, float size, string colour)
    {
        var path = new StringBuilder();
        const int corners = 6;
        double turn = Next(hand, 0f, (float)Math.PI);
        for (int corner = 0; corner < corners; corner++)
        {
            double angle = turn + corner * 2 * Math.PI / corners;
            float reach = size * 0.5f * Next(hand, 0.55f, 1.15f);
            path.Append(F($"{(corner == 0 ? "M" : "L")}{x + (float)Math.Cos(angle) * reach} {y + (float)Math.Sin(angle) * reach * 0.8f}"));
        }

        svg.Append(F($"<path d=\"{path}Z\" fill=\"{colour}\"/>"));
    }

    private static float Sq(float value) => value * value;
}
