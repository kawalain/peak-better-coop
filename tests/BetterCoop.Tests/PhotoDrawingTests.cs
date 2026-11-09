using System.Linq;
using System.Xml.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class PhotoDrawingTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void A_print_is_white_paper_over_its_shadow()
    {
        XElement print = XElement.Parse(FormDrawing.Print(124f, 140f));

        Assert.Equal("0 0 124 140", print.Attribute("viewBox")!.Value);
        Assert.Equal(2, print.Descendants(Svg + "path").Count());
        Assert.Equal(FormDrawing.PrintWhite.Hex, print.Descendants(Svg + "path").Last().Attribute("fill")!.Value);
    }

    [Fact]
    public void An_album_holds_a_print_by_four_corners_each_with_its_fold()
    {
        XElement corners = XElement.Parse(FormDrawing.PhotoCorners(132f, 148f));

        Assert.Equal(8, corners.Descendants(Svg + "path").Count());
    }

    [Fact]
    public void A_paper_clip_is_one_wire_drawn_with_its_shine()
    {
        XElement clip = XElement.Parse(FormDrawing.PaperClip(15f, 42f));

        Assert.Equal("0 0 15 42", clip.Attribute("viewBox")!.Value);
        string[] wires = clip.Descendants(Svg + "path").Select(p => p.Attribute("d")!.Value).ToArray();
        Assert.Equal(2, wires.Length);
        Assert.Equal(wires[0], wires[1]);
    }
}
