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

    /// <summary>
    /// Whether this copy stands in for an Unmodded Player: a development
    /// build's Twin asked to. The plugin then starts only the tools.
    /// </summary>
    public static bool ActsUnmodded
    {
        get
        {
            bool on = false;
            UnmoddedWanted(ref on);
            return on;
        }
    }

    /// <summary>A picture of the Scoutmaster's hand as taken, to keep for a look.</summary>
    public static void HandPictured(Texture2D picture, string name) => OnHandPictured(picture, name);

    /// <summary>
    /// The user id the mod knows a player by: Photon's, unless a development
    /// build knows better (the Twin shares its first copy's Steam account).
    /// </summary>
    public static string ScoutId(Photon.Realtime.Player player)
    {
        string id = player.UserId;
        ScoutIdOf(player, ref id);
        return id;
    }

    static partial void OnStarted(BaseUnityPlugin plugin, GameObject runtime);

    static partial void OnPauseMenu(PauseMenuMainPage menu, UIPageHandler handler, PauseMenuControlsPage controls, Button after, Func<string, Action, Button, Button> add);

    static partial void UnmoddedWanted(ref bool on);

    static partial void OnHandPictured(Texture2D picture, string name);

    static partial void ScoutIdOf(Photon.Realtime.Player player, ref string id);
}
