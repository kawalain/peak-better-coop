using System;
using System.Globalization;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// The marks of a paper form, as SVG drawings made for their own size: a
/// check box, the ring a pen draws round a chosen answer, the dotted leader
/// between a printed label and its answer, the line to sign on, a rubber
/// stamp, a passport photo's frame, a card, and an empty slot. Printed parts
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

    private static Ink Paper => PaperDrawing.Paper;

    /// <summary>A printed box, with a pen check through it when <paramref name="checkedBox"/>.</summary>
    public static string CheckBox(float size, bool checkedBox)
    {
        var svg = Open(size, size);
        float box = size * 0.62f;
        float x = (size - box) / 2f;
        float y = size - box - size * 0.08f;
        svg.Append(F($"<rect x=\"{x}\" y=\"{y}\" width=\"{box}\" height=\"{box}\" rx=\"{box * 0.08f}\" fill=\"none\" stroke=\"{PaperDrawing.InkBlack.Hex}\" stroke-width=\"{size * 0.05f}\"/>"));
        if (checkedBox)
        {
            // A quick hand tick that runs past the box's top right, as a pen leaves one.
            float w = size * 0.11f;
            svg.Append(F($"<path d=\"M{x + box * 0.12f} {y + box * 0.52f}C{x + box * 0.25f} {y + box * 0.62f} {x + box * 0.33f} {y + box * 0.78f} {x + box * 0.4f} {y + box * 0.92f}C{x + box * 0.62f} {y + box * 0.45f} {x + box * 0.85f} {y + box * 0.05f} {x + box * 1.12f} {y - box * 0.22f}\" fill=\"none\" stroke=\"{Pen.Hex}\" stroke-width=\"{w}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        }

        return Close(svg);
    }

    /// <summary>The ring a pen draws round a chosen answer: one stroke that overshoots where it started.</summary>
    public static string Ring(float width, float height)
    {
        var svg = Open(width, height);
        float stroke = Math.Max(1.6f, height * 0.06f);
        float rx = width / 2f - stroke;
        float ry = height / 2f - stroke;
        float cx = width / 2f;
        float cy = height / 2f;
        // Once round and a little more, growing slightly so the overshoot
        // runs outside the start rather than over it.
        const int steps = 56;
        var path = new StringBuilder();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            double angle = Math.PI * (1.15 + 2.18 * t);
            float grow = 0.9f + 0.1f * t;
            float px = cx + (float)Math.Cos(angle) * rx * grow;
            float py = cy - (float)Math.Sin(angle) * ry * grow;
            path.Append(F($"{(i == 0 ? 'M' : 'L')}{px} {py}"));
        }

        svg.Append(F($"<path d=\"{path}\" fill=\"none\" stroke=\"{Pen.Hex}\" stroke-width=\"{stroke}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        return Close(svg);
    }

    /// <summary>Dots leading the eye from a printed label to its answer.</summary>
    public static string Leader(float width, float height)
    {
        var svg = Open(width, height);
        string fill = Paper.Mix(PaperDrawing.InkBlack, 0.45f).Hex;
        const float step = 7f;
        float radius = Math.Min(1.2f, height / 2f);
        int count = Math.Max(0, (int)Math.Floor((width - 2f * radius) / step) + 1);
        float x = width - radius - (count - 1) * step;
        for (int i = 0; i < count; i++, x += step)
        {
            svg.Append(F($"<circle cx=\"{x}\" cy=\"{height - radius - 1f}\" r=\"{radius}\" fill=\"{fill}\"/>"));
        }

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
    public static string Stamp(float width, float height, int seed = 3)
    {
        var svg = Open(width, height);
        string red = StampRed.Hex;
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
            svg.Append(F($"<circle cx=\"{sx}\" cy=\"{sy}\" r=\"{Next(random, outer * 0.15f, outer * 0.45f)}\" fill=\"{paper}\"/>"));
        }

        return Close(svg);
    }

    /// <summary>A passport photo's frame: a white border with a thin ink edge, held by four paper corners.</summary>
    public static string PhotoFrame(float width, float height)
    {
        var svg = Open(width, height);
        float border = Math.Max(3f, width * 0.07f);
        string white = Paper.Mix(new Ink(1f, 1f, 1f), 0.7f).Hex;
        svg.Append(F($"<path fill-rule=\"evenodd\" d=\"M0 0H{width}V{height}H0Z M{border} {border}V{height - border}H{width - border}V{border}Z\" fill=\"{white}\"/>"));
        svg.Append(F($"<rect x=\"0.6\" y=\"0.6\" width=\"{width - 1.2f}\" height=\"{height - 1.2f}\" fill=\"none\" stroke=\"{Paper.Mix(PaperDrawing.InkBlack, 0.55f).Hex}\" stroke-width=\"1.2\"/>"));
        string corner = Paper.Mix(PaperDrawing.InkBlack, 0.25f).Hex;
        float c = border * 2.4f;
        svg.Append(F($"<path d=\"M0 0H{c}L0 {c}Z\" fill=\"{corner}\"/>"));
        svg.Append(F($"<path d=\"M{width} 0V{c}L{width - c} 0Z\" fill=\"{corner}\"/>"));
        svg.Append(F($"<path d=\"M0 {height}V{height - c}L{c} {height}Z\" fill=\"{corner}\"/>"));
        svg.Append(F($"<path d=\"M{width} {height}H{width - c}L{width} {height - c}Z\" fill=\"{corner}\"/>"));
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

    private static StringBuilder Open(float width, float height) =>
        new StringBuilder(F($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">"));

    private static string Close(StringBuilder svg) => svg.Append("</svg>").ToString();

    private static float Next(Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
