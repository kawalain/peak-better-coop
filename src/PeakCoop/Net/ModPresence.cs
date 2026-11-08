using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace PeakCoop.Net;

/// <summary>
/// Publishes the local mod version as a player custom property, so the host
/// can tell which players run the same mod.
/// </summary>
internal sealed class ModPresence : MonoBehaviourPunCallbacks
{
    public const string VersionKey = "PeakCoop.Version";

    public static string? VersionOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(VersionKey, out object value) ? value as string : null;

    public override void OnJoinedRoom() => Publish();

    public override void OnEnable()
    {
        base.OnEnable();
        if (PhotonNetwork.InRoom)
        {
            Publish();
        }
    }

    private static void Publish()
    {
        var properties = new PhotonHashtable { [VersionKey] = MyPluginInfo.PLUGIN_VERSION };
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }
}
