using System;
using BetterCoop.Core;
using BetterCoop.Ui.Paper;
using TMPro;
using UnityEngine;

namespace BetterCoop.Ui.Notices;

/// <summary>
/// A small airmail letter that pops out at the left of the screen when the
/// player's Pair or role is about to change: addressed to them, it shows
/// their yearbook photo and the partner's as the Pair page prints them, the
/// countdown in a postmark, and a line on what happens. A partner not yet
/// known, as before a Pair Shuffle, is a grey silhouette until the change is
/// made. After the bodies change, it slips back out of sight.
/// </summary>
internal sealed class PairLetter : MonoBehaviour
{
    private const float Width = 420f;

    private const float Band = 92f;

    private const float Side = 34f;

    private const float Between = 52f;

    private const float InSeconds = 0.45f;

    private const float OutSeconds = 0.35f;

    private const float FadeSeconds = 0.25f;

    private RectTransform rect = null!;

    private CanvasGroup group = null!;

    private TMP_Text template = null!;

    private TMP_Text heading = null!;

    private TMP_Text recipient = null!;

    private TMP_Text line = null!;

    private PostmarkFace postmark = null!;

    private RectTransform photos = null!;

    private CanvasGroup photosFade = null!;

    private IPortraits portraits = null!;

    private Func<int, string> nameOf = actor => $"#{actor}";

    private int me = -1;

    private (int? Partner, bool? Carries, bool Known) drawn;

    private bool hasDrawn;

    private float shown;

    private bool open;

    private float closeIn = float.PositiveInfinity;

    /// <summary>Whether the letter is out or on its way out, not yet closing.</summary>
    public bool Showing => open;

