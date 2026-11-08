using PeakCoop.Core;
using Photon.Pun;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakCoop.Net;

/// <summary>
/// Publishes the local mod version and Role Preference as player custom
/// properties, so the host can check the party and form Pairs.
/// </summary>
internal sealed class ModPresence : MonoBehaviourPunCallbacks
{
    public static string? VersionOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.Version, out object value) ? value as string : null;

    public static RolePreference RoleOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.Role, out object value)
        && value is byte role
        && System.Enum.IsDefined(typeof(RolePreference), role)
            ? (RolePreference)role
            : RolePreference.Random;

    private void Start()
    {
        CoopConfig.RolePreference.SettingChanged += (_, _) => Publish();
    }

    public override void OnJoinedRoom() => Publish();

    public override void OnEnable()
    {
        base.OnEnable();
        Publish();
    }

    private static void Publish()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        var properties = new PhotonHashtable
        {
            [RoomKeys.Version] = MyPluginInfo.PLUGIN_VERSION,
            [RoomKeys.Role] = (byte)CoopConfig.RolePreference.Value,
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }
}
