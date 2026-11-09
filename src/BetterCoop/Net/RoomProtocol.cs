using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// Which clients work together (ADR 0009). The Troop Leader stamps the room
/// with its protocol; a client reads the mod's room values only when the
/// stamp is its own and the Troop Leader speaks it now, and pairs only
/// players who publish the same protocol. A client in a room stamped
/// otherwise or not at all, or led by an Unmodded Player, as when the Troop
/// Leader left and Photon chose one, rests: it acts as an Unmodded Player
/// until that changes, it leaves the room, or it leads it.
/// </summary>
internal static class RoomProtocol
{
    /// <summary>
    /// Raise with any change to a mod room or player property, an event code,
    /// or a wire format, in the same commit. A version bump alone never does.
    /// </summary>
    public const int Current = 1;

    /// <summary>The protocol a player publishes, or null without the mod or before 0.6.0.</summary>
    public static int? Of(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.Protocol, out object value) && value is int protocol ? protocol : null;

    /// <summary>Whether a player can be paired with this client: the same protocol.</summary>
    public static bool Speaks(PhotonPlayer player) => Of(player) == Current;

    /// <summary>The protocol the room is stamped with, or null when no Troop Leader with the mod has stamped it.</summary>
    public static int? OfRoom() =>
        PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Protocol, out object value) && value is int protocol
            ? protocol
            : null;

    /// <summary>Whether the room's mod values are in this client's protocol, kept by a Troop Leader who speaks it.</summary>
    public static bool Readable =>
        OfRoom() == Current
        && (PhotonNetwork.IsMasterClient || PhotonNetwork.MasterClient is PhotonPlayer leader && Speaks(leader));

    /// <summary>Whether this client sits out the room: in it, not leading it, and the room's values not <see cref="Readable"/>.</summary>
    public static bool Resting => PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && !Readable;

    /// <summary>The room's value under a mod key, when this client may read the room's values.</summary>
    public static bool TryGet(string key, out object value)
    {
        value = null!;
        return Readable && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out value);
    }

    /// <summary>The room's value under a mod key, or null.</summary>
    public static object? Get(string key) => TryGet(key, out object value) ? value : null;
}
