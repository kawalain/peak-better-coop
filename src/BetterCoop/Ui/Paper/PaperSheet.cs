using System;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A sheet of paper drawn as the game draws its own windows. The pause
/// menu's windows go through the game's "Scouts/UI" material: a grit texture
/// laid over them in screen space, and dither noise that cuts their faded
/// edges, both jumping to a new place a few times a second, so the panels
/// shimmer and their edges crawl. The paper goes through a copy of that
/// material, tinted for paper, over a rough outline that fades in across the
/// edge for the noise to cut; its shadow is drawn plainly behind it, as the
/// game's buttons draw theirs. Without the game's material the paper is
/// drawn plainly, with a crisp outline.
/// </summary>
internal static class PaperSheet
{
    private const string GrainShader = "Scouts/UI";

    /// <summary>The grit's tint: a warm shade for paper, where the game's windows take a cool grey.</summary>
    private static readonly Color GrainTint = new(0.955f, 0.945f, 0.915f, 1f);

    private static Material? grain;

    /// <summary>How many times a second the game's window noise moves, as its shader steps it.</summary>
    public static float WiggleTime { get; private set; } = 3f;

    /// <summary>
    /// Adds the sheet <paramref name="name"/> under <paramref name="parent"/>,
    /// reaching past it by the paper's margin so the paper's edge is the
    /// parent's, behind everything else there and with its shadow behind it.
    /// <paramref name="paper"/> draws the paper for its size in UI units,
    /// margin and all, rough or plain; <paramref name="shadow"/> draws its
    /// shadow in the same frame.
    /// </summary>
    public static VectorView Add(
        RectTransform parent,
        string name,
        Func<Vector2, bool, string> paper,
        Func<Vector2, string> shadow,
        Color fallback)
    {
        RectTransform sheet = Fill(PaperList.NewRect(name, parent));
        RectTransform behind = Fill(PaperList.NewRect(name + " Shadow", parent));
        sheet.SetAsFirstSibling();
        behind.SetAsFirstSibling();
        VectorView.Add(behind, shadow, Color.clear);

        Material? material = Grain();
        bool rough = material != null;
        VectorView view = VectorView.Add(sheet, size => paper(size, rough), fallback);
        if (material != null)
        {
            view.GetComponent<RawImage>().material = material;
        }

        return view;
    }

    /// <summary>The paper's size inside the margin, from the size of its whole drawing.</summary>
    public static Vector2 Inside(Vector2 size) =>
        new(size.x - (2f * PaperDrawing.Margin), size.y - (2f * PaperDrawing.Margin));

    private static RectTransform Fill(RectTransform rect)
    {
        // A parent that lays out its children leaves the paper where it is.
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-PaperDrawing.Margin, -PaperDrawing.Margin);
        rect.offsetMax = new Vector2(PaperDrawing.Margin, PaperDrawing.Margin);
        return rect;
    }

    /// <summary>The game's window material, copied once and tinted for paper, or null if it cannot be found.</summary>
    private static Material? Grain()
    {
        if (grain != null)
        {
            return grain;
        }

        Material? source = FromPauseMenu();
        if (source == null)
        {
            foreach (Material loaded in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (loaded.name == "UI" && IsGrain(loaded))
                {
                    source = loaded;
                    break;
                }
            }
        }

        if (source == null)
        {
            Plugin.Verbose($"The game's {GrainShader} material is not loaded; paper is drawn plainly.");
            return null;
        }

        grain = new Material(source)
        {
            name = "BetterCoop.PaperGrain",
            hideFlags = HideFlags.HideAndDontSave,
        };
        grain.SetColor("_TexColor", GrainTint);
        if (grain.HasProperty("_WiggleTime"))
        {
            WiggleTime = grain.GetFloat("_WiggleTime");
        }

        Plugin.Verbose($"Paper takes its grain from the game's {source.name} material.");
        return grain;
    }

    /// <summary>The material the pause menu's confirm window is drawn with.</summary>
    private static Material? FromPauseMenu()
    {
        PauseMenuMainPage? main = GUIManager.instance != null ? GUIManager.instance.pauseMenuMainPage : null;
        if (main == null || main.confirmWindow == null)
        {
            return null;
        }

        foreach (Image image in main.confirmWindow.GetComponentsInChildren<Image>(true))
        {
            if (IsGrain(image.material))
            {
                return image.material;
            }
        }

        return null;
    }

    private static bool IsGrain(Material? material) =>
        material != null && material.shader != null && material.shader.name == GrainShader;
}
