using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PeakCoop.Core;
using PeakCoop.Net;
using PeakCoop.Ui;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Zorro.Core.Serizalization;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakCoop.Features;

/// <summary>
/// Pair Shuffles and Role Swaps (ADR 0004). The Troop Leader runs one
/// operation at a time: announces it with a server time, refuses pickups
/// from just before it, then writes the new Pairs and their Body Handover in
/// one room property write. Every client counts down, empties the hands it
/// owns, and applies its own part of the handover once per operation.
/// </summary>
[HarmonyPatch]
internal static class PairShuffle
{
    /// <summary>How long after an operation's time hands stay locked without its handover.</summary>
    private const double LockTimeoutSeconds = 5;

    /// <summary>How far a Rider may be from the new Carrier before moving to them.</summary>
    private const float RiderReach = 3f;

    private static readonly OperationQueue Queue = new();

    /// <summary>The last operation this client published as host, ahead of the room's echo.</summary>
    private static Operation? published;

    private static int executedId;

    private static int appliedHandoverId;

    private static int countdownId;

    private static int countdownShown;

    private static int emptiedHandsId;

    /// <summary>The latest operation: the room's, or this host's newer one not yet echoed back.</summary>
    internal static Operation? Current
    {
        get
        {
            Operation? room = PhotonNetwork.InRoom
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Operation, out object value)
                    ? Operation.Decode(value)
                    : null;
            bool newer = published != null
                && (room == null || published.Id > room.Id || (published.Id == room.Id && published.Done));
            return newer ? published : room;
        }
    }

    /// <summary>Whether an operation is announced and not yet done.</summary>
    internal static bool Busy => Current is { Done: false };

    /// <summary>Whether the local player's hands stay empty for an operation now.</summary>
    internal static bool LocalLocked
    {
        get
        {
            Character local = Character.localCharacter;
            return local != null && IsLocked(Current, PairRoster.ActorOf(local), PhotonNetwork.Time);
        }
    }

    internal static void OnJoinedRoom()
    {
        published = null;
        executedId = 0;
        Queue.Clear();
        appliedHandoverId = ReadHandover()?.Id ?? 0;
        countdownId = Current?.Id ?? 0;
        emptiedHandsId = countdownId;
        hostClock = double.MinValue;
        RoleSwap.OnJoinedRoom();
    }

    /// <summary>Called every frame on every client.</summary>
    internal static void Tick()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        Operation? op = Current;
        ShowCountdown(op);
        EmptyHandsBeforeRun(op);
        ApplyHandover();
        RoleSwap.Tick();
        if (PhotonNetwork.IsMasterClient)
        {
            HostTick(op);
        }
    }

    /// <summary>The Troop Leader asks for a Pair Shuffle or Role Swap.</summary>
    internal static bool Request(OperationKind kind, int swapCarrier = -1, int swapRider = -1)
    {
        if (!PhotonNetwork.IsMasterClient || !PairRoster.Formed)
        {
            return false;
        }

        Operation? op = Queue.Request(Current, kind, PhotonNetwork.Time, swapCarrier, swapRider);
        if (op == null)
        {
            return false;
        }

        Publish(op);
        return true;
    }

    private static void Publish(Operation op)
    {
        published = op;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [RoomKeys.Operation] = op.Encode() });
        Plugin.Verbose($"Announced {op.Kind} #{op.Id} at server time {op.At:F2}.");
    }

    /// <summary>A Campfire Shuffle once the next segment has been revealed.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MountainProgressHandler), nameof(MountainProgressHandler.SetSegmentComplete))]
    private static void ShuffleAtCampfire()
    {
        if (PhotonNetwork.IsMasterClient && RunRules.CampfireShuffle)
        {
            Request(OperationKind.CampfireShuffle);
        }
    }

    private static void HostTick(Operation? op)
    {
        if (!GameHandler.IsOnIsland)
        {
            Queue.Clear();
            hostClock = double.MinValue;
            if (op is { Done: false })
            {
                // Pairs end at the Airport; so does an operation on them.
                published = op.Finished();
                PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [RoomKeys.Operation] = published.Encode() });
            }

            return;
        }

        double now = PhotonNetwork.Time;
        if (op is { Done: false } && now >= op.At && op.Id > executedId)
        {
            Execute(op);
            return;
        }

        if (RunRules.TimedShuffle && PairRoster.Formed && (!Busy || op!.Kind == OperationKind.RoleSwap))
        {
            if (ReadClock() is not double clock)
            {
                StartClock();
            }
            else if (now >= clock + RunRules.TimedShuffleSeconds - Operation.CountdownSeconds)
            {
                Request(OperationKind.TimedShuffle);
            }
        }

        RoleSwap.HostTick();
    }

    private static void Execute(Operation op)
    {
        executedId = op.Id;
        PairingResult? before = PairRoster.Read();
        PairingResult? after = before == null ? null : op.IsShuffle ? Shuffle(before) : Swap(before, op);
        var properties = new PhotonHashtable { [RoomKeys.Operation] = op.Finished().Encode() };
        if (op.IsShuffle)
        {
            hostClock = PhotonNetwork.Time;
            properties[RoomKeys.ShuffleClock] = hostClock;
        }

        if (before != null && after != null)
        {
            HandoverPlan plan = PlanHandover(op.Id, before.Pairs, after.Pairs);
            properties[RoomKeys.Pairs] = PairRoster.Encode(after);
            properties[RoomKeys.Handover] = plan.Encode();
            Plugin.Verbose(
                $"{op.Kind} #{op.Id}: " + string.Join(", ", after.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"))
                + $"; {plan.Entries.Count} handover(s).");
        }
        else
        {
            Plugin.Verbose($"{op.Kind} #{op.Id} changed nothing.");
        }

        Operation finished = op.Finished();
        published = finished;
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        if (Queue.After(finished, PhotonNetwork.Time) is Operation next)
        {
            Publish(next);
        }
    }

    /// <summary>
    /// New Pairs for every living player with the mod; a Pair whose Shared
    /// Body is dead keeps together until it comes back.
    /// </summary>
    private static PairingResult? Shuffle(PairingResult before)
    {
        var kept = before.Pairs.Where(p => !IsAlive(p.Carrier) || !IsAlive(p.Rider)).ToList();
        var candidates = PhotonNetwork.PlayerList
            .Where(p => !p.IsInactive
                && ModPresence.VersionOf(p) == MyPluginInfo.PLUGIN_VERSION
                && IsAlive(p.ActorNumber)
                && !kept.Any(k => k.Contains(p.ActorNumber)))
            .Select(p => new PairCandidate(p.ActorNumber, ModPresence.RoleOf(p)))
            .ToList();
        PairingResult formed = PairingRules.FormAvoiding(candidates, new System.Random(), before.Pairs);
        var pairs = kept.Concat(formed.Pairs).ToList();
        var unpaired = PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => !pairs.Any(p => p.Contains(a)))
            .ToList();
        return new PairingResult(pairs, unpaired, formed.PreferenceOverridden);
    }

    private static PairingResult? Swap(PairingResult before, Operation op)
    {
        Pair? pair = before.Pairs.FirstOrDefault(p => p.Carrier == op.SwapCarrier && p.Rider == op.SwapRider);
        if (pair == null || !IsAlive(pair.Carrier) || !IsAlive(pair.Rider))
        {
            return null;
        }

        var pairs = before.Pairs.Select(p => p == pair ? new Pair(p.Rider, p.Carrier) : p).ToList();
        return new PairingResult(pairs, before.Unpaired, before.PreferenceOverridden.Where(a => !pair.Contains(a)).ToList());
    }

    /// <summary>
    /// Snapshots each former Shared Body a new Carrier takes over, and moves
    /// the items round when the Troop Leader hands items over. Inventories
    /// are the master's to change, so they move here; statuses and places
    /// belong to each body's owner, so they travel in the plan.
    /// </summary>
    private static HandoverPlan PlanHandover(int id, IReadOnlyList<Pair> before, IReadOnlyList<Pair> after)
    {
        var sources = BodyHandover.Sources(before, after)
            .Where(s => s.Key != s.Value && CharacterOf(s.Key) != null && CharacterOf(s.Value) != null)
            .ToDictionary(s => s.Key, s => s.Value);
        var entries = new List<HandoverEntry>();
        foreach (KeyValuePair<int, int> source in sources)
        {
            Character body = CharacterOf(source.Value)!;
            Vector3 place = body.Center;
            entries.Add(new HandoverEntry(
                source.Key,
                source.Value,
                [place.x, place.y, place.z],
                (float[])body.refs.afflictions.currentStatuses.Clone(),
                body.data.currentStamina,
                body.data.extraStamina));
        }

        if (RunRules.HandOverItems)
        {
            MoveInventories(BodyHandover.InventoryMoves(sources));
        }

        return new HandoverPlan(id, RunRules.HandOverPlace, entries);
    }

    private static void MoveInventories(IReadOnlyDictionary<int, int> moves)
    {
        // Snapshot every inventory first: the moves form a permutation.
        var snapshots = new Dictionary<int, byte[]>();
        foreach (int giver in moves.Values)
        {
            Player player = CharacterOf(giver)!.player;
            snapshots[giver] = IBinarySerializable.ToManagedArray(
                new InventorySyncData(player.itemSlots, player.backpackSlot, player.tempFullSlot));
        }

        foreach (KeyValuePair<int, int> move in moves)
        {
            Plugin.Verbose($"Handing the inventory of {move.Value} to {move.Key}.");
            CharacterOf(move.Key)!.player.photonView.RPC("SyncInventoryRPC", RpcTarget.All, snapshots[move.Value], true);
        }
    }

    private static void ShowCountdown(Operation? op)
    {
        Character local = Character.localCharacter;
        if (op == null || local == null || !op.Involves(PairRoster.ActorOf(local)))
        {
            return;
        }

        if (op.Id != countdownId)
        {
            countdownId = op.Id;
            countdownShown = 0;
        }

        int left = op.SecondsLeft(PhotonNetwork.Time);
        if (left > 0 && left != countdownShown)
        {
            countdownShown = left;
            CoopNotice.Info(op.IsShuffle ? Strings.ShuffleCountdown(left) : Strings.RoleSwapCountdown(left));
        }
    }

    private static void EmptyHandsBeforeRun(Operation? op)
    {
        Character local = Character.localCharacter;
        if (op == null || local == null || op.Id == emptiedHandsId || !IsLocked(op, PairRoster.ActorOf(local), PhotonNetwork.Time))
        {
            return;
        }

        emptiedHandsId = op.Id;
        if (local.data.currentItem != null)
        {
            Plugin.Verbose("Putting the held item away for the operation.");
            local.refs.items.EquipSlot(Optionable<byte>.None);
        }
    }

    private static bool IsLocked(Operation? op, int actor, double now) =>
        op != null
        && op.Involves(actor)
        && op.Locks(now)
        && now < op.At + LockTimeoutSeconds
        && op.Id > appliedHandoverId;

    /// <summary>No item comes to hand while an operation moves inventories.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), nameof(CharacterItems.EquipSlot))]
    private static bool NoEquipWhileLocked(CharacterItems __instance, Optionable<byte> slotID) =>
        slotID.IsNone || !__instance.character.IsLocal || !LocalLocked;

    /// <summary>The master refuses pickups by players an operation is about to hand over.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Item), nameof(Item.RequestPickup))]
    private static bool NoPickupWhileLocked(Item __instance, PhotonView characterView)
    {
        if (!PhotonNetwork.IsMasterClient
            || Current is not Operation op
            || !op.Involves(characterView.OwnerActorNr)
            || !op.Locks(PhotonNetwork.Time))
        {
            return true;
        }

        Plugin.Verbose($"Refused a pickup by {characterView.OwnerActorNr} during {op.Kind} #{op.Id}.");
        __instance.view.RPC("DenyPickupRPC", characterView.Owner);
        return false;
    }

    /// <summary>
    /// A held item is its own network object: when the master replaces the
    /// inventory under it and the hands were not emptied in time, put it away.
    /// Only a forced sync does this, which the game sends just on reconnect.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.SyncInventoryRPC))]
    private static void PutAwayAfterHandover(Player __instance, bool forceSync)
    {
        Character character = __instance.character;
        if (forceSync && __instance.view.IsMine && character != null && character.data.currentItem != null)
        {
            Plugin.Verbose("The inventory was handed over; putting the held item away.");
            character.refs.items.EquipSlot(Optionable<byte>.None);
        }
    }

    private static HandoverPlan? ReadHandover() =>
        PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.Handover, out object value)
            ? HandoverPlan.Decode(value)
            : null;

    /// <summary>
    /// When the Timed Shuffle interval started: the room's clock, or the
    /// later one this host wrote and has not had back yet, so one interval
    /// never runs twice.
    /// </summary>
    private static double? ReadClock()
    {
        double? room = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomKeys.ShuffleClock, out object value)
            && value is double clock
                ? clock
                : null;
        return room == null && hostClock == double.MinValue ? null : System.Math.Max(room ?? double.MinValue, hostClock);
    }

    private static double hostClock = double.MinValue;

    /// <summary>Starts the Timed Shuffle interval now.</summary>
    private static void StartClock()
    {
        hostClock = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable { [RoomKeys.ShuffleClock] = hostClock });
    }

    /// <summary>The local share of the latest handover, once per operation, after the Pairs reload.</summary>
    private static void ApplyHandover()
    {
        HandoverPlan? plan = ReadHandover();
        Character local = Character.localCharacter;
        if (plan == null || plan.Id <= appliedHandoverId || local == null)
        {
            return;
        }

        appliedHandoverId = plan.Id;
        int actor = PairRoster.ActorOf(local);
        if (plan.For(actor) is HandoverEntry entry)
        {
            Plugin.Verbose($"Taking over the Shared Body of {entry.Source}.");
            TakeOverStatuses(local, entry);
            if (plan.MovesPlaces)
            {
                MoveTo(local, ToVector(entry.Place));
            }

            return;
        }

        if (PairRoster.RoleOf(local) == PairRole.Rider && PairRoster.TryGetPartner(local, out Character carrier))
        {
            Vector3 target = plan.MovesPlaces && plan.For(PairRoster.ActorOf(carrier)) is HandoverEntry carried
                ? ToVector(carried.Place)
                : carrier.Center;
            if (Vector3.Distance(local.Center, target) > RiderReach)
            {
                MoveTo(local, target + Vector3.up);
            }
        }
    }

    private static void TakeOverStatuses(Character body, HandoverEntry entry)
    {
        CharacterAfflictions afflictions = body.refs.afflictions;
        int count = Mathf.Min(entry.Statuses.Length, afflictions.currentStatuses.Length);
        for (int i = 0; i < count; i++)
        {
            var type = (CharacterAfflictions.STATUSTYPE)i;
            // Weight, thorns, and arrows come from what the body carries;
            // petrification, webs, and fly traps from where it is.
            if (type is CharacterAfflictions.STATUSTYPE.Weight
                or CharacterAfflictions.STATUSTYPE.Thorns
                or CharacterAfflictions.STATUSTYPE.Arrow
                or CharacterAfflictions.STATUSTYPE.Petrify
                or CharacterAfflictions.STATUSTYPE.Web
                or CharacterAfflictions.STATUSTYPE.FlyTrap)
            {
                continue;
            }

            afflictions.SetStatus(type, entry.Statuses[i], pushStatus: false);
        }

        afflictions.PushStatuses();
        body.data.currentStamina = entry.Stamina;
        body.ClampStamina();
        body.SetExtraStamina(entry.ExtraStamina);
    }

    private static void MoveTo(Character body, Vector3 place)
    {
        if (Vector3.Distance(body.Center, place) < 0.5f)
        {
            return;
        }

        body.photonView.RPC("WarpPlayerRPC", RpcTarget.All, place, true);
    }

    private static Vector3 ToVector(float[] place) => new(place[0], place[1], place[2]);

    private static Character? CharacterOf(int actor) =>
        PlayerHandler.TryGetCharacter(actor, out Character character) && character != null ? character : null;

    private static bool IsAlive(int actor) => CharacterOf(actor) is Character character && !character.data.dead;
}

/// <summary>Drives Pair Shuffles and Role Swaps.</summary>
internal sealed class PairShuffleLink : MonoBehaviourPunCallbacks
{
    public override void OnJoinedRoom() => PairShuffle.OnJoinedRoom();

    public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, PhotonHashtable changedProps) =>
        RoleSwap.OnRequestChanged(targetPlayer, changedProps);

    private void Update() => PairShuffle.Tick();
}
