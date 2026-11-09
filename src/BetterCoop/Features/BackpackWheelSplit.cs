using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ExitGames.Client.Photon;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zorro.Core;

namespace BetterCoop.Features;

/// <summary>
/// The Control Split in the backpack wheel (ADR 0005). The Carrier opens a
/// wheel and holds it open with the keyboard, and both screens show it. One
/// side picks with the mouse and the other watches it point: the Rider by
/// default, or the Carrier when the Run rule says so. A pick always runs on
/// the Carrier's client, since only the owner of the Shared Body can move its
/// items.
/// </summary>
/// <remarks>
/// The game has no way to open the backpack you wear; the Carrier opens their
/// own by holding the back-slot key, which then holds the wheel open as
/// interact does for any other backpack. A tap still brings the back item to
/// hand, on release.
/// </remarks>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class BackpackWheelSplit
{
    private const byte BackSlot = Player.BACKPACKSLOTINDEX;

    private const int SoloRider = -2;

    private static readonly BackSlotHold Hold = new();

    private static int trackedFrame = -1;

    private static int tapFrame = -1;

    /// <summary>Whether the back-slot key, not interact, holds the local wheel open.</summary>
    private static bool localWheelOnBackSlot;

    // Carrier side: the Carrier's open wheel, shared with the Rider.
    private static CarrierWheel? held;
    private static int lastSession;
    private static bool carrierPicking;

    // Rider side: the Carrier's wheel as the Rider's screen shows it.
    private static Character? shownFor;
    private static int shownSession = -1;
    private static int shownFromActor = -1;
    private static bool shownRiderPicks;
    private static bool showing;
    private static bool swallowPrimary;

    private static int lastHoverSent = int.MinValue;

    private sealed class CarrierWheel(int session, int slotCount, bool riderPicks, int riderActor)
    {
        public int Session { get; } = session;

        public int SlotCount { get; } = slotCount;

        public bool RiderPicks { get; } = riderPicks;

        public int RiderActor { get; } = riderActor;

        public bool Solo => RiderActor == SoloRider;
    }

    /// <summary>
    /// The character a wheel works for: on the Rider's client, the Shared Body
    /// whose wheel it shows; otherwise the local character, as in the game.
    /// </summary>
    internal static Character WheelCharacter() => shownFor != null ? shownFor : Character.localCharacter;

    /// <summary>Whether this screen's wheel only shows where the other side points.</summary>
    private static bool LocalWatches =>
        (showing && !shownRiderPicks) || (held != null && !held.Solo && held.RiderPicks);

    /// <summary>Whether this screen's wheel picks by click and sends the pick to the Carrier's side.</summary>
    private static bool LocalPicksForCarrier =>
        (showing && shownRiderPicks) || (held != null && held.Solo && held.RiderPicks);

    // ---- Back-slot key: tap or hold ----

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
    private static void TrackBackSlotKey(CharacterInput __instance) => TrackBackSlot(__instance);

    /// <summary>
    /// Holds back the back-slot key's press while it may still become a hold,
    /// and hands the game the press on release when it was a tap.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.HotbarKeyWasPressed))]
    private static void DeferBackSlotKey(CharacterInput __instance, int key, ref bool __result)
    {
        if (key != BackSlot)
        {
            return;
        }

        TrackBackSlot(__instance);
        if (Hold.Tracking)
        {
            __result = false;
        }
        else if (tapFrame == Time.frameCount)
        {
            tapFrame = -1;
            __result = true;
        }
    }

    /// <summary>Once a frame, from whichever of input sampling or hotbar switching asks first.</summary>
    private static void TrackBackSlot(CharacterInput input)
    {
        Character local = Character.localCharacter;
        if (local == null || local.input != input || trackedFrame == Time.frameCount)
        {
            return;
        }

        trackedFrame = Time.frameCount;
        if (!ControlSplit.LocalCarrier(out _, out _))
        {
            Hold.Reset();
            return;
        }

        InputAction key = CharacterInput.hotbarActions[BackSlot];
        if (!Hold.Tracking)
        {
            if (!input.itemSwitchBlocked && key.WasPressedThisFrame() && CanOpenOwnBackpack(local, out _, out _))
            {
                Hold.Begin(Time.unscaledTime);
            }

            return;
        }

        switch (Hold.Step(key.IsPressed(), Time.unscaledTime))
        {
            case BackSlotHold.Outcome.Tap:
                tapFrame = Time.frameCount;
                break;
            case BackSlotHold.Outcome.Open:
                OpenOwnBackpack(local);
                break;
        }
    }

    /// <summary>A backpack, fanny pack, or jetpack on the back, not in hand, and nothing in the way.</summary>
    private static bool CanOpenOwnBackpack(Character local, out int slotCount, out BackpackSlot.BackpackType type)
    {
        slotCount = 0;
        type = BackpackSlot.BackpackType.None;
        if (folded || !local.data.fullyConscious || local.data.isClimbingAnything || !local.CanDoInput())
        {
            return false;
        }

        // Opened from the hand, the backpack itself would be the item to stash.
        if (local.refs.items.currentSelectedSlot.IsSome && local.refs.items.currentSelectedSlot.Value == BackSlot)
        {
            return false;
        }

        ItemSlot back = local.player.backpackSlot;
        if (back.IsEmpty() || back.prefab is not Backpack backpack || backpack.backpackType == BackpackSlot.BackpackType.Rocketpack)
        {
            return false;
        }

        slotCount = backpack.slotCount;
        type = backpack.backpackType;
        return true;
    }

    private static void OpenOwnBackpack(Character local)
    {
        if (!CanOpenOwnBackpack(local, out int slotCount, out BackpackSlot.BackpackType type))
        {
            return;
        }

        GUIManager.instance.OpenBackpackWheel(BackpackReference.GetFromEquippedBackpack(local), slotCount, type);
        if (GUIManager.instance.backpackWheel.gameObject.activeSelf)
        {
            localWheelOnBackSlot = true;
            Plugin.Verbose("Opened the Carrier's own backpack from the back-slot key.");
        }
    }

    /// <summary>Whether the key that opened the wheel is still held; the game asks only for interact.</summary>
    private static bool WheelHeld(CharacterInput input)
    {
        if (showing)
        {
            // The Carrier holds the key; their client says when it lets go.
            return true;
        }

        return localWheelOnBackSlot ? input.HotbarKeyIsPressed(BackSlot) : input.interactIsPressed;
    }

    /// <summary>
    /// Set when the wheel's key check could not be rewritten after the mod
    /// loaded: the wheel then works as in the unmodded game, for the Carrier.
    /// </summary>
    private static bool folded;

    /// <summary>
    /// Harmony runs this again whenever any mod patches the wheel's update.
    /// At the mod's own load a changed wheel throws (ADR 0003); on a later
    /// patch by another mod it must not break that mod's patching, so the
    /// code stays as it is and the shared wheel folds.
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BackpackWheel), "Update")]
    private static IEnumerable<CodeInstruction> AskWhichKeyHolds(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo interact = AccessTools.Field(typeof(CharacterInput), nameof(CharacterInput.interactIsPressed));
        MethodInfo keyHeld = AccessTools.Method(typeof(BackpackWheelSplit), nameof(WheelHeld));
        List<CodeInstruction> original = instructions.ToList();
        if (original.Count(instruction => instruction.LoadsField(interact)) == 1)
        {
            return original.Select(instruction => instruction.LoadsField(interact)
                ? new CodeInstruction(OpCodes.Call, keyHeld).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction)
                : instruction);
        }

        string problem = $"BackpackWheel.Update reads interact {original.Count(instruction => instruction.LoadsField(interact))} times; expected once.";
        if (!Plugin.Patched)
        {
            // ADR 0003: a changed wheel disables the mod rather than half-patching it.
            throw new InvalidOperationException(problem);
        }

        if (!folded)
        {
            folded = true;
            Plugin.Log.LogWarning($"Another mod changed the backpack wheel ({problem}). The wheel now works as in the unmodded game, for the Carrier only.");
        }

        return original;
    }

    // ---- Carrier side: share the open wheel with the Rider ----

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.OpenBackpackWheel))]
    private static void NoteWheelBlocked(GUIManager __instance, out bool __state) =>
        __state = __instance.wheelActive || __instance.windowBlockingInput;

    /// <summary>A wheel the Carrier opened goes to the Rider's screen too.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.OpenBackpackWheel))]
    private static void ShareWheelWithRider(
        GUIManager __instance,
        BackpackReference backpackReference,
        int slotCount,
        BackpackSlot.BackpackType backpackType,
        bool __state)
    {
        if (__state
            || folded
            || showing
            || held != null
            || !__instance.backpackWheel.gameObject.activeSelf
            || !backpackReference.exists
            || !ControlSplit.LocalCarrier(out Character rider, out bool solo))
        {
            return;
        }

        held = new CarrierWheel(++lastSession, slotCount, RunRules.RiderPicksFromBackpack, solo ? SoloRider : PairRoster.ActorOf(rider));
        lastHoverSent = int.MinValue;
        Plugin.Verbose($"Backpack wheel #{held.Session} open; {(held.RiderPicks ? "the Rider" : "the Carrier")} picks.");
        if (!held.Solo)
        {
            Send(held.RiderActor, WheelSignal.Open(
                held.Session,
                backpackReference.type == BackpackReference.BackpackType.Equipped,
                held.RiderPicks,
                backpackReference.view.ViewID,
                slotCount,
                (byte)backpackType));
        }
    }

    /// <summary>Carries out the Rider's pick as the game's wheel would, judging it here first.</summary>
    private static void CarrierPick(WheelSignal pick)
    {
        BackpackWheel wheel = GUIManager.instance.backpackWheel;
        if (held == null || !held.RiderPicks || pick.Session != held.Session || !wheel.gameObject.activeSelf)
        {
            return;
        }

        if (wheel.backpack.exists && SlotExists(wheel, held, pick))
        {
            wheel.chosenSlice = Optionable<BackpackWheelSlice.SliceData>.None;
            wheel.Hover(new BackpackWheelSlice.SliceData
            {
                isBackpackWear = pick.Slice == WheelSlice.Wear,
                isStashSlice = pick.Slice == WheelSlice.Stash,
                isJetpackFuelSlice = pick.Slice == WheelSlice.Fuel,
                backpackReference = wheel.backpack,
                slotID = pick.Slice == WheelSlice.Wear ? byte.MaxValue : pick.SlotId,
            });
            if (wheel.chosenSlice.IsSome)
            {
                Plugin.Verbose($"Rider picked {pick.Slice} {pick.SlotId} from backpack wheel #{pick.Session}.");
                carrierPicking = true;
                try
                {
                    wheel.Choose();
                }
                finally
                {
                    carrierPicking = false;
                }
            }
        }

        GUIManager.instance.CloseBackpackWheel();
    }

    private static bool SlotExists(BackpackWheel wheel, CarrierWheel wheelState, WheelSignal pick) =>
        pick.Slice != WheelSlice.Slot
        || (pick.SlotId < wheelState.SlotCount && pick.SlotId < wheel.backpack.GetData().itemSlots.Length);

    /// <summary>A picking Rider decides; the Carrier's release only closes the wheel.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackpackWheel), nameof(BackpackWheel.Choose))]
    private static bool ChooseOnlyForPicker() =>
        carrierPicking || (!showing && (held == null || !held.RiderPicks));

    // ---- Rider side: show the Carrier's wheel ----

    private static void RiderOpen(WheelSignal open, Character carrier, int fromActor)
    {
        CloseShown();
        PhotonView view = PhotonView.Find(open.ViewId);

        // A folded wheel would read this client's own interact key and close at once.
        if (folded || view == null)
        {
            return;
        }

        BackpackReference backpack;
        if (open.Equipped && view.TryGetComponent(out Character wearer))
        {
            backpack = BackpackReference.GetFromEquippedBackpack(wearer);
        }
        else if (!open.Equipped && view.TryGetComponent(out Item item))
        {
            backpack = BackpackReference.GetFromBackpackItem(item);
        }
        else
        {
            return;
        }

        shownFor = carrier;
        shownSession = open.Session;
        shownFromActor = fromActor;
        shownRiderPicks = open.RiderPicks;
        showing = true;
        lastHoverSent = int.MinValue;
        GUIManager.instance.OpenBackpackWheel(backpack, open.SlotCount, (BackpackSlot.BackpackType)open.BackpackType);
        if (!GUIManager.instance.backpackWheel.gameObject.activeSelf)
        {
            // Another wheel or a menu is open on the Rider's screen.
            ForgetShown();
        }
    }

    private static void CloseShown()
    {
        if (showing)
        {
            GUIManager.instance.CloseBackpackWheel();
        }
    }

    private static void ForgetShown()
    {
        showing = false;
        shownFor = null;
        shownSession = -1;
        shownFromActor = -1;
        shownRiderPicks = false;
    }

    /// <summary>Whichever side's wheel closes, the Carrier's closing ends it for the Rider too.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.CloseBackpackWheel))]
    private static void EndWheel()
    {
        localWheelOnBackSlot = false;
        ForgetShown();
        if (held == null)
        {
            return;
        }

        CarrierWheel closing = held;
        held = null;
        Plugin.Verbose($"Backpack wheel #{closing.Session} closed.");
        if (!closing.Solo)
        {
            Send(closing.RiderActor, WheelSignal.Close(closing.Session));
        }
    }

    /// <summary>The picker's click on a slice picks it.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BackpackWheel), "Update")]
    private static void PickForCarrier(BackpackWheel __instance)
    {
        if (!LocalPicksForCarrier
            || !__instance.gameObject.activeSelf
            || !__instance.chosenSlice.IsSome
            || CharacterInput.action_usePrimary == null
            || !CharacterInput.action_usePrimary.WasPressedThisFrame())
        {
            return;
        }

        BackpackWheelSlice.SliceData chosen = __instance.chosenSlice.Value;
        WheelSlice slice = SliceOf(chosen);
        int session = held != null ? held.Session : shownSession;
        WheelSignal pick = WheelSignal.Pick(session, slice, slice == WheelSlice.Wear ? (byte)0 : chosen.slotID);

        // The click that picked must not reach the Shared Body as a use.
        swallowPrimary = true;
        if (held != null)
        {
            // Solo Pair: the Carrier's side is this client.
            CarrierPick(pick);
            return;
        }

        int carrierActor = shownFromActor;
        CloseShown();
        PhotonNetwork.RaiseEvent(
            RoomKeys.BackpackWheelEvent,
            pick.Encode(),
            new RaiseEventOptions { TargetActors = [carrierActor] },
            SendOptions.SendReliable);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    /// <summary>
    /// Whether the Rider's use buttons stay as they were: while a wheel is on
    /// the Rider's screen, and until the click that picked is let go.
    /// </summary>
    internal static bool HoldsRiderButtons()
    {
        if (showing || (held != null && held.Solo))
        {
            return true;
        }

        if (swallowPrimary && CharacterInput.action_usePrimary != null && CharacterInput.action_usePrimary.IsPressed())
        {
            return true;
        }

        swallowPrimary = false;
        return false;
    }

    // ---- Both sides: the watching screen follows the picker's pointer ----

    /// <summary>The watching side's mouse and stick point at nothing; the picker's pointer shows instead.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackpackWheelSlice), nameof(BackpackWheelSlice.OnPointerEnter))]
    private static bool WatcherDoesNotHover() => !LocalWatches;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackpackWheelSlice), nameof(BackpackWheelSlice.OnPointerExit))]
    private static bool WatcherDoesNotDehover() => !LocalWatches;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackpackWheel), "TestSelectSliceGamepad")]
    private static bool WatcherDoesNotSteer() => !LocalWatches;

    private static WheelSlice SliceOf(BackpackWheelSlice.SliceData data) =>
        data.isBackpackWear ? WheelSlice.Wear
        : data.isStashSlice ? WheelSlice.Stash
        : data.isJetpackFuelSlice ? WheelSlice.Fuel
        : WheelSlice.Slot;

    /// <summary>The picker sends where it points whenever that changes.</summary>
    private static void SendHover()
    {
        BackpackWheel wheel = GUIManager.instance.backpackWheel;
        int to;
        int session;
        if (showing && shownRiderPicks)
        {
            to = shownFromActor;
            session = shownSession;
        }
        else if (held != null && !held.Solo && !held.RiderPicks)
        {
            to = held.RiderActor;
            session = held.Session;
        }
        else
        {
            return;
        }

        if (!wheel.gameObject.activeSelf)
        {
            return;
        }

        bool pointing = wheel.chosenSlice.IsSome;
        WheelSlice slice = pointing ? SliceOf(wheel.chosenSlice.Value) : WheelSlice.Slot;
        byte slot = pointing && slice == WheelSlice.Slot ? wheel.chosenSlice.Value.slotID : (byte)0;
        int key = WheelSignal.HoverKey(pointing ? slice : null, slot);
        if (key == lastHoverSent)
        {
            return;
        }

        lastHoverSent = key;
        Send(to, pointing ? WheelSignal.Hover(session, slice, slot) : WheelSignal.NoHover(session));
    }

    /// <summary>Shows on this wheel the slice the picker points at, as the game shows a stick's choice.</summary>
    private static void ShowHover(WheelSignal hover)
    {
        BackpackWheel wheel = GUIManager.instance.backpackWheel;
        if (!wheel.gameObject.activeSelf)
        {
            return;
        }

        BackpackWheelSlice? target = hover.HasSlice
            ? wheel.slices.Append(wheel.jetpackSlice).FirstOrDefault(slice => Matches(slice, hover))
            : null;
        wheel.chosenSlice = Optionable<BackpackWheelSlice.SliceData>.None;
        wheel.chosenItemText.text = "";
        EventSystem events = EventSystem.current;
        if (target == null)
        {
            if (events != null)
            {
                events.SetSelectedGameObject(null);
            }

            return;
        }

        if (events != null)
        {
            events.SetSelectedGameObject(target.button.gameObject);
        }

        target.Hover();
    }

    private static bool Matches(BackpackWheelSlice slice, WheelSignal hover)
    {
        if (slice == null || !slice.gameObject.activeSelf)
        {
            return false;
        }

        return hover.Slice switch
        {
            WheelSlice.Wear => slice.isBackpackWear && !slice.stashSlice && !slice.jetpackFuelSlice,
            WheelSlice.Stash => slice.stashSlice,
            WheelSlice.Fuel => slice.jetpackFuelSlice,
            _ => !slice.stashSlice && !slice.jetpackFuelSlice && !slice.isBackpackWear && slice.backpackSlot == hover.SlotId,
        };
    }

    // ---- Every frame and the network ----

    /// <summary>Called every frame: sends the picker's pointer, and closes a wheel whose Pair is gone.</summary>
    internal static void Tick()
    {
        if (GUIManager.instance == null)
        {
            return;
        }

        if (held != null)
        {
            Character local = Character.localCharacter;
            bool samePair = local != null
                && ControlSplit.LocalCarrier(out Character rider, out bool solo)
                && (solo ? held.Solo : PairRoster.ActorOf(rider) == held.RiderActor);
            if (!samePair || !GUIManager.instance.backpackWheel.gameObject.activeSelf)
            {
                GUIManager.instance.CloseBackpackWheel();
                return;
            }
        }

        if (showing && !(RiderHud.ObservedCarrier() is Character carrier && carrier == shownFor))
        {
            CloseShown();
            return;
        }

        SendHover();
    }

    private static void Send(int actor, WheelSignal signal) =>
        PhotonNetwork.RaiseEvent(
            RoomKeys.BackpackWheelEvent,
            signal.Encode(),
            new RaiseEventOptions { TargetActors = [actor] },
            SendOptions.SendReliable);

    /// <summary>A Carrier's wheel opens on the Rider's screen a few times a second at most, however often it is sent.</summary>
    private static readonly RateGate OpenGate = new(capacity: 3, perSecond: 2);

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.BackpackWheelEvent || !WheelSignal.TryDecode(photonEvent.CustomData, out WheelSignal signal))
        {
            return;
        }

        // From the Rider, to the Carrier's open wheel.
        if (held != null && !held.Solo && photonEvent.Sender == held.RiderActor && signal.Session == held.Session)
        {
            if (signal.Kind == WheelSignalKind.Pick)
            {
                CarrierPick(signal);
            }
            else if (signal.Kind == WheelSignalKind.Hover && held.RiderPicks)
            {
                ShowHover(signal);
            }

            return;
        }

        // From the Carrier, to the Rider's screen.
        if (RiderHud.ObservedCarrier() is not Character carrier || photonEvent.Sender != PairRoster.ActorOf(carrier))
        {
            return;
        }

        switch (signal.Kind)
        {
            case WheelSignalKind.Open when OpenGate.Take(Time.unscaledTimeAsDouble):
                RiderOpen(signal, carrier, photonEvent.Sender);
                break;
            case WheelSignalKind.Close when showing && signal.Session == shownSession:
                CloseShown();
                break;
            case WheelSignalKind.Hover when showing && !shownRiderPicks && signal.Session == shownSession:
                ShowHover(signal);
                break;
        }
    }
}

