using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PeakCoop.Net;
using PeakCoop.Ui;
using UnityEngine;

namespace PeakCoop;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static void Verbose(string message)
    {
        if (CoopConfig.VerboseLog.Value)
        {
            Log.LogInfo(message);
        }
    }

    private void Awake()
    {
        Log = Logger;
        CoopConfig.Bind(Config);

        // Runtime components live on their own object: the BepInEx manager
        // object may be destroyed by the game, and patches stay for the process.
        var runtime = new GameObject("PeakCoop");
        runtime.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(runtime);

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        try
        {
            harmony.PatchAll(typeof(Plugin).Assembly);
        }
        catch (Exception exception)
        {
            // ADR 0003: a partly patched mod is worse than none.
            Log.LogError($"Patching failed; the mod is disabled for this game version.\n{exception}");
            harmony.UnpatchSelf();
            runtime.AddComponent<DisabledNotice>();
            return;
        }

        runtime.AddComponent<ModPresence>();
        runtime.AddComponent<RunRules>();

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
