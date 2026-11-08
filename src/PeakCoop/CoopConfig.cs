using BepInEx.Configuration;
using PeakCoop.Core;

namespace PeakCoop;

/// <summary>Every BepInEx setting of the mod.</summary>
internal static class CoopConfig
{
    public static ConfigEntry<bool> AllowOddPlayerCount { get; private set; } = null!;

    public static ConfigEntry<RolePreference> RolePreference { get; private set; } = null!;

    public static ConfigEntry<bool> CarryWeight { get; private set; } = null!;

    public static ConfigEntry<bool> RecoveryKey { get; private set; } = null!;

    public static ConfigEntry<bool> VerboseLog { get; private set; } = null!;

    public static ConfigEntry<bool> SoloPair { get; private set; } = null!;

    public static ConfigEntry<int> SoloPairLatency { get; private set; } = null!;

    public static void Bind(ConfigFile config)
    {
        AllowOddPlayerCount = config.Bind(
            "Party",
            "Allow Odd Player Count",
            false,
            "Let a Run depart with an odd number of players; the extra player stays an Unpaired Player. The host's value applies.");

        RolePreference = config.Bind(
            "Pair",
            "Role Preference",
            Core.RolePreference.Random,
            "Your wish when Pairs are formed: Carrier (keyboard), Rider (mouse), or Random. Conflicts are settled at random.");

        CarryWeight = config.Bind(
            "Balance",
            "Carry Weight",
            false,
            "Apply the game's weight for carrying a person to every Carrier. The host's value applies.");

        RecoveryKey = config.Bind(
            "Debug",
            "Recovery Key",
            true,
            "Let the host press F8 to re-attach every Pair's carry and wake every Rider.");

        VerboseLog = config.Bind(
            "Debug",
            "Verbose Log",
            true,
            "Write detailed pairing, carry, and Control Split diagnostics to LogOutput.log.");

        SoloPair = config.Bind(
            "Debug",
            "Solo Pair",
            false,
            "While alone in the room, play both Carrier and Rider: your mouse goes through the Rider's path (encoded, delayed, replayed) before it reaches your body. Also lets a solo Run depart.");

        SoloPairLatency = config.Bind(
            "Debug",
            "Solo Pair Latency (ms)",
            100,
            new ConfigDescription(
                "How long Solo Pair holds the Rider's actions back, standing in for the network.",
                new AcceptableValueRange<int>(0, 1000)));
    }
}
