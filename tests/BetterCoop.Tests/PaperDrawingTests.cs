using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PaperDrawingTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static XElement Draw(float width = 1000f, float height = 900f, int seed = 7) =>
        XElement.Parse(PaperDrawing.Svg(width, height, 96f, 104f, seed));

    [Fact]
    public void The_view_box_holds_the_paper_and_its_margin()
    {
        XElement svg = Draw();

        Assert.Equal("0 0 1032 932", svg.Attribute("viewBox")!.Value);
        Assert.Equal((1032f, 932f), PaperDrawing.Canvas(1000f, 900f));
    }

    [Fact]
    public void Draws_shadow_then_outline_then_paper_before_anything_on_it()
    {
        var fills = Draw().Descendants(Svg + "path").Take(3).Select(p => p.Attribute("fill")!.Value).ToList();

        Assert.Equal(["#000000", PaperDrawing.InkBlack.Hex, PaperDrawing.Paper.Hex], fills);
    }

    [Fact]
    public void A_slip_cuts_only_the_notches_under_its_heading()
    {
        XElement slip = XElement.Parse(PaperDrawing.Slip(620f, 300f, 64f));
        string edge = slip.Descendants(Svg + "path").Skip(1).First().Attribute("d")!.Value;

        Assert.Equal("0 0 652 332", slip.Attribute("viewBox")!.Value);
        Assert.Equal(6, edge.Count(c => c == 'A'));
        Assert.DoesNotContain("NaN", slip.ToString());
    }

    [Fact]
    public void A_corner_never_runs_past_a_perforation_near_it()
    {
        XElement slip = XElement.Parse(PaperDrawing.Slip(480f, 140f, 46f));
        var edges = slip.Descendants(Svg + "path")
            .Where(p => p.Attribute("stroke-width")?.Value == "1.6" || p.Attribute("fill")?.Value == PaperDrawing.InkBlack.Hex)
            .Select(p => p.Attribute("d")!.Value);

        foreach (string edge in edges)
        {
            // Down the right side: the corner's arc ends at or before the notch's starts.
            string[] parts = edge.Split('V', 'A');
            float cornerEnd = float.Parse(parts[1].Split(' ').Last(), CultureInfo.InvariantCulture);
            float notchStart = float.Parse(parts[2], CultureInfo.InvariantCulture);
            Assert.True(cornerEnd <= notchStart + 0.01f, $"{cornerEnd} > {notchStart} in {edge}");
        }
    }

    [Fact]
    public void Each_edge_cuts_four_notches_with_arcs()
    {
        string edge = Draw().Descendants(Svg + "path").Skip(1).First().Attribute("d")!.Value;

        // Four corners and four notches.
        Assert.Equal(8, edge.Count(c => c == 'A'));
        Assert.EndsWith("Z", edge);
    }

    [Fact]
    public void Both_perforations_run_across_the_paper()
    {
        int dashes = Draw().Descendants(Svg + "rect").Count();
        float span = 1000f - 2f * (PaperDrawing.Notch + 14f);

        Assert.Equal(2 * PaperDrawing.DashCount(span), dashes);
        Assert.InRange(PaperDrawing.DashCount(span), 40, 50);
    }

    [Fact]
    public void A_dashed_rule_matches_the_perforations_and_fills_its_strip()
    {
        XElement rule = XElement.Parse(PaperDrawing.DashedRule(400f, 4f));
        var dashes = rule.Descendants(Svg + "rect").ToList();

        Assert.Equal("0 0 400 4", rule.Attribute("viewBox")!.Value);
        Assert.Equal(PaperDrawing.DashCount(400f), dashes.Count);
        Assert.All(dashes, d => Assert.Equal(PaperDrawing.DashInk.Hex, d.Attribute("fill")!.Value));
        float first = float.Parse(dashes[0].Attribute("x")!.Value, CultureInfo.InvariantCulture);
        float last = float.Parse(dashes[^1].Attribute("x")!.Value, CultureInfo.InvariantCulture)
            + float.Parse(dashes[^1].Attribute("width")!.Value, CultureInfo.InvariantCulture);
        Assert.Equal(400f - last, first, 2);
    }

    [Fact]
    public void A_strip_too_short_for_a_dash_stays_empty() =>
        Assert.Empty(XElement.Parse(PaperDrawing.DashedRule(5f, 4f)).Descendants(Svg + "rect"));

    [Fact]
    public void Only_the_shadow_lets_anything_show_through()
    {
        var translucent = Draw().Descendants()
            .Where(e => e.Attribute("fill-opacity") != null || e.Attribute("opacity") != null || e.Attribute("stroke-opacity") != null)
            .ToList();

        XElement shadow = Assert.Single(translucent);
        Assert.Equal(PaperDrawing.ShadowOpacity, float.Parse(shadow.Attribute("fill-opacity")!.Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Numbers_are_written_the_same_in_every_culture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.DoesNotContain(",5", PaperDrawing.Svg(1000.5f, 900f, 96f, 104f));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void The_same_seed_draws_the_same_paper() =>
        Assert.Equal(PaperDrawing.Svg(800f, 600f, 96f, 104f, 3), PaperDrawing.Svg(800f, 600f, 96f, 104f, 3));
}
