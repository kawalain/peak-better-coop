using System;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// One question asked over a page before going on, such as whether to
/// throw a draft away, in the game's own window that asks before leaving
/// the game, so it looks as the game's questions do. Back answers "stay".
/// Only when that window cannot be found is it asked on a slip of paper:
/// the question, a line of small print, and two stamps, the one that goes
/// on in red and the one that stays in pen blue, dropped onto the page and
/// lifted away softly.
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

    /// <summary>The game's own confirmation window this question was asked in, or null for a paper slip.</summary>
    private MenuWindow? window;

    public bool Open => isActiveAndEnabled && !closing;

    /// <summary>
    /// Asks a question over <paramref name="page"/> in the game's own
    /// confirmation window, the one the pause menu asks before leaving the
    /// game; <paramref name="onGo"/> runs when the player confirms. Only when
    /// that window cannot be found is the question laid on a paper slip.
    /// </summary>
    public static PaperQuestion Ask(RectTransform page, TMP_Text template, string question, string note, string go, string stay, Action onGo)
    {
        try
        {
            if (InGameWindow(page, question, note, go, stay, onGo) is PaperQuestion asked)
            {
                return asked;
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PaperQuestion.InGameWindow", exception);
        }

        return OnSlip(page, template, question, note, go, stay, onGo);
    }

    /// <summary>
    /// A copy of the pause menu's leave-the-game window, its words and buttons
    /// our own, as the game itself reuses that window to ask about kicking a
    /// player. A copy, since the window belongs to the pause menu's first
    /// page, which is put away while another page is open.
    /// </summary>
    private static PaperQuestion? InGameWindow(RectTransform page, string question, string note, string go, string stay, Action onGo)
    {
        PauseMenuMainPage? main = GUIManager.instance != null ? GUIManager.instance.pauseMenuMainPage : null;
        if (main == null || main.confirmWindow == null || main.m_confirmOkButton == null || main.m_confirmCancelButton == null)
        {
            return null;
        }

        Transform source = main.confirmWindow.transform;
        string? okPath = PathFrom(source, main.m_confirmOkButton.transform);
        string? cancelPath = PathFrom(source, main.m_confirmCancelButton.transform);
        string? textPath = main.confirmText != null ? PathFrom(source, main.confirmText.transform) : null;
        if (okPath == null || cancelPath == null)
        {
            return null;
        }

        GameObject copy = Instantiate(source.gameObject, page);
        copy.name = "BetterCoop.Question";
        copy.transform.SetAsLastSibling();

        // The game's words come back whenever a localized text wakes; these words are ours.
        foreach (LocalizedText localized in copy.GetComponentsInChildren<LocalizedText>(true))
        {
            localized.enabled = false;
        }

        MenuWindow window = copy.GetComponent<MenuWindow>();
        Button ok = Find(copy.transform, okPath).GetComponent<Button>();
        Button cancel = Find(copy.transform, cancelPath).GetComponent<Button>();
        PaperQuestion asking = copy.AddComponent<PaperQuestion>();
        asking.window = window;
        asking.onGo = onGo;
        ok.onClick.RemoveAllListeners();
        ok.onClick.AddListener(asking.Go);
        cancel.onClick.RemoveAllListeners();
        cancel.onClick.AddListener(asking.Stay);
        Label(ok, go);
        Label(cancel, stay);
        if (textPath != null && Find(copy.transform, textPath).GetComponentInChildren<TMP_Text>(true) is TMP_Text words)
        {
            // The note set apart from the question by a little air, and kept clear of the window's edges.
            words.text = string.IsNullOrEmpty(note) ? question : $"{question}\n<size=45%> </size>\n<size=68%>{note}</size>";
            words.margin = new Vector4(words.margin.x + 36f, words.margin.y, words.margin.z + 36f, words.margin.w);
            words.textWrappingMode = TextWrappingModes.Normal;
            Shrink(words, 0.6f);
        }

        window.Open();
        cancel.Select();
        return asking;
    }

    /// <summary>The path from <paramref name="root"/> down to <paramref name="child"/>, or null when it is not under it.</summary>
    private static string? PathFrom(Transform root, Transform child)
    {
        string path = string.Empty;
        for (Transform? at = child; at != null; at = at.parent)
        {
            if (at == root)
            {
                return path;
            }

            path = path.Length == 0 ? at.name : at.name + "/" + path;
        }

        return null;
    }

    private static Transform Find(Transform root, string path) => path.Length == 0 ? root : root.Find(path);

    /// <summary>Writes a button's word on one line, smaller if it would not fit.</summary>
    private static void Label(Button button, string text)
    {
        if (button.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
        {
            label.text = text;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Shrink(label, 0.55f);
        }
    }

    /// <summary>Lets the text shrink to <paramref name="share"/> of its size to fit its box.</summary>
    private static void Shrink(TMP_Text text, float share)
    {
        float size = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
        text.fontSizeMax = size;
        text.fontSizeMin = size * share;
        text.enableAutoSizing = true;
    }

    /// <summary>The question on a slip of paper laid over the page, for when the game's window is not there.</summary>
    private static PaperQuestion OnSlip(RectTransform page, TMP_Text template, string question, string note, string go, string stay, Action onGo)
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
        VectorView sheet = PaperSheet.Add(
            slip,
            "Sheet",
            (size, rough) =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Slip(inside.x, inside.y, Band, rough: rough);
            },
            size =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Shadow(inside.x, inside.y, Band);
            },
            PaperText.Paper);
        sheet.GetComponent<RawImage>().raycastTarget = true;

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

    /// <summary>Answers "stay": the question goes away and the page carries on.</summary>
    public void Stay()
    {
        closing = true;
        CloseWindow();
    }

    private void Go()
    {
        closing = true;
        CloseWindow();
        Action? go = onGo;
        onGo = null;
        go?.Invoke();
    }

    /// <summary>Takes the question away at once, unanswered, as when the page goes.</summary>
    public void Dismiss()
    {
        closing = true;
        if (window != null && window.isOpen)
        {
            window.Close();
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>Closes the game's window the question was asked in, as the game closes it, and lets it go.</summary>
    private void CloseWindow()
    {
        if (window == null)
        {
            return;
        }

        if (window.isOpen)
        {
            window.Close();
        }

        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (window != null)
        {
            return;
        }

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
