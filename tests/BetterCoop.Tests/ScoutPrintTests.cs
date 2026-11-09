using System;
using System.Linq;
using System.Xml.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class ScoutPrintTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void Every_trail_sign_is_a_drawing_of_its_own_size()
    {
        foreach (TrailSign sign in Enum.GetValues<TrailSign>())
        {
            XElement drawing = XElement.Parse(FormDrawing.Sign(sign, 40f));
            Assert.Equal("0 0 40 40", drawing.Attribute("viewBox")!.Value);
            Assert.NotEmpty(drawing.Elements());
        }
    }

    [Fact]
    public void Gone_home_is_a_ring_of_stones_round_one_stone()
    {
        XElement drawing = XElement.Parse(FormDrawing.Sign(TrailSign.GoneHome, 46f));

        Assert.Equal(10, drawing.Descendants(Svg + "path").Count());
    }

    [Fact]
    public void Sections_take_the_signs_in_turn_and_never_the_trails_end()
    {
        Assert.DoesNotContain(TrailSign.GoneHome, FormDrawing.SectionSigns);
        Assert.Equal(FormDrawing.SectionSigns.Length, FormDrawing.SectionSigns.Distinct().Count());
    }

    [Fact]
    public void A_heading_rule_is_a_heavy_line_over_a_hairline()
    {
        XElement rule = XElement.Parse(FormDrawing.HeadingRule(800f, 5f));

        Assert.Equal(2, rule.Descendants(Svg + "rect").Count());
    }

    [Fact]
    public void An_advancement_chart_rules_a_line_between_each_pair_of_columns()
    {
        XElement ruling = XElement.Parse(FormDrawing.ColumnRules(1000f, 300f, [600f, 760f, 920f, 1200f]));

        // The line past the chart's edge is left out.
        Assert.Equal(3, ruling.Descendants(Svg + "rect").Count());
    }

    [Fact]
    public void The_rope_knot_pulls_tight_and_stays_within_its_strip()
    {
        string loose = FormDrawing.Rope(380f, 30f, 0f);
        string tight = FormDrawing.Rope(380f, 30f, 1f);

        Assert.NotEqual(loose, tight);
        Assert.Equal(tight, FormDrawing.Rope(380f, 30f, 2f));
        foreach (string rope in new[] { loose, tight })
        {
            XElement drawing = XElement.Parse(rope);
            Assert.Equal("0 0 380 30", drawing.Attribute("viewBox")!.Value);
        }
    }
}
