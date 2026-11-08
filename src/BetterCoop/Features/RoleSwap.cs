using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterCoop.Features;

/// <summary>
/// Role Swap: both partners press Swap Places in the pause menu within 30
/// seconds, and the Troop Leader runs it as an operation (ADR 0004). Each
/// player's request is a player property naming the partner and the server
/// time it was made, so it lapses by itself and dies with the Pair.
/// </summary>
[HarmonyPatch]
internal static class RoleSwap
{
    private const double RequestSeconds = 30;

    /// <summary>The local request ahead of its echo: partner actor and server time, or none.</summary>
    private static (int Partner, double At)? pending;

    private static bool partnerOffered;

    internal static bool Pending => pending != null;

    /// <summary>Whether the local player may ask now: alive, paired, on the island, and no operation under way.</summary>
    internal static bool CanRequest(out Character partner)
    {
        partner = null!;
        Character local = Character.localCharacter;
        return local != null
            && !local.data.dead
            && GameHandler.IsOnIsland
            && !PairShuffle.Busy
            && PairRoster.TryGetPartner(local, out partner)
            && !partner.data.dead;
    }

    internal static void Toggle()
    {
        if (pending != null)
        {
            Withdraw(Strings.SwapCancelled);
            return;
        }

        if (!CanRequest(out Character partner))
        {
            return;
        }

        int actor = PairRoster.ActorOf(partner);
        pending = (actor, PhotonNetwork.Time);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            [RoomKeys.SwapRequest] = new object[] { actor, pending.Value.At },
        });
        Plugin.Verbose($"Asked {actor} to swap places.");
        CoopNotice.Info(Strings.SwapAsked(partner.characterName));
    }

    private static void Withdraw(string? notice)
    {
        pending = null;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { [RoomKeys.SwapRequest] = null });
        if (notice != null)
        {
            CoopNotice.Info(notice);
        }
    }

    internal static void OnJoinedRoom()
    {
        pending = null;
        partnerOffered = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(RoomKeys.SwapRequest))
        {
            Withdraw(null);
        }
    }

    /// <summary>Lapses the local request: after 30 seconds, when the Pair changes, or for a Pair Shuffle.</summary>
    internal static void Tick()
    {
        if (pending is not (int partner, double at))
        {
            return;
        }

        Character local = Character.localCharacter;
        if (PairShuffle.Current is { Done: false, IsShuffle: true })
        {
            Withdraw(Strings.SwapCancelledByShuffle);
        }
        else if (PhotonNetwork.Time - at > RequestSeconds)
        {
            Withdraw(Strings.SwapExpired);
        }
        else if (local == null
            || local.data.dead
            || !GameHandler.IsOnIsland
            || !PairRoster.TryGetPair(local, out Pair pair)
            || pair.PartnerOf(PairRoster.ActorOf(local)) != partner)
        {
            Withdraw(Strings.SwapCancelled);
        }
    }

    /// <summary>On the Troop Leader: a Pair whose partners both ask in time swaps places.</summary>
    internal static void HostTick()
    {
        if (PairShuffle.Busy)
        {
            return;
        }

        double now = PhotonNetwork.Time;
        foreach (Pair pair in PairRoster.Pairs)
        {
            PhotonPlayer? carrier = PhotonNetwork.CurrentRoom.GetPlayer(pair.Carrier);
            PhotonPlayer? rider = PhotonNetwork.CurrentRoom.GetPlayer(pair.Rider);
            if (carrier == null
                || rider == null
                || !Asks(carrier, pair.Rider, now)
                || !Asks(rider, pair.Carrier, now)
                || !PairShuffle.Request(OperationKind.RoleSwap, pair.Carrier, pair.Rider))
            {
                continue;
            }

            var clear = new PhotonHashtable { [RoomKeys.SwapRequest] = null };
            carrier.SetCustomProperties(clear);
            rider.SetCustomProperties(clear);
            return;
        }
    }

    private static bool Asks(PhotonPlayer player, int partner, double now) =>
        ReadRequest(player) is (int asked, double at) && asked == partner && now - at <= RequestSeconds;

    private static (int Partner, double At)? ReadRequest(PhotonPlayer player) =>
        player.CustomProperties.TryGetValue(RoomKeys.SwapRequest, out object value)
            && value is object[] { Length: 2 } parts
            && parts[0] is int partner
            && parts[1] is double at
                ? (partner, at)
                : null;

    /// <summary>Tells the partner about a request, and follows the Troop Leader clearing the local one.</summary>
    internal static void OnRequestChanged(PhotonPlayer player, PhotonHashtable changed)
    {
        if (!changed.ContainsKey(RoomKeys.SwapRequest))
        {
            return;
        }

        if (player.IsLocal)
        {
            if (ReadRequest(player) == null)
            {
                pending = null;
            }

            return;
        }

        Character local = Character.localCharacter;
        if (local == null
            || !PairRoster.TryGetPair(local, out Pair pair)
            || pair.PartnerOf(PairRoster.ActorOf(local)) != player.ActorNumber)
        {
            return;
        }

        int me = PairRoster.ActorOf(local);
        if (ReadRequest(player) is (int asked, double at) && asked == me && PhotonNetwork.Time - at <= RequestSeconds)
        {
            partnerOffered = true;
            if (pending == null)
            {
                CoopNotice.Info(Strings.SwapOffered(player.NickName));
            }

            return;
        }

        if (partnerOffered)
        {
            partnerOffered = false;
            bool swapping = PairShuffle.Current is { Kind: OperationKind.RoleSwap } op
                && op.Involves(me)
                && PhotonNetwork.Time - op.At < Operation.CountdownSeconds + 2;
            if (!swapping)
            {
                CoopNotice.Info(Strings.SwapWithdrawn(player.NickName));
            }
        }
    }

    /// <summary>
    /// The other menu buttons open another page; this one stays, so after a
    /// mouse click it would keep the selected look. A gamepad keeps its
    /// selection to navigate.
    /// </summary>
    private static void OnClicked()
    {
        Toggle();
        if (InputHandler.GetCurrentUsedInputScheme() != InputScheme.Gamepad && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>Adds Swap Places to the pause menu, after Resume.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    private static void AddButton(PauseMenuMainPage __instance)
    {
        Button template = __instance.m_settingsButton;
        Transform parent = template.transform.parent;
        GameObject clone = Object.Instantiate(template.gameObject, parent);
        clone.name = "BetterCoop.SwapPlaces";
        foreach (LocalizedText text in clone.GetComponentsInChildren<LocalizedText>(true))
        {
            text.autoSet = false;
            text.enabled = false;
        }

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OnClicked);
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        ColorBlock colors = button.colors;
        Plugin.Verbose(
            $"Swap Places colours: normal {colors.normalColor}, highlighted {colors.highlightedColor}, "
            + $"selected {colors.selectedColor}, disabled {colors.disabledColor}; transition {button.transition}.");
        clone.AddComponent<SwapPlacesButton>().Bind(button, clone.GetComponentInChildren<TMP_Text>(true));

        if (parent.GetComponent<LayoutGroup>() != null)
        {
            clone.transform.SetSiblingIndex(__instance.m_resumeButton.transform.GetSiblingIndex() + 1);
            Plugin.Verbose("Added Swap Places to the pause menu's layout.");
            return;
        }

        // Without a layout group, sit one step below the lowest menu button.
        var buttons = new[]
            {
                __instance.m_resumeButton, __instance.m_settingsButton, __instance.m_accoladesButton,
                __instance.m_controllsButton, __instance.m_inviteButton, __instance.m_quitButton,
            }
            .Where(b => b != null && b.transform.parent == parent)
            .Select(b => (RectTransform)b.transform)
            .OrderByDescending(r => r.anchoredPosition.y)
            .ToList();
        float step = buttons.Count > 1 ? buttons[0].anchoredPosition.y - buttons[1].anchoredPosition.y : 60f;
        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = buttons.Count > 0
            ? buttons[^1].anchoredPosition - new Vector2(0, step)
            : rect.anchoredPosition - new Vector2(0, step);
        Plugin.Verbose($"Added Swap Places to the pause menu {step:F0} below its last button.");
    }
}

/// <summary>Keeps the Swap Places label and availability current while the pause menu is open.</summary>
internal sealed class SwapPlacesButton : MonoBehaviour
{
    private Button button = null!;

    private TMP_Text? label;

    internal void Bind(Button target, TMP_Text? text)
    {
        button = target;
        label = text;
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (button == null)
        {
            return;
        }

        bool pending = RoleSwap.Pending;
        button.interactable = pending || RoleSwap.CanRequest(out _);
        if (label != null)
        {
            string text = pending ? Strings.CancelSwap : Strings.SwapPlaces;
            if (label.text != text)
            {
                label.text = text;
            }
        }
    }
}
