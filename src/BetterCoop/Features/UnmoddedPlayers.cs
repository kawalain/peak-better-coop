using System.Collections.Generic;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Tells of Unmodded Players (ADR 0009): everyone with the Troop Leader's
/// protocol hears once of each Unmodded Player in the room, and a client
/// resting in the room hears once why. A player is judged only after they
/// have been in the room a while, as their properties arrive after them.
/// </summary>
internal sealed class UnmoddedPlayers : MonoBehaviourPunCallbacks
{
    /// <summary>How long a player is in the room before they are judged.</summary>
    private const float SettleSeconds = 5f;

    private readonly Dictionary<int, float> firstSeen = new();

    private readonly HashSet<int> told = new();

    /// <summary>The Troop Leader this client last told it rests under, or 0.</summary>
    private int restingToldUnder;

    private float joinedAt;

    private float nextCheck;

    public override void OnJoinedRoom()
    {
        joinedAt = Time.unscaledTime;
        firstSeen.Clear();
        told.Clear();
        restingToldUnder = 0;
    }

    public override void OnLeftRoom()
    {
        firstSeen.Clear();
        told.Clear();
        restingToldUnder = 0;
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        firstSeen.Remove(otherPlayer.ActorNumber);
        told.Remove(otherPlayer.ActorNumber);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now < nextCheck || !PhotonNetwork.InRoom)
        {
            return;
        }

        nextCheck = now + 1f;
        try
        {
            Check(now);
        }
        catch (System.Exception exception)
        {
            Guard.Report("UnmoddedPlayers.Update", exception);
        }
    }

    private void Check(float now)
    {
        if (now - joinedAt < SettleSeconds)
        {
            return;
        }

        PhotonPlayer? leader = PhotonNetwork.MasterClient;
        if (RoomProtocol.Resting)
        {
            if (leader != null && restingToldUnder != leader.ActorNumber)
            {
                restingToldUnder = leader.ActorNumber;
                string name = KnownPlayers.NameOf(leader, leader.ActorNumber);
                Plugin.Log.LogInfo($"Resting in the room: the Troop Leader {name} speaks protocol {RoomProtocol.Of(leader)?.ToString() ?? "none"} (the room's is {RoomProtocol.OfRoom()?.ToString() ?? "none"}), version {ModPresence.VersionOf(leader) ?? "none"}.");
                CoopNotice.Warning(Strings.Resting(name, ModPresence.VersionOf(leader), MyPluginInfo.PLUGIN_VERSION));
            }

            return;
        }

        restingToldUnder = 0;
        foreach (PhotonPlayer player in PhotonNetwork.PlayerListOthers)
        {
            int actor = player.ActorNumber;
            if (!firstSeen.TryGetValue(actor, out float seen))
            {
                firstSeen[actor] = now;
                continue;
            }

            if (now - seen < SettleSeconds || told.Contains(actor) || RoomProtocol.Speaks(player))
            {
                continue;
            }

            told.Add(actor);
            string name = KnownPlayers.NameOf(player, actor);
            string? theirs = ModPresence.VersionOf(player);
            Plugin.Log.LogInfo($"{name} is an Unmodded Player: version {theirs ?? "none"}, protocol {RoomProtocol.Of(player)?.ToString() ?? "none"}.");
            CoopNotice.Info(Strings.UnmoddedPlayer(name, theirs, leader != null ? ModPresence.VersionOf(leader) : null));
        }
    }
}
