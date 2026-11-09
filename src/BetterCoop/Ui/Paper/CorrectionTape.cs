using System.Collections.Generic;
using BetterCoop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Takes a typed line off the form: a stroke of correction tape is drawn
/// across it from left to right, and once it has settled the line fades from
/// the page.
/// </summary>
internal sealed class CorrectionTape : MonoBehaviour
{
    private const float DrawSeconds = 0.3f;

    private const float HoldSeconds = 0.35f;

    private const float FadeSeconds = 0.25f;

    private RectTransform line = null!;

    private RectTransform drawn = null!;

    private RectTransform strip = null!;

    private CanvasGroup faint = null!;

    /// <summary>What the tape covers, put away once it is covered so none of it shows through as the line fades.</summary>
    private readonly List<GameObject> covered = new();

    /// <summary>What the line shows, kept as faint as the line was while the tape over it is drawn in full ink.</summary>
    private readonly List<CanvasGroup> under = new();

    private float startedAt;

    private float alpha = 1f;

    /// <summary>Whether the tape is being drawn over the line, or it is fading.</summary>
    public bool Striking => enabled;

    /// <summary>Readies tape for <paramref name="target"/>, whose <paramref name="fade"/> it fades.</summary>
    public static CorrectionTape Add(RectTransform target, CanvasGroup fade, int seed)
    {
        // How far the tape has been drawn, clipping a strip as long as the line.
        RectTransform drawn = PaperList.NewRect("Tape", target);
        drawn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        drawn.anchorMin = Vector2.zero;
        drawn.anchorMax = new Vector2(0f, 1f);
        drawn.pivot = new Vector2(0f, 0.5f);
        drawn.offsetMin = new Vector2(-4f, -3f);
        drawn.offsetMax = new Vector2(0f, 3f);
        drawn.gameObject.AddComponent<RectMask2D>();
        RectTransform strip = PaperList.NewRect("Strip", drawn);
        strip.anchorMin = Vector2.zero;
        strip.anchorMax = new Vector2(0f, 1f);
        strip.pivot = new Vector2(0f, 0.5f);
        strip.offsetMin = strip.offsetMax = Vector2.zero;
        VectorView.Add(strip, size => FormDrawing.CorrectionTape(size.x, size.y, seed), Color.clear);
        drawn.gameObject.SetActive(false);

        CorrectionTape tape = target.gameObject.AddComponent<CorrectionTape>();
        tape.line = target;
        tape.drawn = drawn;
        tape.strip = strip;
        tape.faint = fade;
        tape.enabled = false;
        return tape;
    }

    /// <summary>Starts drawing the tape over the line; the line is put away once it has faded.</summary>
    public void Strike()
    {
        if (enabled)
        {
            return;
        }

        alpha = faint.alpha;
        foreach (Transform shown in line)
        {
            if (shown != drawn)
            {
                CanvasGroup group = shown.GetComponent<CanvasGroup>() ?? shown.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alpha;
                under.Add(group);
            }
        }

        startedAt = Time.unscaledTime;
        drawn.SetAsLastSibling();
        drawn.gameObject.SetActive(true);
        enabled = true;
        Pose();
    }

    /// <summary>Peels the tape off for the line to be typed again.</summary>
    public void Lift()
    {
        foreach (GameObject under in covered)
        {
            under.SetActive(true);
        }

        covered.Clear();
        foreach (CanvasGroup group in under)
        {
            group.alpha = 1f;
        }

        under.Clear();
        enabled = false;
        drawn.gameObject.SetActive(false);
        faint.alpha = alpha;
    }

    private void LateUpdate() => Pose();

    private void Pose()
    {
        float width = line.rect.width + 8f;
        float elapsed = Time.unscaledTime - startedAt;
        strip.sizeDelta = new Vector2(width, 0f);
        drawn.sizeDelta = new Vector2(width * Easing.InOut(elapsed / DrawSeconds), drawn.sizeDelta.y);
        if (elapsed >= DrawSeconds && covered.Count == 0)
        {
            foreach (Transform under in line)
            {
                if (under != drawn && under.name != "Ruled" && under.gameObject.activeSelf)
                {
                    covered.Add(under.gameObject);
                    under.gameObject.SetActive(false);
                }
            }
        }

        float fading = (elapsed - DrawSeconds - HoldSeconds) / FadeSeconds;
        faint.alpha = 1f - Easing.Out(fading);
        if (fading >= 1f)
        {
            Lift();
            line.gameObject.SetActive(false);
        }
    }
}
