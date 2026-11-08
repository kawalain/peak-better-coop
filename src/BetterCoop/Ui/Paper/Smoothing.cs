using System;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// An answer that slides when it changes: the old one slips out to one side
/// as the new one comes in from the other, the way the player stepped.
/// </summary>
internal sealed class SlidingText : MonoBehaviour
{
    private const float Seconds = 0.2f;

    private const float Distance = 36f;

    private TMP_Text now = null!;

    private TMP_Text was = null!;

    private float progress = 1f;

    private int direction = 1;

    public TMP_Text Text => now;

    public static SlidingText Add(RectTransform holder, TMP_Text template, float size, Color color)
    {
        SlidingText sliding = holder.gameObject.AddComponent<SlidingText>();
        sliding.was = Line(holder, "Was", template, size, color);
        sliding.now = Line(holder, "Now", template, size, color);
        sliding.was.alpha = 0f;
        return sliding;
    }

    /// <summary>Shows <paramref name="value"/>; a change slides in from the side of <paramref name="towards"/> (+1 from the right).</summary>
    public void Set(string value, int towards)
    {
        if (now.text == value)
        {
            return;
        }

        if (string.IsNullOrEmpty(now.text))
        {
            now.text = value;
            return;
        }

        was.text = now.text;
        now.text = value;
        direction = towards >= 0 ? 1 : -1;
        progress = 0f;
        Pose();
    }

    private static TMP_Text Line(RectTransform holder, string name, TMP_Text template, float size, Color color)
    {
        RectTransform rect = PaperList.NewRect(name, holder);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TMP_Text text = PaperText.Make(rect.gameObject, template, size, TextAlignmentOptions.Bottom);
        text.color = color;
        return text;
    }

    private void LateUpdate()
    {
        if (progress >= 1f)
        {
            return;
        }

        progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / Seconds);
        Pose();
    }

    private void Pose()
    {
        float eased = Easing.Out(progress);
        now.rectTransform.anchoredPosition = new Vector2(direction * Distance * (1f - eased), 0f);
        now.alpha = eased;
        was.rectTransform.anchoredPosition = new Vector2(-direction * Distance * eased, 0f);
        was.alpha = 1f - eased;
    }
}

/// <summary>
/// Carries a thing from where it was to where the layout put it: it starts
/// at its old place and settles into the new one, fading in when it had none.
/// </summary>
internal sealed class Glide : MonoBehaviour
{
    private const float Seconds = 0.32f;

    private Vector2 offset;

    private CanvasGroup? fade;

    private float shown = 1f;

    /// <summary>Starts <paramref name="rect"/> as far from its place as <paramref name="from"/>, in world space; null to fade in where it is.</summary>
    public static void Start(RectTransform rect, Vector3? from)
    {
        Glide glide = rect.GetComponent<Glide>() ?? rect.gameObject.AddComponent<Glide>();
        if (from is Vector3 old && rect.parent is RectTransform parent)
        {
            glide.offset = (Vector2)(parent.InverseTransformPoint(old) - parent.InverseTransformPoint(rect.position));
        }
        else
        {
            glide.fade = rect.GetComponent<CanvasGroup>() ?? rect.gameObject.AddComponent<CanvasGroup>();
            glide.shown = 0f;
        }

        glide.Pose();
    }

    private void LateUpdate()
    {
        float elapsed = Time.unscaledDeltaTime;
        offset = new Vector2(
            Easing.Towards(offset.x, 0f, Seconds, elapsed),
            Easing.Towards(offset.y, 0f, Seconds, elapsed));
        if (offset.sqrMagnitude < 0.01f)
        {
            offset = Vector2.zero;
        }

        shown = Mathf.Min(1f, shown + elapsed / Seconds);
        Pose();
        if (offset == Vector2.zero && shown >= 1f)
        {
            enabled = false;
        }
    }

    private void Pose()
    {
        ((RectTransform)transform).anchoredPosition = offset;
        if (fade != null)
        {
            fade.alpha = Easing.Out(shown);
        }

        enabled = true;
    }
}
