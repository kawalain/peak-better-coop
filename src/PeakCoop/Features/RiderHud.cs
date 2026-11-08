using HarmonyLib;
using UnityEngine;

namespace PeakCoop.Features;

/// <summary>
/// The Rider's HUD shows the Shared Body: the Carrier's hand slots, stamina,
/// and statuses. The game draws its HUD for the observed character, so while
/// the carry holds the Rider observes their Carrier.
/// </summary>
[HarmonyPatch]
internal static class RiderHud
{
    private static int cachedFrame = -1;

    private static Character? cachedCarrier;

    internal static Character? ObservedCarrier()
    {
        if (cachedFrame != Time.frameCount)
        {
            cachedFrame = Time.frameCount;
            cachedCarrier = RiderView.TryGetCarrierOfLocalRider(out Character carrier) ? carrier : null;
        }

        return cachedCarrier;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.observedCharacter), MethodType.Getter)]
    private static void ObserveCarrier(ref Character __result)
    {
        if (MainCameraMovement.specCharacter == null && ObservedCarrier() is Character carrier)
        {
            __result = carrier;
        }
    }
}

/// <summary>Keeps the Rider's slot highlight in step with the Carrier's choice.</summary>
internal sealed class RiderHudRefresher : MonoBehaviour
{
    private float nextRefresh;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh || GUIManager.instance == null)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + 0.1f;
        if (RiderHud.ObservedCarrier() != null)
        {
            GUIManager.instance.UpdateItemSelections();
        }
    }
}
