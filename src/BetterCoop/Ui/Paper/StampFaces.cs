using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// What a stamp leaves on the paper. A <see cref="StampMark"/> handles the
/// pressing, the thump, and fading when unavailable; its face draws the
/// impression and its words. A new kind of stamp is a new face. A face shows
/// its outline and words in ink until struck: then its impression fills
/// with ink and its words show in the paper's colour (see <see cref="Strike"/>),
/// and it dries back in patches, each word's letters turning back to ink as
/// the patch under them dries (see <see cref="InkDry"/>).
/// </summary>
internal abstract class StampFace
{
    private VectorView? outline;

    private VectorView? solid;

    private CanvasGroup? solidInk;

    private InkDry? dry;

    private RectTransform? inkArea;

    private float inked;

    private int pattern = 1;

    protected StampFace(Ink ink) => Ink = ink;

    /// <summary>The colour of the stamp pad.</summary>
    public Ink Ink { get; private set; }

    /// <summary>The words on the face, coloured with the ink or, struck, the paper.</summary>
    protected List<TMP_Text> Words { get; } = new();

    /// <summary>Builds the impression inside <paramref name="holder"/>, sizing the holder's layout.</summary>
    public abstract void Build(RectTransform holder, TMP_Text template);

    /// <summary>Shows the stamp's word, read again every frame.</summary>
    public abstract void Show(string word);

    /// <summary>Inks the stamp in another colour.</summary>
    public void Recolour(Ink ink)
    {
        Ink = ink;
        outline?.Redraw();
        solid?.Redraw();
        Paint();
    }

    /// <summary>
    /// How struck the face shows: 0 its outline, 1 filled with ink, the words
    /// in the paper's colour, and between, dried back that far in the patches
    /// of <paramref name="seed"/>'s pattern.
    /// </summary>
    public void Strike(float amount, int seed = 1)
    {
        amount = Mathf.Clamp01(amount);
        if (!Mathf.Approximately(amount, inked) || seed != pattern)
        {
            inked = amount;
            pattern = seed;
            Paint();
        }
    }

    /// <summary>
    /// Draws the impression over <paramref name="mark"/>: <paramref name="draw"/>
    /// makes its SVG for a size, outlined or struck solid. Words added after
    /// this stay above it.
    /// </summary>
    protected void Impression(RectTransform mark, Func<Vector2, bool, string> draw)
    {
        outline = VectorView.Add(mark, s => draw(s, false), Color.clear);
        RectTransform over = PaperList.NewRect("Inked", mark);
        over.anchorMin = Vector2.zero;
        over.anchorMax = Vector2.one;
        over.offsetMin = over.offsetMax = Vector2.zero;
        solidInk = over.gameObject.AddComponent<CanvasGroup>();
        solidInk.blocksRaycasts = false;
        solidInk.interactable = false;
        solid = VectorView.Add(over, s => draw(s, true), Color.clear);
        dry = over.gameObject.AddComponent<InkDry>();
        inkArea = over;
        Paint();
    }

    /// <summary>Colours the words and shows the solid impression as struck as the face is.</summary>
    protected void Paint()
    {
        if (solidInk != null)
        {
            solidInk.alpha = inked > 0f ? 1f : 0f;
        }

        if (dry != null)
        {
            dry.Seed = pattern;
            dry.Dried = 1f - inked;
        }

        Color ink = WordInk;
        Color paper = WordStruck;
        bool drying = inked > 0f && inked < 1f && inkArea != null;
        foreach (TMP_Text word in Words)
        {
            if (word == null)
            {
                continue;
            }

            if (!drying)
            {
                word.color = inked >= 1f ? paper : ink;
                continue;
            }

            word.color = ink;
            PaintLetters(word, ink, paper);
        }
    }

    /// <summary>Colours each letter of <paramref name="word"/> by the ink left under it: the paper's colour while wet there, the ink's once dry.</summary>
    private void PaintLetters(TMP_Text word, Color ink, Color paper)
    {
        word.ForceMeshUpdate();
        TMP_TextInfo info = word.textInfo;
        Rect area = inkArea!.rect;
        float dried = 1f - inked;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo letter = info.characterInfo[i];
            if (!letter.isVisible)
            {
                continue;
            }

            Vector3 middle = (letter.bottomLeft + letter.topRight) * 0.5f;
            Vector2 under = inkArea.InverseTransformPoint(word.transform.TransformPoint(middle));
            Color32 colour = Color.Lerp(ink, paper, InkDry.LeftAt(area, under, dried, pattern));
            Color32[] colours = info.meshInfo[letter.materialReferenceIndex].colors32;
            for (int corner = 0; corner < 4; corner++)
            {
                colours[letter.vertexIndex + corner] = colour;
            }
        }

