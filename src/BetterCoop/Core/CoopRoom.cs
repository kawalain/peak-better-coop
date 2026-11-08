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

    /// <summary>The local player.</summary>
    int Me { get; }

    IReadOnlyList<int> Players { get; }

    IReadOnlyList<Pair> Pairs { get; }

    RolePreference Preference { get; set; }

    /// <summary>Whether the local player has asked for a Role Swap.</summary>
    bool SwapPending { get; }

    bool CanAskSwap { get; }

    /// <summary>Whether a Pair change is under way, so another must wait.</summary>
    bool Busy { get; }

    /// <summary>Seconds to the next Timed Shuffle, or null when none is coming.</summary>
    double? SecondsToTimedShuffle { get; }

    /// <summary>Players the Troop Leader may name in a Pair Assignment.</summary>
    IReadOnlyList<int> Pickable { get; }

    int ShuffleMinutes { get; set; }

    string NameOf(int actor);

    /// <summary>Asks for a Role Swap with the local player's partner, or cancels the ask.</summary>
    void ToggleSwap();

    /// <summary>Orders a Pair Shuffle; false when it cannot start now.</summary>
    bool Shuffle();

    /// <summary>Orders the Role Swap of a Pair; false when it cannot start now.</summary>
    bool SwapRoles(Pair pair);

    /// <summary>Re-attaches every Pair's carry and wakes every Rider.</summary>
    void Recover();

    /// <summary>Whether a Pair Assignment setting these Pairs could run now.</summary>
    bool CanArrange(IReadOnlyList<Pair> pairs);

    /// <summary>Orders a Pair Assignment setting these Pairs; false when it cannot start now.</summary>
    bool Arrange(IReadOnlyList<Pair> pairs);

    bool Rule(RunRule rule);

    void SetRule(RunRule rule, bool on);

    /// <summary>Lets time pass for a room that keeps its own clock.</summary>
    void Tick(float seconds);
}

/// <summary>Questions about a room that every room answers the same way.</summary>
internal static class CoopRooms
{
    public static Pair? PairOf(this ICoopRoom room, int actor) => room.Pairs.FirstOrDefault(p => p.Contains(actor));

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
        string players = string.Join(",", room.Players);
        return $"{room.IsTroopLeader}|{room.Formed}|{pairs}|{players}";
    }
}
