using HarmonyLib;
using UnityEngine;
using Zorro.ControllerSupport;

namespace BetterCoop.Features;

/// <summary>
/// The Rider looks through the Shared Body's binoculars. The game opens the
/// binocular view only on the client that used them, the Carrier's, so the
/// Rider's client opens the same view when the Carrier's binoculars open.
/// The view turns with the local look, so the Rider aims it without delay,
/// and the Rider's own scrolling zooms it as it zooms the Carrier's.
/// </summary>
[HarmonyPatch]
internal static class RiderBinoculars
{
    /// <summary>The Carrier's binoculars as last opened, seen on the Rider's client.</summary>
    private static Action_ShowBinocularOverlay? opened;

    /// <summary>The binoculars whose view the Rider's camera shows.</summary>
    private static Action_ShowBinocularOverlay? shown;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Action_ShowBinocularOverlay), nameof(Action_ShowBinocularOverlay.ToggleUseRPC))]
    private static void NoteCarrierBinoculars(Action_ShowBinocularOverlay __instance, bool open)
    {
        Character? carrier = RiderHud.ObservedCarrier();
        if (carrier == null || __instance.item.holderCharacter != carrier || __instance.isProp)
        {
            return;
        }

        opened = open ? __instance : null;
    }

    /// <summary>Called every frame: the Rider's view follows the Carrier's held binoculars.</summary>
    internal static void Keep()
    {
        Character? carrier = RiderHud.ObservedCarrier();
        Action_ShowBinocularOverlay? wanted =
            opened != null && carrier != null && carrier.data.currentItem == opened.item ? opened : null;
        if (wanted == shown)
        {
            return;
        }

        // A destroyed item turns its view off itself; only a live one needs closing.
        if (shown != null)
        {
            shown.featureManager.setFeatureActive(false);
            if (MainCamera.instance.camOverride == shown.cameraOverride)
            {
                MainCamera.instance.SetCameraOverride(null);
            }
        }

        if (wanted != null)
        {
            wanted.featureManager.setFeatureActive(true);
            MainCamera.instance.SetCameraOverride(wanted.cameraOverride);
        }

        Plugin.Verbose($"Rider binocular view {(wanted != null ? "open" : "closed")}.");
        shown = wanted;
        if (wanted == null)
        {
            opened = null;
        }
    }

    /// <summary>The Rider's scroll zooms the Rider's view, as the game's binoculars do.</summary>
    internal static void Zoom(CharacterInput input)
    {
        if (shown == null || shown.item.GetComponentInChildren<Actions_Binoculars>() is not Actions_Binoculars zoom)
        {
            return;
        }

        if (input.scrollInput != 0f && InputHandler.GetCurrentUsedInputScheme() == InputScheme.KeyboardMouse)
        {
            zoom.cameraOverride.AdjustFOV(-input.scrollInput * zoom.scrollSpeed);
        }

        if (input.scrollForwardIsPressed)
        {
            zoom.cameraOverride.AdjustFOV(-zoom.scrollSpeedButton * Time.deltaTime);
        }

        if (input.scrollBackwardIsPressed)
        {
            zoom.cameraOverride.AdjustFOV(zoom.scrollSpeedButton * Time.deltaTime);
        }
    }
}
