using System.Collections.Generic;
using BetterCoop.Core;
using Photon.Pun;
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
        // Kept for parted Pairs; a flood of joins cannot grow it without end.
        string? id = player == null ? null : IdOf(player);
        if (player != null && WireLimits.IsId(id) && (UserIds.Count < 256 || UserIds.ContainsKey(player.ActorNumber)))
        {
            UserIds[player.ActorNumber] = id!;
        }
    }

    /// <summary>The user id the mod knows a player by. Read it here, never from <c>Player.UserId</c>.</summary>
    public static string IdOf(PhotonPlayer player) => DevHooks.ScoutId(player);

    /// <summary>A player's name as the mod shows it, made safe (see <see cref="WireLimits.Name"/>).</summary>
    public static string NameOf(PhotonPlayer? player, int actor) => WireLimits.Name(player?.NickName, $"#{actor}");

    /// <summary>The name of a player in the room, made safe, or their actor number.</summary>
    public static string NameOf(int actor) => NameOf(PhotonNetwork.CurrentRoom?.GetPlayer(actor), actor);

    public static void Clear() => UserIds.Clear();

    public static string? UserIdOf(int actor) => UserIds.TryGetValue(actor, out string userId) ? userId : null;
}
