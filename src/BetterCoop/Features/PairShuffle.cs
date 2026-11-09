using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;
using Zorro.Core.Serizalization;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Pair Shuffles, Role Swaps, and Late Pairings (ADR 0004). The Troop Leader runs one
/// operation at a time: announces it with a server time, refuses pickups
/// from just before it, then writes the new Pairs and their Body Handover in
/// one room property write. Every client counts down, empties the hands it
/// owns, and applies its own part of the handover once per operation.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class PairShuffle
{
    /// <summary>How long after an operation's time hands stay locked without its handover.</summary>
    private const double LockTimeoutSeconds = 5;

    /// <summary>How far a Rider may be from the new Carrier before moving to them.</summary>
    private const float RiderReach = 3f;

    private static readonly OperationQueue Queue = new();

    /// <summary>The last operation this client published as host, ahead of the room's echo.</summary>
    private static Operation? published;

    /// <summary>The Pairs of the last Pair Assignment this client published as host, ahead of the room's echo.</summary>
    private static object[]? publishedAssignment;

    private static int executedId;

    private static int appliedHandoverId;

    private static int countdownId;

    private static int countdownShown;

    private static int emptiedHandsId;

    private static float nextLatePairingCheck;

    /// <summary>The latest operation this client told other mods had started, and had finished (ADR 0008).</summary>
    private static int startedId;

    private static int finishedId;

    private static readonly ArrivalClock Arrivals = new();

    /// <summary>The latest operation: the room's, or this host's newer one not yet echoed back.</summary>
    internal static Operation? Current
    {
        get
        {
            Operation? room = RoomOperation();
            bool newer = published != null
                && (room == null || published.Id > room.Id || (published.Id == room.Id && published.Done));
            return newer ? published : room;
        }
    }

    /// <summary>The room's operation, decoded once for each value the room holds: it is read several times a frame.</summary>
    private static Operation? RoomOperation()
    {
        object? value = RoomProtocol.Get(RoomKeys.Operation);
        if (!ReferenceEquals(value, decodedOperationFrom))
        {
            decodedOperationFrom = value;
            decodedOperation = value == null ? null : Operation.Decode(value);
        }

        return decodedOperation;
    }

    private static object? decodedOperationFrom;

    private static Operation? decodedOperation;

    /// <summary>
    /// Whether the room shows the operation this host last wrote, and with it
    /// the Pairs it wrote: until the server echoes a write back, the room
    /// still shows the Pairs from before.
    /// </summary>
    private static bool Echoed =>
        published == null
        || (RoomOperation() is Operation room
            && (room.Id > published.Id || (room.Id == published.Id && room.Done == published.Done)));

    /// <summary>Whether an operation is announced and not yet done.</summary>
    internal static bool Busy => Current is { Done: false };

    /// <summary>Whether an operation on this player ran in the last few seconds, so its Body Handover may still be arriving.</summary>
    internal static bool HandingOver(int actor) =>
        Current is { Done: true, CalledOff: false } op && op.Involves(actor) && ServerClock.Since(PhotonNetwork.Time, op.At) < LockTimeoutSeconds;

    /// <summary>Whether the local player's hands stay empty for an operation now.</summary>
    /// <summary>Whether an operation on this player is about to run or running, so their Pair is changing.</summary>
    internal static bool Locked(int actor) => IsLocked(Current, actor, PhotonNetwork.Time);

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
        publishedAssignment = null;
        executedId = 0;
        Queue.Clear();
        appliedHandoverId = ReadHandover()?.Id ?? 0;
        countdownId = Current?.Id ?? 0;
        calledOffShown = countdownId;
        emptiedHandsId = countdownId;
        startedId = countdownId;
        finishedId = countdownId;
        hostClock = null;
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
        TellOtherMods(op);
        ShowCountdown(op);
        EmptyHandsBeforeRun(op);
        ApplyHandover();
        RoleSwap.Tick();
        if (PhotonNetwork.IsMasterClient)
        {
            HostTick(op);
        }
    }

    /// <summary>Seconds until the next Timed Shuffle, or null when none is coming.</summary>
    internal static double? SecondsToTimedShuffle()
    {
        if (!PhotonNetwork.InRoom || !RunRules.TimedShuffle || !PairRoster.Formed || ReadClock() is not double clock)
        {
            return null;
        }

        return Math.Max(0, RunRules.TimedShuffleSeconds - ServerClock.Since(PhotonNetwork.Time, clock));
    }

    /// <summary>The Troop Leader asks for a Pair Shuffle, Role Swap, or Late Pairing.</summary>
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

    /// <summary>
    /// The Troop Leader asks for a Pair Assignment setting these Pairs. They
    /// are written with the announcement, so a new Troop Leader can run it.
    /// </summary>
    internal static bool RequestAssignment(IReadOnlyList<Pair> pairs)
    {
        if (!PhotonNetwork.IsMasterClient || !PairRoster.Formed)
        {
            return false;
        }

        Operation? op = Queue.Request(Current, OperationKind.PairAssignment, PhotonNetwork.Time);
        if (op == null)
        {
            return false;
        }

        publishedAssignment = Arrangement.Encode(op.Id, pairs);
        Publish(op, new PhotonHashtable { [RoomKeys.Assignment] = publishedAssignment });
        return true;
    }

    /// <summary>Whether the Troop Leader could call off a Pair Shuffle now.</summary>
    internal static bool CanCallOff =>
        PhotonNetwork.IsMasterClient && PairRoster.Formed && Operation.CallOff(Current, SecondsToTimedShuffle() != null, PhotonNetwork.Time) != null;

    /// <summary>
    /// As the Troop Leader, calls off the coming Pair Shuffle: the one counting
    /// down now, or the next Timed Shuffle. Either way the Timed Shuffle counts
    /// again from the start, and everyone is told.
    /// </summary>
    internal static bool CallOff()
    {
        if (!PhotonNetwork.IsMasterClient
            || !PairRoster.Formed
            || Operation.CallOff(Current, SecondsToTimedShuffle() != null, PhotonNetwork.Time) is not Operation called)
        {
            return false;
        }

        Queue.Clear();
        published = called;
        hostClock = PhotonNetwork.Time;
        RoomWrites.Set(new PhotonHashtable { [RoomKeys.Operation] = called.Encode(), [RoomKeys.ShuffleClock] = hostClock });
        Plugin.Verbose($"Called off {called.Kind} #{called.Id}.");
        return true;
    }

    /// <summary>Whether a Pair Assignment setting these Pairs could run now.</summary>
    internal static bool CanArrange(IReadOnlyList<Pair> pairs) =>
        PairRoster.Formed && PairRoster.Read() is PairingResult before && Arranged(before, pairs) != null;

    private static void Publish(Operation op, PhotonHashtable? with = null)
    {
        published = op;
        PhotonHashtable properties = with ?? new PhotonHashtable();
        properties[RoomKeys.Operation] = op.Encode();
        RoomWrites.Set(properties);
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
            hostClock = null;
            if (op is { Done: false })
            {
                // Pairs end at the Airport; so does an operation on them.
                published = op.Finished();
                RoomWrites.Set(new PhotonHashtable { [RoomKeys.Operation] = published.Encode() });
            }

            return;
        }

        double now = PhotonNetwork.Time;
        // Only an operation this Troop Leader announced, or found announced on taking over, runs.
        if (op is { Done: false } && ServerClock.Since(now, op.At) >= 0 && op.Id > executedId && (op == published || RoomWrites.IsOwn(RoomKeys.Operation)))
        {
            Execute(op);
            return;
        }

        if (RunRules.TimedShuffle && PairRoster.Formed && (!Busy || !op!.IsShuffle))
        {
            if (ReadClock() is not double clock)
            {
                StartClock();
            }
            else if (ServerClock.Since(now, clock) >= RunRules.TimedShuffleSeconds - Operation.CountdownSeconds)
            {
                Request(OperationKind.TimedShuffle);
            }
        }

        if (!Busy && Echoed && Time.unscaledTime >= nextLatePairingCheck)
        {
            nextLatePairingCheck = Time.unscaledTime + 1f;
            RequestLatePairing();
        }

        RoleSwap.HostTick();
    }

    /// <summary>Announces a Late Pairing when two Unpaired Players can be paired.</summary>
    private static void RequestLatePairing()
    {
        if (RunRules.LatePairing
            && PairRoster.Formed
            && PairRoster.Read() is PairingResult current
            && LatePairingRules.PickTwo(LateCandidates(current), FormerPairs.InRoom()) is (int first, int second))
        {
            Request(OperationKind.LatePairing, first, second);
        }
    }

    /// <summary>
    /// Players outside every Pair who run this version and whose character
    /// has settled: other mods move and revive a player who has just joined.
    /// </summary>
    private static List<LateCandidate> LateCandidates(PairingResult current)
    {
        var candidates = new List<LateCandidate>();
        foreach (PhotonPlayer player in PhotonNetwork.PlayerList)
        {
            if (player.IsInactive
                || !RoomProtocol.Speaks(player)
                || current.Pairs.Any(p => p.Contains(player.ActorNumber))
                || CharacterOf(player.ActorNumber) is not Character character
                || !Arrivals.Settled(player.ActorNumber, character.GetInstanceID(), Time.unscaledTime))
            {
                continue;
            }

            bool awake = !character.data.dead && !character.data.passedOut && !character.data.fullyPassedOut;
            candidates.Add(new LateCandidate(player.ActorNumber, ModPresence.PairingRoleOf(player), awake, character.Center.y));
        }

        return candidates;
    }

    private static void Execute(Operation op)
    {
        executedId = op.Id;
        PairingResult? before = PairRoster.Read();
        PairingResult? after = null;
        HandoverPlan? plan = null;
        if (before != null && op.Kind == OperationKind.LatePairing)
        {
            after = LatePair(before, op, out plan);
        }
        else if (before != null && op.Kind == OperationKind.PairAssignment)
        {
            after = Assign(before, op, out plan);
        }
        else if (before != null)
        {
            after = op.IsShuffle ? Shuffle(before) : Swap(before, op);
            plan = after == null ? null : PlanHandover(op.Id, before.Pairs, after.Pairs);
        }

        var properties = new PhotonHashtable { [RoomKeys.Operation] = op.Finished().Encode() };
        if (op.IsShuffle)
        {
            hostClock = PhotonNetwork.Time;
            properties[RoomKeys.ShuffleClock] = hostClock;
        }

        if (after != null && plan != null)
        {
            properties[RoomKeys.Pairs] = PairRoster.Encode(after);
            properties[RoomKeys.Handover] = plan.Encode();
            if (op.Kind == OperationKind.LatePairing)
            {
                properties[RoomKeys.Parted] = FormerPairs.HostCopy().Encode();
            }

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
        RoomWrites.Set(properties);
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
                && RoomProtocol.Speaks(p)
                && IsAlive(p.ActorNumber)
                && !kept.Any(k => k.Contains(p.ActorNumber)))
            .Select(p => new PairCandidate(p.ActorNumber, ModPresence.PairingRoleOf(p)))
            .ToList();
        PairingResult formed = PairingRules.Reshuffle(candidates, new System.Random(), before.Pairs, before.Unpaired);
        var pairs = kept.Concat(formed.Pairs).ToList();
        var unpaired = PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => !pairs.Any(p => p.Contains(a)))
            .ToList();
        return new PairingResult(pairs, unpaired, formed.PreferenceOverridden);
    }

    /// <summary>
    /// The two of a Late Pairing as a new Pair, when both are still outside
    /// every Pair and one is awake. Nothing moves but the Shared Body, to the
    /// higher member's place; the new Rider's statuses clear as at any pairing.
    /// </summary>
    private static PairingResult? LatePair(PairingResult before, Operation op, out HandoverPlan? plan)
    {
        plan = null;
        List<LateCandidate> candidates = LateCandidates(before);
        LateCandidate? first = candidates.FirstOrDefault(c => c.Actor == op.SwapCarrier);
        LateCandidate? second = candidates.FirstOrDefault(c => c.Actor == op.SwapRider);
        IReadOnlyList<Pair> former = FormerPairs.InRoom();
        if (first == null
            || second == null
            || LatePairingRules.Form(first, second, new System.Random(), former) is not LatePairing pairing)
        {
            return null;
        }

        if (former.Any(p => p.Contains(first.Actor) && p.Contains(second.Actor))
            && KnownPlayers.UserIdOf(first.Actor) is string firstId
            && KnownPlayers.UserIdOf(second.Actor) is string secondId)
        {
            Plugin.Verbose($"Late Pairing reunites {first.Actor} and {second.Actor}.");
            FormerPairs.HostCopy().Remove(firstId, secondId);
        }

        Pair pair = pairing.Pair;
        var pairs = before.Pairs.Append(pair).ToList();
        var unpaired = PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => !pairs.Any(p => p.Contains(a)))
            .ToList();
        var overridden = before.PreferenceOverridden
            .Where(a => !pair.Contains(a))
            .Concat(pairing.PreferenceOverridden)
            .ToList();

        var entries = new List<HandoverEntry>();
        if (pairing.CarrierMovesToRider && CharacterOf(pair.Rider) is Character riderBody)
        {
            Vector3 place = riderBody.Center;
            entries.Add(new HandoverEntry(pair.Carrier, pair.Rider, [place.x, place.y, place.z], [], 0f, 0f));
        }

        plan = new HandoverPlan(op.Id, movesPlaces: true, entries);
        return new PairingResult(pairs, unpaired, overridden);
    }

    /// <summary>
    /// The Pairs the Troop Leader set, checked again now: the Pairs may have
    /// changed since, as when a member left. Each new Carrier keeps their body
    /// and place, and their Rider comes to them, the new Rider's statuses
    /// cleared as at any pairing. A Pair only turned round hands its body over
    /// as a Role Swap does.
    /// </summary>
    private static PairingResult? Assign(PairingResult before, Operation op, out HandoverPlan? plan)
    {
        plan = null;
        object? stored = op.Id == Arrangement.IdOf(publishedAssignment)
            ? publishedAssignment
            : RoomProtocol.Get(RoomKeys.Assignment);
        if (Arrangement.Decode(stored, op.Id) is not IReadOnlyList<Pair> wanted
            || Arranged(before, wanted) is not PairingResult after)
        {
            return null;
        }

        IReadOnlyList<(Pair Before, Pair After)> turned = PairingRules.TurnedRound(before.Pairs, after.Pairs);
        plan = turned.Count == 0
            ? new HandoverPlan(op.Id, movesPlaces: false, [])
            : PlanHandover(op.Id, turned.Select(t => t.Before).ToList(), turned.Select(t => t.After).ToList());
        return after;
    }

    private static PairingResult? Arranged(PairingResult before, IReadOnlyList<Pair> wanted) =>
        PairingRules.Arrange(
            before,
            wanted,
            PhotonNetwork.PlayerList.Select(p => p.ActorNumber),
            IsEligible,
            IsAlive,
            actor => CharacterOf(actor) is Character body && !body.data.dead && !body.data.passedOut && !body.data.fullyPassedOut);

    private static bool IsEligible(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor) is PhotonPlayer player
        && !player.IsInactive
        && RoomProtocol.Speaks(player)
        && CharacterOf(actor) != null;

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

    private static void TellOtherMods(Operation? op)
    {
        if (op == null)
        {
            return;
        }

        if (op.CalledOff && op.Id > startedId)
        {
            // Called off before it started here: nothing to tell.
            startedId = finishedId = op.Id;
            return;
        }

        if (op.Id > startedId)
        {
            startedId = op.Id;
            Api.RaisePairChangeStarting(op.Kind);
        }

        if (op.Done && op.Id > finishedId)
        {
            finishedId = op.Id;
            Api.RaisePairChangeFinished(op.Kind);
        }
    }

    private static int calledOffShown;

    private static void ShowCountdown(Operation? op)
    {
        if (op is { CalledOff: true } && op.Id > calledOffShown)
        {
            calledOffShown = op.Id;
            double? next = SecondsToTimedShuffle();
            CoopNotice.Info(next is double seconds ? Strings.ShuffleCalledOffUntil(Strings.Clock(seconds)) : Strings.ShuffleCalledOff);
        }

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
        if (PairLetters.Follow(op, left))
        {
            return;
        }

        if (left > 0 && left != countdownShown)
        {
            countdownShown = left;
            CoopNotice.Info(op.Kind switch
            {
                OperationKind.RoleSwap => Strings.RoleSwapCountdown(left),
                OperationKind.LatePairing => Strings.LatePairingCountdown(left),
                OperationKind.PairAssignment => Strings.AssignmentCountdown(left),
                _ => Strings.ShuffleCountdown(left),
            });
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
        && ServerClock.Since(now, op.At) < LockTimeoutSeconds
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
        if (forceSync
            && __instance.view.IsMine
            && character != null
            && character.data.currentItem != null
            && !StillHolds(__instance, character))
        {
            Plugin.Verbose("The inventory was handed over; putting the held item away.");
            character.refs.items.EquipSlot(Optionable<byte>.None);
        }
    }

    /// <summary>Whether the selected slot still holds the item in hand, as when only the back slot changed.</summary>
    private static bool StillHolds(Player player, Character character)
    {
        Optionable<byte> selected = character.refs.items.currentSelectedSlot;
        ItemSlot? slot = selected.IsSome ? player.GetItemSlot(selected.Value) : null;
        Item held = character.data.currentItem;
        return slot != null
            && !slot.IsEmpty()
            && slot.data != null
            && held.data != null
            && slot.data.guid == held.data.guid;
    }

    /// <summary>The room's latest handover, decoded once for each value the room holds: it is looked at every frame.</summary>
    private static HandoverPlan? ReadHandover()
    {
        object? value = RoomProtocol.Get(RoomKeys.Handover);
        if (!ReferenceEquals(value, decodedHandoverFrom))
        {
            decodedHandoverFrom = value;
            decodedHandover = value == null ? null : HandoverPlan.Decode(value);
        }

        return decodedHandover;
    }

    private static object? decodedHandoverFrom;

    private static HandoverPlan? decodedHandover;

    /// <summary>
    /// When the Timed Shuffle interval started: the room's clock, or the
    /// later one this host wrote and has not had back yet, so one interval
    /// never runs twice.
    /// </summary>
    private static double? ReadClock()
    {
        double? room = RoomProtocol.TryGet(RoomKeys.ShuffleClock, out object value)
            && value is double clock
            && ServerClock.IsTime(clock)
            && ServerClock.Since(PhotonNetwork.Time, clock) >= -ServerClock.Skew
                ? clock
                : null;
        return ServerClock.Later(room, hostClock);
    }

    private static double? hostClock;

    /// <summary>Starts the Timed Shuffle interval now.</summary>
    private static void StartClock()
    {
        hostClock = PhotonNetwork.Time;
        RoomWrites.Set(new PhotonHashtable { [RoomKeys.ShuffleClock] = hostClock });
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
            if (entry.TakesStatuses)
            {
                TakeOverStatuses(local, entry);
            }

            if (plan.MovesPlaces)
            {
                MoveTo(local, ToVector(entry.Place));
            }

            return;
        }

        // A dead Rider comes back at the Carrier instead (Shared Life).
        if (PairRoster.RoleOf(local) == PairRole.Rider
            && !local.data.dead
            && PairRoster.TryGetPartner(local, out Character carrier))
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

/// <summary>Drives Pair Shuffles, Role Swaps, and Late Pairings.</summary>
internal sealed class PairShuffleLink : MonoBehaviourPunCallbacks
{
    public override void OnJoinedRoom()
    {
        try
        {
            PairShuffle.OnJoinedRoom();
        }
        catch (Exception exception)
        {
            Guard.Report("PairShuffle.OnJoinedRoom", exception);
        }
    }

    public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, PhotonHashtable changedProps)
    {
        try
        {
            RoleSwap.OnRequestChanged(targetPlayer, changedProps);
        }
        catch (Exception exception)
        {
            Guard.Report("RoleSwap.OnRequestChanged", exception);
        }
    }

    private void Update()
    {
        try
        {
            PairShuffle.Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("PairShuffle.Tick", exception);
        }
    }
}
