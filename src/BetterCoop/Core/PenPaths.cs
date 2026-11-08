using System;
using System.Collections.Generic;

namespace BetterCoop.Core;

/// <summary>
/// The paths a pen takes for the marks written on a form: a tick in a check
/// box and a ring round a chosen answer. Each is a polyline in a box of the
/// given size, y down, in the order the pen moves. No two are drawn quite
/// the same: every control point wanders a little with the hand, by an
/// amount that follows the mark's size.
/// </summary>
internal static class PenPaths
{
    private const int CurveSteps = 14;

    private const int RingSteps = 64;

    /// <summary>A tick for a check box of <paramref name="size"/>, drawn where the printed box is, running past its top right.</summary>
    public static List<(float X, float Y)> Tick(float size, Random hand)
    {
        float box = CheckBox(size, out float x, out float y);
        (float, float) P(float u, float v) =>
            (x + box * (u + Wobble(hand, 0.05f)), y + box * (v + Wobble(hand, 0.05f)));

        var points = new List<(float X, float Y)>();
        (float, float) start = P(0.12f, 0.52f);
        (float, float) bottom = P(0.40f, 0.92f);
        Curve(points, start, P(0.25f, 0.62f), P(0.33f, 0.78f), bottom, first: true);
        Curve(points, bottom, P(0.62f, 0.45f), P(0.85f, 0.05f), P(1.12f, -0.22f), first: false);
        return points;
    }

    /// <summary>
    /// A ring round an answer in a box of <paramref name="width"/> by
    /// <paramref name="height"/>: once round and a little more, slightly
    /// tilted and growing, so the end overshoots outside the start.
    /// </summary>
    public static List<(float X, float Y)> Ring(float width, float height, float stroke, Random hand)
    {
        float rx = width / 2f - stroke;
        float ry = height / 2f - stroke;
        float cx = width / 2f;
        float cy = height / 2f;
        double start = Math.PI * (1.05 + 0.2 * hand.NextDouble());
        double sweep = Math.PI * (2.1 + 0.2 * hand.NextDouble());
        float from = 0.86f + 0.06f * (float)hand.NextDouble();
        double tilt = (hand.NextDouble() - 0.5) * 0.12;
        double phase = hand.NextDouble() * Math.PI * 2;
        float wave = 0.025f + 0.02f * (float)hand.NextDouble();
        var points = new List<(float X, float Y)>(RingSteps + 1);
        for (int i = 0; i <= RingSteps; i++)
        {
            float t = i / (float)RingSteps;
            double angle = start + sweep * t;
            float grow = (from + (1f - from) * t) * (1f + wave * (float)Math.Sin(3 * angle + phase));
            double px = Math.Cos(angle) * rx * grow;
            double py = -Math.Sin(angle) * ry * grow;
            points.Add((
                cx + (float)(px * Math.Cos(tilt) - py * Math.Sin(tilt)),
                cy + (float)(px * Math.Sin(tilt) + py * Math.Cos(tilt))));
        }

        return points;
    }

    /// <summary>The length along a path, to draw or erase part of it at an even speed.</summary>
    public static float Length(IReadOnlyList<(float X, float Y)> points)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            length += Distance(points[i - 1], points[i]);
        }

        return length;
    }

    public static float Distance((float X, float Y) a, (float X, float Y) b) =>
        (float)Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    /// <summary>The printed box of a check box of <paramref name="size"/>, as <see cref="FormDrawing.CheckBox"/> draws it.</summary>
    public static float CheckBox(float size, out float x, out float y)
    {
        float box = size * 0.62f;
        x = (size - box) / 2f;
        y = size - box - size * 0.08f;
        return box;
    }

    private static void Curve(List<(float X, float Y)> points, (float X, float Y) a, (float X, float Y) b, (float X, float Y) c, (float X, float Y) d, bool first)
    {
        for (int i = first ? 0 : 1; i <= CurveSteps; i++)
        {
            float t = i / (float)CurveSteps;
            float u = 1f - t;
            float w0 = u * u * u;
            float w1 = 3f * u * u * t;
            float w2 = 3f * u * t * t;
            float w3 = t * t * t;
            points.Add((
                (w0 * a.X) + (w1 * b.X) + (w2 * c.X) + (w3 * d.X),
                (w0 * a.Y) + (w1 * b.Y) + (w2 * c.Y) + (w3 * d.Y)));
        }
    }

    private static float Wobble(Random hand, float amount) => ((float)hand.NextDouble() * 2f - 1f) * amount;
}
