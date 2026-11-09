using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Ui.Paper;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Notices;

/// <summary>
/// Where the mod's notices come in on paper, over the game: slips that slide
/// in from the left edge and stack down from the top, each on the paper its
/// kind calls for, and the Pair letter that pops out at the left's middle.
/// It needs a font that writes the player's language, taken from the game's
/// own player log once one is about; until then, or if a slip cannot be
/// made, notices go to the game's log instead.
/// </summary>
internal sealed class NoticeBoard : MonoBehaviour
{
    /// <summary>How far in from the left edge slips settle.</summary>
    public const float Left = 30f;

    private const float Top = 120f;

    private const float Gap = 12f;

    private static NoticeBoard? current;

    private static TMP_FontAsset? font;

    private readonly List<NoticeSlip> slips = new();

    private RectTransform? root;

    private TMP_Text? template;

    private PairLetter? letter;

    private PlayerConnectionLog? log;

    /// <summary>The Pair letter, or null while notices cannot be shown on paper.</summary>
    public static PairLetter? Letter
    {
        get
        {
            if (current == null || !current.Prepare())
            {
                return null;
            }

            if (current.letter == null)
            {
                current.letter = PairLetter.Create(current.root!, current.template!);
            }

            return current.letter;
        }
    }

    /// <summary>Shows a notice on paper; false when it cannot, so the caller falls back to the game's log.</summary>
    public static bool TryShow(NoticeKind kind, string message)
    {
        if (current == null)
        {
            return false;
        }

        try
        {
            if (!current.Prepare())
            {
                return false;
            }

            current.Add(kind, message);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"A paper notice failed, so it goes to the game's log: {e}");
            return false;
        }
    }

    /// <summary>Plays the game's own sound for a notice: the join chime, or the leave one for bad news.</summary>
    public static void Chime(bool bad)
    {
        PlayerConnectionLog? found = current?.Log();
        SFX_Instance? sound = found == null ? null : bad ? found.sfxLeave : found.sfxJoin;
        sound?.Play();
    }

    private void Awake() => current = this;

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }

        if (root != null)
        {
            Destroy(root.gameObject);
        }
    }

    private PlayerConnectionLog? Log()
    {
        if (log == null)
        {
            log = FindAnyObjectByType<PlayerConnectionLog>();
        }

        return log;
    }

    /// <summary>Builds the board's canvas once a font is at hand; false while none is.</summary>
    private bool Prepare()
    {
        if (root != null && template != null)
        {
            return true;
        }

        font ??= FindFont();
        if (font == null)
        {
            return false;
        }

        var board = new GameObject("BetterCoop.Notices", typeof(RectTransform));
        DontDestroyOnLoad(board);
        Canvas canvas = board.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = board.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)board.transform;

        // Never shown: only lends its font to every line on the slips.
        RectTransform holder = PaperList.NewRect("Font", root);
        holder.gameObject.SetActive(false);
        TextMeshProUGUI sample = holder.gameObject.AddComponent<TextMeshProUGUI>();
        sample.font = font;
        template = sample;
        return true;
    }

    /// <summary>The game's log font, or else any font in the scene that writes Korean.</summary>
    private TMP_FontAsset? FindFont()
    {
        if (Log()?.text?.font is TMP_FontAsset logged)
        {
            return logged;
        }

        return FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .Select(text => text.font)
            .FirstOrDefault(asset => asset != null && asset.HasCharacter('짝', searchFallbacks: true));
    }

    private void Add(NoticeKind kind, string message)
    {
        foreach (NoticeSlip old in slips.Where(s => s != null && !s.Leaving).Take(NoticeTiming.Overflow(slips.Count(s => s != null && !s.Leaving))).ToList())
        {
            old.Leave();
        }

        slips.Add(NoticeSlip.Create(root!, template!, kind, message));
        Chime(kind == NoticeKind.Caution);
    }

    private void LateUpdate()
    {
        slips.RemoveAll(slip => slip == null);
        float y = Top;
        for (int i = 0; i < slips.Count; i++)
        {
            NoticeSlip slip = slips[i];
            if (!slip.Leaving)
            {
                slip.Target = y;
                y += slip.Height + Gap;
            }
        }
    }
}

/// <summary>
/// One notice on paper: the paper and heading its kind calls for, its words
/// under the perforation, and the time it came in the corner. It slides in
/// from the left, keeps its place in the stack, and slides back out when
/// its time is up or a newer notice needs the room.
/// </summary>
internal sealed class NoticeSlip : MonoBehaviour
{
    public const float Width = 480f;

    private const float Band = 46f;

