using System;
using System.Collections.Generic;
using System.Linq;

using BetterCoop.Core;

namespace BetterCoop.DevTools;

/// <summary>
/// A made-up room for trying the Roster without other players: it holds
/// its players, Pairs, and Run Rules in memory and changes its Pairs with
/// the same rules a real room uses, at once rather than over the network.
/// Nothing it does reaches the game, the config, or the room. Development
/// builds only, for the Zoo; the tests reach it too.
/// </summary>
internal sealed class FakeRoom : ICoopRoom
{
    private static readonly string[] Names =
    [
        "Alder", "Birch", "Cedar", "Dahlia", "Elm", "Fern", "Gorse", "Hazel",
        "Iris", "Juniper", "Kelp", "Larch", "Moss", "Nettle", "Oak", "Pine",
    ];

    private readonly Random random;

    private readonly Dictionary<int, RolePreference> preferences = new();

    private readonly Dictionary<RunRule, bool> rules = new()
    {
        [RunRule.CampfireShuffle] = true,
        [RunRule.TimedShuffle] = false,
        [RunRule.HandOverPlace] = true,
        [RunRule.HandOverItems] = true,
        [RunRule.CarryWeight] = true,
        [RunRule.RiderPicksFromBackpack] = true,
        [RunRule.AllowOddPlayerCount] = false,
        [RunRule.FollowRolePreferences] = true,
        [RunRule.AllowSwapRequests] = true,
        [RunRule.LatePairing] = true,
    };

    private readonly List<int> players;

    private List<Pair> pairs = new();

    private List<int> unpaired = new();

    private List<Pair> plan = new();

    private double untilShuffle;

    private int shuffleMinutes = 10;

    /// <summary>The segments a made-up Run climbs, by the game's text keys for their names.</summary>
    private static readonly string[] Titles = ["SHORE", "TROPICS", "ALPINE", "CALDERA", "THE KILN", "PEAK"];

    /// <summary>A scout who climbed the first segment and left the room.</summary>
    private const string Leaver = "zoo:left";

    private readonly int seed;

    private CarryRecord? record;

    private readonly bool reported;

    private RunReport? report;

    /// <param name="count">Players in the room, the local player first.</param>
    /// <param name="troopLeader">Whether the local player leads.</param>
    /// <param name="formed">Whether the Run's Pair Formation has happened.</param>
    /// <param name="reported">Whether a room not yet formed holds the Run Report of a made-up last Run.</param>
    /// <param name="ending">How that last Run ended; without one, it changes with the seed.</param>
    public FakeRoom(int count, bool troopLeader, bool formed, int seed = 1, bool reported = false, RunEnding? ending = null)
    {
        this.reported = reported;
        this.ending = ending;
        random = new Random(seed);
        this.seed = seed;
        players = Enumerable.Range(1, Math.Max(1, count)).ToList();
        IsTroopLeader = troopLeader;
        var wishes = new[] { RolePreference.Random, RolePreference.Random, RolePreference.Carrier, RolePreference.Rider };
        foreach (int actor in players)
        {
            preferences[actor] = actor == Me ? RolePreference.Random : wishes[random.Next(wishes.Length)];
        }

        // An odd party only departs when the rule allows it.
        rules[RunRule.AllowOddPlayerCount] = players.Count % 2 != 0;
        untilShuffle = shuffleMinutes * 60.0;
        if (formed)
        {
            Form();
        }
    }

    public bool InRoom => true;

    public bool IsTroopLeader { get; }

    /// <summary>The local player when they lead, else the next player in the room.</summary>
    public int TroopLeader => IsTroopLeader || players.Count < 2 ? Me : players[1];

    public bool Formed { get; private set; }

    /// <summary>A room not yet formed stands at the Airport.</summary>
    public bool Planning => !Formed;

    public IReadOnlyList<Pair> Plan => plan;

    /// <summary>A room not yet formed remembers its scouts' last Run as Pairs of neighbours, turned round.</summary>
    public IReadOnlyList<Pair> LastRunPairs => Formed
        ? []
        : Enumerable.Range(0, players.Count / 2).Select(i => new Pair(players[2 * i + 1], players[2 * i])).ToList();

    private readonly RunEnding? ending;

    /// <summary>On the island, a made-up record of the Run so far: two segments, Pairs drawn anew for each.</summary>
    public CarryRecord? Record => Formed ? record ??= MakeRecord(2, pairs) : null;

