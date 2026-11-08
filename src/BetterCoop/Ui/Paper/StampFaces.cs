using System;
using System.Collections.Generic;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// What a stamp leaves on the paper. A <see cref="StampMark"/> handles the
/// pressing, the thump, and fading when unavailable; its face draws the
/// impression and its words. A new kind of stamp is a new face.
/// </summary>
internal abstract class StampFace
{
    protected StampFace(Ink ink) => Ink = ink;

    /// <summary>The colour of the stamp pad.</summary>
    public Ink Ink { get; private set; }

    /// <summary>Builds the impression inside <paramref name="holder"/>, sizing the holder's layout.</summary>
    public abstract void Build(RectTransform holder, TMP_Text template);

    /// <summary>Shows the stamp's word, read again every frame.</summary>
    public abstract void Show(string word);

    /// <summary>Inks the stamp in another colour.</summary>
    public void Recolour(Ink ink)
    {
        Ink = ink;
        Redraw();
    }

    protected abstract void Redraw();

    protected static Color ToColor(Ink ink) => new(ink.R, ink.G, ink.B, ink.A);
}

/// <summary>An office rubber stamp: its word in capitals inside a worn double border, pressed at a slant.</summary>
internal sealed class RubberFace : StampFace
{
    private const float Height = 56f;

    private const float Slant = -5f;

    private VectorView face = null!;

    private TMP_Text text = null!;

    public RubberFace()
        : base(FormDrawing.StampRed)
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
        face = VectorView.Add(border, s => FormDrawing.Stamp(s.x, s.y, colour: Ink), Color.clear);

        RectTransform label = PaperList.NewRect("Word", holder);
        text = PaperText.Make(label.gameObject, template, PaperText.Value, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        text.color = ToColor(Ink);
        text.characterSpacing = 6f;
        label.localRotation = Quaternion.Euler(0f, 0f, Slant);
    }

    public override void Show(string word) => PaperText.Set(text, word);

    protected override void Redraw()
    {
        text.color = ToColor(Ink);
        face.Redraw();
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

    private readonly Func<string> top;

    private readonly Func<string> bottom;

    private readonly List<TMP_Text> letters = new();

    private VectorView face = null!;

    private TMP_Text text = null!;

    private TMP_Text time = null!;

    private RectTransform ring = null!;

    private TMP_Text template = null!;

    private string shownTop = string.Empty;

    private string shownBottom = string.Empty;

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
        face = VectorView.Add(mark, s => FormDrawing.Postmark(s.x, s.y, Ring, colour: Ink), Color.clear);

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
        Redraw();
    }

    public override void Show(string word)
    {
        PaperText.Set(text, word);
        PaperText.Set(time, DateTime.Now.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        string up = top();
        string down = bottom();
        if (up != shownTop || down != shownBottom)
        {
            shownTop = up;
            shownBottom = down;
            Letter();
        }
    }

    protected override void Redraw()
    {
        if (text == null)
        {
            return;
        }

        Color color = ToColor(Ink);
        text.color = time.color = color;
        foreach (TMP_Text letter in letters)
        {
            letter.color = color;
        }

        face.Redraw();
    }

    /// <summary>Sets the office's name and the date round the ring, a letter at a time.</summary>
    private void Letter()
    {
        foreach (TMP_Text letter in letters)
        {
            UnityEngine.Object.Destroy(letter.gameObject);
        }

        letters.Clear();
        float radius = Ring / 2f - 15f;
        Arc(shownTop, radius, upper: true);
        Arc(shownBottom, radius, upper: false);
    }

    private void Arc(string words, float radius, bool upper)
    {
        float[] widths = new float[words.Length];
        float total = 0f;
        for (int i = 0; i < words.Length; i++)
        {
            // Hangul is square; Latin letters and figures are narrower.
            widths[i] = LetterSize * (words[i] >= 0x1100 ? 1.0f : words[i] == ' ' ? 0.45f : 0.68f);
            total += widths[i];
        }

        float spread = Mathf.Min(total / radius, Mathf.PI * 0.9f);
        float scale = spread * radius / Mathf.Max(1f, total);
        float centre = upper ? 90f : 270f;
        float along = -spread / 2f;
        Color color = ToColor(Ink);
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
            rect.sizeDelta = new Vector2(LetterSize * 1.2f, LetterSize * 1.4f);
            rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle - centre);
            TMP_Text letter = PaperText.Make(rect.gameObject, template, LetterSize, TextAlignmentOptions.Center);
            letter.fontStyle = FontStyles.Bold;
            letter.text = words[i].ToString();
            letter.color = color;
            letters.Add(letter);
        }
    }
}
