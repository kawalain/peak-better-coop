using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>A character's place in the Pairs of the current Run.</summary>
internal enum PairRole
{
    None,
    Carrier,
    Rider,
}

/// <summary>
/// The Pairs of the current Run as every client sees them: read from the room
/// property the host writes (ADR 0001). Pairs exist only on the island.
/// </summary>
internal static class PairRoster
{
    private static readonly Dictionary<int, Pair> ByActor = new();

    private static PairingResult? published;

    /// <summary>True when the host has formed Pairs for the Run on this island.</summary>
    public static bool Formed => published != null && GameHandler.IsOnIsland;

    public static IReadOnlyList<Pair> Pairs => Formed ? published!.Pairs : [];

    public static PairRole RoleOf(Character? character)
    {
        if (!TryGetPair(character, out Pair pair))
        {
            return PairRole.None;
        }

        return pair.Carrier == ActorOf(character!) ? PairRole.Carrier : PairRole.Rider;
    }

    public static bool TryGetPair(Character? character, out Pair pair)
    {
        pair = null!;
        if (character == null || character.isBot || !Formed)
        {
            return false;
        }

        return ByActor.TryGetValue(ActorOf(character), out pair!);
    }

    /// <summary>The partner's character, when both are in the room with a character.</summary>
    public static bool TryGetPartner(Character? character, out Character partner)
    {
        partner = null!;
        return TryGetPair(character, out Pair pair)
            && PlayerHandler.TryGetCharacter(pair.PartnerOf(ActorOf(character!)), out partner)
            && partner != null;
    }

    /// <summary>The Carrier and Rider of the local player's Pair.</summary>
    public static bool TryGetLocalPair(out Character carrier, out Character rider)
    {
        carrier = null!;
        rider = null!;
        Character local = Character.localCharacter;
        if (!TryGetPair(local, out Pair pair))
        {
            return false;
        }

        return PlayerHandler.TryGetCharacter(pair.Carrier, out carrier) && carrier != null
            && PlayerHandler.TryGetCharacter(pair.Rider, out rider) && rider != null;
    }

    public static int ActorOf(Character character) => character.photonView.Owner.ActorNumber;

    /// <summary>The Pairs as the room shows them, which every client follows.</summary>
    public static PairingResult? Read() => RoomProtocol.TryGet(RoomKeys.Pairs, out object value) ? Decode(value) : null;

    /// <summary>
    /// The Pairs a change works from, on the Troop Leader: the ones it last
    /// wrote. The room shows a write only after a server round trip, so a
    /// change worked out from the room before then, such as a Pair dissolved
    /// as a member leaves just after a Pair Shuffle, would write the old Pairs
    /// back over the new ones.
    /// </summary>
    public static PairingResult? ReadLeading() =>
        RoomWrites.TryOwn(RoomKeys.Pairs, out object? value) ? Decode(value) : Read();

    private static PairingResult? Decode(object? value)
    {
        if (value is not object[] { Length: 3 } parts
            || parts[0] is not int[] flat
            || parts[1] is not int[] unpaired
            || parts[2] is not int[] overridden
            || flat.Length > 2 * WireLimits.MaxScouts
            || unpaired.Length > WireLimits.MaxScouts
            || overridden.Length > WireLimits.MaxScouts)
        {
            return null;
        }

        var pairs = new List<Pair>();
        for (int i = 0; i + 1 < flat.Length; i += 2)
        {
            pairs.Add(new Pair(flat[i], flat[i + 1]));
        }

        return new PairingResult(PairingRules.Distinct(pairs), unpaired, overridden);
    }

    /// <summary>The room property value of <paramref name="result"/>, for writing it with other keys.</summary>
    public static object? Encode(PairingResult? result) => result == null
        ? null
        : new object[]
        {
            result.Pairs.SelectMany(p => new[] { p.Carrier, p.Rider }).ToArray(),
            result.Unpaired.ToArray(),
            result.PreferenceOverridden.ToArray(),
        };

