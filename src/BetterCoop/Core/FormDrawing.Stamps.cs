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

    /// <summary>A notary's seal: a milled edge, a ring inside, the office round its rim.</summary>
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
    /// words show in the paper's colour. Worn in places as hand stamps are.
    /// Its words are set over it as text.
    /// </summary>
    public static string Seal(StampShape shape, float width, float height, Ink ink, bool inked, int seed = 5)
    {
        if (shape == StampShape.Rubber)
        {
            return inked ? StampInked(width, height, seed, ink) : Stamp(width, height, seed, ink);
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
                Disc(svg, cx, cy, r, 3f, on, off, inked);
                break;
            case StampShape.DoubleRing:
                Disc(svg, cx, cy, r, 2.6f, on, off, inked);
                Ring(svg, cx, cy, r - 19f, 1.6f, inked ? off : on);
                break;
            case StampShape.Duplex:
                Disc(svg, cx, cy, r, 3f, on, off, inked);
                for (int bar = 0; bar < 4; bar++)
                {
                    float y = cy - 28f + bar * 16f;
                    svg.Append(F($"<rect x=\"{cx + r + 8f}\" y=\"{y}\" width=\"{width - cx - r - 10f}\" height=\"9\" rx=\"1.5\" fill=\"{on}\"/>"));
                }

                break;
            case StampShape.MuteOval:
                float rx = width / 2f - 2f;
                float ry = height / 2f - 2f;
                svg.Append(inked
                    ? F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx}\" ry=\"{ry}\" fill=\"{on}\"/>")
                    : F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx - 1.5f}\" ry=\"{ry - 1.5f}\" fill=\"none\" stroke=\"{on}\" stroke-width=\"3\"/>"));
                svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx - 9f}\" ry=\"{ry - 9f}\" fill=\"none\" stroke=\"{(inked ? off : on)}\" stroke-width=\"1.6\"/>"));
                break;
            case StampShape.Target:
                Disc(svg, cx, cy, r, 3f, on, off, inked);
                for (int ring = 1; ring <= 3; ring++)
                {
                    Ring(svg, cx, cy, r - ring * 10f, 3f, inked ? off : on);
                }

                Band(svg, cx, cy, r, inked ? on : off, inked ? off : on);
                break;
            case StampShape.Star:
                Disc(svg, cx, cy, r, 3f, on, off, inked);
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
                Disc(svg, cx, cy, r, 4.4f, on, off, inked);
                Ring(svg, cx, cy, r - 21f, 1.6f, inked ? off : on);
                break;
            case StampShape.Notary:
                var edge = new StringBuilder();
                const int teeth = 44;
                for (int tooth = 0; tooth < teeth * 2; tooth++)
                {
                    double angle = tooth * Math.PI / teeth;
                    float reach = tooth % 2 == 0 ? r : r - 4f;
                    edge.Append(F($"{(tooth == 0 ? "M" : "L")}{cx + (float)Math.Cos(angle) * reach} {cy + (float)Math.Sin(angle) * reach}"));
                }

                svg.Append(inked
                    ? F($"<path d=\"{edge}Z\" fill=\"{on}\"/>")
                    : F($"<path d=\"{edge}Z\" fill=\"none\" stroke=\"{on}\" stroke-width=\"1.8\" stroke-linejoin=\"round\"/>"));
                Ring(svg, cx, cy, r - 8f, 1.4f, inked ? off : on);
                Ring(svg, cx, cy, r - 23f, 1.4f, inked ? off : on);
                break;
        }

        Wear(svg, width, height, seed, inked ? 30 : 46);
        return Close(svg);
    }

    /// <summary>A rubber stamp struck solid: the border filled in, its inner rule in the paper's colour.</summary>
    private static string StampInked(float width, float height, int seed, Ink ink)
    {
        var svg = Open(width, height);
        float outer = Math.Max(2f, height * 0.07f);
        float r = height * 0.18f;
        svg.Append(F($"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" rx=\"{r + outer / 2f}\" fill=\"{ink.Hex}\"/>"));
        float gap = outer * 2.2f;
        svg.Append(F($"<rect x=\"{gap}\" y=\"{gap}\" width=\"{width - 2f * gap}\" height=\"{height - 2f * gap}\" rx=\"{Math.Max(1f, r - gap / 2f)}\" fill=\"none\" stroke=\"{Paper.Hex}\" stroke-width=\"{outer * 0.45f}\"/>"));
        Wear(svg, width, height, seed, (int)((width + height) / 7f));
        return Close(svg);
    }

    /// <summary>A ring, or struck solid, a disc of ink.</summary>
    private static void Disc(StringBuilder svg, float cx, float cy, float r, float stroke, string on, string off, bool inked)
    {
        if (inked)
        {
            svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{on}\"/>"));
            Ring(svg, cx, cy, r - stroke - 1.6f, 1f, off);
        }
        else
        {
            Ring(svg, cx, cy, r - stroke / 2f, stroke, on);
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

    /// <summary>Where the ink ran thin: flecks of paper over the marks.</summary>
    private static void Wear(StringBuilder svg, float width, float height, int seed, int flecks)
    {
        var hand = new Random(seed);
        string paper = Paper.Hex;
        for (int i = 0; i < flecks; i++)
        {
            float x = Next(hand, 0f, width);
            float y = Next(hand, 0f, height);
            float fleck = Next(hand, 0.8f, 2.2f);
            svg.Append(F($"<rect x=\"{x}\" y=\"{y}\" width=\"{fleck}\" height=\"{fleck * 0.6f}\" fill=\"{paper}\"/>"));
        }
    }
}
