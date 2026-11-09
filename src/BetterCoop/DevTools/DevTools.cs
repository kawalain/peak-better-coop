using System;
using BepInEx;
using BetterCoop.Ui.Paper;
using UnityEngine;
using UnityEngine.UI;
using Zorro.UI;

namespace BetterCoop;

/// <summary>
/// The development build's side of <see cref="DevHooks"/>. Everything under
/// <c>DevTools/</c> is compiled into development (Debug) builds only; a test
/// build or a Thunderstore release has none of it.
/// </summary>
internal static partial class DevHooks
{
    static partial void OnStarted(BaseUnityPlugin plugin, GameObject runtime)
    {
        DevTools.SoloPair.Bind(plugin.Config);
        DevTools.ControlLine.Bind(plugin.Config);
        DevTools.Twin.Start(runtime);
        if (DevTools.ControlLine.Asked)
        {
            runtime.AddComponent<DevTools.ControlLine>();
        }

        runtime.AddComponent<DevTools.SoloPairNotice>();
        PhotoDarkroom.More = DevTools.ZooPortraits.WarmOne;
        Plugin.Log.LogWarning("This is a development build: the Zoo, Solo Pair, the Twin, and the Control Line are in it. Never hand it to players.");
    }

    static partial void OnPauseMenu(PauseMenuMainPage menu, UIPageHandler handler, PauseMenuControlsPage controls, Button after, Func<string, Action, Button, Button> add)
    {
        Button template = menu.m_settingsButton;
        DevTools.ZooPage? zoo = null;
        add("Zoo", () => (zoo ??= DevTools.ZooPage.Build(handler, controls, template, menu)).Open(), after);
    }

    static partial void SoloPairWanted(ref bool on) => on = DevTools.SoloPair.Wanted;

    static partial void SoloPairLatency(ref float seconds) => seconds = DevTools.SoloPair.Latency.Value / 1000f;

    static partial void ScoutIdOf(Photon.Realtime.Player player, ref string id) => id = DevTools.Twin.ScoutId(player, id);

    static partial void OnHandPictured(Texture2D picture, string name)
    {
        string file = System.IO.Path.Combine(Paths.CachePath, $"BetterCoop-{name}.png");
        System.IO.File.WriteAllBytes(file, picture.EncodeToPNG());
        Plugin.Verbose($"Saved a picture of the hand to {file}.");
    }
}
