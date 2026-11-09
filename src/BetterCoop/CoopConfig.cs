using BepInEx.Configuration;
using BetterCoop.Core;

namespace BetterCoop;

/// <summary>Every BepInEx setting of the mod.</summary>
internal static class CoopConfig
{
    public static ConfigEntry<bool> AllowOddPlayerCount { get; private set; } = null!;

    public static ConfigEntry<RolePreference> RolePreference { get; private set; } = null!;

    public static ConfigEntry<bool> RiderPicksFromBackpack { get; private set; } = null!;

    public static ConfigEntry<bool> CarryWeight { get; private set; } = null!;

    public static ConfigEntry<bool> CampfireShuffle { get; private set; } = null!;

    public static ConfigEntry<bool> TimedShuffle { get; private set; } = null!;

    public static ConfigEntry<int> TimedShuffleInterval { get; private set; } = null!;

    public static ConfigEntry<bool> HandOverPlace { get; private set; } = null!;

    public static ConfigEntry<bool> HandOverItems { get; private set; } = null!;

    public static ConfigEntry<bool> FollowRolePreferences { get; private set; } = null!;

    public static ConfigEntry<bool> AllowSwapRequests { get; private set; } = null!;

    public static ConfigEntry<bool> LatePairing { get; private set; } = null!;

    public static ConfigEntry<bool> VerboseLog { get; private set; } = null!;


    public static void Bind(ConfigFile config)
    {
        AllowOddPlayerCount = config.Bind(
            "Party",
            "Allow Odd Player Count",
            false,
            "Let a Run depart with an odd number of players; the extra player stays an Unpaired Player. The Troop Leader's value applies.");

        RolePreference = config.Bind(
            "Pair",
            "Role Preference",
            Core.RolePreference.Random,
            "Your wish when Pairs are formed: Carrier (keyboard), Rider (mouse), or Random for either. Conflicts are settled at random.");

        FollowRolePreferences = config.Bind(
            "Pair",
            "Honour Role Preferences",
            true,
            "When Pairs are formed, shuffled, or paired late, give players their Role Preference where it fits. Off: roles are drawn at random. A Pair Plan or Pair Assignment always comes first. The Troop Leader's value applies.");

        AllowSwapRequests = config.Bind(
            "Pair",
            "Allow Role Swaps",
            true,
            "Let the two of a Pair trade Carrier and Rider when both ask for it. The Troop Leader can always set roles on the Roster. The Troop Leader's value applies.");

        LatePairing = config.Bind(
            "Pair",
            "Late Pairing",
            true,
            "During a Run, pair two players without a partner as soon as there are two. Off: they climb alone until the next Pair Shuffle or Pair Assignment. The Troop Leader's value applies.");

        RiderPicksFromBackpack = config.Bind(
            "Pair",
            "Rider Picks From Backpack",
            true,
            "In a backpack wheel the Carrier opens, the Rider picks with the mouse while the Carrier holds the key. Off: the Carrier picks, and only the Carrier sees the wheel. The Troop Leader's value applies.");

        CarryWeight = config.Bind(
            "Balance",
            "Carry Weight",
            false,
            "Apply the game's weight for carrying a person to every Carrier. The Troop Leader's value applies.");

        CampfireShuffle = config.Bind(
            "Shuffle",
            "Campfire Shuffle",
            true,
            "Form the Pairs anew when a campfire is lit and the Run moves on to the next segment. The Troop Leader's value applies.");

        TimedShuffle = config.Bind(
            "Shuffle",
            "Timed Shuffle",
            false,
            "Form the Pairs anew at a fixed interval. The Troop Leader's value applies.");

        TimedShuffleInterval = config.Bind(
            "Shuffle",
            "Timed Shuffle Interval",
            ShuffleInterval.DefaultMinutes,
            new ConfigDescription(
                $"How often a Timed Shuffle comes, in minutes ({ShuffleInterval.MinMinutes}-{ShuffleInterval.MaxMinutes}). The Troop Leader's value applies.",
                new AcceptableValueRange<int>(ShuffleInterval.MinMinutes, ShuffleInterval.MaxMinutes)));

        HandOverPlace = config.Bind(
            "Shuffle",
            "Hand Over Place",
            true,
            "When Pairs change, each new Carrier moves to where the Shared Body they take over stood. Riders always move to their new Carrier. The Troop Leader's value applies.");

        HandOverItems = config.Bind(
            "Shuffle",
            "Hand Over Items",
            true,
            "When Pairs change, each new Carrier takes over the items of the Shared Body they take over; the items swap, so nothing is lost. Statuses are always handed over. The Troop Leader's value applies.");

        VerboseLog = config.Bind(
            "Debug",
            "Verbose Log",
            false,
            "Write detailed pairing, carry, and Control Split diagnostics to LogOutput.log.");
    }
}