    /// <summary>
    /// At the Airport, when made with one, the report of a made-up last Run
    /// up the whole mountain; how it ended changes with the seed.
    /// </summary>
    public RunReport? Report => !Formed && reported
        ? report ??= new RunReport(ending ?? (RunEnding)(seed % 3), 3000 + (seed * 731 % 4000), MakeRecord(Titles.Length, []))
        : null;

    /// <summary>A made-up Run some way up the mountain, its clock running while the room is formed.</summary>
    public double RunSeconds { get; private set; } = 1800;

    public bool Present(string id) =>
        id.StartsWith("zoo:", StringComparison.Ordinal) && int.TryParse(id.Substring(4), out int actor) && players.Contains(actor);

    /// <summary>The user id a made-up scout has in a record.</summary>
    public static string IdOf(int actor) => $"zoo:{actor}";

    public int Me => 1;

    public IReadOnlyList<int> Players => players;

    public IReadOnlyList<Pair> Pairs => pairs;

    public RolePreference Preference
    {
        get => preferences[Me];
        set => preferences[Me] = value;
    }

    public bool SwapPending => swapAsked;

    /// <summary>Whether the made-up partner has asked for a Role Swap; they have, unless set otherwise.</summary>
    public bool PartnerAsksSwap { get; set; } = true;

    private bool swapAsked;

    public bool CanAskSwap => rules[RunRule.AllowSwapRequests] && this.PairOf(Me) != null;

    public bool Busy => false;

    public double? SecondsToTimedShuffle => Formed && rules[RunRule.TimedShuffle] ? untilShuffle : null;

    public int ShuffleMinutes
    {
        get => shuffleMinutes;
        set
        {
            if (!IsTroopLeader)
            {
                return;
            }

            shuffleMinutes = ShuffleInterval.Clamp(value);
            untilShuffle = shuffleMinutes * 60.0;
        }
    }

    public string NameOf(int actor) =>
        actor >= 1 && actor <= Names.Length ? Names[actor - 1] : $"Player {actor}";

    /// <summary>Forms the Pairs as on arriving on the island, following the Pair Plan.</summary>
    public void Form()
    {
        Apply(PairingRules.FormWithPlan(Candidates(), plan, random));
        plan = new List<Pair>();
        Formed = true;
        record = null;
        report = null;
    }

    public bool SetPlan(IReadOnlyList<Pair> wanted)
    {
        if (!IsTroopLeader || !Planning || !PairingRules.IsPlan(wanted, players))
        {
            return false;
        }

        plan = wanted.ToList();
        return true;
    }

    public IReadOnlyList<Pair> DrawPlan() => PairingRules.Form(Candidates(), random).Pairs;

    /// <summary>
    /// When the partner has asked, the two trade roles straight away;
    /// otherwise the request waits for them, and a second press withdraws it.
    /// </summary>
    public void ToggleSwap()
    {
        if (swapAsked)
        {
            swapAsked = false;
        }
        else if (CanAskSwap && this.PairOf(Me) is Pair pair)
        {
            if (PartnerAsksSwap)
            {
                SwapRoles(pair);
            }
            else
            {
                swapAsked = true;
            }
        }
    }

    public bool CanCallOffShuffle => IsTroopLeader && SecondsToTimedShuffle != null;

    public bool CallOffShuffle()
    {
        if (!CanCallOffShuffle)
        {
            return false;
        }

        untilShuffle = shuffleMinutes * 60.0;
        return true;
    }

    public bool Shuffle()
    {
        if (!Formed)
        {
            return false;
        }

        Apply(PairingRules.Reshuffle(Candidates(), random, pairs, unpaired));
        untilShuffle = shuffleMinutes * 60.0;
        return true;
    }

    /// <summary>Turns a Pair round at once.</summary>
    public bool SwapRoles(Pair pair)
    {
        int index = pairs.FindIndex(p => p.Carrier == pair.Carrier && p.Rider == pair.Rider);
        if (index < 0)
        {
            return false;
        }

        pairs[index] = new Pair(pair.Rider, pair.Carrier);
        return true;
    }

    public void Recover()
    {
    }

    public bool CanArrange(IReadOnlyList<Pair> wanted) => Formed && Arranged(wanted) != null;

