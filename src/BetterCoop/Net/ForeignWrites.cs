using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Realtime;
using UnityEngine;

namespace BetterCoop.Net;

/// <summary>
/// Drops the mod's shared values written by a player who may not write them,
/// before Photon applies them on this client. Photon lets any player write a
/// room property, or another player's property, and every client would act
/// on such a value before the Troop Leader could put it back. Room values are
/// the Troop Leader's alone; a player's own values are theirs, or the Troop
/// Leader's to clear. The Troop Leader still puts back what was dropped, so
/// the room on the server, which a joining player reads, holds its values.
/// </summary>
[HarmonyPatch]
internal static class ForeignWrites
{
    /// <summary>Player keys only their player, or the Troop Leader, writes.</summary>
    private static readonly string[] OwnKeys = [RoomKeys.Version, RoomKeys.Role, RoomKeys.SwapRequest];

    private const double NoteSeconds = 10;

    private static double nextNote;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LoadBalancingClient), nameof(LoadBalancingClient.OnEvent))]
    private static void DropForeign(LoadBalancingClient __instance, EventData photonEvent)
    {
        try
        {
            if (photonEvent.Code != EventCode.PropertiesChanged
                || __instance.CurrentRoom is not Room room
                || photonEvent[ParameterCode.Properties] is not Hashtable properties)
            {
                return;
            }

            int target = photonEvent.Parameters.ContainsKey(ParameterCode.TargetActorNr) && photonEvent[ParameterCode.TargetActorNr] is int actor ? actor : 0;
            int sender = photonEvent.Sender;
            int master = room.MasterClientId;
            if (sender == master)
            {
                return;
            }

            if (target == 0)
            {
                List<string>? dropped = Drop(properties, RoomWrites.KeptKeys);
                if (dropped != null)
                {
                    RoomWrites.Forged(dropped);
                    Note($"Player {sender} wrote the room's {string.Join(", ", dropped)}, which only the Troop Leader writes; dropped.");
                }
            }
            else if (sender != target && Drop(properties, OwnKeys) is List<string> dropped)
            {
                Note($"Player {sender} wrote player {target}'s {string.Join(", ", dropped)}, which only that player or the Troop Leader writes; dropped.");
            }
        }
        catch (Exception exception)
        {
            Guard.Report("ForeignWrites.DropForeign", exception);
        }
    }

    private static List<string>? Drop(Hashtable properties, IReadOnlyList<string> keys)
    {
        List<string>? dropped = null;
        for (int i = 0; i < keys.Count; i++)
        {
            if (properties.ContainsKey(keys[i]))
            {
                properties.Remove(keys[i]);
                (dropped ??= new List<string>()).Add(keys[i]);
            }
        }

        return dropped;
    }

    private static void Note(string message)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextNote)
        {
            return;
        }

        nextNote = now + NoteSeconds;
        Plugin.Log.LogWarning(message);
    }
}
