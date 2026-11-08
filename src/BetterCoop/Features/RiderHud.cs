using System;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace BetterCoop.Features;

/// <summary>
/// The Rider's HUD shows the Shared Body: the Carrier's hand slots, stamina,
/// and statuses. The game draws its HUD for the observed character, so a
/// living Rider observes their Carrier, also while the carry is briefly
/// broken (after a revival, before the Carrier picks the Rider up again).
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
            Character local = Character.localCharacter;
            cachedCarrier = local != null
                && !local.data.dead
                && PairRoster.RoleOf(local) == PairRole.Rider
                && PairRoster.TryGetPartner(local, out Character carrier)
                    ? carrier
                    : null;
        }

        return cachedCarrier;
    }

    /// <summary>Set while a game check that hides details from spectators runs for the Rider's own HUD.</summary>
    private static bool asOwnHud;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.observedCharacter), MethodType.Getter)]
    private static void ObserveCarrier(ref Character __result)
    {
        if (!asOwnHud && MainCameraMovement.specCharacter == null && ObservedCarrier() is Character carrier)
        {
            __result = carrier;
        }
    }

    /// <summary>
    /// The game shows an item's remaining uses only on one's own HUD; the
    /// Rider's HUD is the Shared Body's own, so it shows them too.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.TrySetFuel))]
    private static void ShowFuelAsOwn(out bool __state)
    {
        __state = asOwnHud;
        asOwnHud = true;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.TrySetFuel))]
    private static void EndFuelAsOwn(bool __state) => asOwnHud = __state;
}

/// <summary>Keeps the Rider's HUD and view in step with the Carrier: slot highlight, item prompts, binocular view, hidden bodies.</summary>
internal sealed class RiderHudRefresher : MonoBehaviour
{
    private float nextRefresh;

    private Character? selectionOf;

    private int shownSelection = int.MinValue;

    private void Update()
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("RiderHudRefresher.Update", exception);
        }
    }

    private void Tick()
    {
        RiderBinoculars.Keep();
        RiderView.Keep();
        if (Time.unscaledTime < nextRefresh || GUIManager.instance == null)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + 0.1f;
        if (RiderHud.ObservedCarrier() is Character carrier)
        {
            // The game highlights a slot when the local player switches; the
            // Carrier switches on another client. Each highlight replays the
            // slot's grow animation, so it runs only when the slot changes.
            Optionable<byte> selected = carrier.refs.items.currentSelectedSlot;
            int selection = selected.IsSome ? selected.Value : -1;
            if (carrier != selectionOf || selection != shownSelection)
            {
                selectionOf = carrier;
                shownSelection = selection;
                GUIManager.instance.UpdateItemSelections();
            }

            // Burning and spooling items count down on the Carrier's client
            // only, and an inventory resync brings back older values.
            SharedViewHud.ShowCarrierSlotUses(carrier);

            // The game refreshes item prompts when the local item changes;
            // the Rider holds none, so follow the Carrier's.
            GUIManager.instance.UpdateItemPrompts();
        }
    }
}