    /// <summary>Reloads the cache; returns the previous value.</summary>
    public static PairingResult? Reload()
    {
        PairingResult? previous = published;
        published = Read();
        ByActor.Clear();
        if (published != null)
        {
            foreach (Pair pair in published.Pairs)
            {
                ByActor[pair.Carrier] = pair;
                ByActor[pair.Rider] = pair;
            }
        }

        return previous;
    }
}

/// <summary>
/// The Pair Plan as every client sees it: the room property the Troop Leader
/// writes at the Airport, used and cleared by the Pair Formation.
/// </summary>
internal static class PairPlans
{
    public static IReadOnlyList<Pair> Read()
    {
        if (!RoomProtocol.TryGet(RoomKeys.Plan, out object value)
            || value is not int[] flat
            || flat.Length > 2 * WireLimits.MaxScouts)
        {
            return [];
        }

        // The Roster asks every frame; the value is read again only when it changes.
        if (!ReferenceEquals(flat, readPlanFrom))
        {
            var plan = new List<Pair>();
            for (int i = 0; i + 1 < flat.Length; i += 2)
            {
                plan.Add(new Pair(flat[i], flat[i + 1]));
            }

            readPlanFrom = flat;
            readPlan = plan;
        }

        return readPlan;
    }

    private static int[]? readPlanFrom;

    private static IReadOnlyList<Pair> readPlan = [];

    /// <summary>As the Troop Leader at the Airport, announces the plan to the room; false when it cannot be set now.</summary>
    public static bool Announce(IReadOnlyList<Pair> plan)
    {
        if (!PhotonNetwork.InRoom
            || !PhotonNetwork.IsMasterClient
            || GameHandler.IsOnIsland
            || !PairingRules.IsPlan(plan, PhotonNetwork.PlayerList.Select(p => p.ActorNumber)))
        {
            return false;
        }

        object? flat = plan.Count == 0 ? null : plan.SelectMany(p => new[] { p.Carrier, p.Rider }).ToArray();
        RoomWrites.Set(new PhotonHashtable { [RoomKeys.Plan] = flat });
        Plugin.Verbose($"Announced a Pair Plan: {string.Join(", ", plan.Select(p => $"{p.Carrier}>{p.Rider}"))}.");
        return true;
    }

    /// <summary>Where the last Run's Pairs are kept between games.</summary>
    private static string LastRunFile => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "in.kawala.bettercoop.last-pairs.txt");

    /// <summary>Keeps the Pairs a Run ended with, by user id, for the next plan to start from.</summary>
    public static void RememberLastRun(PairingResult ended)
    {
        var pairs = ended.Pairs
            .Select(p => (Carrier: KnownPlayers.UserIdOf(p.Carrier), Rider: KnownPlayers.UserIdOf(p.Rider)))
            .Where(p => p.Carrier != null && p.Rider != null)
            .Select(p => (p.Carrier!, p.Rider!))
            .ToList();
        if (pairs.Count == 0)
        {
            return;
        }

        try
        {
            System.IO.File.WriteAllText(LastRunFile, LastRunPairs.Encode(pairs));
            Plugin.Verbose($"Kept the {pairs.Count} Pair(s) this Run ended with.");
        }
        catch (Exception exception)
        {
            Guard.Report("PairPlans.RememberLastRun", exception);
        }
    }

    /// <summary>The last Run's Pairs whose two members are both in the room now.</summary>
    public static IReadOnlyList<Pair> LastRun()
    {
        if (!PhotonNetwork.InRoom || !System.IO.File.Exists(LastRunFile))
        {
            return [];
        }

        try
        {
            return LastRunPairs.Map(
                LastRunPairs.Decode(System.IO.File.ReadAllText(LastRunFile)),
                id => PhotonNetwork.PlayerList.FirstOrDefault(p => !p.IsInactive && KnownPlayers.IdOf(p) == id)?.ActorNumber);
        }
        catch (Exception exception)
        {
            Guard.Report("PairPlans.LastRun", exception);
            return [];
        }
    }

    /// <summary>A plan drawn at random from the players who run this version, as the Pair Formation would form them.</summary>
    public static IReadOnlyList<Pair> Draw() => PairingRules.Form(
        PhotonNetwork.PlayerList
            .Where(p => !p.IsInactive && RoomProtocol.Speaks(p))
            .Select(p => new PairCandidate(p.ActorNumber, ModPresence.PairingRoleOf(p)))
            .ToList(),
        new System.Random()).Pairs;
}

