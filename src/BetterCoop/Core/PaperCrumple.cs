using System;
using System.Collections.Generic;
using System.Numerics;

namespace BetterCoop.Core;

/// <summary>
/// A sheet of paper crumpled into a ball, as a grid of points in 3D: x to
/// the right, y up, z away from the viewer, centred on the sheet's middle.
/// Creases come first, a few ridges running across the sheet at random
/// angles, then the sheet closes round itself into a lumpy ball, its middle
/// facing the viewer and its edges folded round the back. The same seed
/// crumples the same way.
/// </summary>
internal sealed class PaperCrumple
{
    /// <summary>How much of the crumple is spent creasing before the sheet closes up.</summary>
    private const float Creasing = 0.35f;

    private readonly Crease[] creases;

    private readonly float amplitudes;

    public PaperCrumple(float width, float height, int columns, int rows, int seed)
    {
        Width = width;
        Height = height;
        Columns = columns;
        Rows = rows;
        float span = Math.Min(width, height);
        Ball = span * 0.2f;
        var hand = new Random(seed);
        creases = new Crease[6];
        for (int i = 0; i < creases.Length; i++)
        {
            double angle = hand.NextDouble() * Math.PI;
            creases[i] = new Crease(
                new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)),
                span * Next(hand, 0.16f, 0.34f),
                Next(hand, 0f, 1f),
                span * Next(hand, 0.03f, 0.07f) * (hand.Next(2) == 0 ? -1f : 1f));
            amplitudes += Math.Abs(creases[i].Amplitude);
        }
    }

    public float Width { get; }

    public float Height { get; }

    /// <summary>Grid points across, one more than the cells.</summary>
    public int Columns { get; }

    /// <summary>Grid points down, one more than the cells.</summary>
    public int Rows { get; }

    /// <summary>The finished ball's radius.</summary>
    public float Ball { get; }

    public int Count => Columns * Rows;

    /// <summary>Where grid point (<paramref name="column"/>, <paramref name="row"/>) lies on the flat sheet; row 0 is the bottom.</summary>
    public Vector3 Rest(int column, int row) => new(
        ((column / (float)(Columns - 1)) - 0.5f) * Width,
        ((row / (float)(Rows - 1)) - 0.5f) * Height,
        0f);

    /// <summary>Fills <paramref name="into"/> with every grid point, row by row, <paramref name="progress"/> (0 to 1) of the way to a ball.</summary>
    public void Shape(float progress, Vector3[] into)
    {
        progress = Math.Max(0f, Math.Min(1f, progress));
        float folds = Math.Min(1f, progress / Creasing);
        float closing = Easing.InOut((progress - (Creasing * 0.4f)) / (1f - (Creasing * 0.4f)));
        float reach = (float)Math.Sqrt((Width * Width) + (Height * Height)) / 2f;
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                Vector3 rest = Rest(column, row);
                float ridge = Ridge(new Vector2(rest.X, rest.Y));

                // Creased, and drawn in a little as the creases take up the paper.
                var creased = new Vector3(rest.X * (1f - (0.12f * folds)), rest.Y * (1f - (0.12f * folds)), ridge * folds);

                // On the ball: the middle at the front, the edges wrapped round the back, lumpy where it creased.
                float along = (float)Math.Sqrt((rest.X * rest.X) + (rest.Y * rest.Y)) / reach;
                float round = along * (float)Math.PI * 0.92f;
                float around = (float)Math.Atan2(rest.Y, rest.X);
                float radius = Ball * (1f + (0.7f * ridge / Math.Max(1e-4f, amplitudes)));
                var ball = new Vector3(
                    (float)(Math.Sin(round) * Math.Cos(around)) * radius,
                    (float)(Math.Sin(round) * Math.Sin(around)) * radius,
                    -(float)Math.Cos(round) * radius);

                into[(row * Columns) + column] = Vector3.Lerp(creased, ball, closing);
            }
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

    private float Ridge(Vector2 point)
    {
        float sum = 0f;
        foreach (Crease crease in creases)
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