/// <summary>
/// The game's wheel reads the local character for the held item, stash
/// labels, and reach. On the Rider's client that is the Rider, whose hands
/// stay empty; the wheel shown there works for the Shared Body instead.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class BackpackWheelShownCharacter
{
    private static readonly FieldInfo LocalCharacter = AccessTools.Field(typeof(Character), nameof(Character.localCharacter));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        List<MethodBase> targets = new[] { typeof(BackpackWheel), typeof(BackpackWheelSlice) }
            .SelectMany(AccessTools.GetDeclaredMethods)
            .Where(method => method.GetMethodBody() != null && ReadsLocalCharacter(method))
            .Cast<MethodBase>()
            .ToList();
        string[] needed = ["InitWheel", "Update", "Choose", "Hover"];
        string[] missing = needed
            .Where(name => !targets.Any(method => method.DeclaringType == typeof(BackpackWheel) && method.Name == name))
            .ToArray();
        if (missing.Length > 0)
        {
            // ADR 0003: a changed wheel disables the mod rather than half-patching it.
            throw new InvalidOperationException($"BackpackWheel no longer reads the local character in {string.Join(", ", missing)}.");
        }

        return targets;
    }

    private static bool ReadsLocalCharacter(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method).Any(instruction => instruction.LoadsField(LocalCharacter));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo wheelCharacter = AccessTools.Method(typeof(BackpackWheelSplit), nameof(BackpackWheelSplit.WheelCharacter));
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction.LoadsField(LocalCharacter)
                ? new CodeInstruction(OpCodes.Call, wheelCharacter).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction)
                : instruction;
        }
    }
}
