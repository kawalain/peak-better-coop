using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PaperCrumpleTests
{
    private static readonly PaperCrumple Sheet = new(1200f, 900f, 13, 10, seed: 4);

    private static Vector3[] Shaped(float progress)
    {
        var points = new Vector3[Sheet.Count];
        Sheet.Shape(progress, points);
        return points;
    }

    [Fact]
    public void Uncrumpled_it_lies_flat_where_it_was()
    {
        Vector3[] points = Shaped(0f);

        Assert.Equal(Sheet.Rest(0, 0), points[0]);
        Assert.Equal(Sheet.Rest(12, 9), points[^1]);
        Assert.All(points, p => Assert.Equal(0f, p.Z));
    }

    [Fact]
    public void Its_edges_fold_over_across_the_sheet()
    {
        // Halfway, before it is pressed, the edge strips lie over the sheet, turned past upright.
        Vector3[] points = Shaped(0.5f);
        Vector3 corner = points[0];

        Assert.True(corner.X > -600f * 0.75f || corner.Y > -450f * 0.75f, $"{corner}");
        Assert.Contains(points, p => System.Math.Abs(p.Z) > 100f);
    }

    [Fact]
    public void Crumpled_it_is_pressed_into_a_small_wad()
    {
        Vector3[] points = Shaped(1f);
        float width = points.Max(p => p.X) - points.Min(p => p.X);
        float height = points.Max(p => p.Y) - points.Min(p => p.Y);

        Assert.True(width * height < 1200f * 900f * 0.3f, $"{width} x {height}");
        Assert.All(points, p => Assert.InRange(p.Z, -900f, 900f));
    }

    [Fact]
    public void The_same_seed_crumples_the_same_way()
    {
        var again = new PaperCrumple(1200f, 900f, 13, 10, seed: 4);
        var other = new PaperCrumple(1200f, 900f, 13, 10, seed: 5);
        var a = new Vector3[Sheet.Count];
        var b = new Vector3[Sheet.Count];
        var c = new Vector3[Sheet.Count];
        Sheet.Shape(0.6f, a);
        again.Shape(0.6f, b);
        other.Shape(0.6f, c);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Triangles_come_far_to_near_and_a_flat_sheet_shows_its_face_lit()
    {
        var triangles = new List<WadTriangle>();

        Sheet.Triangles(Shaped(0f), Quaternion.Identity, Vector3.Zero, 1800f, new Vector3(0f, 0f, -1f), triangles);
        Assert.Equal(12 * 9 * 2, triangles.Count);
        Assert.All(triangles, t => Assert.False(t.Back));
        Assert.All(triangles, t => Assert.Equal(1f, t.Light, 3));

        Sheet.Triangles(Shaped(0.8f), Quaternion.CreateFromYawPitchRoll(0.4f, 0.3f, 0.1f), Vector3.Zero, 1800f, new Vector3(-0.4f, 0.6f, -1f), triangles);
        float[] depths = triangles.Select(t => t.Depth).ToArray();
        Assert.Equal(depths.OrderByDescending(d => d), depths);
    }

    [Fact]
    public void Farther_away_looks_smaller() =>
        Assert.True(PaperCrumple.Project(new Vector3(100f, 0f, 600f), 1800f).X < PaperCrumple.Project(new Vector3(100f, 0f, 0f), 1800f).X);
}
