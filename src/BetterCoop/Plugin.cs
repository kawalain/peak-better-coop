using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using BetterCoop.Features;
using BetterCoop.Net;
using BetterCoop.Ui;
using UnityEngine;

namespace BetterCoop;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
// Only orders loading: Co-op Mod, when installed, loads first, so Awake sees it.
[BepInDependency(CoopModGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    /// <summary>Co-op Mod, the mod BetterCoop grew from; it patches the same carry and stamina.</summary>
    private const string CoopModGuid = "com.peak.coopmod";

    internal static ManualLogSource Log { get; private set; } = null!;

    /// <summary>Whether the mod's own patching is done; later re-patches come from other mods.</summary>
    internal static bool Patched { get; private set; }

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
        var runtime = new GameObject("BetterCoop");
        runtime.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(runtime);

        // ADR 0003: two mods patching the same carry is a half-working mod too.
        if (Chainloader.PluginInfos.ContainsKey(CoopModGuid))
        {
            Log.LogError($"Co-op Mod ({CoopModGuid}) is also loaded; the mod is disabled. Remove Co-op Mod to play.");
            runtime.AddComponent<DisabledNotice>().Message = () => Strings.ModDisabledByCoopMod;
            return;
        }

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        try
        {
            harmony.PatchAll(typeof(Plugin).Assembly);
            Patched = true;
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
        runtime.AddComponent<PairingDirector>();
        runtime.AddComponent<CarryKeeper>();
        runtime.AddComponent<ControlSplitLink>();
        runtime.AddComponent<RiderHudRefresher>();
        runtime.AddComponent<RiderBarsKeeper>();
        runtime.AddComponent<RiderEffectsKeeper>();
        runtime.AddComponent<RiderPreviewsKeeper>();
        runtime.AddComponent<SharedViewSoundsKeeper>();
        runtime.AddComponent<PairShuffleLink>();

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
