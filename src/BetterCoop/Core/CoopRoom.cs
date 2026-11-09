using System.Collections.Generic;
using System.Linq;

namespace BetterCoop.Core;

/// <summary>A Run Rule that is on or off.</summary>
internal enum RunRule
{
    CampfireShuffle,
    TimedShuffle,
    HandOverPlace,
    HandOverItems,
    CarryWeight,
    RiderPicksFromBackpack,
    AllowOddPlayerCount,
    FollowRolePreferences,
    AllowSwapRequests,
    LatePairing,
}

/// <summary>
/// What the Roster shows and asks for: the room as the local player sees
/// it. The game's room answers in play; a made-up room answers in the Zoo.
/// Players are named by actor number.
/// </summary>
internal interface ICoopRoom
{
    bool InRoom { get; }

    bool IsTroopLeader { get; }

    /// <summary>The player who leads the room, or -1 when none is known.</summary>
    int TroopLeader { get; }

    /// <summary>Whether the Run's Pair Formation has happened.</summary>
    bool Formed { get; }

    /// <summary>Whether the party is at the Airport, where the Troop Leader sets the Pair Plan.</summary>
    bool Planning { get; }

    /// <summary>The Pair Plan the Troop Leader announced; empty when there is none.</summary>
    IReadOnlyList<Pair> Plan { get; }

    /// <summary>The Pairs the last Run ended with whose two members are both here now.</summary>
    IReadOnlyList<Pair> LastRunPairs { get; }

    /// <summary>The Carry Record of the Run under way, or null before one is shared.</summary>
    CarryRecord? Record { get; }

    /// <summary>The Run Report of the last Run, kept until the next departs, or null.</summary>
    RunReport? Report { get; }

    /// <summary>How long the Run under way has gone on, in seconds.</summary>
    double RunSeconds { get; }

    /// <summary>Whether the player of a user id in the Carry Record is in the room now.</summary>
    bool Present(string id);

    /// <summary>The local player.</summary>
    int Me { get; }

    IReadOnlyList<int> Players { get; }

    IReadOnlyList<Pair> Pairs { get; }

    RolePreference Preference { get; set; }

    /// <summary>Whether the local player has asked for a Role Swap.</summary>
    bool SwapPending { get; }

    /// <summary>Whether the local player's partner has asked them for a Role Swap.</summary>
    bool PartnerAsksSwap { get; }

    bool CanAskSwap { get; }

    /// <summary>Whether a Pair change is under way, so another must wait.</summary>
    bool Busy { get; }

    /// <summary>Seconds to the next Timed Shuffle, or null when none is coming.</summary>
    double? SecondsToTimedShuffle { get; }

    int ShuffleMinutes { get; set; }

    string NameOf(int actor);

    /// <summary>Asks for a Role Swap with the local player's partner, or cancels the ask.</summary>
    void ToggleSwap();

    /// <summary>Orders a Pair Shuffle; false when it cannot start now.</summary>
    bool Shuffle();

    /// <summary>Whether the Troop Leader could call off the coming Pair Shuffle now.</summary>
    bool CanCallOffShuffle { get; }

    /// <summary>Calls off the coming Pair Shuffle; the Timed Shuffle counts again from the start.</summary>
    bool CallOffShuffle();

    /// <summary>Re-attaches every Pair's carry and wakes every Rider.</summary>
    void Recover();

    /// <summary>Whether a Pair Assignment setting these Pairs could run now.</summary>
    bool CanArrange(IReadOnlyList<Pair> pairs);

    /// <summary>Orders a Pair Assignment setting these Pairs; false when it cannot start now.</summary>
    bool Arrange(IReadOnlyList<Pair> pairs);

    /// <summary>Announces a Pair Plan; false when it cannot be set now.</summary>
    bool SetPlan(IReadOnlyList<Pair> plan);

    /// <summary>A plan drawn at random, as the Pair Formation would form the Pairs now.</summary>
    IReadOnlyList<Pair> DrawPlan();

    bool Rule(RunRule rule);

    void SetRule(RunRule rule, bool on);

    /// <summary>Lets time pass for a room that keeps its own clock.</summary>
    void Tick(float seconds);
}

/// <summary>Questions about a room that every room answers the same way.</summary>
internal static class CoopRooms
{
    public static Pair? PairOf(this ICoopRoom room, int actor) => room.Pairs.FirstOrDefault(p => p.Contains(actor));

    /// <summary>Whether the Run Report can be read now: the last Run's, or the one under way.</summary>
    public static bool HasReport(this ICoopRoom room) => room.Report != null || (room.Formed && room.Record != null);

    public static IEnumerable<int> Unpaired(this ICoopRoom room) =>
        room.Players.Where(a => !room.Pairs.Any(p => p.Contains(a)));

    /// <summary>What decides the Roster's rows: the room, the Pairs, and who leads.</summary>
    public static string Shape(this ICoopRoom room)
    {
        if (!room.InRoom)
        {
            return "out";
        }

        string pairs = string.Join(",", room.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"));
        string plan = string.Join(",", room.Plan.Select(p => $"{p.Carrier}>{p.Rider}"));
        string players = string.Join(",", room.Players);
        return $"{room.IsTroopLeader}|{room.Formed}|{room.Planning}|{pairs}|{plan}|{players}|{room.Record != null}|{room.HasReport()}";
    }
}
