using System.Linq;
using System.Xml.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class FormDrawingTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void Every_mark_is_drawn_for_its_own_size()
    {
        string[] drawings =
        [
            FormDrawing.CheckBox(46f, true),
            FormDrawing.Ring(120f, 40f),
            FormDrawing.Leader(300f, 10f),
            FormDrawing.Line(300f, 4f),
            FormDrawing.Arrow(28f, 40f, true),
            FormDrawing.Stamp(150f, 56f),
            FormDrawing.PhotoFrame(90f, 110f),
            FormDrawing.Card(200f, 140f, lifted: false),
            FormDrawing.Slot(200f, 140f, marked: true),
            FormDrawing.Silhouette(90f, 110f, new Ink(0.9f, 0.7f, 0.5f)),
        ];

        foreach (string drawing in drawings)
        {
            XElement svg = XElement.Parse(drawing);
            string[] box = svg.Attribute("viewBox")!.Value.Split(' ');
            Assert.Equal(svg.Attribute("width")!.Value, box[2]);
            Assert.Equal(svg.Attribute("height")!.Value, box[3]);
        }
    }

    [Fact]
    public void A_check_box_gets_a_pen_tick_only_when_checked()
    {
        XElement empty = XElement.Parse(FormDrawing.CheckBox(46f, false));
        XElement ticked = XElement.Parse(FormDrawing.CheckBox(46f, true));

        Assert.Empty(empty.Descendants(Svg + "path"));
        Assert.Equal(FormDrawing.Pen.Hex, ticked.Descendants(Svg + "path").Single().Attribute("stroke")!.Value);
    }

    [Fact]
    public void A_ring_stays_inside_its_box()
    {
        string d = XElement.Parse(FormDrawing.Ring(120f, 40f)).Descendants(Svg + "path").Single().Attribute("d")!.Value;
        var points = d.Split('M', 'L').Where(p => p.Length > 0)
            .Select(p => p.Split(' ').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
            .ToList();

        Assert.All(points, p =>
        {
            Assert.InRange(p[0], 0f, 120f);
            Assert.InRange(p[1], 0f, 40f);
        });
    }

    [Fact]
    public void Leader_dots_end_at_the_right_edge()
    {
        var dots = XElement.Parse(FormDrawing.Leader(300f, 10f)).Descendants(Svg + "circle").ToList();
        float last = float.Parse(dots[^1].Attribute("cx")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        float radius = float.Parse(dots[^1].Attribute("r")!.Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(dots.Count > 30);
        Assert.Equal(300f - radius, last, 3);
        Assert.Empty(XElement.Parse(FormDrawing.Leader(1f, 10f)).Descendants(Svg + "circle"));
    }

    [Fact]
    public void A_stamp_is_inked_in_stamp_red_and_worn_the_same_way_each_time()
    {
        string stamp = FormDrawing.Stamp(150f, 56f, seed: 3);

        Assert.Equal(stamp, FormDrawing.Stamp(150f, 56f, seed: 3));
        Assert.All(
            XElement.Parse(stamp).Descendants(Svg + "rect"),
            r => Assert.Equal(FormDrawing.StampRed.Hex, r.Attribute("stroke")!.Value));
    }

    [Fact]
    public void Arrows_point_their_own_way()
    {
        static float Tip(bool back)
        {
            string d = XElement.Parse(FormDrawing.Arrow(28f, 40f, back)).Descendants(Svg + "path").Single().Attribute("d")!.Value;
            string middle = d.Split('L')[1];
            return float.Parse(middle.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.True(Tip(back: true) < 14f);
        Assert.True(Tip(back: false) > 14f);
    }
}

public class StepsTests
{
    private static readonly int[] Choices = [1, 2, 5, 10];

    [Theory]
    [InlineData(2, 1, 5)]
    [InlineData(2, -1, 1)]
    [InlineData(10, 1, 1)]
    [InlineData(1, -1, 10)]
    public void Steps_round_both_ways(int current, int by, int expected) =>
        Assert.Equal(expected, Steps.Next(Choices, current, by));

    [Fact]
    public void An_answer_not_offered_steps_to_an_end()
    {
        Assert.Equal(1, Steps.Next(Choices, -1, 1));
        Assert.Equal(10, Steps.Next(Choices, -1, -1));
    }

    [Fact]
    public void Nothing_to_choose_keeps_the_answer() =>
        Assert.Equal(7, Steps.Next([], 7, 1));
}
