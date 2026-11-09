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

    private static readonly RoomWard Ward = new();

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

    public override void OnJoinedRoom() => Run("RoomWrites.OnJoinedRoom", TrustIfLeading);

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient) => Run("RoomWrites.OnMasterClientSwitched", TrustIfLeading);

    public override void OnLeftRoom() => Ward.Clear();

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

    /// <summary>A new Troop Leader can know no better than the room: what is there becomes its own.</summary>
    private static void TrustIfLeading()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
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
        var restore = new PhotonHashtable();
        foreach (string key in Kept)
        {
            if (changed.ContainsKey(key) && !Ward.IsOwn(key, changed[key], now))
            {
                restore[key] = Ward.Restore(key);
            }
        }

        if (restore.Count == 0)
        {
            return;
        }

        Plugin.Log.LogWarning($"Another player changed {string.Join(", ", restore.Keys.Cast<object>())}; the Troop Leader put it back.");
        Set(restore);
    }
}
