using ExitGames.Client.Photon;
using HarmonyLib;
using PeakCoop.Core;
using PeakCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// What each partner's screen says about the Control Split's actions. The
/// Rider uses items, so the Rider sees their prompts and progress (sent back
/// from the Carrier's client, where the use runs); the Carrier interacts, so
/// only the Carrier sees interaction prompts. Names show on both screens.
/// </summary>
[HarmonyPatch]
internal static class UseFeedback
{
    private static readonly ProgressFeed Feed = new();

    private static readonly ProgressSmoother RiderProgress = new();

    /// <summary>Whether the Shared Body's use is under way, on the Rider's client.</summary>
    internal static bool RiderSeesUse => RiderProgress.IsActive(Time.unscaledTime);

    /// <summary>Called every frame: the Carrier sends the progress of the Rider's action.</summary>
    internal static void SendFromCarrier()
    {
        Character local = Character.localCharacter;
        if (local == null
            || PairRoster.RoleOf(local) != PairRole.Carrier
            || !PairRoster.TryGetPartner(local, out Character rider))
        {
            Feed.Reset();
            return;
        }

        float progress = ProgressOf(local);
        if (!Feed.ShouldSend(progress, Time.unscaledTime, out bool reliable))
        {
            return;
        }

        PhotonNetwork.RaiseEvent(
            RoomKeys.UseProgressEvent,
            progress,
            new RaiseEventOptions { TargetActors = [PairRoster.ActorOf(rider)] },
            reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.UseProgressEvent
            || photonEvent.CustomData is not float progress
            || RiderHud.ObservedCarrier() is not Character carrier
            || photonEvent.Sender != PairRoster.ActorOf(carrier))
        {
            return;
        }

        RiderProgress.Receive(progress, Time.unscaledTime);
    }

    /// <summary>The progress bar the game would show the one using the item.</summary>
    private static float ProgressOf(Character character)
    {
        Item? item = character.data.currentItem;
        float use = item != null && item.shouldShowCastProgress ? item.progress : 0f;
        return Mathf.Clamp01(Mathf.Max(use, character.refs.items.climbingSpikeCastProgress));
    }

    /// <summary>The Rider's progress bar shows the progress of the Shared Body's use, smoothed between updates.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UI_UseItemProgress), nameof(UI_UseItemProgress.UpdateFillAmount))]
    private static bool ShowCarrierProgress(UI_UseItemProgress __instance, ref bool __result)
    {
        if (RiderHud.ObservedCarrier() == null)
        {
            RiderProgress.Reset();
            return true;
        }

        float shown = RiderProgress.Display(Time.unscaledTime);
        __result = shown > 0f;
        if (__result)
        {
            __instance.fill.fillAmount = shown;
        }

        return false;
    }

    /// <summary>The Rider sees what they look at by name only: interacting is the Carrier's.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.RefreshInteractablePrompt))]
    private static void RiderSeesNamesOnly(GUIManager __instance)
    {
        if (RiderHud.ObservedCarrier() == null)
        {
            return;
        }

        __instance.interactPromptPrimary.SetActive(false);
        __instance.interactPromptSecondary.SetActive(false);
        __instance.interactPromptHold.SetActive(false);
    }

    /// <summary>
    /// Use, secondary use, and scroll prompts of the held item show on the
    /// Rider's screen; drop and throw prompts on the Carrier's.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.UpdateItemPrompts))]
    private static void SplitItemPrompts(GUIManager __instance)
    {
        if (RiderHud.ObservedCarrier() is Character carrier)
        {
            ShowUsePrompts(__instance, carrier.data.currentItem);
            return;
        }

        Character local = Character.localCharacter;
        if (local != null && PairRoster.RoleOf(local) == PairRole.Carrier && PairRoster.TryGetPartner(local, out _))
        {
            __instance.itemPromptMain.gameObject.SetActive(false);
            __instance.itemPromptSecondary.gameObject.SetActive(false);
            __instance.itemPromptScroll.gameObject.SetActive(false);
        }
    }

    private static void ShowUsePrompts(GUIManager gui, Item? item)
    {
        // The game has already hidden every prompt, the HUD showing someone else.
        if (item == null)
        {
            return;
        }

        Item.ItemUIData ui = item.UIData;
        gui.itemPromptMain.text = gui.GetMainInteractPrompt(item);
        gui.itemPromptSecondary.text = gui.GetSecondaryInteractPrompt(item);
        gui.itemPromptScroll.text = LocalizedText.GetText(ui.scrollInteractPrompt);
        gui.itemPromptMain.gameObject.SetActive(ui.hasMainInteract && item.CanUsePrimary());
        gui.itemPromptSecondary.gameObject.SetActive(ui.hasSecondInteract && !ui.hideSecondInteract && item.CanUseSecondary());
        gui.itemPromptScroll.gameObject.SetActive(ui.hasScrollingInteract);
    }
}