/// <summary>
/// Pairs that ended because a member left (<see cref="PartedPairs"/>), as
/// the Troop Leader keeps them: its own copy is ahead of the room's echo,
/// so two Pairs parting in quick succession are both kept.
/// </summary>
internal static class FormerPairs
{
    private static PartedPairs? hostCopy;

    /// <summary>The Troop Leader's copy, loaded from the room the first time.</summary>
    public static PartedPairs HostCopy() => hostCopy ??= PartedPairs.Decode(RoomProtocol.Get(RoomKeys.Parted));

    /// <summary>Drops the copy; a client that becomes Troop Leader later reloads it from the room.</summary>
    public static void Forget() => hostCopy = null;

    /// <summary>The former Pairs whose two players are both in the room now.</summary>
    public static IReadOnlyList<Pair> InRoom() => HostCopy().Resolve(userId =>
        PhotonNetwork.PlayerList.FirstOrDefault(p => !p.IsInactive && KnownPlayers.IdOf(p) == userId)?.ActorNumber);
}

/// <summary>
/// Forms the Pairs once per Run on the host, dissolves a Pair when a member
/// leaves, clears them back at the Airport, and announces changes on every client.
/// </summary>
internal sealed class PairingDirector : MonoBehaviourPunCallbacks
{
    private const float SpawnWaitSeconds = 30f;

    private float? windowOpenedAt;

    private float nextTick;

    /// <summary>
    /// Set once this host has written the Pairs for the island. The room
    /// property arrives back only after a server round trip, and pairing twice
    /// could swap roles mid-carry.
    /// </summary>
    private bool formedOnThisIsland;

    public override void OnJoinedRoom()
    {
        try
        {
            PairRoster.Reload();
            Api.RaisePairsChanged();
            FormerPairs.Forget();
            foreach (PhotonPlayer player in PhotonNetwork.PlayerList)
            {
                KnownPlayers.Remember(player);
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnJoinedRoom", exception);
        }
    }

    public override void OnLeftRoom()
    {
        try
        {
            PairRoster.Reload();
            Api.RaisePairsChanged();
            FormerPairs.Forget();
            KnownPlayers.Clear();
            CarryRecords.OnLeftRoom();
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnLeftRoom", exception);
        }
    }

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        try
        {
            KnownPlayers.Remember(newPlayer);
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnPlayerEnteredRoom", exception);
        }
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable changed)
    {
        try
        {
            CarryRecords.OnRoomPropertiesChanged(changed);
            RoomPropertiesChanged(changed);
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnRoomPropertiesUpdate", exception);
        }
    }

    private const float PlanNoticeSeconds = 10f;

    private static float nextPlanNotice;

    private static void RoomPropertiesChanged(PhotonHashtable changed)
    {
        if (changed.ContainsKey(RoomKeys.Protocol))
        {
            // The room took another protocol (ADR 0009): what this client read of it no longer holds.
            PairRoster.Reload();
            Api.RaisePairsChanged();
            return;
        }

        if (!RoomProtocol.Readable)
        {
            return;
        }

        if (changed.ContainsKey(RoomKeys.Plan)
            && changed[RoomKeys.Plan] != null
            && !GameHandler.IsOnIsland
            && !PhotonNetwork.IsMasterClient
            && Time.unscaledTime >= nextPlanNotice)
        {
            // A plan announced again and again tells everyone once in a while.
            nextPlanNotice = Time.unscaledTime + PlanNoticeSeconds;
            CoopNotice.Show(NoticeKind.Telegram, Strings.PlanAnnounced);
        }

        if (!changed.ContainsKey(RoomKeys.Pairs))
        {
            return;
        }

        PairingResult? previous = PairRoster.Reload();
        if (previous is { Pairs.Count: > 0 } && PairRoster.Read() == null)
        {
            // The Pairs are cleared as the party is back at the Airport: the Run ended with these.
            PairPlans.RememberLastRun(previous);
        }

        Announce(previous, PairRoster.Read());
        Api.RaisePairsChanged();
    }

