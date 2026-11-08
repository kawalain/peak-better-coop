using System;
using System.Globalization;
using System.Text;

namespace BetterCoop.Core;

/// <summary>A colour with straight (not premultiplied) alpha, each channel 0 to 1.</summary>
internal readonly struct Ink
{
    public Ink(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public float R { get; }

    public float G { get; }

    public float B { get; }

    public float A { get; }

    /// <summary>This ink mixed toward <paramref name="other"/> by <paramref name="amount"/>, as one opaque colour.</summary>
    public Ink Mix(Ink other, float amount) => new(
        R + (other.R - R) * amount,
        G + (other.G - G) * amount,
        B + (other.B - B) * amount);

    public string Hex => FormattableString.Invariant($"#{Byte(R):x2}{Byte(G):x2}{Byte(B):x2}");

    private static int Byte(float value) => (int)Math.Round(Math.Max(0f, Math.Min(1f, value)) * 255f);
}

/// <summary>
/// The Coop Menu's paper as an SVG drawing: a card like the game's passport
/// and boarding pass, drawn from shapes rather than taken from the game's
/// textures. A rounded sheet of cream paper with a hard drop shadow like the
/// game's buttons, a near-black outline, faint watercolour blotches and
/// fibres, a thin printed rule inside the edge, and two perforations with
/// notches cut into the edges, one under the title and one over the footer,
/// like a ticket's stubs.
/// </summary>
/// <remarks>
/// Every length is in panel units, and the drawing is made for the panel's
/// exact size so nothing stretches. Inside the paper every colour is opaque,
/// mixed with the paper beforehand, so the paper stays opaque wherever it is
/// drawn into a texture.
/// </remarks>
internal static class PaperDrawing
{
    public static readonly Ink Paper = new(0.953f, 0.918f, 0.827f);

    /// <summary>The near-black of the game's outlines and of text on the paper.</summary>
    public static readonly Ink InkBlack = new(0.141f, 0.122f, 0.110f);

    /// <summary>The passport's blue, for the printed rule, perforations, and headings.</summary>
    public static readonly Ink PassportBlue = new(0.118f, 0.255f, 0.812f);

    /// <summary>Room around the paper for its shadow.</summary>
    public const float Margin = 16f;

    public const float Radius = 26f;

    public const float Outline = 4.5f;

    public const float Notch = 15f;

    /// <summary>How far the printed rule lies inside the edge.</summary>
    public const float RuleInset = 15f;

    /// <summary>The shadow's drop, as the game's buttons cast theirs.</summary>
    public const float ShadowDrop = 8f;

    /// <summary>The shadow's opacity, as the game's button shadows have.</summary>
    public const float ShadowOpacity = 0.27f;

    private const float Dash = 11f;

    private const float Gap = 9f;

    private const float DashThickness = 2.6f;

    /// <summary>The whole drawing's size, paper and margin, in panel units.</summary>
    public static (float Width, float Height) Canvas(float width, float height) =>
        (width + 2f * Margin, height + 2f * Margin);

    /// <summary>
    /// The drawing for paper of the given size, with perforations
    /// <paramref name="topTear"/> below its top and <paramref name="bottomTear"/>
    /// above its bottom. The view box starts at the margin's outer corner.
    /// </summary>
    public static string Svg(float width, float height, float topTear, float bottomTear, int seed = 7)
    {
        var sheet = new Sheet(width, height, topTear, height - bottomTear);
        (float canvasWidth, float canvasHeight) = Canvas(width, height);
        var svg = new StringBuilder();
        svg.Append(F($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{canvasWidth}\" height=\"{canvasHeight}\" viewBox=\"0 0 {canvasWidth} {canvasHeight}\">"));
        svg.Append(F($"<g transform=\"translate({Margin} {Margin})\">"));

        svg.Append(F($"<path d=\"{sheet.Edge(0f)}\" transform=\"translate(0 {ShadowDrop})\" fill=\"#000000\" fill-opacity=\"{ShadowOpacity}\"/>"));
        svg.Append(F($"<path d=\"{sheet.Edge(0f)}\" fill=\"{InkBlack.Hex}\"/>"));
        svg.Append(F($"<path d=\"{sheet.Edge(Outline)}\" fill=\"{Paper.Hex}\"/>"));

        var random = new Random(seed);
        AppendBlotches(svg, random, width, height);
        AppendFibres(svg, random, width, height);

        svg.Append(F($"<path d=\"{sheet.Edge(RuleInset)}\" fill=\"none\" stroke=\"{Paper.Mix(PassportBlue, 0.5f).Hex}\" stroke-width=\"1.6\"/>"));
        AppendPerforation(svg, width, sheet.TopTear);
        AppendPerforation(svg, width, sheet.BottomTear);

        svg.Append("</g></svg>");
        return svg.ToString();
    }

    /// <summary>How many dashes a perforation across paper this wide has.</summary>
    public static int DashCount(float width) =>
        (int)Math.Floor((width - 2f * (Notch + 14f) + Gap) / (Dash + Gap));

    private static void AppendPerforation(StringBuilder svg, float width, float y)
    {
        float start = Notch + 14f;
        int count = DashCount(width);
        float used = count * (Dash + Gap) - Gap;
        float x = start + (width - 2f * start - used) / 2f;
        string fill = Paper.Mix(PassportBlue, 0.7f).Hex;
        for (int i = 0; i < count; i++, x += Dash + Gap)
        {
            svg.Append(F($"<rect x=\"{x}\" y=\"{y - DashThickness / 2f}\" width=\"{Dash}\" height=\"{DashThickness}\" rx=\"{DashThickness / 2f}\" fill=\"{fill}\"/>"));
        }
    }

    /// <summary>
    /// Faint stains like the passport's watercolour, kept clear of the edge.
    /// Each is a few nested blobs a shade apart, so it darkens toward its
    /// middle as a wash does rather than showing one hard edge.
    /// </summary>
    private static void AppendBlotches(StringBuilder svg, Random random, float width, float height)
    {
        var light = new Ink(1f, 0.99f, 0.95f);
        int count = Math.Max(5, (int)(width * height / 80000f));
        for (int i = 0; i < count; i++)
        {
            float size = Next(random, 80f, 170f);
            float cx = Next(random, 50f + size * 0.6f, width - 50f - size * 0.6f);
            float cy = Next(random, 50f + size * 0.6f, height - 50f - size * 0.6f);
            bool dark = random.NextDouble() < 0.55;
            float depth = dark ? Next(random, 0.02f, 0.035f) : Next(random, 0.1f, 0.18f);
            for (int layer = 1; layer <= 3; layer++)
            {
                Ink tone = Paper.Mix(dark ? InkBlack : light, depth * layer / 3f);
                float scale = 1f - 0.24f * (layer - 1);
                svg.Append(F($"<path d=\"{Blob(random, cx, cy, size * scale)}\" fill=\"{tone.Hex}\"/>"));
            }
        }
    }

    /// <summary>Tiny fibres and specks in the paper.</summary>
    private static void AppendFibres(StringBuilder svg, Random random, float width, float height)
    {
        int count = (int)(width * height / 7000f);
        for (int i = 0; i < count; i++)
        {
            float x = Next(random, 30f, width - 30f);
            float y = Next(random, 30f, height - 30f);
            float length = Next(random, 2f, 7f);
            double angle = random.NextDouble() * Math.PI;
            float dx = (float)Math.Cos(angle) * length / 2f;
            float dy = (float)Math.Sin(angle) * length / 2f;
            string tone = Paper.Mix(InkBlack, Next(random, 0.07f, 0.16f)).Hex;
            svg.Append(F($"<path d=\"M{x - dx} {y - dy}L{x + dx} {y + dy}\" stroke=\"{tone}\" stroke-width=\"{Next(random, 0.6f, 1.1f)}\" stroke-linecap=\"round\" fill=\"none\"/>"));
        }
    }

    /// <summary>A closed, smooth, irregular blob through points round a centre.</summary>
    private static string Blob(Random random, float cx, float cy, float size)
    {
        const int points = 7;
        var xs = new float[points];
        var ys = new float[points];
        for (int i = 0; i < points; i++)
        {
            double angle = 2 * Math.PI * i / points + Next(random, -0.25f, 0.25f);
            float radius = size * Next(random, 0.55f, 1f);
            xs[i] = cx + (float)Math.Cos(angle) * radius;
            ys[i] = cy + (float)Math.Sin(angle) * radius * 0.75f;
        }

        // Catmull-Rom through the points, as cubic Béziers.
        var path = new StringBuilder(F($"M{xs[0]} {ys[0]}"));
        for (int i = 0; i < points; i++)
        {
            int a = (i + points - 1) % points;
            int b = (i + 1) % points;
            int c = (i + 2) % points;
            path.Append(F($"C{xs[i] + (xs[b] - xs[a]) / 6f} {ys[i] + (ys[b] - ys[a]) / 6f} {xs[b] - (xs[c] - xs[i]) / 6f} {ys[b] - (ys[c] - ys[i]) / 6f} {xs[b]} {ys[b]}"));
        }

        return path.Append('Z').ToString();
    }

    private static float Next(Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>The paper's outline and the outlines inside it, in SVG's downward y.</summary>
    private readonly struct Sheet
    {
        private readonly float width;

        private readonly float height;

        public Sheet(float width, float height, float topTear, float bottomTear)
        {
            this.width = width;
            this.height = height;
            TopTear = topTear;
            BottomTear = bottomTear;
        }

        public float TopTear { get; }

        public float BottomTear { get; }

        /// <summary>
        /// The paper's edge moved <paramref name="inset"/> inwards: corners
        /// tighter, notches wider, meeting the sides at sharp corners.
        /// </summary>
        public string Edge(float inset)
        {
            float d = inset;
            float r = Math.Max(1f, Radius - d);
            float n = Notch + d;
            float j = (float)Math.Sqrt(n * n - d * d);
            float left = d;
            float right = width - d;
            float top = d;
            float bottom = height - d;
            var path = new StringBuilder();
            path.Append(F($"M{left + r} {top}H{right - r}A{r} {r} 0 0 1 {right} {top + r}"));
            path.Append(F($"V{TopTear - j}A{n} {n} 0 0 0 {right} {TopTear + j}"));
            path.Append(F($"V{BottomTear - j}A{n} {n} 0 0 0 {right} {BottomTear + j}"));
            path.Append(F($"V{bottom - r}A{r} {r} 0 0 1 {right - r} {bottom}"));
            path.Append(F($"H{left + r}A{r} {r} 0 0 1 {left} {bottom - r}"));
            path.Append(F($"V{BottomTear + j}A{n} {n} 0 0 0 {left} {BottomTear - j}"));
            path.Append(F($"V{TopTear + j}A{n} {n} 0 0 0 {left} {TopTear - j}"));
            path.Append(F($"V{top + r}A{r} {r} 0 0 1 {left + r} {top}Z"));
            return path.ToString();
        }
    }
}
