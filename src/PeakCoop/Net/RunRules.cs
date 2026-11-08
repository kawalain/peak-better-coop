using System;
using PeakCoop.Core;
using Photon.Pun;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakCoop.Net;

/// <summary>
/// The rules of a Run, set by the host's config and shared through a room
/// property so every client judges the Run the same way.
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
    }

    /// <summary>The Timed Shuffle interval's minutes sit above the flags.</summary>
    private const int IntervalShift = 8;

    public static bool AllowOddPlayerCount => Has(Flags.AllowOddPlayerCount);

    public static bool CarryWeight => Has(Flags.CarryWeight);

    public static bool CampfireShuffle => Has(Flags.CampfireShuffle);

    public static bool TimedShuffle => Has(Flags.TimedShuffle);

    public static bool HandOverPlace => Has(Flags.HandOverPlace);

    public static bool HandOverItems => Has(Flags.HandOverItems);

    public static double TimedShuffleSeconds => Math.Max(1, Current >> IntervalShift) * 60.0;

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

    private static int LocalRules()
    {
        Flags flags = Flags.None;
        if (CoopConfig.AllowOddPlayerCount.Value)
        {
            flags |= Flags.AllowOddPlayerCount;
        }

        if (CoopConfig.CarryWeight.Value)
        {
            flags |= Flags.CarryWeight;
        }

        if (CoopConfig.CampfireShuffle.Value)
        {
            flags |= Flags.CampfireShuffle;
        }

        if (CoopConfig.TimedShuffle.Value)
        {
            flags |= Flags.TimedShuffle;
        }

        if (CoopConfig.HandOverPlace.Value)
        {
            flags |= Flags.HandOverPlace;
        }

        if (CoopConfig.HandOverItems.Value)
        {
            flags |= Flags.HandOverItems;
        }

        return (int)flags | ((int)CoopConfig.TimedShuffleInterval.Value << IntervalShift);
    }

    private void Start()
    {
        CoopConfig.AllowOddPlayerCount.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.CarryWeight.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.CampfireShuffle.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.TimedShuffle.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.TimedShuffleInterval.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.HandOverPlace.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.HandOverItems.SettingChanged += (_, _) => PublishIfHost();
    }

    public override void OnJoinedRoom() => PublishIfHost();

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient) => PublishIfHost();

    private static void PublishIfHost()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int rules = LocalRules();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [RoomKeys.Rules] = rules });
        Plugin.Verbose($"Published Run rules {(Flags)(rules & 0xFF)}, Timed Shuffle every {rules >> IntervalShift} min.");
    }
}
