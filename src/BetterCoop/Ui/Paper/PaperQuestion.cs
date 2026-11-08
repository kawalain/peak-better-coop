using System;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A slip of paper laid over a page to ask one question before going on,
/// such as whether to throw a draft away: the question, a line of small
/// print, and two stamps, the one that goes on in red and the one that
/// stays in pen blue. Back, or a click beside the slip, answers "stay".
/// It drops onto the page and lifts away softly.
/// </summary>
internal sealed class PaperQuestion : MonoBehaviour
{
    private const float Width = 620f;

    private const float Height = 250f;

    private const float Seconds = 0.18f;

    private CanvasGroup group = null!;

    private RectTransform slip = null!;

    private Action? onGo;

    private float shown;

    private bool closing;

    public bool Open => isActiveAndEnabled && !closing;

    /// <summary>Lays a question over <paramref name="page"/>; <paramref name="go"/> runs when the player stamps the first answer.</summary>
    public static PaperQuestion Ask(RectTransform page, TMP_Text template, string question, string note, string go, string stay, Action onGo)
    {
        RectTransform cover = PaperList.NewRect("BetterCoop.Question", page);
        cover.anchorMin = Vector2.zero;
        cover.anchorMax = Vector2.one;
        cover.offsetMin = cover.offsetMax = Vector2.zero;
        cover.SetAsLastSibling();
        // A shade over the page that takes every click meant for it.
        cover.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        PaperQuestion asking = cover.gameObject.AddComponent<PaperQuestion>();
        asking.group = cover.gameObject.AddComponent<CanvasGroup>();
        asking.group.alpha = 0f;
        asking.onGo = onGo;
        cover.gameObject.AddComponent<ClickCatcher>().Clicked = asking.Stay;

        RectTransform slip = PaperList.NewRect("Slip", cover);
        slip.sizeDelta = new Vector2(Width, Height);
        slip.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        asking.slip = slip;
        VectorView.Add(slip, s => FormDrawing.Card(s.x, s.y, lifted: true), PaperText.Paper);
        slip.gameObject.GetComponent<RawImage>().raycastTarget = true;

        VerticalLayoutGroup lines = slip.gameObject.AddComponent<VerticalLayoutGroup>();
        lines.padding = new RectOffset(40, 46, 30, 30);
        lines.spacing = 10f;
        lines.childControlWidth = lines.childControlHeight = true;
        lines.childForceExpandWidth = true;
        lines.childForceExpandHeight = false;
        lines.childAlignment = TextAnchor.UpperCenter;
        TMP_Text asked = PaperText.Make(PaperList.NewRect("Question", slip).gameObject, template, PaperText.Heading, TextAlignmentOptions.Center);
        asked.text = question;
        asked.fontStyle = FontStyles.Bold;
        TMP_Text small = PaperText.Make(PaperList.NewRect("Note", slip).gameObject, template, PaperText.Note, TextAlignmentOptions.Center);
        small.text = note;
        small.textWrappingMode = TextWrappingModes.Normal;
        small.color = PaperList.Faded(PaperText.Ink, 0.7f);

        RectTransform answers = PaperList.NewRect("Answers", slip);
        HorizontalLayoutGroup row = answers.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 48f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        answers.gameObject.AddComponent<LayoutElement>().minHeight = 70f;
        var goStamp = new StampMark(answers, template, () => go, asking.Go);
        var stayStamp = new StampMark(answers, template, () => stay, asking.Stay);
        stayStamp.Recolour(PaperList.Pen);
        goStamp.Refresh(true);
        stayStamp.Refresh(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(slip);
        VectorView.DrawAll(cover);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(stayStamp.Control.gameObject);
        }

        asking.Pose();
        return asking;
    }

    /// <summary>Answers "stay": the slip lifts away and the page carries on.</summary>
    public void Stay() => closing = true;

    private void Go()
    {
        closing = true;
        Action? go = onGo;
        onGo = null;
        go?.Invoke();
    }

    private void LateUpdate()
    {
        shown = Mathf.Clamp01(shown + (closing ? -1f : 1f) * Time.unscaledDeltaTime / Seconds);
        Pose();
        if (closing && shown <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void Pose()
    {
        float eased = Easing.Out(shown);
        group.alpha = eased;
        group.blocksRaycasts = !closing;
        slip.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, eased);
        slip.anchoredPosition = new Vector2(0f, Mathf.Lerp(18f, 0f, eased));
    }
}

/// <summary>Hands a click on something with nothing else to do to a handler, as on the shade round a slip.</summary>
internal sealed class ClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public Action? Clicked { get; set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == gameObject)
        {
            Clicked?.Invoke();
        }
    }
}
