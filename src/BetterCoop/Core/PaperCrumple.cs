using System;
using System.Collections.Generic;
using System.Numerics;

namespace BetterCoop.Core;

/// <summary>
/// A sheet of paper crumpled up by hand, as a grid of points in 3D: x to
/// the right, y up, z away from the viewer, centred on the sheet's middle.
/// First it creases; then its edges and a corner fold over one after
/// another, flaps laid across the sheet as hands fold paper; then the whole
/// is pressed together into a small crinkled wad. The same seed crumples
/// the same way.
/// </summary>
internal sealed class PaperCrumple
{
    /// <summary>When the folds begin and by when the last is made.</summary>
    private const float FoldsFrom = 0.06f;

    private const float FoldsTo = 0.72f;

    /// <summary>When the folded sheet begins to be pressed together.</summary>
    private const float PressFrom = 0.5f;

    /// <summary>How small the pressed wad is beside the folded sheet.</summary>
    private const float Pressed = 0.42f;

    private readonly Crease[] creases;

    private readonly Crease[] crinkles;

    private readonly Fold[] folds;

    private readonly float amplitudes;

    public PaperCrumple(float width, float height, int columns, int rows, int seed)
    {
        Width = width;
        Height = height;
        Columns = columns;
        Rows = rows;
        float span = Math.Min(width, height);
        var hand = new Random(seed);
        creases = Creases(hand, 5, span, 0.18f, 0.36f, 0.018f, 0.04f);
        crinkles = Creases(hand, 6, span, 0.06f, 0.14f, 0.02f, 0.045f);
        foreach (Crease crease in creases)
        {
            amplitudes += Math.Abs(crease.Amplitude);
        }

        // The four edges in a random order, then a corner, each folded over across the sheet.
        var edges = new List<Fold>
        {
            Edge(hand, new Vector2(-1f, 0f), width / 2f, width),
            Edge(hand, new Vector2(1f, 0f), width / 2f, width),
            Edge(hand, new Vector2(0f, 1f), height / 2f, height),
            Edge(hand, new Vector2(0f, -1f), height / 2f, height),
        };
        for (int i = edges.Count - 1; i > 0; i--)
        {
            int j = hand.Next(i + 1);
            (edges[i], edges[j]) = (edges[j], edges[i]);
        }

        var corner = Vector2.Normalize(new Vector2(hand.Next(2) == 0 ? -width : width, hand.Next(2) == 0 ? -height : height));
        edges.Add(new Fold(corner, Math.Abs(Vector2.Dot(new Vector2(width, height) / 2f, corner)) * Next(hand, 0.55f, 0.7f), Angle(hand), Toward(hand)));
        folds = edges.ToArray();
    }

    public float Width { get; }

    public float Height { get; }

    /// <summary>Grid points across, one more than the cells.</summary>
    public int Columns { get; }

    /// <summary>Grid points down, one more than the cells.</summary>
    public int Rows { get; }

    public int Count => Columns * Rows;

    /// <summary>Where grid point (<paramref name="column"/>, <paramref name="row"/>) lies on the flat sheet; row 0 is the bottom.</summary>
    public Vector3 Rest(int column, int row) => new(
        ((column / (float)(Columns - 1)) - 0.5f) * Width,
        ((row / (float)(Rows - 1)) - 0.5f) * Height,
        0f);

