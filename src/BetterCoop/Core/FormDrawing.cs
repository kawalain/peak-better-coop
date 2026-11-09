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

    /// <summary>The grey of a pencil's lead.</summary>
    public static readonly Ink Lead = new(0.360f, 0.360f, 0.400f);

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
    /// A scout drawn in the game's way where no photo can be taken: the big
    /// round head in <paramref name="skin"/> under a beanie, a neckerchief
    /// over the jacket, thick dark outlines, on the photo studio's backdrop.
    /// A <paramref name="mystery"/> scout, one not known yet, is all grey
    /// with a big question mark drawn on the face instead of eyes.
    /// </summary>
    public static string Silhouette(float width, float height, Ink skin, bool mystery = false)
    {
        var svg = Open(width, height);
        string outline = PaperDrawing.InkBlack.Hex;
        float line = Math.Max(2f, width * 0.026f);
        var grey = new Ink(0.60f, 0.62f, 0.66f);
        Ink jacket = mystery ? grey.Mix(PaperDrawing.InkBlack, 0.1f) : new Ink(0.55f, 0.50f, 0.36f);
        Ink scarf = mystery ? grey.Mix(new Ink(1f, 1f, 1f), 0.2f) : new Ink(0.80f, 0.24f, 0.20f);
        Ink hat = mystery ? grey.Mix(PaperDrawing.InkBlack, 0.3f) : new Ink(0.30f, 0.40f, 0.29f);
        Ink face = mystery ? grey.Mix(new Ink(1f, 1f, 1f), 0.25f) : skin;
        svg.Append(F($"<rect width=\"{width}\" height=\"{height}\" fill=\"{Backdrop.Hex}\"/>"));

        float cx = width / 2f;
        float r = width * 0.33f;
        float cy = height * 0.47f;

        // Shoulders in the jacket, and the neckerchief's knot under the chin.
        float shoulder = height * 0.80f;
        svg.Append(F($"<path d=\"M{width * 0.04f} {height + line}C{width * 0.05f} {shoulder} {width * 0.24f} {height * 0.74f} {cx} {height * 0.74f}C{width * 0.76f} {height * 0.74f} {width * 0.95f} {shoulder} {width * 0.96f} {height + line}Z\" fill=\"{jacket.Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\" stroke-linejoin=\"round\"/>"));
        svg.Append(F($"<path d=\"M{cx - r * 0.62f} {cy + r * 0.86f}L{cx + r * 0.62f} {cy + r * 0.86f}L{cx} {height * 0.97f}Z\" fill=\"{scarf.Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\" stroke-linejoin=\"round\"/>"));

        // The head, with a soft highlight as the game's cel shading leaves.
        svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{face.Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));
        svg.Append(F($"<ellipse cx=\"{cx - r * 0.42f}\" cy=\"{cy - r * 0.05f}\" rx=\"{r * 0.16f}\" ry=\"{r * 0.26f}\" fill=\"#ffffff\" fill-opacity=\"0.28\"/>"));

        // A beanie pulled over the top of the head, with its turned-up band and bobble.
        float band = cy - r * 0.48f;
        svg.Append(F($"<path d=\"M{cx - r * 0.98f} {band}C{cx - r * 0.98f} {cy - r * 1.42f} {cx + r * 0.98f} {cy - r * 1.42f} {cx + r * 0.98f} {band}Z\" fill=\"{hat.Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\" stroke-linejoin=\"round\"/>"));
        svg.Append(F($"<rect x=\"{cx - r * 1.04f}\" y=\"{band - r * 0.2f}\" width=\"{r * 2.08f}\" height=\"{r * 0.3f}\" rx=\"{r * 0.14f}\" fill=\"{hat.Mix(PaperDrawing.InkBlack, 0.18f).Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));
        svg.Append(F($"<circle cx=\"{cx}\" cy=\"{cy - r * 1.18f}\" r=\"{r * 0.17f}\" fill=\"{hat.Mix(new Ink(1f, 1f, 1f), 0.25f).Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));

        if (mystery)
        {
            // A big question mark drawn on the face: a dark stroke under a pale one.
            float top = cy - r * 0.34f;
            string mark = F($"M{cx - r * 0.34f} {top}C{cx - r * 0.34f} {top - r * 0.5f} {cx + r * 0.42f} {top - r * 0.5f} {cx + r * 0.38f} {top + r * 0.02f}C{cx + r * 0.35f} {top + r * 0.32f} {cx} {top + r * 0.3f} {cx} {top + r * 0.62f}");
            float dot = top + r * 0.98f;
            svg.Append(F($"<path d=\"{mark}\" fill=\"none\" stroke=\"{outline}\" stroke-width=\"{r * 0.3f}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
            svg.Append(F($"<circle cx=\"{cx}\" cy=\"{dot}\" r=\"{r * 0.15f}\" fill=\"{outline}\"/>"));
            svg.Append(F($"<path d=\"{mark}\" fill=\"none\" stroke=\"{Paper.Hex}\" stroke-width=\"{r * 0.15f}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
            svg.Append(F($"<circle cx=\"{cx}\" cy=\"{dot}\" r=\"{r * 0.075f}\" fill=\"{Paper.Hex}\"/>"));
        }
        else
        {
            // Two tall eyes with a glint each.
            foreach (float side in new[] { -1f, 1f })
            {
                float ex = cx + side * r * 0.3f;
                float ey = cy + r * 0.02f;
                svg.Append(F($"<ellipse cx=\"{ex}\" cy=\"{ey}\" rx=\"{r * 0.11f}\" ry=\"{r * 0.19f}\" fill=\"{outline}\"/>"));
                svg.Append(F($"<circle cx=\"{ex - r * 0.03f}\" cy=\"{ey - r * 0.08f}\" r=\"{r * 0.04f}\" fill=\"#ffffff\"/>"));
            }
        }

        return Close(svg);
    }

    /// <summary>
    /// A scout's arm reaching down, drawn in the game's way where no picture
    /// of it can be taken: a jacket sleeve from the top, its cuff, and a
    /// round mitten of a hand in <paramref name="skin"/> with its thumb out,
    /// thick dark outlines, on a clear ground.
    /// </summary>
    public static string Hand(float width, float height, Ink skin)
    {
        var svg = Open(width, height);
        string outline = PaperDrawing.InkBlack.Hex;
        float line = Math.Max(2.5f, width * 0.03f);
        var sleeve = new Ink(0.55f, 0.50f, 0.36f);
        float cx = width / 2f;
        float arm = width * 0.34f;
        float cuff = height * 0.62f;

        svg.Append(F($"<path d=\"M{cx - arm} {-line}L{cx - arm * 0.9f} {cuff}L{cx + arm * 0.9f} {cuff}L{cx + arm} {-line}Z\" fill=\"{sleeve.Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\" stroke-linejoin=\"round\"/>"));
        // A crease down the sleeve, as the game's cloth folds.
        svg.Append(F($"<path d=\"M{cx + arm * 0.25f} {height * 0.1f}C{cx + arm * 0.1f} {height * 0.3f} {cx + arm * 0.35f} {height * 0.42f} {cx + arm * 0.2f} {cuff - line * 2f}\" fill=\"none\" stroke=\"{sleeve.Mix(PaperDrawing.InkBlack, 0.35f).Hex}\" stroke-width=\"{line * 0.6f}\" stroke-linecap=\"round\"/>"));

        // The hand: a round palm with the fingers together, and the thumb out to one side.
        float palm = width * 0.36f;
        float py = cuff + (palm * 0.85f);
        string flesh = skin.Hex;
        svg.Append(F($"<ellipse cx=\"{cx - palm * 0.95f}\" cy=\"{py - palm * 0.35f}\" rx=\"{palm * 0.32f}\" ry=\"{palm * 0.5f}\" transform=\"rotate(-28 {cx - palm * 0.95f} {py - palm * 0.35f})\" fill=\"{flesh}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));
        svg.Append(F($"<ellipse cx=\"{cx}\" cy=\"{py}\" rx=\"{palm}\" ry=\"{palm * 1.08f}\" fill=\"{flesh}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));
        for (int finger = -1; finger <= 1; finger++)
        {
            float fx = cx + (finger * palm * 0.42f);
            svg.Append(F($"<path d=\"M{fx} {py + palm * 0.35f}L{fx} {py + palm * 0.85f}\" stroke=\"{skin.Mix(PaperDrawing.InkBlack, 0.4f).Hex}\" stroke-width=\"{line * 0.6f}\" stroke-linecap=\"round\" fill=\"none\"/>"));
        }

        // The cuff over the wrist.
        svg.Append(F($"<rect x=\"{cx - arm * 0.98f}\" y=\"{cuff - height * 0.05f}\" width=\"{arm * 1.96f}\" height=\"{height * 0.07f}\" rx=\"{height * 0.02f}\" fill=\"{sleeve.Mix(PaperDrawing.InkBlack, 0.15f).Hex}\" stroke=\"{outline}\" stroke-width=\"{line}\"/>"));
        return Close(svg);
    }

    /// <summary>The photo studio's backdrop, behind a drawn scout as behind a photographed one.</summary>
    public static readonly Ink Backdrop = new(0.80f, 0.86f, 0.90f);

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
    /// <param name="frame">Which of the doodle's hand-drawn frames: the lines wobble from one to the next.</param>
    public static string Doodle(float width, float height, string signer, int frame = 0)
    {
        var svg = Open(width, height);
        var hand = new Random((int)(Hash(signer) & 0x7fffffff) ^ (frame * 7919));
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
    /// A silly sea anemone pencilled in a margin: a squat body on a wavy foot,
    /// tentacles waving every way, crossed eyes of two sizes, and a goofy
    /// grin with its tongue out.
    /// </summary>
    /// <param name="frame">Which of the doodle's hand-drawn frames: the lines wobble from one to the next.</param>
    public static string Anemone(float width, float height, int frame = 0)
    {
        var svg = Open(width, height);

        // The tentacles keep their shape from frame to frame; only the hand wobbles.
        var shape = new Random(7);
        var hand = new Random(7 + frame * 7919);
        float w = width;
        float h = height;
        float left = w * 0.32f;
        float right = w * 0.68f;
        float rim = h * 0.50f;
        float foot = h * 0.93f;

        // The body: a squat cup, its rim an ellipse, standing on a wavy foot.
        Pencil(svg, hand, [(left + w * 0.03f, foot), (left - w * 0.02f, h * 0.72f), (left, rim)], Lead, h);
        Pencil(svg, hand, [(right - w * 0.03f, foot), (right + w * 0.02f, h * 0.72f), (right, rim)], Lead, h);
        Pencil(svg, hand, Round((w * 0.5f, rim), (right - left) / 2f, h * 0.05f, 0.0, 2.0, 20), Lead, h);
        var sole = new List<(float X, float Y)>();
        for (int i = 0; i <= 12; i++)
        {
            float t = i / 12f;
            sole.Add((left - w * 0.06f + (right - left + w * 0.12f) * t, foot + (float)Math.Sin(t * Math.PI * 4) * h * 0.02f));
        }

        Pencil(svg, hand, sole, Lead, h);

        // Tentacles, each waving its own way, with a knob at the tip.
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            (float X, float Y) from = (left + (right - left) * t, rim - h * 0.02f);
            double angle = Math.PI * (1.22 + 0.56 * t);
            float length = h * Next(shape, 0.30f, 0.40f);
            double phase = Next(shape, 0f, 6.3f);
            var tentacle = new List<(float X, float Y)>();
            for (int s = 0; s <= 8; s++)
            {
                float along = length * s / 8f;
                float sway = (float)Math.Sin(phase + s * 0.9) * h * 0.04f * s / 8f;
                tentacle.Add((
                    from.X + (float)Math.Cos(angle) * along - (float)Math.Sin(angle) * sway,
                    from.Y + (float)Math.Sin(angle) * along + (float)Math.Cos(angle) * sway));
            }

            Pencil(svg, hand, tentacle, Lead, h);
            Pencil(svg, hand, Round(tentacle[tentacle.Count - 1], h * 0.025f, h * 0.025f, 0.0, 2.0, 8), Lead, h);
        }

        // Crossed eyes of two sizes, looking at each other.
        (float X, float Y) leftEye = (w * 0.43f, h * 0.63f);
        (float X, float Y) rightEye = (w * 0.575f, h * 0.62f);
        Pencil(svg, hand, Round(leftEye, h * 0.045f, h * 0.045f, 0.0, 2.0, 12), Lead, h);
        Pencil(svg, hand, Round(rightEye, h * 0.065f, h * 0.065f, 0.0, 2.0, 14), Lead, h);
        string lead = Lead.Hex;
        svg.Append(F($"<circle cx=\"{leftEye.X + h * 0.02f}\" cy=\"{leftEye.Y + h * 0.01f}\" r=\"{h * 0.02f}\" fill=\"{lead}\"/>"));
        svg.Append(F($"<circle cx=\"{rightEye.X - h * 0.03f}\" cy=\"{rightEye.Y + h * 0.02f}\" r=\"{h * 0.022f}\" fill=\"{lead}\"/>"));

        // A goofy grin, its tongue out to one side.
        Pencil(svg, hand, Round((w * 0.5f, h * 0.73f), w * 0.09f, h * 0.06f, 0.05, 0.9, 10), Lead, h);
        Pencil(svg, hand, Round((w * 0.53f, h * 0.80f), w * 0.025f, h * 0.03f, 0.0, 1.0, 6), Lead, h);
        return Close(svg);
    }

    /// <summary>
    /// A clumsy grey pegasus pony pencilled in a margin, after Derpy: a big
    /// round head on a chubby body, ears laid back under shaggy bangs, big
    /// eyes that look two ways at once, an open grin, a wing half raised,
    /// and a muffin she has her eye on.
    /// </summary>
    /// <param name="frame">Which of the doodle's hand-drawn frames: the lines wobble from one to the next.</param>
    public static string Pony(float width, float height, int frame = 0)
    {
        var svg = Open(width, height);
        var hand = new Random(11 + frame * 7919);
        float w = width;
        float h = height;
        string lead = Lead.Hex;
        string paper = Paper.Hex;

        // A chubby body, open where the head sits on it, and short legs.
        Pencil(svg, hand, Round((w * 0.40f, h * 0.68f), w * 0.18f, h * 0.12f, -0.12, 1.50, 24), Lead, h);
        foreach (float x in new[] { 0.28f, 0.35f, 0.45f, 0.52f })
        {
            Pencil(svg, hand, [(w * x, h * 0.76f), (w * x, h * 0.93f), (w * x + h * 0.04f, h * 0.93f)], Lead, h);
        }

        // A tail flowing back in three wavy strands.
        for (int i = 0; i < 3; i++)
        {
            var strand = new List<(float X, float Y)>();
            for (int s = 0; s <= 10; s++)
            {
                float t = s / 10f;
                strand.Add((
                    w * (0.225f - 0.19f * t - 0.02f * i * t),
                    h * (0.64f + (0.16f + 0.05f * i) * (float)Math.Pow(t, 1.4) + 0.025f * (float)Math.Sin(t * Math.PI * 2.2 + i))));
            }

            Pencil(svg, hand, strand, Lead, h);
        }

        // A wing half raised from the shoulder, its feathers scalloped along the back.
        (float X, float Y) shoulder = (w * 0.44f, h * 0.62f);
        (float X, float Y) tip = (w * 0.17f, h * 0.30f);
        (float X, float Y) root = (w * 0.31f, h * 0.62f);
        Pencil(svg, hand, [shoulder, (w * 0.38f, h * 0.44f), (w * 0.28f, h * 0.33f), tip], Lead, h);
        const int feathers = 4;
        for (int i = 0; i < feathers; i++)
        {
            float a = (float)i / feathers;
            float b = (float)(i + 1) / feathers;
            (float X, float Y) from = (tip.X + (root.X - tip.X) * a, tip.Y + (root.Y - tip.Y) * a);
            (float X, float Y) to = (tip.X + (root.X - tip.X) * b, tip.Y + (root.Y - tip.Y) * b);
            (float X, float Y) mid = ((from.X + to.X) / 2f - h * 0.05f, (from.Y + to.Y) / 2f + h * 0.01f);
            Pencil(svg, hand, [from, (from.X - h * 0.04f, from.Y + h * 0.02f), mid, to], Lead, h);
        }

        // A big round head on a short neck, its muzzle bulging forward.
        (float X, float Y) head = (w * 0.62f, h * 0.37f);
        float r = h * 0.235f;
        Pencil(svg, hand, Round(head, r, r, 0.45, 1.75, 30), Lead, h);
        Pencil(svg, hand, Round((head.X + r * 0.80f, head.Y + r * 0.50f), r * 0.36f, r * 0.36f, 1.40, 1.12, 14), Lead, h);
        svg.Append(F($"<circle cx=\"{head.X + r * 1.02f}\" cy=\"{head.Y + r * 0.42f}\" r=\"{r * 0.045f}\" fill=\"{lead}\"/>"));

        // Ears laid back, each with its inner line; a pegasus has no horn.
        Pencil(svg, hand, [(head.X - r * 0.30f, head.Y - r * 0.88f), (head.X - r * 0.55f, head.Y - r * 1.38f), (head.X - r * 0.02f, head.Y - r * 0.98f)], Lead, h);
        Pencil(svg, hand, [(head.X - r * 0.30f, head.Y - r * 0.98f), (head.X - r * 0.46f, head.Y - r * 1.24f)], Lead, h);
        Pencil(svg, hand, [(head.X - r * 0.62f, head.Y - r * 0.72f), (head.X - r * 0.88f, head.Y - r * 1.18f), (head.X - r * 0.42f, head.Y - r * 0.86f)], Lead, h);

        // Shaggy bangs: locks swept from the crown across the forehead, each to a point.
        Pencil(svg, hand, Curve((head.X - r * 0.25f, head.Y - r * 1.00f), (head.X + r * 0.62f, head.Y - r * 1.30f), (head.X + r * 0.98f, head.Y - r * 0.36f)), Lead, h);
        Pencil(svg, hand, Curve((head.X - r * 0.45f, head.Y - r * 0.92f), (head.X + r * 0.30f, head.Y - r * 1.08f), (head.X + r * 0.46f, head.Y - r * 0.36f)), Lead, h);
        Pencil(svg, hand, Curve((head.X - r * 0.72f, head.Y - r * 0.72f), (head.X - r * 0.10f, head.Y - r * 0.88f), (head.X - r * 0.02f, head.Y - r * 0.40f)), Lead, h);
        Pencil(svg, hand, Curve((head.X + r * 0.98f, head.Y - r * 0.36f), (head.X + r * 0.72f, head.Y - r * 0.62f), (head.X + r * 0.56f, head.Y - r * 0.62f)), Lead, h);

        // The mane down the back of the neck.
        Pencil(svg, hand, [(head.X - r * 1.00f, head.Y), (head.X - r * 1.12f, head.Y + r * 0.70f), (head.X - r * 0.75f, head.Y + r * 1.25f)], Lead, h);
        Pencil(svg, hand, [(head.X - r * 0.88f, head.Y + r * 0.30f), (head.X - r * 0.92f, head.Y + r * 0.95f)], Lead, h);

        // Big eyes that look two ways at once, with a shine in each and lashes.
        (float X, float Y) near = (head.X + r * 0.08f, head.Y + r * 0.05f);
        (float X, float Y) far = (head.X + r * 0.62f, head.Y + r * 0.02f);
        Pencil(svg, hand, Round(near, r * 0.28f, r * 0.36f, 0.0, 2.0, 16), Lead, h);
        Pencil(svg, hand, Round(far, r * 0.20f, r * 0.32f, 0.0, 2.0, 14), Lead, h);
        (float X, float Y) nearPupil = (near.X - r * 0.10f, near.Y - r * 0.15f);
        (float X, float Y) farPupil = (far.X + r * 0.06f, far.Y + r * 0.15f);
        svg.Append(F($"<circle cx=\"{nearPupil.X}\" cy=\"{nearPupil.Y}\" r=\"{r * 0.14f}\" fill=\"{lead}\"/>"));
        svg.Append(F($"<circle cx=\"{farPupil.X}\" cy=\"{farPupil.Y}\" r=\"{r * 0.11f}\" fill=\"{lead}\"/>"));
        svg.Append(F($"<circle cx=\"{nearPupil.X + r * 0.05f}\" cy=\"{nearPupil.Y - r * 0.05f}\" r=\"{r * 0.045f}\" fill=\"{paper}\"/>"));
        svg.Append(F($"<circle cx=\"{farPupil.X + r * 0.04f}\" cy=\"{farPupil.Y - r * 0.04f}\" r=\"{r * 0.035f}\" fill=\"{paper}\"/>"));
        Pencil(svg, hand, [(near.X - r * 0.22f, near.Y - r * 0.25f), (near.X - r * 0.36f, near.Y - r * 0.34f)], Lead, h);
        Pencil(svg, hand, [(near.X - r * 0.26f, near.Y - r * 0.12f), (near.X - r * 0.40f, near.Y - r * 0.16f)], Lead, h);
        Pencil(svg, hand, [(far.X + r * 0.16f, far.Y - r * 0.24f), (far.X + r * 0.27f, far.Y - r * 0.33f)], Lead, h);

        // An open grin under the muzzle, its tongue showing.
        (float X, float Y) mouth = (head.X + r * 0.72f, head.Y + r * 0.66f);
        Pencil(svg, hand, [(mouth.X - r * 0.30f, mouth.Y - r * 0.04f), (mouth.X + r * 0.28f, mouth.Y - r * 0.02f)], Lead, h);
        Pencil(svg, hand, Round((mouth.X, mouth.Y - r * 0.03f), r * 0.29f, r * 0.20f, 0.0, 1.0, 10), Lead, h);
        Pencil(svg, hand, Round((mouth.X + r * 0.02f, mouth.Y + r * 0.12f), r * 0.12f, r * 0.06f, 1.0, 1.0, 6), Lead, h);

        // The muffin: a fluted cup, a domed top, and a few berries.
        (float X, float Y) cup = (w * 0.89f, h * 0.85f);
        float m = h * 0.07f;
        Pencil(svg, hand, [(cup.X - m, cup.Y - m * 0.6f), (cup.X - m * 0.7f, cup.Y + m), (cup.X + m * 0.7f, cup.Y + m), (cup.X + m, cup.Y - m * 0.6f)], Lead, h);
        Pencil(svg, hand, [(cup.X - m * 0.3f, cup.Y - m * 0.5f), (cup.X - m * 0.25f, cup.Y + m)], Lead, h);
        Pencil(svg, hand, [(cup.X + m * 0.3f, cup.Y - m * 0.5f), (cup.X + m * 0.25f, cup.Y + m)], Lead, h);
        Pencil(svg, hand, Round((cup.X, cup.Y - m * 0.6f), m * 1.2f, m * 0.9f, 1.0, 1.0, 12), Lead, h);
        svg.Append(F($"<circle cx=\"{cup.X - m * 0.4f}\" cy=\"{cup.Y - m * 1.0f}\" r=\"{m * 0.16f}\" fill=\"{lead}\"/>"));
        svg.Append(F($"<circle cx=\"{cup.X + m * 0.3f}\" cy=\"{cup.Y - m * 1.2f}\" r=\"{m * 0.16f}\" fill=\"{lead}\"/>"));
        return Close(svg);
    }

    /// <summary>One pencil line along <paramref name="points"/>, gone over twice by a light hand that never draws quite straight.</summary>
    private static void Pencil(StringBuilder svg, Random hand, IReadOnlyList<(float X, float Y)> points, Ink colour, float size)
    {
        // A pencil's line is about as thin whatever the size of the doodle.
        float width = Math.Max(1.1f, Math.Min(1.7f, size * 0.0076f));
        float[] passes = [1f, 0.6f];
        float[] shades = [0.1f, 0.35f];
        for (int pass = 0; pass < passes.Length; pass++)
        {
            List<(float X, float Y)> line = Wander(points, hand, size);
            var path = new StringBuilder();
            for (int i = 0; i < line.Count; i++)
            {
                float jitter = width * 0.2f;
                path.Append(F($"{(i == 0 ? 'M' : 'L')}{line[i].X + Next(hand, -jitter, jitter)} {line[i].Y + Next(hand, -jitter, jitter)}"));
            }

            svg.Append(F($"<path d=\"{path}\" fill=\"none\" stroke=\"{colour.Mix(Paper, shades[pass]).Hex}\" stroke-width=\"{width * passes[pass]}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"));
        }
    }

    /// <summary>
    /// The line a hand draws along <paramref name="points"/>: cut into short
    /// steps that wander slowly to either side of where it means to go, as no
    /// hand draws a straight line.
    /// </summary>
    private static List<(float X, float Y)> Wander(IReadOnlyList<(float X, float Y)> points, Random hand, float size)
    {
        var line = new List<(float X, float Y)>();
        if (points.Count < 2)
        {
            line.AddRange(points);
            return line;
        }

        float step = Math.Max(1.2f, size * 0.022f);
        float reach = size * 0.011f;
        double phase = hand.NextDouble() * Math.PI * 2;
        double wave = Math.PI * 2 / (size * Next(hand, 0.16f, 0.28f));
        double drift = Next(hand, 0.4f, 0.8f);
        float along = 0f;
        line.Add(points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            (float X, float Y) a = points[i - 1];
            (float X, float Y) b = points[i];
            float length = PenPaths.Distance(a, b);
            if (length < 1e-3f)
            {
                continue;
            }

            (float X, float Y) normal = (-(b.Y - a.Y) / length, (b.X - a.X) / length);
            int count = Math.Max(1, (int)Math.Ceiling(length / step));
            for (int k = 1; k <= count; k++)
            {
                float t = (float)k / count;
                double d = along + length * t;

                // Two slow waves of different lengths, so the wandering never repeats evenly.
                float off = reach * (float)(Math.Sin(phase + d * wave) * 0.7 + Math.Sin(phase * 1.7 + d * wave * 2.3 * drift) * 0.3);
                if (i == points.Count - 1 && k == count)
                {
                    off *= 0.4f;
                }

                line.Add((a.X + (b.X - a.X) * t + normal.X * off, a.Y + (b.Y - a.Y) * t + normal.Y * off));
            }

            along += length;
        }

        return line;
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

    /// <summary>Points along a curve from <paramref name="from"/> to <paramref name="to"/>, bent toward <paramref name="toward"/>.</summary>
    private static List<(float X, float Y)> Curve((float X, float Y) from, (float X, float Y) toward, (float X, float Y) to, int steps = 10)
    {
        var points = new List<(float X, float Y)>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float u = 1f - t;
            points.Add((u * u * from.X + 2f * u * t * toward.X + t * t * to.X, u * u * from.Y + 2f * u * t * toward.Y + t * t * to.Y));
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