    /// <summary>A Pair Assignment, then the Late Pairing of the players it leaves Unpaired.</summary>
    public bool Arrange(IReadOnlyList<Pair> wanted)
    {
        if (!Formed || Arranged(wanted) is not PairingResult after)
        {
            return false;
        }

        Apply(after);
        PairLeftovers();
        return true;
    }

    public bool Rule(RunRule rule) => rules[rule];

    public void SetRule(RunRule rule, bool on)
    {
        if (IsTroopLeader)
        {
            LeaderSets(rule, on);
        }
    }

    /// <summary>The room's Troop Leader, whoever it is, sets a rule, as one led by someone else sees it change.</summary>
    public void LeaderSets(RunRule rule, bool on)
    {
        rules[rule] = on;
        if (rule == RunRule.TimedShuffle)
        {
            untilShuffle = shuffleMinutes * 60.0;
        }
    }

    public void Tick(float seconds)
    {
        if (Formed)
        {
            RunSeconds += seconds;
        }

        if (SecondsToTimedShuffle == null)
        {
            return;
        }

        untilShuffle -= seconds;
        if (untilShuffle <= 0.0)
        {
            Shuffle();
        }
    }

    /// <summary>
    /// A record of <paramref name="segments"/> segments climbed, the last by
    /// <paramref name="last"/> and every other by Pairs drawn at random, the
    /// same for the same seed. In the first, a scout since gone carried in
    /// place of the first Pair's Carrier. Each Pair climbs a little most
    /// samples, and now and then its Shared Body is dead for one.
    /// </summary>
    private CarryRecord MakeRecord(int segments, IReadOnlyList<Pair> last)
    {
        var made = new CarryRecord();
        var draw = new Random((seed * 31) + segments);
        for (int segment = 0; segment < segments; segment++)
        {
            List<(string Carrier, string Rider)> climbing =
                (segment == segments - 1 && last.Count > 0 ? last : PairingRules.Form(Candidates(), draw).Pairs)
                .Select(p => (IdOf(p.Carrier), IdOf(p.Rider)))
                .ToList();
            if (segment == 0 && climbing.Count > 0)
            {
                climbing[0] = (Leaver, climbing[0].Rider);
            }

            var heights = climbing.ToDictionary(p => p, _ => segment * 250.0);
            int samples = 80 + draw.Next(160);
            for (int i = 0; i < samples; i++)
            {
                var taken = new List<PairSample>();
                foreach ((string carrier, string rider) in climbing)
                {
                    heights[(carrier, rider)] += draw.NextDouble() * 1.6;
                    taken.Add(new PairSample(carrier, ScoutName(carrier), rider, ScoutName(rider), draw.Next(30) != 0, heights[(carrier, rider)]));
                }

                made.Count(taken, 2, segment, Titles[segment % Titles.Length]);
            }
        }

        return made;
    }

    private string ScoutName(string id) =>
        id == Leaver ? "Quill" : int.TryParse(id.Substring(4), out int actor) ? NameOf(actor) : id;

    private void Apply(PairingResult result)
    {
        pairs = result.Pairs.ToList();
        unpaired = result.Unpaired.ToList();
    }

    /// <summary>Pairs up the Unpaired Players two by two, as Late Pairing would once they settle.</summary>
    private void PairLeftovers()
    {
        while (rules[RunRule.LatePairing])
        {
            var candidates = unpaired
                .Select(a => new LateCandidate(a, Wish(a), awake: true, height: 0f))
                .ToList();
            if (LatePairingRules.PickTwo(candidates) is not (int first, int second)
                || LatePairingRules.Form(candidates.First(c => c.Actor == first), candidates.First(c => c.Actor == second), random) is not LatePairing formed)
            {
                return;
            }

            pairs.Add(formed.Pair);
            unpaired.Remove(first);
            unpaired.Remove(second);
        }
    }

    /// <summary>Everyone in a made-up room is awake and may be named.</summary>
    private PairingResult? Arranged(IReadOnlyList<Pair> wanted) =>
        PairingRules.Arrange(new PairingResult(pairs, unpaired, []), wanted, players, players.Contains, _ => true, _ => true);

    private List<PairCandidate> Candidates() =>
        players.Select(a => new PairCandidate(a, Wish(a))).ToList();

    /// <summary>A player's Role Preference, unless the Run Rules draw roles by lot.</summary>
    private RolePreference Wish(int actor) =>
        rules[RunRule.FollowRolePreferences] ? preferences[actor] : RolePreference.Random;
}
