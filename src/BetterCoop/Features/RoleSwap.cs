using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Role Swap: both partners press Swap Places in the pause menu within 30
/// seconds, and the Troop Leader runs it as an operation (ADR 0004). Each
/// player's request is a player property naming the partner and the server
/// time it was made, so it lapses by itself and dies with the Pair.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class RoleSwap
{
    private const double RequestSeconds = 30;

    /// <summary>The local request ahead of its echo: partner actor and server time, or none.</summary>
    private static (int Partner, double At)? pending;

    private static bool partnerOffered;

    internal static bool Pending => pending != null;

    /// <summary>Whether the local player's partner has asked them, in time, to swap places.</summary>
    internal static bool PartnerAsks
    {
        get
        {
            Character local = Character.localCharacter;
            return local != null
                && PairRoster.TryGetPartner(local, out Character partner)
                && partner.photonView?.Owner is PhotonPlayer player
                && Asks(player, PairRoster.ActorOf(local), PhotonNetwork.Time);
        }
    }

    /// <summary>Whether the local player may ask now: the Run Rules allow it, alive, paired, on the island, and no operation under way.</summary>
    internal static bool CanRequest(out Character partner)
    {
        partner = null!;
        Character local = Character.localCharacter;
        return RunRules.AllowSwapRequests
            && local != null
            && !local.data.dead
            && GameHandler.IsOnIsland
            && !PairShuffle.Busy
            && PairRoster.TryGetPartner(local, out partner)
            && !partner.data.dead;
    }

    internal static void Toggle()
    {
        if (pending != null)
        {
            Withdraw(Strings.SwapCancelled);
            return;
        }

        if (!RunRules.AllowSwapRequests)
        {
            CoopNotice.Info(Strings.SwapNotAllowed);
            return;
        }

        if (!CanRequest(out Character partner))
        {
            CoopNotice.Info(Strings.SwapUnavailable);
            return;
        }

        int actor = PairRoster.ActorOf(partner);
        pending = (actor, PhotonNetwork.Time);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            [RoomKeys.SwapRequest] = new object[] { actor, pending.Value.At },
        });
        Plugin.Verbose($"Asked {actor} to swap places.");
        CoopNotice.Show(NoticeKind.Memo, Strings.SwapAsked(partner.characterName));
    }

    private static void Withdraw(string? notice)
    {
        pending = null;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { [RoomKeys.SwapRequest] = null });
        if (notice != null)
        {
            CoopNotice.Show(NoticeKind.Memo, notice);
        }
    }

    internal static void OnJoinedRoom()
    {
        pending = null;
        partnerOffered = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoomKeys.SwapRequest))
        {
            Withdraw(null);
        }
    }

    /// <summary>Lapses the local request: after 30 seconds, when the Pair changes, or for a Pair Shuffle.</summary>
    internal static void Tick()
    {
        if (pending is not (int partner, double at))
        {
            return;
        }

        Character local = Character.localCharacter;
        if (!RunRules.AllowSwapRequests)
        {
            Withdraw(Strings.SwapNotAllowed);
        }
        else if (PairShuffle.Current is { Done: false, IsShuffle: true })
        {
            Withdraw(Strings.SwapCancelledByShuffle);
        }
        else if (ServerClock.Since(PhotonNetwork.Time, at) > RequestSeconds)
        {
            Withdraw(Strings.SwapExpired);
        }
        else if (local == null
            || local.data.dead
            || !GameHandler.IsOnIsland
            || !PairRoster.TryGetPair(local, out Pair pair)
            || pair.PartnerOf(PairRoster.ActorOf(local)) != partner)
        {
            Withdraw(Strings.SwapCancelled);
        }
    }

    /// <summary>On the Troop Leader: a Pair whose partners both ask in time swaps places.</summary>
    internal static void HostTick()
    {
        if (PairShuffle.Busy || !RunRules.AllowSwapRequests)
        {
            return;
        }

        double now = PhotonNetwork.Time;
        foreach (Pair pair in PairRoster.Pairs)
        {
            PhotonPlayer? carrier = PhotonNetwork.CurrentRoom.GetPlayer(pair.Carrier);
            PhotonPlayer? rider = PhotonNetwork.CurrentRoom.GetPlayer(pair.Rider);
            if (carrier == null
                || rider == null
                || !Asks(carrier, pair.Rider, now)
                || !Asks(rider, pair.Carrier, now)
                || !PairShuffle.Request(OperationKind.RoleSwap, pair.Carrier, pair.Rider))
            {
                continue;
            }

            var clear = new PhotonHashtable { [RoomKeys.SwapRequest] = null };
            carrier.SetCustomProperties(clear);
            rider.SetCustomProperties(clear);
            return;
        }
    }

    private static bool Asks(PhotonPlayer player, int partner, double now) =>
        ReadRequest(player) is (int asked, double at) && asked == partner && ServerClock.IsRecent(now, at, RequestSeconds);

    private static (int Partner, double At)? ReadRequest(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.SwapRequest, out object value)
            && value is object[] { Length: 2 } parts
            && parts[0] is int partner
            && parts[1] is double at
            && ServerClock.IsTime(at)
                ? (partner, at)
                : null;

    /// <summary>Tells the partner about a request, and follows the Troop Leader clearing the local one.</summary>
    internal static void OnRequestChanged(PhotonPlayer player, PhotonHashtable changed)
    {
        if (!changed.ContainsKey(RoomKeys.SwapRequest))
        {
            return;
        }

        if (player.IsLocal)
        {
            if (ReadRequest(player) == null)
            {
                pending = null;
            }

            return;
        }

        Character local = Character.localCharacter;
        if (local == null
            || !PairRoster.TryGetPair(local, out Pair pair)
            || pair.PartnerOf(PairRoster.ActorOf(local)) != player.ActorNumber)
        {
            return;
        }

        int me = PairRoster.ActorOf(local);
        if (ReadRequest(player) is (int asked, double at) && asked == me && ServerClock.IsRecent(PhotonNetwork.Time, at, RequestSeconds))
        {
            partnerOffered = true;
            if (pending == null)
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.SwapOffered(KnownPlayers.NameOf(player, player.ActorNumber)));
            }

            return;
        }

        if (partnerOffered)
        {
            partnerOffered = false;
            bool swapping = PairShuffle.Current is { Kind: OperationKind.RoleSwap } op
                && op.Involves(me)
                && ServerClock.Since(PhotonNetwork.Time, op.At) < Operation.CountdownSeconds + 2;
            if (!swapping)
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.SwapWithdrawn(KnownPlayers.NameOf(player, player.ActorNumber)));
            }
        }
    }
}