    private const float Side = 30f;

    private const float InSeconds = 0.35f;

    private const float OutSeconds = 0.3f;

    /// <summary>The longest a telegram's words take to come off the teleprinter.</summary>
    private const float TeleprinterSeconds = 1.2f;

    private RectTransform rect = null!;

    private CanvasGroup group = null!;

    private float life;

    private float age;

    private float shown;

    private float y;

    private bool placed;

    public float Height { get; private set; }

    /// <summary>How far below the top of the screen its place in the stack is.</summary>
    public float Target { get; set; }

    public bool Leaving { get; private set; }

    public static NoticeSlip Create(RectTransform parent, TMP_Text template, NoticeKind kind, string message)
    {
        NoticeStyle style = NoticeStyle.Of(kind);
        RectTransform rect = PaperList.NewRect("Notice", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-1.2f, 1.2f));
        NoticeSlip slip = rect.gameObject.AddComponent<NoticeSlip>();
        slip.rect = rect;
        slip.group = rect.gameObject.AddComponent<CanvasGroup>();
        slip.group.blocksRaycasts = false;
        slip.life = NoticeTiming.Seconds(kind, message.Length);

        float left = Side + style.Indent;
        float inner = Width - left - Side;
        // The heading and the time sit in the middle of the head, by the glyphs' own height.
        const float headHeight = Band - 20f;
        float headTop = PaperDrawing.HeadMiddle(Band) - (headHeight / 2f);
        TMP_Text heading = Line(rect, template, "Heading", PaperText.Note, TextAlignmentOptions.MidlineLeft);
        heading.text = Strings.NoticeHeading(kind);
        heading.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        heading.characterSpacing = 4f;
        heading.color = style.Heading;
        Place(heading.rectTransform, left, headTop, inner, headHeight);
        TMP_Text clock = Line(rect, template, "Time", PaperText.Note * 0.85f, TextAlignmentOptions.MidlineRight);
        clock.text = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        clock.color = PaperList.Faded(PaperText.Ink, 0.55f);
        clock.characterSpacing = 2f;
        Place(clock.rectTransform, left, headTop, inner - style.CornerRoom, headHeight);

        TMP_Text words = Line(rect, template, "Words", PaperText.Value, TextAlignmentOptions.TopLeft);
        words.textWrappingMode = TextWrappingModes.Normal;
        words.fontStyle = style.Words;
        words.color = style.Ink;
        PaperText.Set(words, message);
        float height = Mathf.Max(PaperText.Value * 1.3f, words.GetPreferredValues(words.text, inner, 0f).y);
        Place(words.rectTransform, left, Band + 14f, inner, height);
        slip.Height = Band + 14f + height + 22f;
        rect.sizeDelta = new Vector2(Width, slip.Height);

