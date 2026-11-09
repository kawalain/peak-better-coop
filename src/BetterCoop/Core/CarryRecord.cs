using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// The Carry Record of a Run: for each player, by user id, the time spent as
/// Carrier and as Rider, the height climbed while carrying, and the time spent
/// carrying a Rescued (ADR 0010); and for each
/// segment, how long each Pair climbed it. The Troop Leader counts it from
/// samples of the Pairs and shares it as a room property.
/// </summary>
internal sealed class CarryRecord
{
    /// <summary>A climb larger than this between two samples is a warp or a revive, not climbing.</summary>
    public const double MaxClimbPerSample = 10;

    private readonly List<Scout> scouts = new();

    private readonly List<Stretch> stretches = new();

    /// <summary>Each Pair's height at its last sample, by Carrier and Rider; not shared.</summary>
    private readonly Dictionary<(string Carrier, string Rider), double> heights = new();

    public IReadOnlyList<Scout> Scouts => scouts;

    public IReadOnlyList<Stretch> Stretches => stretches;

    public bool IsEmpty => scouts.Count == 0;

    /// <summary>
    /// Counts <paramref name="seconds"/> of the Pairs as sampled now in
    /// <paramref name="segment"/>, named <paramref name="title"/>, and of the
    /// players carrying a Rescued now. A Pair whose Shared Body is dead counts
    /// nothing; a Pair's height counts only upward, and not while its Shared
    /// Body is passed out, being carried rather than climbing.
    /// </summary>
    public void Count(IEnumerable<PairSample> pairs, double seconds, int segment, string title) =>
        Count(pairs, [], seconds, segment, title);

    /// <inheritdoc cref="Count(IEnumerable{PairSample}, double, int, string)"/>
    public void Count(IEnumerable<PairSample> pairs, IEnumerable<(string Id, string Name)> rescuers, double seconds, int segment, string title)
    {
        foreach ((string id, string name) in rescuers.Distinct())
        {
            ScoutOf(id, name).Rescuing += seconds;
        }

        var sampled = new HashSet<(string, string)>();
        foreach (PairSample pair in pairs)
        {
            var key = (pair.CarrierId, pair.RiderId);
            Scout carrier = ScoutOf(pair.CarrierId, pair.CarrierName);
            Scout rider = ScoutOf(pair.RiderId, pair.RiderName);
            if (!pair.Alive)
            {
                heights.Remove(key);
                continue;
            }

            sampled.Add(key);
            carrier.Carried += seconds;
            rider.Ridden += seconds;
            if (heights.TryGetValue(key, out double last))
            {
                double climb = pair.Height - last;
                if (climb > 0 && climb <= MaxClimbPerSample && !pair.PassedOut)
                {
                    carrier.Climbed += climb;
                }
            }

            heights[key] = pair.Height;
            StretchOf(segment, title, pair.CarrierId, pair.RiderId).Seconds += seconds;
        }

        foreach ((string, string) gone in heights.Keys.Where(k => !sampled.Contains(k)).ToList())
        {
            heights.Remove(gone);
        }
    }

    /// <summary>Keeps a player's latest name.</summary>
    public void Name(string id, string name) => ScoutOf(id, name);

    private Scout ScoutOf(string id, string name)
    {
        Scout? scout = scouts.FirstOrDefault(s => s.Id == id);
        if (scout == null)
        {
            scout = new Scout(id, name);
            scouts.Add(scout);
        }
        else if (name.Length > 0)
        {
            scout.Name = name;
        }

        return scout;
    }

    private Stretch StretchOf(int segment, string title, string carrier, string rider)
    {
        Stretch? stretch = stretches.FirstOrDefault(s => s.Segment == segment && s.Carrier == carrier && s.Rider == rider);
        if (stretch == null)
        {
            stretch = new Stretch(segment, title, carrier, rider);
            stretches.Add(stretch);
        }

        return stretch;
    }

    /// <summary>The player of a user id, or null.</summary>
    public Scout? Find(string id) => scouts.FirstOrDefault(s => s.Id == id);

