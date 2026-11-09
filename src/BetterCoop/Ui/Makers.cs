using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Ui.Paper;
using Steamworks;
using UnityEngine;

namespace BetterCoop.Ui;

/// <summary>The mod's makers, pencilled at the foot of the Roster; each name opens their Steam profile.</summary>
internal static class Makers
{
    private static readonly (string Name, string Profile)[] People =
    [
        ("kawalain", "https://steamcommunity.com/profiles/76561198436496102"),
        ("Puro", "https://steamcommunity.com/profiles/76561198105109754"),
    ];

    public static void Add(PaperList list) =>
        list.Credits(People.Select(p => (p.Name, (Action)(() => Open(p.Profile)))).ToList());

    /// <summary>Opens a page in the Steam overlay, or in the browser when the overlay is off.</summary>
    private static void Open(string url)
    {
        try
        {
            if (SteamUtils.IsOverlayEnabled())
            {
                SteamFriends.ActivateGameOverlayToWebPage(url);
                return;
            }
        }
        catch (Exception exception)
        {
            // Steam not running: the browser still opens it.
            Plugin.Verbose($"The Steam overlay could not open {url}: {exception.Message}");
        }

        Application.OpenURL(url);
    }
}
