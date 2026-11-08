using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BetterCoop.Features;

/// <summary>
/// The Control Split in the backpack wheel (ADR 0005). The game has no way to
/// open the backpack you wear; the Carrier opens their own by holding the
/// back-slot key, which keeps the wheel open as interact does for any other
/// backpack. A tap still brings the back item to hand, on release.
/// </summary>
[HarmonyPatch]
internal static class BackpackWheelSplit
{
    private const byte BackSlot = Player.BACKPACKSLOTINDEX;

    private static readonly BackSlotHold Hold = new();

    private static int trackedFrame = -1;

    private static int tapFrame = -1;

    /// <summary>Whether the back-slot key, not interact, holds the local wheel open.</summary>
    private static bool localWheelOnBackSlot;

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
        if (!local.data.fullyConscious || local.data.isClimbingAnything || !local.CanDoInput())
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

        localWheelOnBackSlot = true;
        GUIManager.instance.OpenBackpackWheel(BackpackReference.GetFromEquippedBackpack(local), slotCount, type);
        localWheelOnBackSlot = GUIManager.instance.backpackWheel.gameObject.activeSelf;
        Plugin.Verbose("Opened the Carrier's own backpack from the back-slot key.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.CloseBackpackWheel))]
    private static void ForgetWheelKey() => localWheelOnBackSlot = false;

    /// <summary>Whether the key that opened the wheel is still held; the game asks only for interact.</summary>
    private static bool WheelHeld(CharacterInput input) =>
        localWheelOnBackSlot ? input.HotbarKeyIsPressed(BackSlot) : input.interactIsPressed;

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BackpackWheel), "Update")]
    private static IEnumerable<CodeInstruction> AskWhichKeyHolds(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo interact = AccessTools.Field(typeof(CharacterInput), nameof(CharacterInput.interactIsPressed));
        MethodInfo held = AccessTools.Method(typeof(BackpackWheelSplit), nameof(WheelHeld));
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.LoadsField(interact))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, held).MoveLabelsFrom(instruction);
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
}
