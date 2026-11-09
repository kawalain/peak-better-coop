using System.Linq;
using System.Xml.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class ScoutPrintTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

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
}