    /// <summary>A new Troop Leader may speak another protocol, or none (ADR 0009): the Pairs are read again.</summary>
    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        try
        {
            PairRoster.Reload();
            Api.RaisePairsChanged();
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnMasterClientSwitched", exception);
        }
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        try
        {
            PlayerLeft(otherPlayer);
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.OnPlayerLeftRoom", exception);
        }
    }

    private static void PlayerLeft(PhotonPlayer otherPlayer)
    {
        KnownPlayers.Remember(otherPlayer);
        if (PhotonNetwork.IsMasterClient && PairRoster.ReadLeading() is PairingResult current)
        {
            DissolveAbsent(current, otherPlayer.ActorNumber);
        }
    }

    /// <summary>
    /// Dissolves the Pairs of players who left, from the Pairs the Troop
    /// Leader last wrote (<see cref="PairRoster.ReadLeading"/>): rerunning it
    /// finds nothing more to dissolve rather than writing again, and it never
    /// writes back Pairs an operation has just replaced.
    /// </summary>
    private static void DissolveAbsent(PairingResult current, int leavingActor = -1)
    {
        Dictionary<int, PhotonPlayer> inRoom = PhotonNetwork.CurrentRoom.Players;
        if (!PairingRules.TryDissolveAbsent(
                current,
                actor => actor != leavingActor && inRoom.ContainsKey(actor),
                out PairingResult reconciled))
        {
            return;
        }

        Plugin.Verbose($"Dissolving {current.Pairs.Count - reconciled.Pairs.Count} Pair(s) with a member gone.");
        PartedPairs parted = FormerPairs.HostCopy();
        foreach (Pair gone in current.Pairs.Except(reconciled.Pairs))
        {
            if (KnownPlayers.UserIdOf(gone.Carrier) is string carrier && KnownPlayers.UserIdOf(gone.Rider) is string rider)
            {
                parted.Add(carrier, rider);
            }
        }

        RoomWrites.Set(new PhotonHashtable
        {
            [RoomKeys.Pairs] = PairRoster.Encode(reconciled),
            [RoomKeys.Parted] = parted.Encode(),
        });
    }

    private void Update()
    {
        try
        {
            CarryRecords.Tick();
            Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("PairingDirector.Update", exception);
        }
    }

    private void Tick()
    {
        if (Time.unscaledTime < nextTick)
        {
            return;
        }

        nextTick = Time.unscaledTime + 0.5f;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            windowOpenedAt = null;
            formedOnThisIsland = false;
            FormerPairs.Forget();
            return;
        }

        if (!GameHandler.IsOnIsland)
        {
            windowOpenedAt = null;
            formedOnThisIsland = false;
            PhotonHashtable room = PhotonNetwork.CurrentRoom.CustomProperties;
            FormerPairs.Forget();
            if (PairRoster.Read() != null
                || room.ContainsKey(RoomKeys.ShuffleClock)
                || room.ContainsKey(RoomKeys.Handover)
                || room.ContainsKey(RoomKeys.Parted))
            {
                Plugin.Verbose("Back at the Airport; clearing Pairs.");
                var cleared = new PhotonHashtable
                {
                    [RoomKeys.Pairs] = null,
                    [RoomKeys.ShuffleClock] = null,
                    [RoomKeys.Handover] = null,
                    [RoomKeys.Parted] = null,
                    [RoomKeys.Plan] = null,
                };
                CarryRecords.EndIfUnreported(cleared);
                RoomWrites.Set(cleared);
            }

            return;
        }

        if (PairRoster.ReadLeading() is PairingResult current)
        {
            CarryRecords.Sample(current);

            // Also covers leaves this client missed as master, such as the old host's.
            DissolveAbsent(current);
            return;
        }

        if (formedOnThisIsland)
        {
            return;
        }

        windowOpenedAt ??= Time.unscaledTime;
        PhotonPlayer[] players = PhotonNetwork.PlayerList.Where(p => !p.IsInactive).ToArray();
        var spawned = players.Where(p => PlayerHandler.TryGetCharacter(p.ActorNumber, out Character c) && c != null).ToList();
        bool waited = Time.unscaledTime - windowOpenedAt.Value >= SpawnWaitSeconds;
        if (spawned.Count < players.Length && !waited)
        {
            return;
        }

        FormPairs(players, spawned);
        formedOnThisIsland = true;
    }

    private static void FormPairs(PhotonPlayer[] players, List<PhotonPlayer> spawned)
    {
        var candidates = spawned
            .Where(p => RoomProtocol.Speaks(p))
            .Select(p => new PairCandidate(p.ActorNumber, ModPresence.PairingRoleOf(p)))
            .ToList();
        // The Troop Leader's Pair Plan, set at the Airport, comes first.
        PairingResult formed = PairingRules.FormWithPlan(candidates, PairPlans.Read(), new System.Random());

        var lateOrUnmodded = players
            .Select(p => p.ActorNumber)
            .Where(a => candidates.All(c => c.Actor != a));
        var result = new PairingResult(
            formed.Pairs,
            formed.Unpaired.Concat(lateOrUnmodded).ToList(),
            formed.PreferenceOverridden);

        Plugin.Verbose(
            $"Formed {result.Pairs.Count} Pairs: "
            + string.Join(", ", result.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"))
            + $"; unpaired [{string.Join(", ", result.Unpaired)}].");
        var properties = new PhotonHashtable
        {
            [RoomKeys.Pairs] = PairRoster.Encode(result),
            [RoomKeys.ShuffleClock] = PhotonNetwork.Time,
            [RoomKeys.Plan] = null,
        };
        CarryRecords.Begin(properties);
        RoomWrites.Set(properties);
    }

    private static void Announce(PairingResult? previous, PairingResult? current)
    {
        if (current == null || !GameHandler.IsOnIsland)
        {
            return;
        }

        if (previous == null)
        {
            foreach (Pair pair in current.Pairs.Where(pair => !PairLetters.Formed(pair)))
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.PairFormed(NameOf(pair.Carrier), NameOf(pair.Rider)));
            }

            foreach (int actor in current.PreferenceOverridden)
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.PreferenceOverridden(NameOf(actor)));
            }

            // An Unmodded Player is never paired, and everyone heard so when they came.
            foreach (int actor in current.Unpaired.Where(a => PhotonNetwork.CurrentRoom?.GetPlayer(a) is not PhotonPlayer player || RoomProtocol.Speaks(player)))
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.Unpaired(NameOf(actor)));
            }

            return;
        }

        // A Pair Shuffle or Role Swap forms new Pairs; a member leaving only ends one.
        foreach (Pair formed in current.Pairs.Where(c => !previous.Pairs.Any(p => p.Carrier == c.Carrier && p.Rider == c.Rider)))
        {
            // The player's own new Pair comes in their letter.
            if (!PairLetters.Formed(formed))
            {
                CoopNotice.Show(NoticeKind.Memo, Strings.PairFormed(NameOf(formed.Carrier), NameOf(formed.Rider)));
            }
        }

        foreach (Pair gone in previous.Pairs.Where(p => !current.Pairs.Any(c => c.Contains(p.Carrier) || c.Contains(p.Rider))))
        {
            CoopNotice.Warning(Strings.PairDissolved(NameOf(gone.Carrier), NameOf(gone.Rider)));
        }
    }

    private static string NameOf(int actor) => KnownPlayers.NameOf(actor);
}
