using System;
using System.Text;

namespace BetterCoop.Core;

internal static partial class FormDrawing
{
    /// <summary>How far a print's white border reaches in from its edge to the picture.</summary>
    public const float PrintBorder = 5f;

    /// <summary>How far an album's photo corner reaches past the print's corner.</summary>
    public const float CornerReach = 4f;

    /// <summary>The white of photographic paper.</summary>
    public static Ink PrintWhite => Paper.Mix(new Ink(1f, 1f, 1f), 0.78f);

    /// <summary>
    /// A photographic print's paper, <paramref name="width"/> by
    /// <paramref name="height"/>: white, its edge cut with a deckle edge's
    /// small scallops, as snapshots were trimmed, and a faint shadow where it
    /// stands off the page. The picture goes inside its border.
    /// </summary>
    public static string Print(float width, float height)
    {
        var svg = Open(width, height);
        const float shadow = 1.6f;
        float w = width - shadow;
        float h = height - shadow;
        string edge = Deckle(0f, 0f, w, h);
        svg.Append(F($"<path transform=\"translate({shadow} {shadow})\" d=\"{edge}\" fill=\"{Paper.Mix(PaperDrawing.InkBlack, 0.22f).Hex}\"/>"));
        svg.Append(F($"<path d=\"{edge}\" fill=\"{PrintWhite.Hex}\" stroke=\"{Paper.Mix(PaperDrawing.InkBlack, 0.3f).Hex}\" stroke-width=\"0.8\"/>"));
        return Close(svg);
    }

    /// <summary>
    /// An album's four photo corners round a print: black paper pockets
    /// slipped over each of its corners, each with the light catching its
    /// folded edge. Drawn on an area <see cref="CornerReach"/> larger all round
    /// than the print.
    /// </summary>
    public static string PhotoCorners(float width, float height)
    {
        var svg = Open(width, height);
        const float leg = 19f;
        string black = PaperDrawing.InkBlack.Hex;
        string fold = Paper.Mix(PaperDrawing.InkBlack, 0.55f).Hex;
        foreach ((float x, float y, float sx, float sy) in new[] { (0f, 0f, 1f, 1f), (width, 0f, -1f, 1f), (0f, height, 1f, -1f), (width, height, -1f, -1f) })
        {
            // A right triangle along the two edges, its long side across the print's corner.
            svg.Append(F($"<path d=\"M{x} {y}L{x + sx * leg} {y}L{x} {y + sy * leg}Z\" fill=\"{black}\" stroke=\"{black}\" stroke-width=\"1\" stroke-linejoin=\"round\"/>"));
            svg.Append(F($"<path d=\"M{x + sx * (leg - 1.5f)} {y + sy * 1.2f}L{x + sx * 1.2f} {y + sy * (leg - 1.5f)}\" stroke=\"{fold}\" stroke-width=\"1.4\" stroke-linecap=\"round\"/>"));
        }

        return Close(svg);
    }

    /// <summary>
    /// A steel paper clip, <paramref name="width"/> by <paramref name="height"/>,
    /// standing upright: a wire bent in the two nested loops of a Gem clip.
    /// </summary>
    public static string PaperClip(float width, float height)
    {
        var svg = Open(width, height);
        float w = width;
        float h = height;
        float r1 = w * 0.42f;
        float r2 = w * 0.27f;
        float c = w / 2f;
        // From the inner leg's end up, round the top, down the outer leg, round the foot, and up the middle.
        string d = F($"M{c - r2 * 0.1f} {h * 0.32f}L{c - r2 * 0.1f} {h - r2 - 3f}A{r2} {r2} 0 0 0 {c + r2 * 1.1f} {h - r2 - 3f}L{c + r2 * 1.1f} {r1 + 2f}A{r1} {r1} 0 0 0 {c - r1} {r1 + 2f}L{c - r1} {h - r1 * 0.9f}");
        svg.Append(F($"<path d=\"{d}\" fill=\"none\" stroke=\"{PaperDrawing.InkBlack.Hex}\" stroke-width=\"{w * 0.2f}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        svg.Append(F($"<path d=\"{d}\" fill=\"none\" stroke=\"{new Ink(0.72f, 0.75f, 0.79f).Hex}\" stroke-width=\"{w * 0.1f}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        return Close(svg);
    }

    /// <summary>A rectangle's outline from (<paramref name="x"/>, <paramref name="y"/>) with its edges cut in small scallops.</summary>
    private static string Deckle(float x, float y, float width, float height)
    {
        const float scallop = 4.2f;
        const float depth = 0.9f;
        var path = new StringBuilder(F($"M{x} {y}"));
        void side(float fromX, float fromY, float toX, float toY, float nx, float ny)
        {
            float length = (float)Math.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY));
            int bumps = Math.Max(1, (int)Math.Round(length / scallop));
            for (int i = 1; i <= bumps; i++)
            {
                float t0 = (i - 0.5f) / bumps;
                float t1 = i / (float)bumps;
                float cx = fromX + (toX - fromX) * t0 + nx * depth;
                float cy = fromY + (toY - fromY) * t0 + ny * depth;
                path.Append(F($"Q{cx} {cy} {fromX + (toX - fromX) * t1} {fromY + (toY - fromY) * t1}"));
            }
        }

        side(x, y, x + width, y, 0f, 1f);
        side(x + width, y, x + width, y + height, -1f, 0f);
        side(x + width, y + height, x, y + height, 0f, -1f);
        side(x, y + height, x, y, 1f, 0f);
        return path.Append('Z').ToString();
    }
}