    /// <summary>Fills <paramref name="into"/> with every grid point, row by row, <paramref name="progress"/> (0 to 1) of the way to a wad.</summary>
    public void Shape(float progress, Vector3[] into)
    {
        progress = Math.Max(0f, Math.Min(1f, progress));
        float creasing = Easing.Out(progress / 0.4f);
        float pressing = Easing.InOut((progress - PressFrom) / (1f - PressFrom));
        float span = Math.Min(Width, Height);
        float stagger = (FoldsTo - FoldsFrom) / folds.Length;
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                Vector3 rest = Rest(column, row);
                var flat = new Vector2(rest.X, rest.Y);
                Vector3 point = new(rest.X, rest.Y, Ridge(creases, flat) * creasing);

                // Each fold lays whatever lies beyond its line now over the rest, as a stack of paper folds,
                // carrying earlier folds with it.
                for (int i = 0; i < folds.Length; i++)
                {
                    Fold fold = folds[i];
                    float start = FoldsFrom + (i * stagger);
                    float turn = Easing.InOut((progress - start) / (stagger * 1.6f));
                    if (turn > 0f)
                    {
                        point = fold.Turn(point, turn);
                    }
                }

                // Pressed together into a wad: drawn in towards the middle, crinkling as it goes...
                float crinkle = Ridge(crinkles, flat);
                float shrink = 1f - ((1f - Pressed) * pressing);
                float wobble = 1f + (0.35f * pressing * crinkle / span * 8f);
                var pressed = new Vector3(
                    point.X * shrink * wobble,
                    point.Y * shrink * wobble,
                    (point.Z * (1f + pressing)) + (crinkle * pressing * 1.8f));

                // ...and rounded: the folded sheet closes round a lumpy ball, its layers kept apart.
                float reach = 0.5f * Math.Max(Width, Height) * 0.75f;
                float outward = Math.Min(1f, new Vector2(point.X, point.Y).Length() / reach);
                float round = outward * (float)Math.PI * 0.8f;
                float around = (float)Math.Atan2(point.Y, point.X);
                float radius = span * 0.2f * (1f + (0.25f * crinkle / (span * 0.1f)));
                var ball = new Vector3(
                    (float)(Math.Sin(round) * Math.Cos(around)) * radius,
                    (float)(Math.Sin(round) * Math.Sin(around)) * radius,
                    (-(float)Math.Cos(round) * radius) + (point.Z * 0.3f));
                into[(row * Columns) + column] = Vector3.Lerp(pressed, ball, 0.6f * pressing);
            }
        }

        // Folding takes paper from some sides more than others; the hands keep the wad where the sheet was.
        Vector3 middle = Vector3.Zero;
        for (int i = 0; i < Count; i++)
        {
            middle += into[i];
        }

        middle = new Vector3(middle.X, middle.Y, 0f) / Count * Easing.InOut(progress / PressFrom);
        for (int i = 0; i < Count; i++)
        {
            into[i] -= middle;
        }
    }

    /// <summary>
    /// The crumpled points turned by <paramref name="turn"/>, moved by
    /// <paramref name="shift"/> and seen in perspective from
    /// <paramref name="focal"/> in front, as triangles from the farthest to
    /// the nearest, each lit from <paramref name="light"/> (towards the light).
    /// </summary>
    public void Triangles(Vector3[] shape, Quaternion turn, Vector3 shift, float focal, Vector3 light, List<WadTriangle> into)
    {
        into.Clear();
        light = Vector3.Normalize(light);
        var placed = new Vector3[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            placed[i] = Vector3.Transform(shape[i], turn) + shift;
        }

        for (int row = 0; row < Rows - 1; row++)
        {
            for (int column = 0; column < Columns - 1; column++)
            {
                int a = (row * Columns) + column;
                int b = a + 1;
                int c = a + Columns;
                int d = c + 1;
                into.Add(Triangle(placed, a, b, c, focal, light));
                into.Add(Triangle(placed, b, d, c, focal, light));
            }
        }

        into.Sort((x, y) => y.Depth.CompareTo(x.Depth));
    }

    /// <summary>Where a point lands on the screen, seen from <paramref name="focal"/> in front of the sheet.</summary>
    public static Vector2 Project(Vector3 point, float focal)
    {
        float scale = focal / Math.Max(1f, focal + point.Z);
        return new Vector2(point.X * scale, point.Y * scale);
    }

    private static WadTriangle Triangle(Vector3[] placed, int a, int b, int c, float focal, Vector3 light)
    {
        // With x right, y up and z away, this order faces the viewer while the sheet lies flat.
        Vector3 normal = Vector3.Cross(placed[c] - placed[a], placed[b] - placed[a]);
        float size = normal.Length();
        normal = size > 1e-6f ? normal / size : new Vector3(0f, 0f, -1f);
        bool back = normal.Z > 0f;
        if (back)
        {
            normal = -normal;
        }

        float lit = 0.5f + (0.5f * Math.Max(0f, Vector3.Dot(normal, light)));
        return new WadTriangle(
            a,
            b,
            c,
            Project(placed[a], focal),
            Project(placed[b], focal),
            Project(placed[c], focal),
            (placed[a].Z + placed[b].Z + placed[c].Z) / 3f,
            back ? lit * 0.78f : lit,
            back);
    }

    /// <summary>A strip along one edge, a fifth to a third of the sheet deep, its line a little askew.</summary>
    private static Fold Edge(Random hand, Vector2 outward, float half, float across)
    {
        double skew = Next(hand, -0.22f, 0.22f);
        var tilted = new Vector2(
            (float)((outward.X * Math.Cos(skew)) - (outward.Y * Math.Sin(skew))),
            (float)((outward.X * Math.Sin(skew)) + (outward.Y * Math.Cos(skew))));
        return new Fold(tilted, half - (across * Next(hand, 0.2f, 0.32f)), Angle(hand), Toward(hand));
    }

    /// <summary>Which way a flap folds: mostly towards the viewer, so the folding shows.</summary>
    private static float Toward(Random hand) => hand.NextDouble() < 0.75 ? -1f : 1f;

    /// <summary>How far a flap folds over: most of the way, never quite flat, so flaps do not lie in one another.</summary>
    private static float Angle(Random hand) => (float)Math.PI * Next(hand, 0.72f, 0.9f);

    private static Crease[] Creases(Random hand, int count, float span, float spacingMin, float spacingMax, float heightMin, float heightMax)
    {
        var made = new Crease[count];
        for (int i = 0; i < count; i++)
        {
            double angle = hand.NextDouble() * Math.PI;
            made[i] = new Crease(
                new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)),
                span * Next(hand, spacingMin, spacingMax),
                Next(hand, 0f, 1f),
                span * Next(hand, heightMin, heightMax) * (hand.Next(2) == 0 ? -1f : 1f));
        }

        return made;
    }

    private static float Ridge(Crease[] set, Vector2 point)
    {
        float sum = 0f;
        foreach (Crease crease in set)
        {
            float x = (Vector2.Dot(point, crease.Across) / crease.Spacing) + crease.Phase;
            float fraction = x - (float)Math.Floor(x);
            // A sharp-crested wave: straight slopes meeting at creases.
            sum += crease.Amplitude * ((2f * Math.Abs(fraction - 0.5f)) - 0.5f);
        }

        return sum;
    }

    private static float Next(Random random, float min, float max) => min + ((float)random.NextDouble() * (max - min));

    private readonly struct Crease
    {
        public Crease(Vector2 across, float spacing, float phase, float amplitude)
        {
            Across = across;
            Spacing = spacing;
            Phase = phase;
            Amplitude = amplitude;
        }

        public Vector2 Across { get; }

        public float Spacing { get; }

        public float Phase { get; }

        public float Amplitude { get; }
    }

    /// <summary>
    /// A fold along a straight line on the flat sheet: everything farther
    /// out than <see cref="Line"/> along <see cref="Out"/> turns over the
    /// line, towards the viewer or away, by up to <see cref="Most"/>. The
    /// paper bends round a narrow hinge rather than at a knife edge, so it
    /// stays whole across the line wherever the grid's points fall.
    /// </summary>
    private readonly struct Fold
    {
        /// <summary>How wide the bend at the fold is, a little more than a grid cell.</summary>
        private const float Hinge = 80f;

        public Fold(Vector2 outward, float line, float most, float side)
        {
            Out = outward;
            Line = line;
            Most = most;
            Side = side;
        }

        public Vector2 Out { get; }

        public float Line { get; }

        public float Most { get; }

        /// <summary>-1 to fold towards the viewer, 1 away.</summary>
        public float Side { get; }

        /// <summary>Bends <paramref name="point"/> over the fold's line, <paramref name="share"/> of the way over.</summary>
        public Vector3 Turn(Vector3 point, float share)
        {
            var along = new Vector2(-Out.Y, Out.X);
            var flat = new Vector2(point.X, point.Y);
            float u = Vector2.Dot(flat, along);
            float s = Vector2.Dot(flat, Out) - Line;
            float angle = Most * Math.Min(1f, share);
            if (s <= 0f || angle <= 1e-4f)
            {
                return point;
            }

            // Round the hinge, a circle's arc as wide as the hinge, then straight on at the fold's angle.
            float radius = Hinge / angle;
            float bent = Math.Min(s, Hinge) / radius;
            float acrossS = radius * (float)Math.Sin(bent);
            float acrossZ = Side * radius * (1f - (float)Math.Cos(bent));
            if (s > Hinge)
            {
                acrossS += (s - Hinge) * (float)Math.Cos(angle);
                acrossZ += Side * (s - Hinge) * (float)Math.Sin(angle);
            }

            // What already stood off the paper stands off it the same way, along the bent paper's normal.
            float turned = s > Hinge ? angle : bent;
            acrossS += -Side * (float)Math.Sin(turned) * point.Z;
            acrossZ += (float)Math.Cos(turned) * point.Z;
            Vector2 moved = (along * u) + (Out * (Line + acrossS));
            return new Vector3(moved.X, moved.Y, acrossZ);
        }
    }
}

/// <summary>One triangle of a crumpled sheet as seen: its grid points, where they land, how far, how lit, and whether its back shows.</summary>
internal readonly struct WadTriangle
{
    public WadTriangle(int a, int b, int c, Vector2 seenA, Vector2 seenB, Vector2 seenC, float depth, float light, bool back)
    {
        A = a;
        B = b;
        C = c;
        SeenA = seenA;
        SeenB = seenB;
        SeenC = seenC;
        Depth = depth;
        Light = light;
        Back = back;
    }

    public int A { get; }

    public int B { get; }

    public int C { get; }

    public Vector2 SeenA { get; }

    public Vector2 SeenB { get; }

    public Vector2 SeenC { get; }

    /// <summary>How far away, along z; the farthest are drawn first.</summary>
    public float Depth { get; }

    /// <summary>How brightly lit, 0 to 1.</summary>
    public float Light { get; }

    public bool Back { get; }
}
