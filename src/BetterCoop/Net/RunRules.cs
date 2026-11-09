using System;
using BetterCoop.Core;
using Photon.Pun;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// The rules of a Run, set by the Troop Leader's config and shared through a
/// room property so every client judges the Run the same way. A player who
/// becomes Troop Leader keeps the room's rules, writing them into their own
/// config, rather than bringing their own.
/// </summary>
internal sealed class RunRules : MonoBehaviourPunCallbacks
{
    [Flags]
    private enum Flags
    {
        None = 0,
        AllowOddPlayerCount = 1,
        CarryWeight = 2,
        CampfireShuffle = 4,
        TimedShuffle = 8,
        HandOverPlace = 16,
        HandOverItems = 32,
        RiderPicksFromBackpack = 64,
        FollowRolePreferences = 128,
        AllowSwapRequests = 256,
        LatePairing = 512,
    }

    /// <summary>The Timed Shuffle interval's minutes sit above the flags.</summary>
    private const int IntervalShift = 16;

    private const int FlagMask = (1 << IntervalShift) - 1;

    /// <summary>Set while the room's rules are written into the config, so the half-written config is not published.</summary>
    private static bool adopting;

    public static bool AllowOddPlayerCount => Has(Flags.AllowOddPlayerCount);

    public static bool CarryWeight => Has(Flags.CarryWeight);

    public static bool CampfireShuffle => Has(Flags.CampfireShuffle);

    public static bool TimedShuffle => Has(Flags.TimedShuffle);

    public static bool HandOverPlace => Has(Flags.HandOverPlace);

    public static bool HandOverItems => Has(Flags.HandOverItems);

    public static bool RiderPicksFromBackpack => Has(Flags.RiderPicksFromBackpack);

    public static bool FollowRolePreferences => Has(Flags.FollowRolePreferences);

    public static bool AllowSwapRequests => Has(Flags.AllowSwapRequests);

    public static bool LatePairing => Has(Flags.LatePairing);

    /// <summary>Whether a rule is on in the room, as every client reads it.</summary>
    public static bool Is(RunRule rule) => Has(FlagOf(rule));

    /// <summary>The Troop Leader's interval, held to the allowed range whatever arrives.</summary>
    public static double TimedShuffleSeconds => ShuffleInterval.Clamp(Current >> IntervalShift) * 60.0;

    private static int Current
    {
        get
        {
            if (PhotonNetwork.InRoom
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Rules, out object value)
                && value is int rules)
            {
                return rules;
            }

            return LocalRules();
        }
    }

    private static bool Has(Flags flag) => (Current & (int)flag) != 0;

    private static Flags FlagOf(RunRule rule) => rule switch
    {
        RunRule.AllowOddPlayerCount => Flags.AllowOddPlayerCount,
        RunRule.CarryWeight => Flags.CarryWeight,
        RunRule.CampfireShuffle => Flags.CampfireShuffle,
        RunRule.TimedShuffle => Flags.TimedShuffle,
        RunRule.HandOverPlace => Flags.HandOverPlace,
        RunRule.HandOverItems => Flags.HandOverItems,
        RunRule.RiderPicksFromBackpack => Flags.RiderPicksFromBackpack,
        RunRule.FollowRolePreferences => Flags.FollowRolePreferences,
        RunRule.AllowSwapRequests => Flags.AllowSwapRequests,
        RunRule.LatePairing => Flags.LatePairing,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    /// <summary>The config entry that holds a rule for the Troop Leader.</summary>
    internal static BepInEx.Configuration.ConfigEntry<bool> Entry(RunRule rule) => rule switch
    {
        RunRule.AllowOddPlayerCount => CoopConfig.AllowOddPlayerCount,
        RunRule.CarryWeight => CoopConfig.CarryWeight,
        RunRule.CampfireShuffle => CoopConfig.CampfireShuffle,
        RunRule.TimedShuffle => CoopConfig.TimedShuffle,
        RunRule.HandOverPlace => CoopConfig.HandOverPlace,
        RunRule.HandOverItems => CoopConfig.HandOverItems,
        RunRule.RiderPicksFromBackpack => CoopConfig.RiderPicksFromBackpack,
        RunRule.FollowRolePreferences => CoopConfig.FollowRolePreferences,
        RunRule.AllowSwapRequests => CoopConfig.AllowSwapRequests,
        RunRule.LatePairing => CoopConfig.LatePairing,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    private static readonly RunRule[] All = (RunRule[])Enum.GetValues(typeof(RunRule));

    private static int LocalRules()
    {
        Flags flags = Flags.None;
        foreach (RunRule rule in All)
        {
            if (Entry(rule).Value)
            {
                flags |= FlagOf(rule);
            }
        }

        return (int)flags | (ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value) << IntervalShift);
    }

    private void Start()
    {
        foreach (RunRule rule in All)
        {
            Entry(rule).SettingChanged += (_, _) => PublishIfHost();
        }

        CoopConfig.TimedShuffleInterval.SettingChanged += (_, _) => PublishIfHost();
    }

    public override void OnJoinedRoom()
    {
        try
        {
            TakeOver();
        }
        catch (Exception exception)
        {
            Guard.Report("RunRules.OnJoinedRoom", exception);
        }
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        try
        {
            TakeOver();
        }
        catch (Exception exception)
        {
            Guard.Report("RunRules.OnMasterClientSwitched", exception);
        }
    }

    /// <summary>
    /// As the Troop Leader: keeps the rules the room already has, written
    /// into this player's config, or brings this player's own to a room that
    /// has none yet.
    /// </summary>
    private static void TakeOver()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Rules, out object value) && value is int rules)
        {
            Adopt(rules);
            Plugin.Verbose("Kept the room's Run rules as the new Troop Leader.");
        }

        PublishIfHost();
    }

    private static void Adopt(int rules)
    {
        adopting = true;
        try
        {
            foreach (RunRule rule in All)
            {
                Entry(rule).Value = (rules & (int)FlagOf(rule)) != 0;
            }

            CoopConfig.TimedShuffleInterval.Value = ShuffleInterval.Clamp(rules >> IntervalShift);
        }
        finally
        {
            adopting = false;
        }
    }

    private static void PublishIfHost()
    {
        if (adopting || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int rules = LocalRules();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [RoomKeys.Rules] = rules });
        Plugin.Verbose($"Published Run rules {(Flags)(rules & FlagMask)}, Timed Shuffle every {rules >> IntervalShift} min.");
    }
}
