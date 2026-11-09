using System.Linq;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class CarryRecordTests
{
    private static PairSample Sample(string carrier, string rider, double height, bool alive = true) =>
        new(carrier, carrier.ToUpperInvariant(), rider, rider.ToUpperInvariant(), alive, height);

    [Fact]
    public void Time_counts_for_the_Carrier_and_the_Rider()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0)], 2, 0, "SHORE");
        record.Count([Sample("a", "b", 0)], 3, 0, "SHORE");

        Assert.Equal(5, record.Find("a")!.Carried);
        Assert.Equal(0, record.Find("a")!.Ridden);
        Assert.Equal(5, record.Find("b")!.Ridden);
    }

    [Fact]
    public void Only_climbing_counts_as_height_not_falling_or_warping()
    {
        var record = new CarryRecord();
        foreach (double height in new[] { 0, 2, 5, 1, 4, 100, 101 })
        {
            record.Count([Sample("a", "b", height)], 0.5, 0, "SHORE");
        }

        // 0→2→5 climbs 5, the fall to 1 counts nothing, 1→4 climbs 3, the warp to 100 is skipped, 100→101 climbs 1.
        Assert.Equal(9, record.Find("a")!.Climbed, 3);
        Assert.Equal(0, record.Find("b")!.Climbed);
    }

    [Fact]
    public void A_dead_Shared_Body_counts_nothing_and_climbs_on_from_where_it_comes_back()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0)], 1, 0, "SHORE");
        record.Count([Sample("a", "b", 0, alive: false)], 60, 0, "SHORE");
        record.Count([Sample("a", "b", 50)], 1, 0, "SHORE");
        record.Count([Sample("a", "b", 52)], 1, 0, "SHORE");

        Assert.Equal(3, record.Find("a")!.Carried);
        Assert.Equal(2, record.Find("a")!.Climbed, 3);
    }

    [Fact]
    public void Each_segment_keeps_how_long_each_Pair_climbed_it()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0), Sample("c", "d", 0)], 10, 0, "SHORE");
        record.Count([Sample("b", "a", 0)], 4, 1, "TROPICS");

        Assert.Equal(3, record.Stretches.Count);
        Assert.Contains(record.Stretches, s => s.Segment == 1 && s.Carrier == "b" && s.Rider == "a" && s.Seconds == 4);
    }

    [Fact]
    public void Merit_Badges_go_to_the_best_in_each_thing()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0), Sample("c", "d", 0)], 10, 0, "SHORE");
        record.Count([Sample("a", "b", 30), Sample("c", "d", 1)], 10, 0, "SHORE");
        record.Count([Sample("b", "a", 0), Sample("c", "d", 2)], 5, 1, "TROPICS");

        var badges = record.Badges().ToDictionary(b => b.Kind);

        Assert.Equal(["c"], badges[MeritBadgeKind.SteadyBack].Ids);
        Assert.Equal(["d"], badges[MeritBadgeKind.ComfyPassenger].Ids);
        Assert.Equal(["c"], badges[MeritBadgeKind.ClimbingKing].Ids);
        Assert.Equal(25, badges[MeritBadgeKind.BestBuddies].Amount);
    }

    [Fact]
    public void The_table_ranks_the_most_time_carrying_first_then_carried_then_name()
    {
        var record = new CarryRecord();
        record.Count([Sample("b", "a", 0), Sample("c", "d", 0)], 10, 0, "SHORE");
        record.Count([Sample("c", "d", 0)], 5, 0, "SHORE");
        record.Count([Sample("e", "f", 0)], 0, 0, "SHORE");

        Assert.Equal(["c", "b", "d", "a", "e", "f"], record.Ranked().Select(s => s.Id));
    }

    [Fact]
    public void Segments_come_in_order_with_the_longest_Pair_first()
    {
        var record = new CarryRecord();
        record.Count([Sample("c", "d", 0)], 5, 1, "TROPICS");
        record.Count([Sample("a", "b", 0), Sample("c", "d", 0)], 2, 0, "SHORE");
        record.Count([Sample("c", "d", 0)], 4, 0, "SHORE");

        var segments = record.Segments();

        Assert.Equal(["SHORE", "TROPICS"], segments.Select(s => s.Title));
        Assert.Equal(["c", "a"], segments[0].Pairs.Select(p => p.Carrier));
    }

    [Fact]
    public void Nobody_earns_a_badge_for_nothing()
    {
        Assert.Empty(new CarryRecord().Badges());
    }

    [Fact]
    public void A_record_travels_whole()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0), Sample("c", "d", 0)], 10, 0, "SHORE");
        record.Count([Sample("a", "b", 3), Sample("c", "d", 0)], 10, 2, "ALPINE");

        CarryRecord copy = CarryRecord.Decode(record.Encode())!;

        Assert.Equal(record.Scouts.Select(s => (s.Id, s.Name, s.Carried, s.Ridden, s.Climbed)), copy.Scouts.Select(s => (s.Id, s.Name, s.Carried, s.Ridden, s.Climbed)));
        Assert.Equal(record.Stretches.Select(s => (s.Segment, s.Title, s.Carrier, s.Rider, s.Seconds)), copy.Stretches.Select(s => (s.Segment, s.Title, s.Carrier, s.Rider, s.Seconds)));
    }

    [Fact]
    public void A_broken_record_is_refused()
    {
        object[] parts = new CarryRecord().Encode();
        parts[7] = new[] { 5 };

        Assert.Null(CarryRecord.Decode(parts));
        Assert.Null(CarryRecord.Decode("nothing"));
    }

    [Fact]
    public void A_Run_Report_travels_whole()
    {
        var record = new CarryRecord();
        record.Count([Sample("a", "b", 0)], 10, 0, "SHORE");

        RunReport copy = RunReport.Decode(new RunReport(RunEnding.Lost, 1234, record).Encode())!;

        Assert.Equal(RunEnding.Lost, copy.Ending);
        Assert.Equal(1234, copy.Seconds);
        Assert.Equal(10, copy.Record.Find("a")!.Carried);
    }
}