        Ink paper = style.Paper;
        int seed = UnityEngine.Random.Range(1, 1000);
        PaperSheet.Add(
            rect,
            "Sheet",
            (size, rough) =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Slip(inside.x, inside.y, Band, seed, paper, rough);
            },
            size =>
            {
                Vector2 inside = PaperSheet.Inside(size);
                return PaperDrawing.Shadow(inside.x, inside.y, Band);
            },
            Color.clear);
        style.Decorate?.Invoke(rect, template, slip.Height);
        VectorView.DrawAll(rect);
        switch (style.Printing)
        {
            case NoticePrinting.Teleprinter:
                // The message comes off the teleprinter a letter at a time as the slip slides in.
                Typewritten.Add(words, typeIn: true, delay: InSeconds * 0.5f).Longest = TeleprinterSeconds;
                break;
            case NoticePrinting.Ditto:
                DittoCopy.Add(rect, [heading, clock, words], delay: InSeconds * 0.4f);
                break;
        }

        return slip;
    }

    /// <summary>Sends the slip away before its time, as when a newer one needs its room.</summary>
    public void Leave() => Leaving = true;

    public static TMP_Text Line(RectTransform parent, TMP_Text template, string name, float size, TextAlignmentOptions alignment) =>
        PaperText.Make(PaperList.NewRect(name, parent).gameObject, template, size, alignment);

    /// <summary>Puts <paramref name="child"/> <paramref name="top"/> below its parent's top edge, <paramref name="left"/> in from its left.</summary>
    public static void Place(RectTransform child, float left, float top, float width, float height)
    {
        child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
        child.pivot = new Vector2(0f, 1f);
        child.anchoredPosition = new Vector2(left, -top);
        child.sizeDelta = new Vector2(width, height);
    }

    private void LateUpdate()
    {
        float elapsed = Time.unscaledDeltaTime;
        age += elapsed;
        if (!Leaving && age > life)
        {
            Leave();
        }

        shown = Leaving
            ? Mathf.Max(0f, shown - elapsed / OutSeconds)
            : Mathf.Min(1f, shown + elapsed / InSeconds);
        y = placed ? Easing.Towards(y, Target, 0.25f, elapsed) : Target;
        placed = true;
        float x = Mathf.Lerp(-Width - 2f * PaperDrawing.Margin, NoticeBoard.Left, Easing.Out(shown));
        rect.anchoredPosition = new Vector2(x, -y);
        group.alpha = Leaving ? Easing.Out(shown) : 1f;
        if (Leaving && shown <= 0f)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>The paper, inks, and marks of each kind of notice.</summary>
internal sealed class NoticeStyle
{
    private NoticeStyle(Ink paper, Color heading, Color ink, FontStyles words)
    {
        Paper = paper;
        Heading = heading;
        Ink = ink;
        Words = words;
    }

    public Ink Paper { get; }

    public Color Heading { get; }

    public Color Ink { get; }

    public FontStyles Words { get; }

    /// <summary>Extra room at the left for a mark there, such as a tag's hole.</summary>
    public float Indent { get; private set; }

    /// <summary>Room kept clear in the heading's right corner, such as for a stamp.</summary>
    public float CornerRoom { get; private set; }

    /// <summary>How the words come onto the paper.</summary>
    public NoticePrinting Printing { get; private set; }

    /// <summary>Marks drawn over the paper once it is laid out: the slip, a template, and its height.</summary>
    public Action<RectTransform, TMP_Text, float>? Decorate { get; private set; }

    public static NoticeStyle Of(NoticeKind kind) => kind switch
    {
        // A pad memo, written in pen by another scout, with the pad's red margin line.
        NoticeKind.Memo => new NoticeStyle(new Ink(0.985f, 0.975f, 0.940f), PaperText.Blue, PaperList.Pen, FontStyles.Italic)
        {
            Indent = 14f,
            Printing = NoticePrinting.Ditto,
            Decorate = (slip, _, height) =>
            {
                RectTransform margin = PaperList.NewRect("Margin", slip);
                Image line = margin.gameObject.AddComponent<Image>();
                line.color = PaperList.Faded(PaperList.StampRed, 0.55f);
                line.raycastTarget = false;
                NoticeSlip.Place(margin, 30f, 52f, 2f, height - 70f);
            },
        },

        // A manila baggage tag, punched at the left for its string.
        NoticeKind.Tag => new NoticeStyle(new Ink(0.880f, 0.770f, 0.560f), PaperText.Ink, PaperText.Ink, FontStyles.Normal)
        {
            Indent = 34f,
            Printing = NoticePrinting.Ditto,
            Decorate = (slip, _, height) =>
            {
                RectTransform hole = PaperList.NewRect("Hole", slip);
                NoticeSlip.Place(hole, 22f, (height - 30f) / 2f + 10f, 30f, 30f);
                VectorView.Add(hole, size => FormDrawing.TagHole(size.x), Color.clear);
            },
        },

        // A caution, under the office's red rubber stamp.
        NoticeKind.Caution => new NoticeStyle(PaperDrawing.Paper, PaperList.StampRed, PaperText.Ink, FontStyles.Normal)
        {
            CornerRoom = 150f,
            Decorate = (slip, template, _) =>
            {
                RectTransform holder = PaperList.NewRect("Stamp", slip);
                holder.anchorMin = holder.anchorMax = Vector2.one;
                holder.pivot = Vector2.one;
                holder.sizeDelta = new Vector2(140f, 56f);
                holder.anchoredPosition = new Vector2(-18f, -2f);
                holder.localRotation = Quaternion.Euler(0f, 0f, 7f);
                var face = new RubberFace();
                face.Build(holder, template);
                face.Show(Strings.CautionStamp);
            },
        },

        // A telegram, printed in capitals by the teleprinter on yellow paper.
        _ => new NoticeStyle(new Ink(0.965f, 0.875f, 0.520f), PaperText.Ink, PaperText.Ink, FontStyles.UpperCase)
        {
            Printing = NoticePrinting.Teleprinter,
        },
    };
}

/// <summary>How a notice's words come onto its paper.</summary>
internal enum NoticePrinting
{
    /// <summary>Already on it.</summary>
    Printed,

    /// <summary>A letter at a time, off a teleprinter.</summary>
    Teleprinter,

    /// <summary>From the top down, off a spirit duplicator's drum.</summary>
    Ditto,
}
