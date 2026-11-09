using BepInEx.Configuration;
using BetterCoop.Core;
using BetterCoop.Ui;

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

    public static ConfigEntry<bool> RescueCarry { get; private set; } = null!;

    public static ConfigEntry<bool> VerboseLog { get; private set; } = null!;


    public static void Bind(ConfigFile config)
    {
        AllowOddPlayerCount = config.Bind(
            "Party",
            "Allow Odd Player Count",
            false,
            Strings.English("config.allowOddPlayerCount"));

        RolePreference = config.Bind(
            "Pair",
            "Role Preference",
            Core.RolePreference.Random,
            Strings.English("config.rolePreference"));

        FollowRolePreferences = config.Bind(
            "Pair",
            "Honour Role Preferences",
            true,
            Strings.English("config.followRolePreferences"));

        AllowSwapRequests = config.Bind(
            "Pair",
            "Allow Role Swaps",
            true,
            Strings.English("config.allowSwapRequests"));

        LatePairing = config.Bind(
            "Pair",
            "Late Pairing",
            true,
            Strings.English("config.latePairing"));

        RiderPicksFromBackpack = config.Bind(
            "Pair",
            "Rider Picks From Backpack",
            true,
            Strings.English("config.riderPicksFromBackpack"));

        RescueCarry = config.Bind(
            "Balance",
            "Rescue Carry",
            true,
            Strings.English("config.rescueCarry"));

        CarryWeight = config.Bind(
            "Balance",
            "Carry Weight",
            false,
            Strings.English("config.carryWeight"));

        CampfireShuffle = config.Bind(
            "Shuffle",
            "Campfire Shuffle",
            true,
            Strings.English("config.campfireShuffle"));

        TimedShuffle = config.Bind(
            "Shuffle",
            "Timed Shuffle",
            false,
            Strings.English("config.timedShuffle"));

        TimedShuffleInterval = config.Bind(
            "Shuffle",
            "Timed Shuffle Interval",
            ShuffleInterval.DefaultMinutes,
            new ConfigDescription(
                Strings.English("config.timedShuffleInterval", ("min", ShuffleInterval.MinMinutes), ("max", ShuffleInterval.MaxMinutes)),
                new AcceptableValueRange<int>(ShuffleInterval.MinMinutes, ShuffleInterval.MaxMinutes)));

        HandOverPlace = config.Bind(
            "Shuffle",
            "Hand Over Place",
            true,
            Strings.English("config.handOverPlace"));

        HandOverItems = config.Bind(
            "Shuffle",
            "Hand Over Items",
            true,
            Strings.English("config.handOverItems"));

        VerboseLog = config.Bind(
            "Debug",
            "Verbose Log",
            false,
            Strings.English("config.verboseLog"));
    }
}
