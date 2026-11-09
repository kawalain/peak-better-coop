using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Peak;
using Peak.Network;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using UnityEngine;
using Zorro.Core;

namespace BetterCoop.DevTools;

/// <summary>
/// A Twin: a copy of the game started for testing, apart from the player's own
/// game. Twins test together: the one started with <see cref="HostArgument"/>
/// opens a room of its own, without a Steam lobby, and writes its name to the
/// room card; the others join it by that name. Photon signs every copy in with
/// the same Steam account, as it takes no other sign-in, so the opening Twin's
/// room lets one account join more than once, and each Twin shows the game and
/// the mod a scout id and name of its own. A Twin leaves the account and the
/// player's files alone: no achievements, stats, rich presence, Steam lobby,
/// quicksave, or saved settings. <c>tools/dev_profile.py twins</c> starts them.
/// </summary>
internal static class Twin
{
    /// <summary>The launch argument that makes this copy a Twin: the path of the room card the Twins share.</summary>
    public const string Argument = "--bettercoop-twin=";

    /// <summary>The launch argument that makes this Twin the one that opens the room.</summary>
    public const string HostArgument = "--bettercoop-twin-host";

    /// <summary>The launch argument that names this Twin, after the player's own name.</summary>
    public const string NameArgument = "--bettercoop-twin-name=";

    /// <summary>
    /// Twin scout ids start here: far above any Steam id, so no real player
    /// is ever taken for a Twin.
    /// </summary>
    private const ulong IdFloor = 9_100_000_000_000_000_000UL;

    /// <summary>The player property the game keeps its scout id in.</summary>
    private const string GameIdKey = "UserID";

    /// <summary>The room card the Twins share, when this copy is one.</summary>
    public static string? RoomCard { get; private set; }

    public static bool Active => RoomCard != null;

    /// <summary>Whether this Twin opens the room the others join.</summary>
    public static bool Hosts { get; private set; }

    private static string label = "Twin";

    /// <summary>The Twin's own scout id, kept in its profile so it stays the same scout.</summary>
    public static ulong Id { get; private set; }

    public static void Start(GameObject runtime)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        string? argument = arguments.FirstOrDefault(a => a.StartsWith(Argument, StringComparison.Ordinal));
        if (argument == null)
        {
            return;
        }

        RoomCard = argument.Substring(Argument.Length).Trim('"');
        Hosts = arguments.Contains(HostArgument);
        string? named = arguments.FirstOrDefault(a => a.StartsWith(NameArgument, StringComparison.Ordinal))?.Substring(NameArgument.Length).Trim('"');
        label = string.IsNullOrWhiteSpace(named) ? "Twin" : named!;
        Id = OwnId();
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID + ".twin");
        LeaveAccountAlone(harmony);
        harmony.Patch(AccessTools.PropertyGetter(typeof(SteamRuntimeManager), nameof(SteamRuntimeManager.UserID)),
            postfix: new HarmonyMethod(typeof(Twin), nameof(OwnUserId)));
        harmony.Patch(AccessTools.Method(typeof(SteamFriends), nameof(SteamFriends.GetPersonaName)),
            postfix: new HarmonyMethod(typeof(Twin), nameof(OwnName)));
        if (Hosts)
        {
            LetOneAccountJoinTwice(harmony);
            runtime.AddComponent<TwinHost>();
        }
        else
        {
            runtime.AddComponent<TwinJoiner>();
        }

        Plugin.Log.LogWarning($"Twin: this copy is {label}, scout id {Id}; it {(Hosts ? "opens" : "joins")} the room on {RoomCard}.");
    }

    /// <summary>The id the mod knows a player by: a Twin's own scout id, or the one Photon gave.</summary>
    public static string ScoutId(Photon.Realtime.Player player, string photonId) =>
        player.CustomProperties.TryGetValue(GameIdKey, out object value) && value is string id && IsTwinId(id) ? id : photonId;

    private static bool IsTwinId(string id) => ulong.TryParse(id, out ulong value) && value >= IdFloor;

    /// <summary>Gives the game the Twin's scout id in the room, whatever it set before the Twin's took over.</summary>
    public static void ClaimOwnId()
    {
        string id = Id.ToString();
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GameIdKey, out object value) && value as string == id)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [GameIdKey] = id });
    }

    private static ulong OwnId()
    {
        string file = Path.Combine(Paths.ConfigPath, "in.kawala.bettercoop.twin-id.txt");
        if (File.Exists(file) && ulong.TryParse(File.ReadAllText(file).Trim(), out ulong kept) && kept >= IdFloor)
        {
            return kept;
        }

        var random = new System.Random();
        ulong made = IdFloor + (ulong)random.Next(0, 1_000_000_000) * 1_000UL + (ulong)random.Next(0, 1_000);
        File.WriteAllText(file, made.ToString());
        return made;
    }

    private static void OwnUserId(ref ulong __result) => __result = Id;

    private static void OwnName(ref string __result) => __result = (__result + " " + label).Trim();

    /// <summary>
    /// Photon's client asks every room it makes to refuse a second player with
    /// the same account; the opening Twin's room does not, so the others can join.
    /// </summary>
    private static void LetOneAccountJoinTwice(Harmony harmony) =>
        harmony.Patch(AccessTools.Method(typeof(LoadBalancingPeer), "RoomOptionsToOpParameters"),
            postfix: new HarmonyMethod(typeof(Twin), nameof(NoUserCheck)));

    private static void NoUserCheck(System.Collections.Generic.Dictionary<byte, object> op)
    {
        op[ParameterCode.CheckUserOnJoin] = false;
        if (op.TryGetValue(191, out object flags) && flags is int bits)
        {
            op[191] = bits & ~1;
        }
    }

    /// <summary>Stops the Twin touching what both copies share: the Steam account and the Windows user's files.</summary>
    private static void LeaveAccountAlone(Harmony harmony)
    {
        var skip = new HarmonyMethod(typeof(Twin).GetMethod(nameof(Skip), BindingFlags.NonPublic | BindingFlags.Static));
        void Each(Type type, Func<MethodInfo, bool> which)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(which))
            {
                harmony.Patch(method, prefix: skip);
            }
        }

        Each(typeof(SteamUserStats), m => m.Name.StartsWith("Set", StringComparison.Ordinal)
            || m.Name.StartsWith("Store", StringComparison.Ordinal)
            || m.Name.StartsWith("Indicate", StringComparison.Ordinal)
            || m.Name.StartsWith("Reset", StringComparison.Ordinal)
            || m.Name.StartsWith("Clear", StringComparison.Ordinal));
        Each(typeof(SteamFriends), m => m.Name is nameof(SteamFriends.SetRichPresence) or nameof(SteamFriends.ClearRichPresence));
        Each(typeof(PlayerPrefs), m => m.Name.StartsWith("Set", StringComparison.Ordinal) || m.Name.StartsWith("Delete", StringComparison.Ordinal));
        Each(typeof(Quicksave), m => m.Name is nameof(Quicksave.SaveNow) or nameof(Quicksave.DestroySaveData));
        harmony.Patch(AccessTools.Method(typeof(SteamLobbyHandler), nameof(SteamLobbyHandler.TryJoinLobbyDuringSession)), prefix: skip);
    }

    /// <summary>Does nothing in place of the method it stands in for; its result is the default.</summary>
    private static bool Skip() => false;
}

