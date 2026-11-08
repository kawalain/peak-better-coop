using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>
/// A made-up room for trying the Roster without other players: it holds
/// its players, Pairs, and Run Rules in memory and changes its Pairs with
/// the same rules a real room uses, at once rather than over the network.
/// Nothing it does reaches the game, the config, or the room.
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
    };

    private readonly List<int> players;

    private List<Pair> pairs = new();

    private List<int> unpaired = new();

    private double untilShuffle;

    private int shuffleMinutes = 10;

    /// <param name="count">Players in the room, the local player first.</param>
    /// <param name="troopLeader">Whether the local player leads.</param>
    /// <param name="formed">Whether the Run's Pair Formation has happened.</param>
    public FakeRoom(int count, bool troopLeader, bool formed, int seed = 1)
    {
        random = new Random(seed);
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

    public int Me => 1;

    public IReadOnlyList<int> Players => players;

    public IReadOnlyList<Pair> Pairs => pairs;

    public RolePreference Preference
    {
        get => preferences[Me];
        set => preferences[Me] = value;
    }

    public bool SwapPending => false;

    public bool CanAskSwap => this.PairOf(Me) != null;

    public bool Busy => false;

    public double? SecondsToTimedShuffle => Formed && rules[RunRule.TimedShuffle] ? untilShuffle : null;

    public IReadOnlyList<int> Pickable => players;

    public int ShuffleMinutes
    {
        get => shuffleMinutes;
        set
        {
            shuffleMinutes = ShuffleInterval.Clamp(value);
            untilShuffle = shuffleMinutes * 60.0;
        }
    }

    public string NameOf(int actor) =>
        actor >= 1 && actor <= Names.Length ? Names[actor - 1] : $"Player {actor}";

    /// <summary>Forms the Pairs as on arriving on the island.</summary>
    public void Form()
    {
        Apply(PairingRules.Form(Candidates(), random));
        Formed = true;
    }

    /// <summary>The partner agrees at once, so the two trade roles straight away.</summary>
    public void ToggleSwap()
    {
        if (this.PairOf(Me) is Pair pair)
        {
            SwapRoles(pair);
        }
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

    public bool CanAssign(int carrier, int rider) =>
        Formed && carrier != rider && players.Contains(carrier) && players.Contains(rider);

    /// <summary>A Pair Assignment, then the Late Pairing of the partners it leaves behind.</summary>
    public bool Assign(int carrier, int rider)
    {
        if (!CanAssign(carrier, rider))
        {
            return false;
        }

        var before = new PairingResult(pairs, unpaired, []);
        Apply(PairingRules.Assign(before, carrier, rider, players));
        PairLeftovers();
        return true;
    }

    public bool Rule(RunRule rule) => rules[rule];

    public void SetRule(RunRule rule, bool on)
    {
        rules[rule] = on;
        if (rule == RunRule.TimedShuffle)
        {
            untilShuffle = shuffleMinutes * 60.0;
        }
    }

    public void Tick(float seconds)
    {
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

    private void Apply(PairingResult result)
    {
        pairs = result.Pairs.ToList();
        unpaired = result.Unpaired.ToList();
    }

    /// <summary>Pairs up the Unpaired Players two by two, as Late Pairing would once they settle.</summary>
    private void PairLeftovers()
    {
        while (true)
        {
            var candidates = unpaired
                .Select(a => new LateCandidate(a, preferences[a], awake: true, height: 0f))
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

    private List<PairCandidate> Candidates() =>
        players.Select(a => new PairCandidate(a, preferences[a])).ToList();
}
