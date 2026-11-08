using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PeakCoop.Features;
using PeakCoop.Net;
using UnityEngine;

namespace PeakCoop;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        PartyGate.Bind(Config);

        // Runtime components live on their own object: the BepInEx manager
        // object may be destroyed by the game, and patches stay for the process.
        var runtime = new GameObject("PeakCoop");
        runtime.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(runtime);
        runtime.AddComponent<ModPresence>();

        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
