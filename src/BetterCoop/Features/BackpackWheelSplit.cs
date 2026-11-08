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
using UnityEngine.InputSystem;
using Zorro.Core;

namespace BetterCoop.Features;

/// <summary>
/// The Control Split in the backpack wheel (ADR 0005). The Carrier opens a
/// wheel and holds it open with the keyboard; the Rider's screen shows it and
/// the Rider's click picks a slice, which the Carrier's client carries out,
/// since only the owner of the Shared Body can move its items. A Run rule
/// hands the pick back to the Carrier, with the wheel on the Carrier's screen.
/// </summary>
/// <remarks>
/// The game has no way to open the backpack you wear; the Carrier opens their
/// own by holding the back-slot key, which then holds the wheel open as
/// interact does for any other backpack. A tap still brings the back item to
/// hand, on release.
/// </remarks>
[HarmonyPatch]
internal static class BackpackWheelSplit
{
    private const byte BackSlot = Player.BACKPACKSLOTINDEX;

    /// <summary>The game closes a wheel whose backpack is farther than this.</summary>
    private const float Reach = 6f;

    private const int SoloRider = -2;

    private static readonly BackSlotHold Hold = new();

    private static int trackedFrame = -1;

    private static int tapFrame = -1;

    /// <summary>Whether the back-slot key, not interact, holds the local wheel open.</summary>
    private static bool localWheelOnBackSlot;

    /// <summary>Set while the back-slot key opens a wheel, so the opening knows its key.</summary>
    private static bool openingOnBackSlot;

    // Carrier side: the wheel the Carrier holds open for the Rider.
    private static CarrierWheel? held;
    private static int lastSession;

    // Rider side: the Carrier's wheel as the Rider's screen shows it.
    private static Character? shownFor;
    private static int shownSession = -1;
    private static int shownFromActor = -1;
    private static bool showing;
    private static bool swallowPrimary;

    private sealed class CarrierWheel(
        int session,
        BackpackReference backpack,
        int slotCount,
        BackpackSlot.BackpackType type,
        bool onBackSlot,
        int riderActor)
    {
        public int Session { get; } = session;

        public BackpackReference Backpack { get; } = backpack;

        public int SlotCount { get; } = slotCount;

        public BackpackSlot.BackpackType Type { get; } = type;

        public bool OnBackSlot { get; } = onBackSlot;

        public int RiderActor { get; } = riderActor;
    }

    /// <summary>
    /// The character a wheel works for: on the Rider's client, the Shared Body
    /// whose wheel it shows; otherwise the local character, as in the game.
    /// </summary>
    internal static Character WheelCharacter() => shownFor != null ? shownFor : Character.localCharacter;

    // ---- Back-slot key: tap or hold ----

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
    private static void TrackBackSlotKey(CharacterInput __instance)
    {
        TrackBackSlot(__instance);
        if (held != null && Character.localCharacter != null && Character.localCharacter.input == __instance)
        {
            // The game freezes whoever holds a wheel open; the Carrier holds it with no wheel on screen.
            __instance.movementInput = Vector2.zero;
            __instance.jumpWasPressed = __instance.jumpIsPressed = false;
            __instance.sprintIsPressed = __instance.sprintWasPressed = false;
            __instance.sprintToggleIsPressed = __instance.sprintToggleWasPressed = false;
            __instance.crouchIsPressed = __instance.crouchWasPressed = __instance.crouchToggleWasPressed = false;
            __instance.dropWasPressed = __instance.dropIsPressed = __instance.dropWasReleased = false;
        }
    }

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
        if (held != null || !local.data.fullyConscious || local.data.isClimbingAnything || !local.CanDoInput())
        {
            return false;
        }

        // Opened from the hand, the backpack itself would be the item to stash.
        if (InHand(local))
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

    private static bool InHand(Character local) =>
        local.refs.items.currentSelectedSlot.IsSome && local.refs.items.currentSelectedSlot.Value == BackSlot;