        word.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    /// <summary>The words' colour before the face is struck: its ink.</summary>
    protected virtual Color WordInk => ToColor(Ink);

    /// <summary>The words' colour struck: the paper's, showing through the solid.</summary>
    protected virtual Color WordStruck => ToColor(PaperDrawing.Paper);

    protected static Color ToColor(Ink ink) => new(ink.R, ink.G, ink.B, ink.A);
}

/// <summary>An office rubber stamp: its word in capitals inside a worn double border, pressed at a slant.</summary>
internal sealed class RubberFace : StampFace
{
    private const float Height = 56f;

    private const float Slant = -5f;

    private TMP_Text text = null!;

    public RubberFace()
        : this(FormDrawing.StampRed)
    {
    }

    public RubberFace(Ink ink)
        : base(ink)
    {
    }

    public override void Build(RectTransform holder, TMP_Text template)
    {
        LayoutElement size = holder.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = Height;
        size.flexibleWidth = 0f;
        HorizontalLayoutGroup pad = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
        pad.padding = new RectOffset(20, 20, 6, 6);
        pad.childControlWidth = pad.childControlHeight = true;
        // Expanding would make the stamp share the line's spare width with the dots.
        pad.childForceExpandWidth = pad.childForceExpandHeight = false;
        pad.childAlignment = TextAnchor.MiddleCenter;

        // The face slants as a stamp pressed by hand does; the layout stays square.
        RectTransform border = PaperList.NewRect("Face", holder);
        border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = new Vector2(4f, 4f);
        border.offsetMax = new Vector2(-4f, -4f);
        border.localRotation = Quaternion.Euler(0f, 0f, Slant);
        Impression(border, (s, inked) => FormDrawing.Seal(StampShape.Rubber, s.x, s.y, Ink, inked, seed: 3));

        RectTransform label = PaperList.NewRect("Word", holder);
        text = PaperText.Make(label.gameObject, template, PaperText.Value, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        text.characterSpacing = 6f;
        label.localRotation = Quaternion.Euler(0f, 0f, Slant);
        Words.Add(text);
        Paint();
    }

    public override void Show(string word) => PaperText.Set(text, word);
}

/// <summary>
/// A hand stamp of another <see cref="StampShape"/>: a ring, oval, or seal,
/// its word across the middle and, for a ringed one, the office's words round
/// its rim, top and foot. Pressed at a slant of its own.
/// </summary>
internal sealed class SealFace : StampFace
{
    private const float LetterSize = 11f;

    private readonly StampShape shape;

    private readonly Func<string>? top;

    private readonly Func<string>? bottom;

    private readonly int seed;

    private readonly RimLetters rim = new();

    private TMP_Text text = null!;

    private RectTransform ring = null!;

    private TMP_Text template = null!;

    private StampLayout layout;


    /// <param name="top">The words round the rim's top, for a ringed stamp.</param>
    /// <param name="bottom">The words round the rim's foot, for a ringed stamp.</param>
    /// <param name="seed">Where the ink ran thin, and which way the stamp slants.</param>
    public SealFace(StampShape shape, Ink ink, Func<string>? top = null, Func<string>? bottom = null, int seed = 5)
        : base(ink)
    {
        this.shape = shape;
        this.top = top;
        this.bottom = bottom;
        this.seed = seed;
    }

