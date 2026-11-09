using System;
using BetterCoop.Core;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Net;

/// <summary>
/// Publishes the local mod version, protocol, and Role Preference as player
/// custom properties, so the Troop Leader can tell who it pairs (ADR 0009),
/// and the player's Climb Input, which the Control Split follows on the
/// Carrier's client.
/// </summary>
internal sealed class ModPresence : MonoBehaviourPunCallbacks
{
    public static string? VersionOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.Version, out object value) && value is string version && version.Length <= 32
            ? WireLimits.Name(version, string.Empty)
            : null;

    public static RolePreference RoleOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.Role, out object value)
        && value is byte role
        && System.Enum.IsDefined(typeof(RolePreference), role)
            ? (RolePreference)role
            : RolePreference.Random;

    /// <summary>Whether <paramref name="player"/> climbs by Toggle, as their own game's Climb Input setting says.</summary>
    public static bool ClimbTogglesOf(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.ClimbToggle, out object value) && value is bool toggles && toggles;

    /// <summary>The role a player is paired by: their Role Preference, unless the Run Rules draw roles at random.</summary>
    public static RolePreference PairingRoleOf(PhotonPlayer player) =>
        RunRules.FollowRolePreferences ? RoleOf(player) : RolePreference.Random;

    /// <summary>The least time between two publishes for a changed Role Preference: each one reaches every client.</summary>
    private const float PublishSeconds = 0.5f;

    private static bool publishWanted;

    private static float nextPublish;

    private void Start()
    {
        CoopConfig.RolePreference.SettingChanged += (_, _) => publishWanted = true;
    }

    public override void OnJoinedRoom()
    {
        try
        {
            Publish();
        }
        catch (Exception exception)
        {
            Guard.Report("ModPresence.OnJoinedRoom", exception);
        }
    }

    private const float SettingCheckSeconds = 1f;

    private static bool? publishedClimbToggle;

    private static float nextSettingCheck;

    /// <summary>The game tells no one when a setting changes, so the Climb Input is looked at now and then.</summary>
    private void Update()
    {
        if (publishWanted && Time.unscaledTime >= nextPublish)
        {
            // A preference changed again and again goes out as its latest, at most so often.
            publishWanted = false;
            nextPublish = Time.unscaledTime + PublishSeconds;
            Publish();
        }

        if (Time.unscaledTime < nextSettingCheck)
        {
            return;
        }

        nextSettingCheck = Time.unscaledTime + SettingCheckSeconds;
        if (PhotonNetwork.InRoom && LocalClimbToggles() != publishedClimbToggle)
        {
            Publish();
        }
    }

    private static bool LocalClimbToggles() =>
        GameHandler.Instance?.SettingsHandler?.GetSetting<ClimbInputSetting>() is ClimbInputSetting setting
        && setting.Value == ClimbInputSetting.InputMode.SettingToggle;

    public override void OnEnable()
    {
        base.OnEnable();
        Publish();
    }

    private static void Publish()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        var properties = new PhotonHashtable
        {
            [RoomKeys.Version] = MyPluginInfo.PLUGIN_VERSION,
            [RoomKeys.Protocol] = RoomProtocol.Current,
            [RoomKeys.Role] = (byte)CoopConfig.RolePreference.Value,
            [RoomKeys.ClimbToggle] = LocalClimbToggles(),
        };
        publishedClimbToggle = (bool)properties[RoomKeys.ClimbToggle];
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }
}
