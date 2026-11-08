using System;
using HarmonyLib;
using Peak;
using Peak.Afflictions;
using UnityEngine;

namespace BetterCoop.Features;

/// <summary>
/// The Rider's screen shows the Shared Body's statuses, afflictions, and
/// surroundings as the Carrier's does. The game draws these effects only for
/// one's own character, so on the Rider's client the Carrier's synced
/// statuses and afflictions, and the anti-gravity bubble around the Carrier,
/// are played locally.
/// </summary>
[HarmonyPatch]
internal static class RiderEffects
{
    /// <summary>A full-screen tint the game starts and ends with an affliction.</summary>
    [Flags]
    private enum Tint
    {
        None = 0,
        SugarRush = 1,
        EnergyDrink = 2,
        Heat = 4,
        Sunscreen = 8,
        Numb = 16,
        AntiGravity = 32,
    }

    private static readonly Tint[] AllTints =
        [Tint.SugarRush, Tint.EnergyDrink, Tint.Heat, Tint.Sunscreen, Tint.Numb, Tint.AntiGravity];

    /// <summary>How long the Carrier counts as inside a bubble after a physics step last found them there.</summary>
    private const float BubbleGrace = 0.25f;

    private static Tint shown;

    private static GUIManager? shownOn;

    private static bool showing;

    private static Character? bubbleCarrier;

    private static float carrierInBubbleAt = float.NegativeInfinity;

    /// <summary>
    /// A bubble finds every character inside it on each client, the Carrier
    /// too; it lets go of a carried character at once, which the Rider always
    /// is. The Rider's screen follows the Carrier's place instead.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AntiSphere), "FixedUpdate")]
    private static void NoteCarrierInBubble(AntiSphere __instance)
    {
        if (RiderHud.ObservedCarrier() is not Character carrier || __instance.coll == null || !__instance.coll.enabled)
        {
            return;
        }

        float radius = __instance.coll.radius * __instance.transform.lossyScale.x;
        if (__instance.ACTIVE_CHARACTERS.Contains(carrier)
            || Vector3.Distance(__instance.transform.position, carrier.Center) <= radius)
        {
            bubbleCarrier = carrier;
            carrierInBubbleAt = Time.time;
        }
    }

    /// <summary>The Rider's own body never counts as in a bubble; the Carrier's does.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.StartAnti))]
    private static bool OnlyCarrierStartsBubble() => showing || RiderHud.ObservedCarrier() == null;

    /// <summary>The game ending the bubble effect for the Rider's own body ends it on screen; show it again if the Carrier is still inside.</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.EndAnti))]
    private static void NoteBubbleEnded()
    {
        if (!showing)
        {
            shown &= ~Tint.AntiGravity;
        }
    }

    /// <summary>A status the Carrier gained reaches the Rider's client: play its screen effect.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
    private static void NoteStatusBefore(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, out float __state) =>
        __state = __instance.GetCurrentStatus(statusType);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
    private static void PlayCarrierStatusEffect(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float __state)
    {
        Character character = __instance.character;
        if (character.IsLocal || character != RiderHud.ObservedCarrier() || GUIManager.instance == null)
        {
            return;
        }

        // The Rider's client learns of the Carrier's statuses as gains, also of
        // those the Carrier's game set without an effect: Weight, which the
        // effect would otherwise show as an injury, and a Body Handover.
        float gained = __instance.GetCurrentStatus(statusType) - __state;
        if (gained > 0f && statusType != CharacterAfflictions.STATUSTYPE.Weight && !PairShuffle.HandingOver(PairRoster.ActorOf(character)))
        {
            GUIManager.instance.AddStatusFX(statusType, gained);
        }
    }

    /// <summary>Called every frame: keeps the Rider's tints in step with the Carrier's afflictions.</summary>
    internal static void Keep()
    {
        GUIManager gui = GUIManager.instance;
        if (gui == null)
        {
            return;
        }

        if (gui != shownOn)
        {
            // A scene load brings a new HUD with no tints on it.
            shownOn = gui;
            shown = Tint.None;
        }

        Character? carrier = RiderHud.ObservedCarrier();
        Tint wanted = carrier == null ? Tint.None : TintsOf(carrier);
        if (wanted == shown)
        {
            return;
        }

        foreach (Tint tint in AllTints)
        {
            bool want = (wanted & tint) != 0;
            if (want != ((shown & tint) != 0))
            {
                Show(gui, tint, want);
            }
        }

        shown = wanted;
    }

    private static Tint TintsOf(Character carrier)
    {
        Tint tints = Tint.None;
        foreach (Affliction affliction in carrier.refs.afflictions.afflictionList)
        {
            tints |= affliction switch
            {
                Affliction_InfiniteStamina => Tint.SugarRush,
                Affliction_FasterBoi => Tint.EnergyDrink,
                Affliction_AdjustColdOverTime cold when cold.statusPerSecond < 0f => Tint.Heat,
                Affliction_Sunscreen => Tint.Sunscreen,
                Affliction_Numb => Tint.Numb,
                _ => Tint.None,
            };
        }

        if (carrier == bubbleCarrier && Time.time - carrierInBubbleAt < BubbleGrace)
        {
            tints |= Tint.AntiGravity;
        }

        return tints;
    }

    private static void Show(GUIManager gui, Tint tint, bool on)
    {
        Plugin.Verbose($"Rider screen tint {tint} {(on ? "on" : "off")}.");
        showing = true;
        try
        {
            Play(gui, tint, on);
        }
        finally
        {
            showing = false;
        }
    }

    private static void Play(GUIManager gui, Tint tint, bool on)
    {
        switch (tint)
        {
            case Tint.SugarRush when on:
                gui.StartSugarRush();
                break;
            case Tint.SugarRush:
                gui.EndSugarRush();
                break;
            case Tint.EnergyDrink when on:
                gui.StartEnergyDrink();
                break;
            case Tint.EnergyDrink:
                gui.EndEnergyDrink();
                break;
            case Tint.Heat when on:
                gui.StartHeat();
                break;
            case Tint.Heat:
                gui.EndHeat();
                break;
            case Tint.Sunscreen when on:
                gui.StartSunscreen();
                break;
            case Tint.Sunscreen:
                gui.EndSunscreen();
                break;
            case Tint.Numb when on:
                gui.StartNumb();
                break;
            case Tint.Numb:
                gui.StopNumb();
                break;
            case Tint.AntiGravity when on:
                gui.StartAnti();
                break;
            case Tint.AntiGravity:
                gui.EndAnti();
                break;
        }
    }
}

/// <summary>Drives the Rider's screen tints.</summary>
internal sealed class RiderEffectsKeeper : MonoBehaviour
{
    private float nextCheck;

    private void Update()
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("RiderEffectsKeeper.Update", exception);
        }
    }

    private void Tick()
    {
        if (Time.unscaledTime < nextCheck)
        {
            return;
        }

        nextCheck = Time.unscaledTime + 0.2f;
        RiderEffects.Keep();
    }
}