    private static void OpenOwnBackpack(Character local)
    {
        if (!CanOpenOwnBackpack(local, out int slotCount, out BackpackSlot.BackpackType type))
        {
            return;
        }

        openingOnBackSlot = true;
        try
        {
            GUIManager.instance.OpenBackpackWheel(BackpackReference.GetFromEquippedBackpack(local), slotCount, type);
        }
        finally
        {
            openingOnBackSlot = false;
        }

        if (held == null && GUIManager.instance.backpackWheel.gameObject.activeSelf)
        {
            localWheelOnBackSlot = true;
        }

        Plugin.Verbose("Opened the Carrier's own backpack from the back-slot key.");
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

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BackpackWheel), "Update")]
    private static IEnumerable<CodeInstruction> AskWhichKeyHolds(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo interact = AccessTools.Field(typeof(CharacterInput), nameof(CharacterInput.interactIsPressed));
        MethodInfo keyHeld = AccessTools.Method(typeof(BackpackWheelSplit), nameof(WheelHeld));
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.LoadsField(interact))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, keyHeld).MoveLabelsFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (replaced != 1)
        {
            // ADR 0003: a changed wheel disables the mod rather than half-patching it.
            throw new InvalidOperationException($"BackpackWheel.Update reads interact {replaced} times; expected once.");
        }
    }

    // ---- Carrier side: hold the wheel open for the Rider ----

    /// <summary>On the Carrier's client, a wheel opening goes to the Rider's screen instead.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.OpenBackpackWheel))]
    private static bool HandWheelToRider(
        GUIManager __instance,
        BackpackReference backpackReference,
        int slotCount,
        BackpackSlot.BackpackType backpackType)
    {
        if (showing
            || !RunRules.RiderPicksFromBackpack
            || !ControlSplit.LocalCarrier(out Character rider, out bool solo))
        {
            return true;
        }

        if (held != null || __instance.wheelActive || __instance.windowBlockingInput || !backpackReference.exists)
        {
            return false;
        }

        held = new CarrierWheel(
            ++lastSession,
            backpackReference,
            slotCount,
            backpackType,
            openingOnBackSlot,
            solo ? SoloRider : PairRoster.ActorOf(rider));
        Plugin.Verbose($"Holding backpack wheel #{held.Session} open for the Rider.");
        SendToRider(WheelSignal.Open(
            held.Session,
            backpackReference.type == BackpackReference.BackpackType.Equipped,
            backpackReference.view.ViewID,
            slotCount,
            (byte)backpackType));
        return false;
    }

    /// <summary>The Carrier switches no hotbar slot while holding a wheel open, as the game's wheel blocks it.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterItems), "DoSwitching")]
    private static bool HoldingWheelDoesNotSwitch(CharacterItems __instance) =>
        !(held != null && __instance.character.IsLocal);

    /// <summary>Called every frame: closes the held wheel when its key is let go or the backpack is out of reach.</summary>
    internal static void Tick()
    {
        TickRider();
        if (held == null)
        {
            return;
        }

        Character local = Character.localCharacter;
        bool samePair = local != null
            && ControlSplit.LocalCarrier(out Character rider, out bool solo)
            && (solo ? held.RiderActor == SoloRider : PairRoster.ActorOf(rider) == held.RiderActor);
        bool keyHeld = samePair && (held.OnBackSlot ? local!.input.HotbarKeyIsPressed(BackSlot) : local!.input.interactIsPressed);
        bool inReach = keyHeld
            && held.Backpack.exists
            && held.Backpack.locationTransform != null
            && Vector3.Distance(held.Backpack.locationTransform.position, local!.Center) <= Reach;
        if (!inReach)
        {
            CloseHeld();
        }
    }

    private static void CloseHeld()
    {
        if (held == null)
        {
            return;
        }

        CarrierWheel closing = held;
        held = null;
        Plugin.Verbose($"Backpack wheel #{closing.Session} closed.");
        SendTo(closing.RiderActor, WheelSignal.Close(closing.Session));
    }

    /// <summary>Carries out the Rider's pick as the game's wheel would, judging it here first.</summary>
    private static void CarrierPick(WheelSignal pick)
    {
        if (held == null || pick.Session != held.Session)
        {
            return;
        }

        CarrierWheel wheelState = held;
        Character local = Character.localCharacter;
        BackpackWheel wheel = GUIManager.instance.backpackWheel;
        bool ownInHand = wheelState.Backpack.IsOnMyBack() && local != null && InHand(local);
        if (local != null && wheelState.Backpack.exists && !wheel.gameObject.activeSelf && !ownInHand && SlotExists(wheelState, pick))
        {
            wheel.backpack = wheelState.Backpack;
            wheel.backpackType = wheelState.Type;
            wheel.chosenSlice = Optionable<BackpackWheelSlice.SliceData>.None;
            wheel.Hover(new BackpackWheelSlice.SliceData
            {
                isBackpackWear = pick.Slice == WheelSlice.Wear,
                isStashSlice = pick.Slice == WheelSlice.Stash,
                isJetpackFuelSlice = pick.Slice == WheelSlice.Fuel,
                backpackReference = wheelState.Backpack,
                slotID = pick.Slice == WheelSlice.Wear ? byte.MaxValue : pick.SlotId,
            });
            if (wheel.chosenSlice.IsSome)
            {
                Plugin.Verbose($"Rider picked {pick.Slice} {pick.SlotId} from backpack wheel #{pick.Session}.");
                wheel.Choose();
            }

            wheel.chosenSlice = Optionable<BackpackWheelSlice.SliceData>.None;
            wheel.chosenItemText.text = "";
        }

        CloseHeld();
    }

    private static bool SlotExists(CarrierWheel wheelState, WheelSignal pick) =>
        pick.Slice != WheelSlice.Slot
        || (pick.SlotId < wheelState.SlotCount && pick.SlotId < wheelState.Backpack.GetData().itemSlots.Length);

    // ---- Rider side: show the Carrier's wheel and pick from it ----

    private static void RiderOpen(WheelSignal open, Character carrier, int fromActor)
    {
        CloseShown();
        PhotonView view = PhotonView.Find(open.ViewId);
        if (view == null)
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
        showing = true;
        GUIManager.instance.OpenBackpackWheel(backpack, open.SlotCount, (BackpackSlot.BackpackType)open.BackpackType);
        if (!GUIManager.instance.backpackWheel.gameObject.activeSelf)
        {
            // Another wheel or a menu is open on the Rider's screen.
            ForgetShown();
        }
    }

    private static void RiderClose(WheelSignal close)
    {
        if (showing && close.Session == shownSession)
        {
            CloseShown();
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
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.CloseBackpackWheel))]
    private static void ForgetWheel()
    {
        localWheelOnBackSlot = false;
        ForgetShown();
    }

    /// <summary>The Rider's screen closes the wheel when the Pair it showed for is gone.</summary>
    private static void TickRider()
    {
        if (!showing)
        {
            return;
        }

        bool solo = shownFromActor == SoloRider;
        bool samePair = solo
            ? ControlSplit.LocalCarrier(out _, out bool stillSolo) && stillSolo
            : RiderHud.ObservedCarrier() is Character carrier && carrier == shownFor;
        if (!samePair)
        {
            CloseShown();
        }
    }

    /// <summary>The Rider's own wheel never picks by itself; the pick goes to the Carrier.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackpackWheel), nameof(BackpackWheel.Choose))]
    private static bool OnlyCarrierChooses() => !showing;

    /// <summary>The Rider's click on a slice picks it.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BackpackWheel), "Update")]
    private static void PickForCarrier(BackpackWheel __instance)
    {
        if (!showing
            || !__instance.gameObject.activeSelf
            || !__instance.chosenSlice.IsSome
            || CharacterInput.action_usePrimary == null
            || !CharacterInput.action_usePrimary.WasPressedThisFrame())
        {
            return;
        }

        BackpackWheelSlice.SliceData chosen = __instance.chosenSlice.Value;
        WheelSlice slice = chosen.isBackpackWear ? WheelSlice.Wear
            : chosen.isStashSlice ? WheelSlice.Stash
            : chosen.isJetpackFuelSlice ? WheelSlice.Fuel
            : WheelSlice.Slot;
        WheelSignal pick = WheelSignal.Pick(shownSession, slice, chosen.isBackpackWear ? (byte)0 : chosen.slotID);
        int carrierActor = shownFromActor;

        // The click that picked must not reach the Shared Body as a use.
        swallowPrimary = true;
        CloseShown();
        if (carrierActor == SoloRider)
        {
            CarrierPick(pick);
            return;
        }

        PhotonNetwork.RaiseEvent(
            RoomKeys.BackpackWheelEvent,
            pick.Encode(),
            new RaiseEventOptions { TargetActors = [carrierActor] },
            SendOptions.SendReliable);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    /// <summary>
    /// Whether the Rider's use buttons stay as they were: while the wheel is on
    /// the Rider's screen, and until the click that picked is let go.
    /// </summary>
    internal static bool HoldsRiderButtons()
    {
        if (showing)
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

    // ---- Network ----

    private static void SendToRider(WheelSignal signal) => SendTo(held!.RiderActor, signal);

    private static void SendTo(int riderActor, WheelSignal signal)
    {
        if (riderActor == SoloRider)
        {
            if (signal.Kind == WheelSignalKind.Open && Character.localCharacter is Character local)
            {
                RiderOpen(signal, local, SoloRider);
            }
            else if (signal.Kind == WheelSignalKind.Close)
            {
                RiderClose(signal);
            }

            return;
        }

        PhotonNetwork.RaiseEvent(
            RoomKeys.BackpackWheelEvent,
            signal.Encode(),
            new RaiseEventOptions { TargetActors = [riderActor] },
            SendOptions.SendReliable);
    }

    internal static void Receive(EventData photonEvent)
    {
        if (photonEvent.Code != RoomKeys.BackpackWheelEvent || !WheelSignal.TryDecode(photonEvent.CustomData, out WheelSignal signal))
        {
            return;
        }

        if (signal.Kind == WheelSignalKind.Pick)
        {
            if (held != null && photonEvent.Sender == held.RiderActor)
            {
                CarrierPick(signal);
            }

            return;
        }

        if (RiderHud.ObservedCarrier() is not Character carrier || photonEvent.Sender != PairRoster.ActorOf(carrier))
        {
            return;
        }

        if (signal.Kind == WheelSignalKind.Open)
        {
            RiderOpen(signal, carrier, photonEvent.Sender);
        }
        else
        {
            RiderClose(signal);
        }
    }
}

/// <summary>
/// The game's wheel reads the local character for the held item, stash
/// labels, and reach. On the Rider's client that is the Rider, whose hands
/// stay empty; the wheel shown there works for the Shared Body instead.
/// </summary>
[HarmonyPatch]
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
