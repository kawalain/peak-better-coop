using System.Collections.Generic;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// The stable user id of every player seen in this room, kept after they
/// leave, so whoever is Troop Leader can name a parted Pair by user id.
/// </summary>
internal static class KnownPlayers
{
    private static readonly Dictionary<int, string> UserIds = new();

    public static void Remember(PhotonPlayer? player)
    {
        if (player != null && !string.IsNullOrEmpty(player.UserId))
        {
            UserIds[player.ActorNumber] = player.UserId;
        }
    }

    public static void Clear() => UserIds.Clear();

    public static string? UserIdOf(int actor) => UserIds.TryGetValue(actor, out string userId) ? userId : null;
}
