using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Features;

/// <summary>
/// Keeps status bars that other mods draw for other players off the screen
/// where they say nothing true (ADR 0006): every Rider's, whose own statuses
/// are kept clear, and on a Rider's screen their Carrier's, which is already
/// the Rider's own HUD (Shared View). It finds HUD components outside the
/// game and BetterCoop that point at such a character and makes them
/// invisible, leaving the other mod's lists and logic alone.
/// </summary>
internal sealed class RiderBarsKeeper : MonoBehaviour
{
    private const float ScanSeconds = 0.25f;

    private static readonly Assembly GameAssembly = typeof(Character).Assembly;

    private static readonly Assembly OwnAssembly = typeof(RiderBarsKeeper).Assembly;

    /// <summary>Each component type's fields that hold a character, found once per type.</summary>
    private static readonly Dictionary<Type, FieldInfo[]> CharacterFields = new();

    private readonly Dictionary<GameObject, HiddenBar> hidden = new();

    private float nextScan;

    private void LateUpdate()
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            Guard.Report("RiderBarsKeeper.LateUpdate", exception);
        }
    }

    /// <summary>
    /// Finds the bars four times a second and holds them invisible every frame,
    /// after other mods' updates have run, so a mod that shows its bar again
    /// each frame never gets it drawn.
    /// </summary>
    private void Tick()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanSeconds;
            Rescan();
        }

        foreach (KeyValuePair<GameObject, HiddenBar> bar in hidden)
        {
            if (bar.Key != null)
            {
                bar.Value.Hold();
            }
        }
    }

    private void Rescan()
    {
        HashSet<Character> characters = CharactersToHide();
        HashSet<GameObject> picked = characters.Count == 0
            ? []
            : BarHiding.Pick(
                FindPieces(),
                characters.Contains,
                (piece, outer) => piece != outer && piece.transform.IsChildOf(outer.transform));
        picked.RemoveWhere(IsGameHud);

        foreach (GameObject bar in hidden.Keys.ToList())
        {
            if (bar == null)
            {
                // Destroyed: the key compares equal to null but still removes.
                hidden.Remove(bar!);
            }
            else if (!picked.Contains(bar))
            {
                Plugin.Verbose($"Showing another mod's bar {bar.name} again.");
                hidden[bar].Release();
                hidden.Remove(bar);
            }
        }

        foreach (GameObject bar in picked)
        {
            if (!hidden.ContainsKey(bar))
            {
                Plugin.Verbose($"Hiding another mod's bar {bar.name} for a Rider's Pair.");
                hidden[bar] = new HiddenBar(bar);
            }
        }
    }

    /// <summary>Every Rider, and on a Rider's own client their Carrier too.</summary>
    private static HashSet<Character> CharactersToHide()
    {
        var characters = new HashSet<Character>();
        foreach (Pair pair in PairRoster.Pairs)
        {
            if (PlayerHandler.TryGetCharacter(pair.Rider, out Character rider) && rider != null)
            {
                characters.Add(rider);
            }
        }

        Character local = Character.localCharacter;
        if (PairRoster.RoleOf(local) == PairRole.Rider && PairRoster.TryGetPartner(local, out Character carrier))
        {
            characters.Add(carrier);
        }

        return characters;
    }

    /// <summary>Other mods' components on any screen canvas, with the characters they point at.</summary>
    private static List<(GameObject Piece, Character Character)> FindPieces()
    {
        var roots = new HashSet<Canvas>();
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.rootCanvas != null)
            {
                roots.Add(canvas.rootCanvas);
            }
        }

        var pieces = new List<(GameObject, Character)>();
        foreach (Canvas root in roots)
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (type.Assembly == GameAssembly || type.Assembly == OwnAssembly)
                {
                    continue;
                }

                foreach (FieldInfo field in FieldsOf(type))
                {
                    if (field.GetValue(component) is Character character && character != null)
                    {
                        pieces.Add((component.gameObject, character));
                    }
                }
            }
        }

        return pieces;
    }

    private static FieldInfo[] FieldsOf(Type type)
    {
        if (CharacterFields.TryGetValue(type, out FieldInfo[] fields))
        {
            return fields;
        }

        var found = new List<FieldInfo>();
        try
        {
            for (Type? current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                found.AddRange(current
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => typeof(Character).IsAssignableFrom(f.FieldType)));
            }
        }
        catch (Exception exception)
        {
            // A type whose fields cannot load is not a bar this can read.
            Plugin.Verbose($"Skipping {type.FullName} in the bar search: {exception.Message}");
            found.Clear();
        }

        fields = found.ToArray();
        CharacterFields[type] = fields;
        return fields;
    }

    /// <summary>
    /// The game's own HUD stays: the HUD root, anything holding the game's
    /// stamina bar, anything another mod placed inside it (on a Rider's
    /// screen that bar is the Shared Body's), and the characters themselves.
    /// </summary>
    private static bool IsGameHud(GameObject piece)
    {
        if (piece.GetComponent<Character>() != null)
        {
            return true;
        }

        GUIManager gui = GUIManager.instance;
        if (gui == null)
        {
            return false;
        }

        if (piece == gui.gameObject)
        {
            return true;
        }

        Transform? bar = gui.bar != null ? gui.bar.transform : null;
        return bar != null && (bar.IsChildOf(piece.transform) || piece.transform.IsChildOf(bar));
    }

    /// <summary>One hidden bar and what it looked like before.</summary>
    private sealed class HiddenBar
    {
        private readonly CanvasGroup group;

        private readonly bool addedGroup;

        private readonly float alpha;

        private readonly bool interactable;

        private readonly bool blocksRaycasts;

        private readonly LayoutElement layout;

        private readonly bool addedLayout;

        private readonly bool ignoreLayout;

        public HiddenBar(GameObject bar)
        {
            CanvasGroup? existingGroup = bar.GetComponent<CanvasGroup>();
            addedGroup = existingGroup == null;
            group = addedGroup ? bar.AddComponent<CanvasGroup>() : existingGroup!;

            alpha = group.alpha;
            interactable = group.interactable;
            blocksRaycasts = group.blocksRaycasts;

            LayoutElement? existingLayout = bar.GetComponent<LayoutElement>();
            addedLayout = existingLayout == null;
            layout = addedLayout ? bar.AddComponent<LayoutElement>() : existingLayout!;

            ignoreLayout = layout.ignoreLayout;
            Hold();
        }

        /// <summary>Invisible, unclickable, and out of any list layout, so it leaves no gap.</summary>
        public void Hold()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        public void Release()
        {
            if (group != null)
            {
                if (addedGroup)
                {
                    Destroy(group);
                }
                else
                {
                    group.alpha = alpha;
                    group.interactable = interactable;
                    group.blocksRaycasts = blocksRaycasts;
                }
            }

            if (layout != null)
            {
                if (addedLayout)
                {
                    Destroy(layout);
                }
                else
                {
                    layout.ignoreLayout = ignoreLayout;
                }
            }
        }
    }
}
