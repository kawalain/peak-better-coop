using System;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using Zorro.UI;

namespace BetterCoop;

/// <summary>
/// The only place the plugin reaches the development build's tools in
/// <c>DevTools/</c>. Each hook is a partial method: a development build fills
/// it in there. A Release build compiles none of <c>DevTools/</c> (see
/// BetterCoop.csproj), so the compiler removes every hook's call and nothing of
/// the tools is left in the plugin; the members here then do nothing.
/// </summary>
internal static partial class DevHooks
{
    /// <summary>Once the plugin has set up its runtime object and patched the game.</summary>
    public static void Started(BaseUnityPlugin plugin, GameObject runtime) => OnStarted(plugin, runtime);

    /// <summary>
    /// Once the pause menu's Roster button is added: the tools add their own
    /// buttons with <paramref name="add"/>, which adds a button after another.
    /// </summary>
    public static void PauseMenu(PauseMenuMainPage menu, UIPageHandler handler, PauseMenuControlsPage controls, Button after, Func<string, Action, Button, Button> add) =>
        OnPauseMenu(menu, handler, controls, after, add);

    /// <summary>Whether Solo Pair applies now: switched on in a development build, and alone in a room.</summary>
    public static bool SoloPair
    {
        get
        {
            bool on = false;
            SoloPairWanted(ref on);
            return on;
        }
    }

    /// <summary>How long Solo Pair holds the Rider's actions back, in seconds.</summary>
    public static float SoloPairDelay
    {
        get
        {
            float seconds = 0f;
            SoloPairLatency(ref seconds);
            return seconds;
        }
    }

    /// <summary>A picture of the Scoutmaster's hand as taken, to keep for a look.</summary>
    public static void HandPictured(Texture2D picture, string name) => OnHandPictured(picture, name);

    static partial void OnStarted(BaseUnityPlugin plugin, GameObject runtime);

    static partial void OnPauseMenu(PauseMenuMainPage menu, UIPageHandler handler, PauseMenuControlsPage controls, Button after, Func<string, Action, Button, Button> add);

    static partial void SoloPairWanted(ref bool on);

    static partial void SoloPairLatency(ref float seconds);

    static partial void OnHandPictured(Texture2D picture, string name);
}
