using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The paper's sounds, borrowed from the game's own guidebook and passport:
/// the mod ships no audio. Each is played flat, as a menu sound, quieter than
/// the game plays it. A sound the game has not loaded yet is skipped, and
/// looked for again a little later.
/// </summary>
internal static class PaperSounds
{
    internal enum Kind
    {
        /// <summary>A page coming up: a passport tab's paper.</summary>
        Rise,

        /// <summary>A stamp coming down: the passport closing on the desk.</summary>
        Stamp,

        /// <summary>A page crushed into a wad: a guidebook page turning.</summary>
        Crumple,

        /// <summary>A page taken from the hand: a passport tab's paper.</summary>
        HandIn,

        /// <summary>A typewriter's key striking: the passport's keys.</summary>
        Key,

        /// <summary>A numbering machine's wheels coming to rest: the passport closing, softly.</summary>
        Clunk,
    }

    private static readonly Dictionary<Kind, (string Effect, float Volume)> Borrowed = new()
    {
        [Kind.Rise] = ("SFXI Passport Tab 2", 0.5f),
        [Kind.Stamp] = ("SFXI Impact Book 2", 0.45f),
        [Kind.Crumple] = ("SFXI UI Handbook Flip 1", 0.55f),
        [Kind.HandIn] = ("SFXI Passport Tab 2", 0.6f),
        [Kind.Key] = ("SFXI Passport Gridbutton 5", 0.3f),
        [Kind.Clunk] = ("SFXI Impact Book 2", 0.2f),
    };

    /// <summary>How long a sound not found waits before it is looked for again: the search walks every loaded asset.</summary>
    private const float RetrySeconds = 10f;

    private static readonly Dictionary<Kind, SFX_Instance> Ready = new();

    private static float nextSearch;

    public static void Play(Kind kind)
    {
        try
        {
            if (SFX_Player.instance == null || Find(kind) is not SFX_Instance sound)
            {
                return;
            }

            SFX_Player.instance.PlaySFX(sound, Vector3.zero);
        }
        catch (Exception exception)
        {
            Guard.Report("PaperSounds.Play", exception);
        }
    }

    private static SFX_Instance? Find(Kind kind)
    {
        if (Ready.TryGetValue(kind, out SFX_Instance ready) && ready != null)
        {
            return ready;
        }

        if (Time.unscaledTime < nextSearch)
        {
            return null;
        }

        nextSearch = Time.unscaledTime + RetrySeconds;
        var loaded = new Dictionary<string, SFX_Instance>();
        foreach (SFX_Instance effect in Resources.FindObjectsOfTypeAll<SFX_Instance>())
        {
            if (effect != null && effect.clips is { Length: > 0 } && !loaded.ContainsKey(effect.name))
            {
                loaded[effect.name] = effect;
            }
        }

        foreach (KeyValuePair<Kind, (string Effect, float Volume)> wanted in Borrowed)
        {
            if (!Ready.ContainsKey(wanted.Key) && loaded.TryGetValue(wanted.Value.Effect, out SFX_Instance source))
            {
                Ready[wanted.Key] = Flat(source, wanted.Value.Volume);
            }
        }

        if (!Ready.ContainsKey(kind))
        {
            Plugin.Verbose($"The paper sound {kind} ({Borrowed[kind].Effect}) is not loaded yet.");
        }

        return Ready.TryGetValue(kind, out ready) ? ready : null;
    }

    /// <summary>The game's sound played everywhere at once, as a menu sound, at a share of its own volume.</summary>
    private static SFX_Instance Flat(SFX_Instance source, float share)
    {
        var flat = ScriptableObject.CreateInstance<SFX_Instance>();
        flat.name = "BetterCoop " + source.name;
        flat.hideFlags = HideFlags.HideAndDontSave;
        flat.clips = source.clips;
        SFX_Settings own = source.settings ?? new SFX_Settings();
        flat.settings = new SFX_Settings
        {
            volume = own.volume * share,
            volume_Variation = own.volume_Variation,
            pitch = own.pitch,
            pitch_Variation = own.pitch_Variation,
            spatialBlend = 0f,
            dopplerLevel = 0f,
            range = 1000f,
            cooldown = Mathf.Max(own.cooldown, 0.05f),
        };
        return flat;
    }
}