    public override void Build(RectTransform holder, TMP_Text template)
    {
        this.template = template;
        layout = FormDrawing.LayoutOf(shape);
        LayoutElement size = holder.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = layout.Width;
        size.minHeight = size.preferredHeight = layout.Height;
        size.flexibleWidth = 0f;

        RectTransform mark = PaperList.NewRect("Seal", holder);
        mark.anchorMin = Vector2.zero;
        mark.anchorMax = Vector2.one;
        mark.offsetMin = mark.offsetMax = Vector2.zero;
        mark.localRotation = Quaternion.Euler(0f, 0f, seed % 2 == 0 ? -4f : 5f);
        Impression(mark, (s, inked) => FormDrawing.Seal(shape, s.x, s.y, Ink, inked, seed));

        ring = PaperList.NewRect("Ring", mark);
        ring.anchorMin = ring.anchorMax = new Vector2(0f, 0.5f);
        ring.pivot = new Vector2(0.5f, 0.5f);
        ring.sizeDelta = new Vector2(layout.Height, layout.Height);
        ring.anchoredPosition = new Vector2(layout.RingX, 0f);

        RectTransform word = PaperList.NewRect("Word", ring);
        word.anchorMin = word.anchorMax = new Vector2(0.5f, 0.5f);
        word.sizeDelta = new Vector2(layout.Word, 30f);
        word.anchoredPosition = Vector2.zero;
        text = PaperText.Make(word.gameObject, template, PaperText.Value, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = PaperText.Value;
        Words.Add(text);
        Paint();
    }

    /// <summary>An embossed seal's words show by their shadow, deeper once struck.</summary>
    protected override Color WordInk => shape == StampShape.Notary ? ToColor(FormDrawing.EmbossShade) : base.WordInk;

    protected override Color WordStruck => shape == StampShape.Notary
        ? ToColor(PaperDrawing.Paper.Mix(PaperDrawing.InkBlack, 0.5f))
        : base.WordStruck;

    public override void Show(string word)
    {
        PaperText.Set(text, word);
        if (layout.Letters <= 0f)
        {
            return;
        }

        if (rim.Show(ring, template, Words, LetterSize, layout.Letters, top?.Invoke() ?? string.Empty, bottom?.Invoke() ?? string.Empty))
        {
            Paint();
        }
    }
}

/// <summary>
/// Words set a letter at a time round a stamp's ring, upright to the reader
/// at its top and foot. The figures of the foot, a date, stand on a dater's
/// wheels (see <see cref="NumberingMachine"/>) and turn on when it changes.
/// </summary>
internal sealed class RimLetters
{
    private readonly List<TMP_Text> letters = new();

    private string shownTop = string.Empty;

    private string shownFoot = string.Empty;

    private string footFrom = string.Empty;

    private string footTo = string.Empty;

    private float footSetAt;

    /// <summary>
    /// Sets <paramref name="up"/> and <paramref name="down"/> round <paramref name="ring"/>,
    /// keeping <paramref name="words"/> in step; true when the letters were set
    /// anew, and want painting.
    /// </summary>
    public bool Show(RectTransform ring, TMP_Text template, List<TMP_Text> words, float size, float radius, string up, string down)
    {
        float now = Time.unscaledTime;
        if (down != footTo)
        {
            footFrom = footTo.Length > 0 && NumberingMachine.Seconds(shownFoot, down) > 0f ? shownFoot : down;
            footTo = down;
            footSetAt = now;
        }

        string foot = NumberingMachine.Shown(footFrom, footTo, now - footSetAt);
        if (up != shownTop || !NumberingMachine.SameLayout(shownFoot, foot))
        {
            shownTop = up;
            shownFoot = foot;
            Set(ring, template, words, size, radius, up, foot);
            return true;
        }

        if (foot != shownFoot)
        {
            // The same letters in the same places: only the turning figures change.
            int at = letters.Count - foot.Count(letter => letter != ' ');
            foreach (char letter in foot)
            {
                if (letter != ' ')
                {
                    PaperText.Set(letters[at++], letter.ToString());
                }
            }

            shownFoot = foot;
        }

        return false;
    }

    private void Set(RectTransform ring, TMP_Text template, List<TMP_Text> words, float size, float radius, string up, string down)
    {
        foreach (TMP_Text letter in letters)
        {
            words.Remove(letter);
            UnityEngine.Object.Destroy(letter.gameObject);
        }

        letters.Clear();
        Arc(ring, template, letters, size, up, radius, upper: true);
        Arc(ring, template, letters, size, down, radius, upper: false);
        words.AddRange(letters);
    }

