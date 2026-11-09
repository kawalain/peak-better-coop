using System.Collections.Generic;
using BetterCoop.Core;
using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// The stable user id and the name of every player seen in this room, kept
/// after they leave and until this player leaves the room, so whoever is
/// Troop Leader can name a parted Pair by user id, and every notice and form
/// can still name a player who just left.
/// </summary>
internal static class KnownPlayers
{
    /// <summary>How many players are kept; a flood of joins cannot grow either list without end.</summary>
    private const int MaxKept = 256;

    private static readonly Dictionary<int, string> UserIds = new();

    private static readonly Dictionary<int, string> Names = new();

    public static void Remember(PhotonPlayer? player)
    {
        // Kept for parted Pairs; a flood of joins cannot grow it without end.
        if (player == null)
        {
            return;
        }

        string id = IdOf(player);
        if (WireLimits.IsId(id) && (UserIds.Count < MaxKept || UserIds.ContainsKey(player.ActorNumber)))
        {
            UserIds[player.ActorNumber] = id;
        }

        KeepName(player);
    }

    private static void KeepName(PhotonPlayer player)
    {
        string name = WireLimits.Name(player.NickName, string.Empty);
        if (name.Length > 0 && (Names.Count < MaxKept || Names.ContainsKey(player.ActorNumber)))
        {
            Names[player.ActorNumber] = name;
        }
    }

    /// <summary>The user id the mod knows a player by. Read it here, never from <c>Player.UserId</c>.</summary>
    public static string IdOf(PhotonPlayer player) => DevHooks.ScoutId(player);

    /// <summary>
    /// A player's name as the mod shows it, made safe (see <see cref="WireLimits.Name"/>):
    /// their name now, or for one who has left, the last name seen in this room.
    /// </summary>
    public static string NameOf(PhotonPlayer? player, int actor)
    {
        if (player != null)
        {
            KeepName(player);
        }

        return Names.TryGetValue(actor, out string kept) ? kept : WireLimits.Name(player?.NickName, $"#{actor}");
    }

    /// <summary>The name of a player in the room, made safe, or their actor number.</summary>
    public static string NameOf(int actor) => NameOf(PhotonNetwork.CurrentRoom?.GetPlayer(actor), actor);

    public static void Clear()
    {
        UserIds.Clear();
        Names.Clear();
    }

    public static string? UserIdOf(int actor) => UserIds.TryGetValue(actor, out string userId) ? userId : null;
}