    /// <summary>Every player, the most time carrying first, then the most time carried, then by name.</summary>
    public IReadOnlyList<Scout> Ranked() =>
        scouts
            .OrderByDescending(s => s.Carried)
            .ThenByDescending(s => s.Ridden)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>The segments climbed, in order, each with its Pairs, the longest together first.</summary>
    public IReadOnlyList<(int Segment, string Title, IReadOnlyList<Stretch> Pairs)> Segments() =>
        stretches
            .GroupBy(s => s.Segment)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.First().Title, (IReadOnlyList<Stretch>)g.OrderByDescending(s => s.Seconds).ToList()))
            .ToList();

    /// <summary>The Merit Badges the record earns: each goes to the best in one thing, if anyone did it at all.</summary>
    public IReadOnlyList<MeritBadge> Badges()
    {
        var badges = new List<MeritBadge>();
        void Best(MeritBadgeKind kind, Func<Scout, double> score, double least)
        {
            Scout? best = scouts.OrderByDescending(score).FirstOrDefault();
            if (best != null && score(best) >= least)
            {
                badges.Add(new MeritBadge(kind, [best.Id], score(best)));
            }
        }

        Best(MeritBadgeKind.SteadyBack, s => s.Carried, 1);
        Best(MeritBadgeKind.ComfyPassenger, s => s.Ridden, 1);
        Best(MeritBadgeKind.RescueRanger, s => s.Rescuing, 1);

        var together = stretches
            .GroupBy(s => string.CompareOrdinal(s.Carrier, s.Rider) < 0 ? (s.Carrier, s.Rider) : (s.Rider, s.Carrier))
            .Select(g => (Ids: g.Key, Seconds: g.Sum(s => s.Seconds)))
            .OrderByDescending(g => g.Seconds)
            .FirstOrDefault();
        if (together.Seconds >= 1)
        {
            badges.Add(new MeritBadge(MeritBadgeKind.BestBuddies, [together.Ids.Item1, together.Ids.Item2], together.Seconds));
        }

        return badges;
    }

    public object[] Encode()
    {
        int IndexOf(string id) => scouts.FindIndex(s => s.Id == id);
        return
        [
            scouts.Select(s => s.Id).ToArray(),
            scouts.Select(s => s.Name).ToArray(),
            scouts.Select(s => s.Carried).ToArray(),
            scouts.Select(s => s.Ridden).ToArray(),
            scouts.Select(s => s.Climbed).ToArray(),
            scouts.Select(s => s.Rescuing).ToArray(),
            stretches.Select(s => s.Segment).ToArray(),
            stretches.Select(s => s.Title).ToArray(),
            stretches.Select(s => IndexOf(s.Carrier)).ToArray(),
            stretches.Select(s => IndexOf(s.Rider)).ToArray(),
            stretches.Select(s => s.Seconds).ToArray(),
        ];
    }

    public static CarryRecord? Decode(object? value)
    {
        if (value is not object[] { Length: 11 } parts
            || parts[0] is not string[] ids
            || parts[1] is not string[] names
            || parts[2] is not double[] carried
            || parts[3] is not double[] ridden
            || parts[4] is not double[] climbed
            || parts[5] is not double[] rescuing
            || parts[6] is not int[] segments
            || parts[7] is not string[] titles
            || parts[8] is not int[] carriers
            || parts[9] is not int[] riders
            || parts[10] is not double[] seconds
            || names.Length != ids.Length
            || carried.Length != ids.Length
            || ridden.Length != ids.Length
            || climbed.Length != ids.Length
            || rescuing.Length != ids.Length
            || titles.Length != segments.Length
            || carriers.Length != segments.Length
            || riders.Length != segments.Length
            || seconds.Length != segments.Length
            || ids.Length > WireLimits.MaxScouts
            || segments.Length > WireLimits.MaxStretches)
        {
            return null;
        }

        var record = new CarryRecord();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Length; i++)
        {
            if (!WireLimits.IsId(ids[i]) || !seen.Add(ids[i]))
            {
                return null;
            }

            record.scouts.Add(new Scout(ids[i], WireLimits.Name(names[i], ids[i]))
            {
                Carried = Amount(carried[i]),
                Ridden = Amount(ridden[i]),
                Climbed = Amount(climbed[i]),
                Rescuing = Amount(rescuing[i]),
            });
        }

        for (int i = 0; i < segments.Length; i++)
        {
            if (carriers[i] < 0 || carriers[i] >= ids.Length || riders[i] < 0 || riders[i] >= ids.Length || carriers[i] == riders[i]
                || segments[i] is < 0 or > 255
                || !WireLimits.IsId(titles[i]))
            {
                return null;
            }

            record.stretches.Add(new Stretch(segments[i], titles[i], ids[carriers[i]], ids[riders[i]]) { Seconds = Amount(seconds[i]) });
        }

        return record;
    }

    /// <summary>A received amount, as no count runs negative or past a day.</summary>
    private static double Amount(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(value, 86400));

    /// <summary>One player's part of the record.</summary>
    internal sealed class Scout
    {
        public Scout(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }

        public string Name { get; set; }

        /// <summary>Seconds as Carrier.</summary>
        public double Carried { get; set; }

        /// <summary>Seconds as Rider.</summary>
        public double Ridden { get; set; }

        /// <summary>Metres climbed as Carrier.</summary>
        public double Climbed { get; set; }

        /// <summary>Seconds carrying a Rescued, alone or as either member of the rescuing Pair.</summary>
        public double Rescuing { get; set; }
    }

    /// <summary>How long one Pair climbed in one segment.</summary>
    internal sealed class Stretch
    {
        public Stretch(int segment, string title, string carrier, string rider)
        {
            Segment = segment;
            Title = title;
            Carrier = carrier;
            Rider = rider;
        }

        public int Segment { get; }

        /// <summary>The game's text key for the segment's name.</summary>
        public string Title { get; }

        public string Carrier { get; }

        public string Rider { get; }

        public double Seconds { get; set; }
    }
}

