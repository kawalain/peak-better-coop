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
            FormDrawing.CheckBox(46f),
            FormDrawing.Line(300f, 4f),
            FormDrawing.Arrow(28f, 40f, true),
            FormDrawing.Stamp(150f, 56f),
            FormDrawing.PhotoEdge(116f, 145f),
            FormDrawing.Postmark(220f, 104f, 104f),
            FormDrawing.Doodle(132f, 112f, "Alder"),
            FormDrawing.Anemone(64f, 56f),
            FormDrawing.Card(200f, 140f, lifted: false),
            FormDrawing.Slot(200f, 140f, marked: true),
            FormDrawing.Silhouette(90f, 110f, new Ink(0.9f, 0.7f, 0.5f)),
            FormDrawing.Silhouette(124f, 155f, new Ink(0.6f, 0.6f, 0.6f), mystery: true),
            FormDrawing.TagHole(30f),
            FormDrawing.Hand(160f, 320f, new Ink(0.9f, 0.7f, 0.5f)),
            FormDrawing.Airmail(300f, 10f),
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
    public void A_check_box_is_only_printed_its_tick_is_written_by_hand() =>
        Assert.Empty(XElement.Parse(FormDrawing.CheckBox(46f)).Descendants(Svg + "path"));

    [Fact]
    public void A_postmark_inks_a_double_ring_and_five_cancelling_lines_in_postmark_black()
    {
        XElement postmark = XElement.Parse(FormDrawing.Postmark(220f, 104f, 104f));

        Assert.Equal(2, postmark.Descendants(Svg + "circle").Count());
        Assert.Equal(5, postmark.Descendants(Svg + "path").Count());
        Assert.All(postmark.Descendants(Svg + "path"), p => Assert.Equal(FormDrawing.PostmarkInk.Hex, p.Attribute("stroke")!.Value));
    }

    [Fact]
    public void Each_signer_gets_the_same_doodle_every_time()
    {
        Assert.Equal(FormDrawing.Doodle(132f, 112f, "Alder"), FormDrawing.Doodle(132f, 112f, "Alder"));
        Assert.Equal(FormDrawing.Anemone(64f, 56f), FormDrawing.Anemone(64f, 56f));
        Assert.Equal(FormDrawing.Pony(80f, 62f), FormDrawing.Pony(80f, 62f));
        Assert.Equal(FormDrawing.Pony(80f, 62f, frame: 2), FormDrawing.Pony(80f, 62f, frame: 2));
        Assert.NotEqual(FormDrawing.Pony(80f, 62f, frame: 0), FormDrawing.Pony(80f, 62f, frame: 1));
        Assert.NotEqual(FormDrawing.Doodle(132f, 112f, "Alder", frame: 0), FormDrawing.Doodle(132f, 112f, "Alder", frame: 1));
        Assert.Equal(FormDrawing.DoodleFor("Alder"), FormDrawing.DoodleFor("Alder"));
        Assert.Equal(3, new[] { "Alder", "Birch", "Cedar", "Dahlia", "Elm", "Fern", "Gorse" }.Select(FormDrawing.DoodleFor).Distinct().Count());
    }

    [Fact]
    public void A_stamp_is_inked_in_stamp_red_and_worn_the_same_way_each_time()
    {
        string stamp = FormDrawing.Stamp(150f, 56f, seed: 3);

        Assert.Equal(stamp, FormDrawing.Stamp(150f, 56f, seed: 3));
        Assert.All(
            XElement.Parse(stamp).Descendants(Svg + "rect").Where(r => r.Attribute("stroke") != null),
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