    public static PairLetter Create(RectTransform parent, TMP_Text template)
    {
        RectTransform rect = PaperList.NewRect("BetterCoop.PairLetter", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        PairLetter letter = rect.gameObject.AddComponent<PairLetter>();
        letter.rect = rect;
        letter.template = template;
        letter.group = rect.gameObject.AddComponent<CanvasGroup>();
        letter.group.blocksRaycasts = false;
        letter.Build();
        letter.Pose();
        return letter;
    }

    /// <summary>Brings the letter out, addressed to <paramref name="me"/>, under <paramref name="title"/>.</summary>
    public void Open(string title, IPortraits portraits, Func<int, string> nameOf, int me)
    {
        this.portraits = portraits;
        this.nameOf = nameOf;
        this.me = me;
        hasDrawn = false;
        PaperText.Set(heading, title);
        PaperText.Set(recipient, Strings.LetterTo(nameOf(me)));
        if (!open && shown <= 0f)
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0.5f, 2.5f));
        }

        open = true;
        closeIn = float.PositiveInfinity;
    }

    /// <summary>
    /// Shows the partner and roles, the postmark's word, and the line under
    /// the photos. The photos are printed again only when who or which role
    /// changes, fading in.
    /// </summary>
    public void Show(int? partner, bool? carries, bool known, string word, string status)
    {
        postmark.Show(word);
        PaperText.Set(line, status);
        var wanted = (partner, carries, known);
        if (hasDrawn && wanted == drawn)
        {
            return;
        }

        drawn = wanted;
        hasDrawn = true;
        Print(partner, carries, known);
    }

    /// <summary>Slips the letter away <paramref name="seconds"/> from now, unless it is to go sooner.</summary>
    public void CloseAfter(float seconds) => closeIn = Mathf.Min(closeIn, seconds);

    private void Build()
    {
        float photoTop = Band + 20f;
        float lineTop = photoTop + ScoutPhoto.Height + 6f;
        float stripTop = lineTop + 40f;
        float height = stripTop + 12f + 22f;
        rect.sizeDelta = new Vector2(Width, height);

        var paper = new Ink(0.925f, 0.940f, 0.965f);
        PaperSheet.Add(
            rect,
            "Sheet",
            (size, rough) =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Slip(inside.x, inside.y, Band, 9, paper, rough);
            },
            size =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Shadow(inside.x, inside.y, Band);
            },
            Color.clear);

        float words = Width - 2f * Side - 150f;
        // Heading and recipient, a pair in the middle of the head.
        const float headingHeight = 32f;
        const float recipientHeight = 26f;
        const float between = 2f;
        float headTop = PaperDrawing.HeadMiddle(Band) - ((headingHeight + between + recipientHeight) / 2f);
        heading = NoticeSlip.Line(rect, template, "Heading", PaperText.Value, TextAlignmentOptions.MidlineLeft);
        heading.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        heading.characterSpacing = 2f;
        heading.color = PaperText.Blue;
        PaperText.Fit(heading, 15f);
        NoticeSlip.Place(heading.rectTransform, Side, headTop, words, headingHeight);
        recipient = NoticeSlip.Line(rect, template, "To", PaperText.Note, TextAlignmentOptions.MidlineLeft);
        recipient.fontStyle = FontStyles.Italic;
        recipient.color = PaperList.Pen;
        PaperText.Fit(recipient, 13f);
        NoticeSlip.Place(recipient.rectTransform, Side, headTop + headingHeight + between, words, recipientHeight);

        // The postmark in the corner counts down, as the letter's cancelling stamp.
        RectTransform mark = PaperList.NewRect("Postmark", rect);
        mark.anchorMin = mark.anchorMax = Vector2.one;
        mark.pivot = Vector2.one;
        mark.sizeDelta = new Vector2(PostmarkFace.Ring + PostmarkFace.Waves, PostmarkFace.Ring);
        mark.localScale = Vector3.one * 0.72f;
        mark.anchoredPosition = new Vector2(-14f, -8f);
        postmark = new PostmarkFace(() => Strings.PostmarkOffice, () => Strings.PostmarkDate(DateTime.Now));
        postmark.Build(mark, template);

        photos = PaperList.NewRect("Photos", rect);
        NoticeSlip.Place(photos, 0f, photoTop, Width, ScoutPhoto.Height);
        photosFade = photos.gameObject.AddComponent<CanvasGroup>();

        line = NoticeSlip.Line(rect, template, "Line", PaperText.Value, TextAlignmentOptions.Center);
        line.fontStyle = FontStyles.Bold;
        PaperText.Fit(line, 15f);
        NoticeSlip.Place(line.rectTransform, Side, lineTop, Width - 2f * Side, 34f);

        RectTransform strip = PaperList.NewRect("Airmail", rect);
        NoticeSlip.Place(strip, 22f, stripTop, Width - 44f, 12f);
        VectorView.Add(strip, size => FormDrawing.Airmail(size.x, size.y), Color.clear);
    }

    /// <summary>Prints the two photos afresh: the player on the left, the partner on the right, arrows between.</summary>
    private void Print(int? partner, bool? carries, bool known)
    {
        foreach (Transform old in photos)
        {
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }

        float left = (Width - 2f * ScoutPhoto.Width - Between) / 2f;
        RectTransform mine = Cell(left);
        // A player left with no Pair has no role to come.
        ScoutPhoto.Add(mine, template, portraits, me, Strings.LetterYou, known && partner == null ? Strings.MenuUnpaired : Role(carries), ScoutPhoto.Mount.Clip);

        RectTransform arrows = PaperList.NewRect("Arrows", photos);
        NoticeSlip.Place(arrows, left + ScoutPhoto.Width + (Between - 28f) / 2f, ScoutPhoto.PhotoHeight / 2f - 34f, 28f, 68f);
        RectTransform there = PaperList.NewRect("There", arrows);
        NoticeSlip.Place(there, 0f, 0f, 28f, 32f);
        VectorView.Add(there, size => FormDrawing.Arrow(size.x, size.y, false), Color.clear);
        RectTransform back = PaperList.NewRect("Back", arrows);
        NoticeSlip.Place(back, 0f, 36f, 28f, 32f);
        VectorView.Add(back, size => FormDrawing.Arrow(size.x, size.y, true), Color.clear);

        RectTransform theirs = Cell(left + ScoutPhoto.Width + Between);
        if (partner is int other)
        {
            ScoutPhoto.Add(theirs, template, portraits, other, nameOf(other), carries is bool iCarry ? Strings.RoleOf(!iCarry) : Strings.LetterRoleUnknown, ScoutPhoto.Mount.Clip);
        }
        else
        {
            // Not known yet, or no one: a grey figure, or an empty place.
            RectTransform place = ScoutPhoto.PhotoArea(theirs);
            Color grey = new(0.62f, 0.62f, 0.64f);
            if (known)
            {
                VectorView.Add(place, size => FormDrawing.Slot(size.x, size.y, marked: false), Color.clear);
            }
            else
            {
                VectorView.Add(place, size => FormDrawing.Silhouette(size.x, size.y, new Ink(grey.r, grey.g, grey.b), mystery: true), grey);
                VectorView.Add(PhotoEdge(place), size => FormDrawing.PhotoEdge(size.x, size.y), Color.clear);
            }

            RectTransform caption = ScoutPhoto.Caption(theirs);
            TMP_Text unknown = PaperText.Make(caption.gameObject, template, PaperText.Value, TextAlignmentOptions.Top);
            unknown.text = known ? "—" : Strings.LetterUnknown;
            unknown.color = PaperList.Faded(PaperText.Ink, 0.6f);
        }

        photosFade.alpha = 0f;
        VectorView.DrawAll(photos);
    }

    private static RectTransform PhotoEdge(RectTransform photo)
    {
        RectTransform edge = PaperList.NewRect("Edge", photo);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.offsetMin = edge.offsetMax = Vector2.zero;
        return edge;
    }

    private static string Role(bool? carries) => carries is bool carrying ? Strings.RoleOf(carrying) : Strings.LetterRoleUnknown;

    private RectTransform Cell(float left)
    {
        RectTransform cell = PaperList.NewRect("Cell", photos);
        NoticeSlip.Place(cell, left, 0f, ScoutPhoto.Width, ScoutPhoto.Height);
        return cell;
    }

    private void LateUpdate()
    {
        float elapsed = Time.unscaledDeltaTime;
        if (open && !float.IsPositiveInfinity(closeIn))
        {
            closeIn -= elapsed;
            if (closeIn <= 0f)
            {
                open = false;
                closeIn = float.PositiveInfinity;
            }
        }

        shown = open
            ? Mathf.Min(1f, shown + elapsed / InSeconds)
            : Mathf.Max(0f, shown - elapsed / OutSeconds);
        photosFade.alpha = Mathf.Min(1f, photosFade.alpha + elapsed / FadeSeconds);
        Pose();
    }

    private void Pose()
    {
        float hidden = -Width - 2f * PaperDrawing.Margin - 20f;
        float x = open
            ? Mathf.LerpUnclamped(hidden, NoticeBoard.Left, Easing.Pop(shown))
            : Mathf.Lerp(hidden, NoticeBoard.Left, Easing.Out(shown));
        rect.anchoredPosition = new Vector2(x, 0f);
        group.alpha = open ? 1f : Easing.Out(shown);
        bool visible = open || shown > 0f;
        if (photos.gameObject.activeSelf != visible)
        {
            photos.gameObject.SetActive(visible);
        }
    }
}
