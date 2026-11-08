using System;
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
    private enum Flags : byte
    {
        None = 0,
        AllowOddPlayerCount = 1,
        CarryWeight = 2,
    }

    public static bool AllowOddPlayerCount => (Current & Flags.AllowOddPlayerCount) != 0;

    public static bool CarryWeight => (Current & Flags.CarryWeight) != 0;

    private static Flags Current
    {
        get
        {
            if (PhotonNetwork.InRoom
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Rules, out object value)
                && value is byte flags)
            {
                return (Flags)flags;
            }

            return LocalFlags();
        }
    }

    private static Flags LocalFlags()
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

        return flags;
    }

    private void Start()
    {
        CoopConfig.AllowOddPlayerCount.SettingChanged += (_, _) => PublishIfHost();
        CoopConfig.CarryWeight.SettingChanged += (_, _) => PublishIfHost();
    }

    public override void OnJoinedRoom() => PublishIfHost();

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient) => PublishIfHost();

    private static void PublishIfHost()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        var properties = new PhotonHashtable { [RoomKeys.Rules] = (byte)LocalFlags() };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        Plugin.Verbose($"Published Run rules {LocalFlags()}.");
    }
}
