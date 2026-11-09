using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// The Troop Leader's writes to the room's shared values. Photon lets any
/// player write a room property, so the Troop Leader puts back at once any
/// of these another player changes (see <see cref="RoomWard"/>). Every write
/// of these keys goes through <see cref="Set"/>.
/// </summary>
internal sealed class RoomWrites : MonoBehaviourPunCallbacks
{
    /// <summary>The room keys only the Troop Leader writes.</summary>
    private static readonly string[] Kept =
    [
        RoomKeys.Protocol,
        RoomKeys.Rules,
        RoomKeys.Pairs,
        RoomKeys.Plan,
        RoomKeys.Operation,
        RoomKeys.Assignment,
        RoomKeys.Handover,
        RoomKeys.Parted,
        RoomKeys.ShuffleClock,
        RoomKeys.Record,
        RoomKeys.Report,
    ];

    /// <summary>The room keys only the Troop Leader writes.</summary>
    internal static IReadOnlyList<string> KeptKeys => Kept;

    private static readonly RoomWard Ward = new();

    /// <summary>How soon the Troop Leader puts values back again, however often another player writes them.</summary>
    private const double PutBackSeconds = 0.5;

    /// <summary>Kept keys another player changed, waiting to be put back.</summary>
    private static readonly HashSet<string> Changed = new();

    /// <summary>Kept keys another player wrote and this client dropped: the room on the server holds that value, which this client never saw.</summary>
    private static readonly HashSet<string> Dropped = new();

    private static double nextPutBack;

    private static double nextWarning;

    private static double Now => Time.realtimeSinceStartupAsDouble;

    /// <summary>Writes room properties as the Troop Leader, noting each kept value as its own.</summary>
    public static void Set(PhotonHashtable properties)
    {
        double now = Now;
        foreach (object key in properties.Keys)
        {
            if (key is string name && Kept.Contains(name))
            {
                Ward.Wrote(name, properties[key], now);
            }
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    /// <summary>Whether the room's value under <paramref name="key"/> is the Troop Leader's own, as this client knows it.</summary>
    public static bool IsOwn(string key)
    {
        object? value = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object found) ? found : null;
        return Ward.IsOwn(key, value, Now);
    }

    /// <summary>
    /// Notes kept keys another player wrote, which <see cref="ForeignWrites"/>
    /// dropped before this client applied them; as the Troop Leader, puts
    /// back its own on the server.
    /// </summary>
    public static void Forged(IEnumerable<string> keys)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        foreach (string key in keys)
        {
            Changed.Add(key);
            Dropped.Add(key);
        }
    }

    public override void OnJoinedRoom() => Run("RoomWrites.OnJoinedRoom", TrustIfLeading);

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient) => Run("RoomWrites.OnMasterClientSwitched", TrustIfLeading);

    public override void OnLeftRoom()
    {
        Ward.Clear();
        Changed.Clear();
        Dropped.Clear();
    }

    private void Update() => Run("RoomWrites.Update", PutBackNow);

    public override void OnRoomPropertiesUpdate(PhotonHashtable changed) => Run("RoomWrites.OnRoomPropertiesUpdate", () => PutBack(changed));

    private static void Run(string site, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Guard.Report(site, exception);
        }
    }

    /// <summary>
    /// A new Troop Leader can know no better than the room: what is there
    /// becomes its own, when it is in this client's protocol. A room in
    /// another protocol (ADR 0009), or one a mod without a protocol wrote, is
    /// stamped with this one and its values cleared, keeping only this
    /// Troop Leader's Run Rules.
    /// </summary>
    private static void TrustIfLeading()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (!RoomProtocol.Readable)
        {
            var stamp = new PhotonHashtable { [RoomKeys.Protocol] = RoomProtocol.Current };
            foreach (string key in Kept)
            {
                if (key != RoomKeys.Protocol && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
                {
                    stamp[key] = null;
                }
            }

            stamp[RoomKeys.Rules] = RunRules.Local;
            Plugin.Verbose($"Stamped the room with protocol {RoomProtocol.Current} (was {RoomProtocol.OfRoom()?.ToString() ?? "none"}) and cleared its other values.");
            Set(stamp);
            return;
        }

        var values = new List<KeyValuePair<string, object?>>();
        foreach (string key in Kept)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
            {
                values.Add(new KeyValuePair<string, object?>(key, value));
            }
        }

        Ward.Trust(values, Now);
    }

    private static void PutBack(PhotonHashtable changed)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        double now = Now;
        foreach (string key in Kept)
        {
            if (changed.ContainsKey(key) && !Ward.IsOwn(key, changed[key], now))
            {
                Changed.Add(key);
            }
        }

        PutBackNow();
    }

    /// <summary>
    /// Puts back what other players changed, at most every
    /// <see cref="PutBackSeconds"/>: each put-back goes to every client, so a
    /// player writing again and again cannot make the Troop Leader flood the room.
    /// </summary>
    private static void PutBackNow()
    {
        double now = Now;
        if (Changed.Count == 0 || now < nextPutBack || !PhotonNetwork.InRoom)
        {
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Changed.Clear();
            Dropped.Clear();
            return;
        }

        var restore = new PhotonHashtable();
        foreach (string key in Changed)
        {
            object? value = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object found) ? found : null;
            if (Dropped.Contains(key) || !Ward.IsOwn(key, value, now))
            {
                restore[key] = Ward.Restore(key);
            }
        }

        Changed.Clear();
        Dropped.Clear();
        nextPutBack = now + PutBackSeconds;
        if (restore.Count == 0)
        {
            return;
        }

        if (now >= nextWarning)
        {
            nextWarning = now + 10;
            Plugin.Log.LogWarning($"Another player changed {string.Join(", ", restore.Keys.Cast<object>())}; the Troop Leader put it back.");
        }

        Set(restore);
    }
}
