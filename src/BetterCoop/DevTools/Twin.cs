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

    /// <summary>The launch argument that gives this Twin its role on the Control Line, as host, guest, or guest2.</summary>
    public const string RoleArgument = "--bettercoop-twin-role=";

    /// <summary>The launch argument that makes this Twin stand in for an Unmodded Player.</summary>
    public const string UnmoddedArgument = "--bettercoop-twin-unmodded";

    /// <summary>The launch argument that puts this Twin's window off screen and silences it; it still renders, for screenshots.</summary>
    public const string HeadlessArgument = "--bettercoop-twin-headless";

    /// <summary>Set while a test keeps this Twin out of the room: it neither joins nor opens one.</summary>
    public static bool Held { get; set; }

    /// <summary>
    /// Whether this copy is a Twin standing in for an Unmodded Player: only
    /// the development tools run, and nothing of the mod reaches the room.
    /// Read from the launch arguments before the plugin patches anything.
    /// </summary>
    public static bool Unmodded
    {
        get
        {
            string[] arguments = Environment.GetCommandLineArgs();
            return arguments.Contains(UnmoddedArgument) && arguments.Any(a => a.StartsWith(Argument, StringComparison.Ordinal));
        }
    }

    /// <summary>This copy's role on the Control Line: the Twin's own, or player for a copy that is not one.</summary>
    public static string Role { get; private set; } = "player";

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
        string? role = arguments.FirstOrDefault(a => a.StartsWith(RoleArgument, StringComparison.Ordinal))?.Substring(RoleArgument.Length).Trim('"');
        Role = string.IsNullOrWhiteSpace(role) ? (Hosts ? "host" : "guest") : role!;
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

        // The opening Twin, once it has left a room the others are still in, joins it again as they do.
        runtime.AddComponent<TwinJoiner>();
        if (arguments.Contains(HeadlessArgument))
        {
            // At once, before the first scene plays a sound or the window shows.
            TwinScreen.Silence();
            runtime.AddComponent<TwinScreen>();
        }
        else
        {
            // Windows may remember a headless Twin's mute for the Twin game's executable.
            TwinScreen.Unmute();
        }

        Plugin.Log.LogWarning($"Twin: this copy is {label} ({Role}{(Unmodded ? ", Unmodded" : string.Empty)}), scout id {Id}; it {(Hosts ? "opens" : "joins")} the room on {RoomCard}.");
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

    /// <summary>Shows the Twin by its own name alone, not the account's every copy shares.</summary>
    private static void OwnName(ref string __result) => __result = label;

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

    /// <summary>
    /// Goes past the game's first-time setup page, which a Twin meets at every
    /// start: it keeps no settings, so the game never learns it was shown.
    /// </summary>
    internal static void PassFirstTimeSetup()
    {
        MainMenuFirstTimeSetupPage? setup = UnityEngine.Object.FindAnyObjectByType<MainMenuFirstTimeSetupPage>();
        if (setup != null && setup.isActiveAndEnabled)
        {
            setup.ContinueButton.onClick.Invoke();
        }
    }

    /// <summary>The room on the room card, or empty when there is none.</summary>
    internal static string CardRoom()
    {
        try
        {
            return File.Exists(RoomCard) ? File.ReadLines(RoomCard).FirstOrDefault()?.Trim() ?? string.Empty : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// A headless Twin: its window off every screen and the taskbar and its sound
/// off, while the game goes on rendering, so screenshots still work. It runs
/// at a steady low frame rate, so that several Twins leave the computer to the
/// player.
/// </summary>
internal sealed class TwinScreen : MonoBehaviour
{
    private const float Every = 1f;

    private const int FrameRate = 30;

    private const int WatchedFrames = 600;

    private float next;

    /// <summary>Mutes the game and puts its window away; the window starts hidden, so it never shows on screen.</summary>
    internal static void Silence()
    {
        try
        {
            AudioListener.volume = 0f;
            MuteProcess();
            PutAwayAll(show: false);
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Silence", exception);
        }
    }

    /// <summary>Lets a visible Twin be heard again.</summary>
    internal static void Unmute()
    {
        try
        {
            MuteProcess(false);
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Unmute", exception);
        }
    }

    private void Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FrameRate;
    }

    private void Update()
    {
        // Every frame at first, while the game still sizes and places its window.
        if (Time.unscaledTime < next && Time.frameCount > WatchedFrames)
        {
            return;
        }

        next = Time.unscaledTime + Every;
        try
        {
            // The game raises the listener's volume back every frame, and a scene change may show the window again.
            AudioListener.volume = 0f;
            MuteProcess();
            Application.targetFrameRate = FrameRate;
            PutAwayAll();
        }
        catch (Exception exception)
        {
            Guard.Report("Twin.Screen", exception);
        }
    }

    // A hidden or minimized window stops presenting, and the screenshot comes
    // out blank; a window off every screen and off the taskbar still renders.
    private const int Away = -20000;

    private const int ExStyle = -20;

    private const long ToolWindow = 0x80, AppWindow = 0x40000, NoActivate = 0x8000000;

    private const uint NoSize = 0x1, NoZOrder = 0x4, NoActivateMove = 0x10, FrameChanged = 0x20, ShowAgain = 0x40;

    /// <summary>The game's own window, hidden or not, and any other window of this process on show.</summary>
    private static void PutAwayAll(bool show = true)
    {
        uint process = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        var name = new System.Text.StringBuilder(64);
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner == process)
            {
                name.Clear();
                GetClassName(window, name, name.Capacity);
                if (name.ToString() == GameWindowClass || IsWindowVisible(window))
                {
                    PutAway(window, show);
                }
            }

            return true;
        }, IntPtr.Zero);
    }

    private const string GameWindowClass = "UnityWndClass";

    private static void PutAway(IntPtr window, bool show)
    {
        long style = GetWindowLongPtr(window, ExStyle).ToInt64();
        long away = (style | ToolWindow | NoActivate) & ~AppWindow;
        bool shown = IsWindowVisible(window);
        KeepAway(window);
        if (away != style)
        {
            // The taskbar drops a button only for a window shown again after the change.
            if (shown)
            {
                ShowWindow(window, 0);
            }

            SetWindowLongPtr(window, ExStyle, new IntPtr(away));
            shown = false;
        }

        if (!shown || (GetWindowRect(window, out Rect rect) && rect.Left > Away / 2))
        {
            // Shown off every screen without taking the focus once the game runs: a hidden window stops presenting.
            SetWindowPos(window, IntPtr.Zero, Away, Away, 0, 0, NoSize | NoZOrder | NoActivateMove | FrameChanged | (show ? ShowAgain : 0));
        }
    }

    /// <summary>
    /// Mutes this process's sound in Windows itself: the game turns its own
    /// listener back up every frame and fades sounds in, so muting the game
    /// alone lets some through.
    /// </summary>
    private static void MuteProcess(bool mute = true)
    {
        IntPtr enumerator = IntPtr.Zero, device = IntPtr.Zero, sessions = IntPtr.Zero, volume = IntPtr.Zero;
        try
        {
            Guid enumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Guid enumeratorInterface = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
            Guid sessionsInterface = new("BFA971F1-4D5E-40BB-935E-967039BFBEE4");
            if (CoCreateInstance(ref enumeratorClass, IntPtr.Zero, AllContexts, ref enumeratorInterface, out enumerator) != 0
                || Call<GetDefaultEndpoint>(enumerator, 4)(enumerator, 0, 0, out device) != 0
                || Call<Activate>(device, 3)(device, ref sessionsInterface, AllContexts, IntPtr.Zero, out sessions) != 0
                || Call<GetSimpleVolume>(sessions, 4)(sessions, IntPtr.Zero, 0, out volume) != 0)
            {
                return;
            }

            Call<SetMute>(volume, 5)(volume, mute ? 1 : 0, IntPtr.Zero);
            if (mute && !muted && Call<GetMute>(volume, 6)(volume, out int now) == 0 && now != 0)
            {
                muted = true;
                Plugin.Log.LogInfo("Twin: muted this copy's sound in Windows.");
            }
        }
        finally
        {
            foreach (IntPtr held in new[] { volume, sessions, device, enumerator })
            {
                if (held != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.Release(held);
                }
            }
        }
    }

    private const int AllContexts = 23;

    /// <summary>The method at that slot of a COM object's table, called without COM interop, which Unity's Mono lacks.</summary>
    private static T Call<T>(IntPtr instance, int slot)
        where T : Delegate
    {
        IntPtr table = System.Runtime.InteropServices.Marshal.ReadIntPtr(instance);
        IntPtr method = System.Runtime.InteropServices.Marshal.ReadIntPtr(table, slot * IntPtr.Size);
        return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private delegate int GetDefaultEndpoint(IntPtr self, int flow, int role, out IntPtr device);

    private delegate int Activate(IntPtr self, ref Guid iid, int context, IntPtr parameters, out IntPtr instance);

    private delegate int GetSimpleVolume(IntPtr self, IntPtr session, int crossProcess, out IntPtr volume);

    private delegate int SetMute(IntPtr self, int mute, IntPtr context);

    private delegate int GetMute(IntPtr self, out int mute);

    private static bool muted;

    [System.Runtime.InteropServices.DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid type, IntPtr outer, int context, ref Guid iid, out IntPtr instance);

    private const int WindowProcedure = -4;

    private const uint PositionChanging = 0x46;

    private const uint NoMove = 0x2;

    private delegate IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static readonly WindowProc Placing = Place;

    private static readonly System.Collections.Generic.Dictionary<IntPtr, IntPtr> Procedures = new();

    /// <summary>
    /// Hooks the window's own procedure so every move or show the game makes
    /// lands off every screen: the game shows and places its window itself
    /// while it starts, after the mod has loaded.
    /// </summary>
    private static void KeepAway(IntPtr window)
    {
        if (Procedures.ContainsKey(window))
        {
            return;
        }

        // Known before the hook goes in, so no message finds it missing.
        Procedures[window] = GetWindowLongPtr(window, WindowProcedure);
        SetWindowLongPtr(window, WindowProcedure, System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(Placing));
    }

    private static IntPtr Place(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == PositionChanging && lParam != IntPtr.Zero)
        {
            // WINDOWPOS: two handles, then x, y, cx, cy and flags.
            int at = 2 * IntPtr.Size;
            uint flags = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(lParam, at + 16);
            System.Runtime.InteropServices.Marshal.WriteInt32(lParam, at, Away);
            System.Runtime.InteropServices.Marshal.WriteInt32(lParam, at + 4, Away);
            System.Runtime.InteropServices.Marshal.WriteInt32(lParam, at + 16, (int)(flags & ~NoMove));
        }

        return Procedures.TryGetValue(window, out IntPtr procedure)
            ? CallWindowProc(procedure, window, message, wParam, lParam)
            : IntPtr.Zero;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr procedure, IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
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
        if (Time.unscaledTime < next || Twin.Held || PhotonNetwork.InRoom || NetworkingUtilities.EnteringRoom)
        {
            return;
        }

        next = Time.unscaledTime + Every;
        try
        {
            Twin.PassFirstTimeSetup();
            string room = Twin.CardRoom();
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
/// but without the lobby, and writes its name on the room card. The card stays
/// when this Twin leaves, as the room goes on with the others; starting the
/// opening Twin again clears it.
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
            Twin.PassFirstTimeSetup();
            // A room on the card is still there for the others: the joiner takes this Twin back to it.
            if (Twin.Held || Twin.CardRoom().Length > 0 || !PhotonNetwork.IsConnectedAndReady || FindAnyObjectByType<MainMenuMainPage>() == null)
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

    public override void OnCreateRoomFailed(short returnCode, string message) =>
        Plugin.Log.LogWarning($"Twin: Photon refused the room ({returnCode}): {message}");
}
