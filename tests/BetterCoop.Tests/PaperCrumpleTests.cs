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
    public void Crumpled_it_is_a_lumpy_ball()
    {
        Vector3[] points = Shaped(1f);

        Assert.All(points, p => Assert.InRange(p.Length(), Sheet.Ball * 0.4f, Sheet.Ball * 1.6f));
        Assert.Contains(points, p => p.Z > 0f);
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