    private static void Arc(RectTransform ring, TMP_Text template, List<TMP_Text> letters, float size, string words, float radius, bool upper)
    {
        float[] widths = new float[words.Length];
        float total = 0f;
        for (int i = 0; i < words.Length; i++)
        {
            // Hangul is square; Latin letters and figures are narrower.
            widths[i] = size * (words[i] >= 0x1100 ? 1.0f : words[i] == ' ' ? 0.45f : 0.68f);
            total += widths[i];
        }

        float spread = Mathf.Min(total / radius, Mathf.PI * 0.9f);
        float scale = spread * radius / Mathf.Max(1f, total);
        float centre = upper ? 90f : 270f;
        float along = -spread / 2f;
        for (int i = 0; i < words.Length; i++)
        {
            float step = widths[i] * scale / radius;
            float middle = along + step / 2f;
            along += step;
            if (words[i] == ' ')
            {
                continue;
            }

            // Along the top the letters run clockwise, along the foot anticlockwise, both upright to the reader.
            float angle = centre + (upper ? -1f : 1f) * middle * Mathf.Rad2Deg;
            float radians = angle * Mathf.Deg2Rad;
            RectTransform rect = PaperList.NewRect("Letter", ring);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size * 1.2f, size * 1.4f);
            rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle - centre);
            TMP_Text letter = PaperText.Make(rect.gameObject, template, size, TextAlignmentOptions.Center);
            letter.fontStyle = FontStyles.Bold;
            letter.text = words[i].ToString();
            letters.Add(letter);
        }
    }
}

/// <summary>
/// A post office's postmark, as letters were cancelled: a double ring with
/// the office's name round its top and the date round its foot, the word
/// and the time across its middle, and wavy cancelling lines running off to
/// the right. Inked black, as post offices inked them.
/// </summary>
internal sealed class PostmarkFace : StampFace
{
    public const float Ring = 104f;

    public const float Waves = 116f;

    private const float Slant = 4f;

    private const float LetterSize = 12.5f;

    /// <summary>How far apart the time's figure wheels stand, in em.</summary>
    private const float ClockAdvance = 0.5f;

    private readonly Func<string> top;

    private readonly Func<string> bottom;

    private readonly RimLetters rim = new();

    private TMP_Text text = null!;

    private TMP_Text time = null!;

    private NumberWheels dater = null!;

    private RectTransform ring = null!;

    private TMP_Text template = null!;


    /// <param name="top">The office's name, round the ring's top.</param>
    /// <param name="bottom">The date, round the ring's foot.</param>
    public PostmarkFace(Func<string> top, Func<string> bottom)
        : base(FormDrawing.PostmarkInk)
    {
        this.top = top;
        this.bottom = bottom;
    }

    public override void Build(RectTransform holder, TMP_Text template)
    {
        this.template = template;
        LayoutElement size = holder.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = Ring + Waves;
        size.minHeight = size.preferredHeight = Ring;
        size.flexibleWidth = 0f;

        RectTransform mark = PaperList.NewRect("Postmark", holder);
        mark.anchorMin = Vector2.zero;
        mark.anchorMax = Vector2.one;
        mark.offsetMin = mark.offsetMax = Vector2.zero;
        mark.localRotation = Quaternion.Euler(0f, 0f, Slant);
        Impression(mark, (s, inked) => FormDrawing.Postmark(s.x, s.y, Ring, colour: Ink, inked: inked));

        ring = PaperList.NewRect("Ring", mark);
        ring.anchorMin = ring.anchorMax = new Vector2(0f, 0.5f);
        ring.pivot = new Vector2(0f, 0.5f);
        ring.sizeDelta = new Vector2(Ring, Ring);
        ring.anchoredPosition = Vector2.zero;

        RectTransform word = PaperList.NewRect("Word", ring);
        word.anchorMin = word.anchorMax = new Vector2(0.5f, 0.5f);
        word.sizeDelta = new Vector2(Ring * 0.62f, 26f);
        word.anchoredPosition = new Vector2(0f, 4f);
        text = PaperText.Make(word.gameObject, template, PaperText.Value * 0.85f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = PaperText.Value * 0.85f;

        RectTransform clock = PaperList.NewRect("Time", ring);
        clock.anchorMin = clock.anchorMax = new Vector2(0.5f, 0.5f);
        clock.sizeDelta = new Vector2(Ring * 0.62f, 18f);
        clock.anchoredPosition = new Vector2(0f, -15f);
        time = PaperText.Make(clock.gameObject, template, LetterSize, TextAlignmentOptions.Center);
        time.characterSpacing = 2f;
        dater = NumberWheels.Add(time, ClockAdvance);
        Words.Add(text);
        Words.Add(time);
        Paint();
    }

    public override void Show(string word)
    {
        PaperText.Set(text, word);
        dater.Set(DateTime.Now.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        if (rim.Show(ring, template, Words, LetterSize, Ring / 2f - 15f, top(), bottom()))
        {
            Paint();
        }
    }
}
