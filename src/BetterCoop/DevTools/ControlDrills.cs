using System;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Features;
using BetterCoop.Net;
using ExitGames.Client.Photon;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace BetterCoop.DevTools;

/// <summary>
/// Drills that play a misbehaving or unlucky player: sending the mod's own
/// events and values far too often, and leaving the room at an awkward
/// moment. They send only what a modded client could, never raw input to
/// this copy's game.
/// </summary>
internal static partial class ControlCommands
{
    /// <summary>The most messages one flood sends: enough to show a gate holds, not to get this copy thrown out.</summary>
    private const int MostFlood = 2000;

    /// <summary>
    /// Sends <c>count</c> of one kind of the mod's messages at once, as a
    /// misbehaving modded client could: <c>rider</c> (Rider snapshots to the
    /// Carrier, each jumping the click counters), <c>revive</c> (revive
    /// requests to the Troop Leader), <c>wheel</c> (backpack wheel openings
    /// to the Rider), <c>sound</c> (item sounds to the Rider), <c>recovery</c>
    /// (a Pair recovery as if from the Troop Leader), <c>swap</c> (Role Swap
    /// asks and withdrawals, past the Roster's pace), <c>pairs</c> (forged
    /// room Pairs).
    /// </summary>
    private static JToken Flood(JObject args)
    {
        if (!PhotonNetwork.InRoom)
        {
            throw new InvalidOperationException("not in a room");
        }

        string kind = (string?)args["kind"] ?? throw new ArgumentException("kind: rider, revive, wheel, sound, recovery, swap, pairs");
        int count = Math.Max(1, Math.Min(MostFlood, (int?)args["count"] ?? 200));
        Character me = Character.localCharacter ?? throw new InvalidOperationException("no scout of this copy");
        int partner = PairRoster.TryGetPartner(me, out Character other) ? PairRoster.ActorOf(other) : -1;
        var toPartner = new RaiseEventOptions { TargetActors = [partner] };
        int sent = 0;
        switch (kind)
        {
            case "rider":
                NeedPartner(partner);
                for (int i = 1; i <= count; i++)
                {
                    var actions = new RiderActions
                    {
                        Sequence = int.MaxValue - MostFlood + i,
                        Primary = new ButtonCounters(false, (ushort)(i * 50), (ushort)(i * 50)),
                        ScrollForward = new ButtonCounters(false, (ushort)(i * 50), 0),
                    };
                    sent += Send(RoomKeys.RiderActionsEvent, actions.Encode(), toPartner);
                }

                break;
            case "revive":
                for (int i = 0; i < count; i++)
                {
                    sent += Send(
                        RoomKeys.ReviveRequestEvent,
                        new object[] { me.photonView.ViewID, false, Vector3.zero, false, -1 },
                        new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient });
                }

                break;
            case "wheel":
                NeedPartner(partner);
                for (int i = 0; i < count; i++)
                {
                    sent += Send(RoomKeys.BackpackWheelEvent, WheelSignal.Open(i + 1, false, true, me.photonView.ViewID, 4, 0).Encode(), toPartner);
                }

                break;
            case "sound":
                NeedPartner(partner);
                for (int i = 0; i < count; i++)
                {
                    sent += Send(RoomKeys.ItemSoundEvent, new object[] { $"Flood{i % 50}", me.Center }, toPartner);
                }

                break;
            case "recovery":
                for (int i = 0; i < count; i++)
                {
                    sent += Send(RoomKeys.RecoveryEvent, null, new RaiseEventOptions { Receivers = ReceiverGroup.Others });
                }

                break;
            case "swap":
                NeedPartner(partner);
                for (int i = 0; i < count; i++)
                {
                    object? ask = i % 2 == 0 ? new object[] { partner, PhotonNetwork.Time } : null;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { [RoomKeys.SwapRequest] = ask });
                    sent++;
                }

                break;
            case "pairs":
                for (int i = 0; i < count; i++)
                {
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
                    {
                        [RoomKeys.Pairs] = new object[] { new[] { 1, 2 + i }, new int[0], new int[0] },
                    });
                    sent++;
                }

                break;
            default:
                throw new ArgumentException("kind: rider, revive, wheel, sound, recovery, swap, pairs");
        }

        PhotonNetwork.SendAllOutgoingCommands();
        return $"sent {sent} {kind}";
    }

    private static void NeedPartner(int partner)
    {
        if (partner < 0)
        {
            throw new InvalidOperationException("this copy has no partner to flood");
        }
    }

    private static int Send(byte code, object? payload, RaiseEventOptions options) =>
        PhotonNetwork.RaiseEvent(code, payload, options, SendOptions.SendReliable) ? 1 : 0;

    /// <summary>
    /// Leaves the room as a player closing the game would, now or
    /// <c>delay</c> seconds after the operation counting down runs on the
    /// Troop Leader, so the leave lands while its Pairs are still on their
    /// way back from the server.
    /// </summary>
    private static JToken Leave(JObject args)
    {
        if (!PhotonNetwork.InRoom)
        {
            throw new InvalidOperationException("not in a room");
        }

        double delay = Math.Max(0, Math.Min(5, (double?)args["delay"] ?? 0.05));
        bool atOperation = (bool?)args["atOperation"] ?? false;
        double at = PhotonNetwork.Time;
        if (atOperation)
        {
            Operation op = PairShuffle.Current is { Done: false } coming
                ? coming
                : throw new InvalidOperationException("no operation counting down");
            at = op.At + delay;
        }

        var timer = new GameObject("BetterCoop.LeaveDrill");
        UnityEngine.Object.DontDestroyOnLoad(timer);
        timer.AddComponent<LeaveTimer>().At = at;
        return atOperation ? $"leaving at server time {at:F2}" : "leaving";
    }

    /// <summary>Leaves the room once server time reaches <see cref="At"/>.</summary>
    private sealed class LeaveTimer : MonoBehaviour
    {
        public double At;

        private void Update()
        {
            if (PhotonNetwork.InRoom && ServerClock.Since(PhotonNetwork.Time, At) < 0)
            {
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                Plugin.Log.LogInfo($"Drill: leaving the room at server time {PhotonNetwork.Time:F2}.");
                PhotonNetwork.LeaveRoom();
            }

            Destroy(gameObject);
        }
    }
}