/// <summary>A Pair as the Troop Leader samples it for the Carry Record.</summary>
internal readonly struct PairSample
{
    public PairSample(string carrierId, string carrierName, string riderId, string riderName, bool alive, double height, bool passedOut = false)
    {
        PassedOut = passedOut;
        CarrierId = carrierId;
        CarrierName = carrierName;
        RiderId = riderId;
        RiderName = riderName;
        Alive = alive;
        Height = height;
    }

    public string CarrierId { get; }

    public string CarrierName { get; }

    public string RiderId { get; }

    public string RiderName { get; }

    /// <summary>Whether the Shared Body is alive.</summary>
    public bool Alive { get; }

    /// <summary>The Shared Body's height, in metres.</summary>
    public double Height { get; }

    /// <summary>Whether the Shared Body is passed out, so any height it gains is a Rescue Carry's, not its own.</summary>
    public bool PassedOut { get; }
}

internal enum MeritBadgeKind : byte
{
    /// <summary>든든한 등: the most time carrying.</summary>
    SteadyBack = 1,

    /// <summary>편안한 승객: the most time carried.</summary>
    ComfyPassenger = 2,

    // 3 was 등반왕 (the most height climbed carrying), dropped as nearly everyone earns it.

    /// <summary>단짝: the two who stayed a Pair the longest.</summary>
    BestBuddies = 4,

    /// <summary>구조대장: the most time carrying a Rescued.</summary>
    RescueRanger = 5,
}

/// <summary>A Merit Badge on the Run Report: who earned it, and the amount that won it.</summary>
internal sealed class MeritBadge
{
    public MeritBadge(MeritBadgeKind kind, IReadOnlyList<string> ids, double amount)
    {
        Kind = kind;
        Ids = ids;
        Amount = amount;
    }

    public MeritBadgeKind Kind { get; }

    /// <summary>Who earned it: one player, or the two of a Pair.</summary>
    public IReadOnlyList<string> Ids { get; }

    /// <summary>Seconds or metres, as the badge counts.</summary>
    public double Amount { get; }
}

/// <summary>How a Run ended.</summary>
internal enum RunEnding : byte
{
    /// <summary>Back at the Airport without the game ending the Run.</summary>
    Returned = 0,

    /// <summary>Someone reached the summit and was flown out.</summary>
    Summit = 1,

    /// <summary>Everyone was lost.</summary>
    Lost = 2,

    /// <summary>Still under way: a report read live from the record so far; never sent.</summary>
    Underway = 3,
}

/// <summary>
/// The Run Report: how the Run ended, how long it took, and its Carry Record,
/// written by the Troop Leader when the Run ends and kept until the next one
/// departs.
/// </summary>
internal sealed class RunReport
{
    public RunReport(RunEnding ending, double seconds, CarryRecord record)
    {
        Ending = ending;
        Seconds = seconds;
        Record = record;
    }

    public RunEnding Ending { get; }

    /// <summary>How long the Run took.</summary>
    public double Seconds { get; }

    public CarryRecord Record { get; }

    public object[] Encode() => [(byte)Ending, Seconds, Record.Encode()];

    /// <summary>
    /// The report to read now: the last Run's, once written; while a Run is
    /// under way, one read live from its record so far, the same object for
    /// the same record.
    /// </summary>
    public static RunReport? Now(RunReport? written, CarryRecord? sofar, ref RunReport? live)
    {
        if (written != null)
        {
            return written;
        }

        if (sofar == null)
        {
            return null;
        }

        if (live == null || !ReferenceEquals(live.Record, sofar))
        {
            live = new RunReport(RunEnding.Underway, 0, sofar);
        }

        return live;
    }

    public static RunReport? Decode(object? value) =>
        value is object[] { Length: 3 } parts
            && parts[0] is byte ending
            && Enum.IsDefined(typeof(RunEnding), ending)
            && ending != (byte)RunEnding.Underway
            && parts[1] is double seconds
            && ServerClock.IsTime(seconds)
            && CarryRecord.Decode(parts[2]) is CarryRecord record
                ? new RunReport((RunEnding)ending, Math.Max(0, seconds), record)
                : null;
}
