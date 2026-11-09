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

    /// <summary>
    /// Sends a death of player <c>actor</c>'s scout that this copy may not
    /// send, as a client that is neither that scout's owner nor the Troop
    /// Leader could: <c>die</c> (the game's death RPC), <c>sync</c> (a join
    /// sync marking the scout dead), or <c>consent</c> (that scout's leave to
    /// be eaten, <c>allow</c>; from the scout's own copy it is rightful and
    /// stands in for its setting). It goes to player <c>to</c> only, or to
    /// every other copy without one, so a test can aim it at a Rider alone.
    /// </summary>
    private static JToken Kill(JObject args)
    {
        if (!PhotonNetwork.InRoom)
        {
            throw new InvalidOperationException("not in a room");
        }

        int actor = (int?)args["actor"] ?? throw new ArgumentException("actor: the player whose scout dies");
        string how = (string?)args["how"] ?? "die";
        Character scout = Character.AllCharacters.FirstOrDefault(c => c != null && c.photonView.Owner?.ActorNumber == actor)
            ?? throw new ArgumentException($"no scout of player {actor}");
        Photon.Realtime.Player? to = args["to"] is JToken target
            ? PhotonNetwork.CurrentRoom.GetPlayer((int)target) ?? throw new ArgumentException($"no player {target}")
            : null;
        CharacterData d = scout.data;
        object[] payload = how switch
        {
            "die" => [],
            "sync" =>
            [
                d.passedOut, d.fullyPassedOut, true, d.isSprinting, d.currentItem?.photonView!, d.isJumping,
                d.isClimbing, d.isRopeClimbing, d.isVineClimbing, d.vinePercent, d.ropePercent, d.isCrouching,
                d.isReaching, d.heldVine?.photonView!, d.heldRope?.photonView!, d.badgeStatus,
                d.cannibalismPermitted, d.isSkeleton, d.petrifyAmount,
            ],
            "consent" => [(bool?)args["allow"] ?? true],
            _ => throw new ArgumentException("how: die, sync or consent"),
        };
        string rpc = how switch { "die" => "RPCA_Die", "sync" => "RPC_SyncOnJoin", _ => "RPCA_SyncCanBeCannibalized" };
        if (how == "consent" && scout.IsLocal)
        {
            d.cannibalismPermitted = (bool)payload[0];
        }

        if (to != null)
        {
            scout.photonView.RPC(rpc, to, payload);
        }
        else
        {
            scout.photonView.RPC(rpc, RpcTarget.Others, payload);
        }

        PhotonNetwork.SendAllOutgoingCommands();
        return $"sent a {how} of {scout.characterName} to {(to == null ? "every other copy" : $"player {to.ActorNumber}")}";
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
        // A Twin left to itself joins again a few seconds later; held, it waits for 'room join'.
        Twin.Held = (bool?)args["hold"] ?? false;
        var timer = new GameObject("BetterCoop.LeaveDrill");
        UnityEngine.Object.DontDestroyOnLoad(timer);
        LeaveTimer leave = timer.AddComponent<LeaveTimer>();
        leave.Delay = delay;
        leave.AtOperation = atOperation;
        leave.ArmedAt = Time.unscaledTime;
        return atOperation ? $"leaving {delay:F2} s after the next operation runs" : "leaving";
    }

    /// <summary>
    /// Leaves the room now, or once an operation counting down, the one now
    /// or the next to be announced, has run plus <see cref="Delay"/>.
    /// </summary>
    private sealed class LeaveTimer : MonoBehaviour
    {
        /// <summary>How long it waits for an operation to be announced before giving up.</summary>
        private const float WaitSeconds = 60f;

        public double Delay;

        public bool AtOperation;

        public float ArmedAt;

        private double? at;

        private void Update()
        {
            if (!PhotonNetwork.InRoom || (AtOperation && Time.unscaledTime - ArmedAt > WaitSeconds && at == null))
            {
                Destroy(gameObject);
                return;
            }

            if (AtOperation && at == null)
            {
                if (PairShuffle.Current is not { Done: false } coming)
                {
                    return;
                }

                at = coming.At + Delay;
                Plugin.Log.LogInfo($"Drill: will leave at server time {at:F2}, after {coming.Kind} #{coming.Id}.");
            }

            if (at is double when && ServerClock.Since(PhotonNetwork.Time, when) < 0)
            {
                return;
            }

            Plugin.Log.LogInfo($"Drill: leaving the room at server time {PhotonNetwork.Time:F2}.");
            PhotonNetwork.LeaveRoom();
            Destroy(gameObject);
        }
    }
}
