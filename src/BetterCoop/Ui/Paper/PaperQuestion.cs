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
    private const float Width = 640f;

    /// <summary>The heading band above the slip's perforation.</summary>
    private const float Band = 62f;

    private const float Side = 46f;

    private const float Seconds = 0.2f;

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
        cover.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.32f);
        PaperQuestion asking = cover.gameObject.AddComponent<PaperQuestion>();
        asking.group = cover.gameObject.AddComponent<CanvasGroup>();
        asking.group.alpha = 0f;
        asking.onGo = onGo;
        cover.gameObject.AddComponent<ClickCatcher>().Clicked = asking.Stay;

        RectTransform slip = PaperList.NewRect("Slip", cover);
        slip.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
        asking.slip = slip;
        float inner = Width - 2f * Side;

        // Every line is placed by hand from its own measured height, so the slip grows to its words.
        TMP_Text heading = Text(slip, template, "Heading", PaperText.Note, TextAlignmentOptions.Left);
        heading.text = Strings.SlipQuery;
        heading.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        heading.characterSpacing = 4f;
        heading.color = PaperText.Blue;
        TMP_Text number = Text(slip, template, "Number", PaperText.Note, TextAlignmentOptions.Right);
        number.text = Strings.FormTitle;
        number.color = PaperList.Faded(PaperText.Ink, 0.55f);

        TMP_Text asked = Text(slip, template, "Question", PaperText.Heading, TextAlignmentOptions.Center);
        asked.text = question;
        asked.fontStyle = FontStyles.Bold;
        asked.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text small = Text(slip, template, "Note", PaperText.Value, TextAlignmentOptions.Center);
        small.text = note;
        small.textWrappingMode = TextWrappingModes.Normal;
        small.color = PaperList.Faded(PaperText.Ink, 0.75f);

        RectTransform answers = PaperList.NewRect("Answers", slip);
        HorizontalLayoutGroup row = answers.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 56f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        var goStamp = new StampMark(answers, template, () => go, asking.Go);
        var stayStamp = new StampMark(answers, template, () => stay, asking.Stay);
        stayStamp.Recolour(PaperList.Pen);
        goStamp.Refresh(true);
        stayStamp.Refresh(true);

        float askedHeight = asked.GetPreferredValues(question, inner, 0f).y;
        float noteHeight = small.GetPreferredValues(note, inner, 0f).y;
        const float answersHeight = 84f;
        float y = Band + 26f;
        Place(heading.rectTransform, Side - 8f, 20f, inner + 16f, Band - 28f);
        Place(number.rectTransform, Side - 8f, 20f, inner + 16f, Band - 28f);
        Place(asked.rectTransform, Side, y, inner, askedHeight);
        y += askedHeight + 10f;
        Place(small.rectTransform, Side, y, inner, noteHeight);
        y += noteHeight + 22f;
        Place(answers, Side, y, inner, answersHeight);
        y += answersHeight + 30f;
        slip.sizeDelta = new Vector2(Width, y);

        // The paper, drawn under everything, with room round it for its shadow.
        RectTransform sheet = PaperList.NewRect("Sheet", slip);
        sheet.SetAsFirstSibling();
        sheet.anchorMin = Vector2.zero;
        sheet.anchorMax = Vector2.one;
        sheet.offsetMin = new Vector2(-PaperDrawing.Margin, -PaperDrawing.Margin);
        sheet.offsetMax = new Vector2(PaperDrawing.Margin, PaperDrawing.Margin);
        VectorView.Add(sheet, size => PaperDrawing.Slip(size.x - 2f * PaperDrawing.Margin, size.y - 2f * PaperDrawing.Margin, Band), PaperText.Paper);
        sheet.gameObject.GetComponent<RawImage>().raycastTarget = true;

        LayoutRebuilder.ForceRebuildLayoutImmediate(answers);
        VectorView.DrawAll(cover);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(stayStamp.Control.gameObject);
        }

        asking.Pose();
        return asking;
    }

    private static TMP_Text Text(RectTransform slip, TMP_Text template, string name, float size, TextAlignmentOptions alignment) =>
        PaperText.Make(PaperList.NewRect(name, slip).gameObject, template, size, alignment);

    /// <summary>Puts <paramref name="rect"/> <paramref name="top"/> below the slip's top edge, <paramref name="left"/> in from its left.</summary>
    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
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
        // Dropped onto the page from a little above, and lifted the same way.
        slip.localScale = Vector3.one * Mathf.Lerp(1.05f, 1f, eased);
        slip.anchoredPosition = new Vector2(0f, Mathf.Lerp(26f, 0f, eased));
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