/// <summary>
/// In the main menu, joins the room on the Twins' room card through the
/// game's own room-code join, and says how Photon answers.
/// </summary>
internal sealed class TwinJoiner : MonoBehaviourPunCallbacks
{
    private const float Every = 3f;

    private float next;

    private string tried = string.Empty;

    private void Update()
    {
        if (Time.unscaledTime < next || PhotonNetwork.InRoom || NetworkingUtilities.EnteringRoom)
        {
            return;
        }

        next = Time.unscaledTime + Every;
        try
        {
            string room = File.Exists(Twin.RoomCard) ? File.ReadLines(Twin.RoomCard).FirstOrDefault()?.Trim() ?? string.Empty : string.Empty;
            MainMenuJoinRoomPage? page = FindAnyObjectByType<MainMenuJoinRoomPage>(FindObjectsInactive.Include);
            if (room.Length == 0 || page == null || !PhotonNetwork.IsConnectedAndReady)
            {
                return;
            }

            if (room != tried)
            {
                Plugin.Log.LogInfo($"Twin: joining room {room}.");
            }

            tried = room;
            Twin.ClaimOwnId();
            page.JoinRoom(room);
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Join", exception);
        }
    }

    public override void OnJoinedRoom() =>
        Plugin.Log.LogWarning($"Twin: joined {PhotonNetwork.CurrentRoom.Name} as actor {PhotonNetwork.LocalPlayer.ActorNumber}.");

    public override void OnJoinRoomFailed(short returnCode, string message) =>
        Plugin.Log.LogWarning($"Twin: Photon refused to let it join ({returnCode}): {message}");

    public override void OnDisconnected(DisconnectCause cause) =>
        Plugin.Log.LogWarning($"Twin: disconnected from Photon: {cause}");
}

/// <summary>
/// In the main menu, opens a room as the game does once a Steam lobby is made,
/// but without the lobby, and keeps its name on the room card while in it.
/// </summary>
internal sealed class TwinHost : MonoBehaviourPunCallbacks
{
    private const float Every = 3f;

    private float next;

    private void Update()
    {
        if (Time.unscaledTime < next || PhotonNetwork.InRoom || NetworkingUtilities.EnteringRoom)
        {
            return;
        }

        next = Time.unscaledTime + Every;
        try
        {
            if (!PhotonNetwork.IsConnectedAndReady || FindAnyObjectByType<MainMenuMainPage>() == null)
            {
                return;
            }

            Plugin.Log.LogInfo("Twin: opening a room.");
            Twin.ClaimOwnId();
            NetworkingUtilities.EnteringRoom = true;
            GameHandler.GetService<ConnectionService>().StateMachine.SwitchState<HostState>().RoomName = Guid.NewGuid().ToString();
            LoadingScreenHandler loading = RetrievableResourceSingleton<LoadingScreenHandler>.Instance;
            loading.Load(LoadingScreen.LoadingScreenType.Basic, null, loading.LoadSceneProcess("Airport", networked: false, yieldForCharacterSpawn: true));
        }
        catch (Exception exception)
        {
            NetworkingUtilities.EnteringRoom = false;
            Guard.Report("Twin.Host", exception);
        }
    }

    public override void OnJoinedRoom()
    {
        try
        {
            File.WriteAllText(Twin.RoomCard, PhotonNetwork.CurrentRoom.Name);
            Plugin.Log.LogWarning($"Twin: opened {PhotonNetwork.CurrentRoom.Name}.");
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Card", exception);
        }
    }

    public override void OnLeftRoom()
    {
        try
        {
            File.Delete(Twin.RoomCard);
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Card", exception);
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message) =>
        Plugin.Log.LogWarning($"Twin: Photon refused the room ({returnCode}): {message}");
}
