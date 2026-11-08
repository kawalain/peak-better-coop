using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The body of a paper form, one entry under another: a masthead, numbered
/// sections ruled with dashes, fields whose printed label leads by dots to
/// an answer in pen, check boxes, answers to circle, rubber stamps, and a
/// line to sign. Every text is a function, read again each frame, so the
/// form follows what it shows.
/// </summary>
internal sealed class PaperList
{
    /// <summary>The least a field takes; an explanation in small print adds to it.</summary>
    public const float FieldHeight = 56f;

    public const float HeaderHeight = 84f;

    private const float RuleHeight = 4f;

    private const float LeaderHeight = 10f;

    private const float CheckSize = 46f;

    private const float SignatureWidth = 340f;

    private const float StampHeight = 56f;

    private const float RingPadding = 10f;

    private const float RingStroke = 2.8f;

    private readonly List<PaperRow> rows = new();

    private readonly List<Func<bool>> backs = new();

    private readonly RectTransform content;

    private readonly TMP_Text template;

    private int section;

    public PaperList(RectTransform content, Button buttonTemplate)
    {
        this.content = content;
        template = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        PaperControl.UseSoundsOf(buttonTemplate);
    }

    public IReadOnlyList<PaperRow> Rows => rows;

    /// <summary>The game's menu text, to set new text in.</summary>
    public TMP_Text Template => template;

    public static Color Pen => ToColor(FormDrawing.Pen);

    public static Color StampRed => ToColor(FormDrawing.StampRed);

    /// <summary>A copy of a menu button with its own action, its text no longer localized by the game.</summary>
    public static Button CloneButton(Button template, Transform parent, string name, Action onClick)
    {
        GameObject clone = Object.Instantiate(template.gameObject, parent);
        clone.name = name;
        foreach (LocalizedText text in clone.GetComponentsInChildren<LocalizedText>(true))
        {
            text.autoSet = false;
            text.enabled = false;
        }

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() =>
        {
            try
            {
                onClick();
            }
            catch (Exception exception)
            {
                Guard.Report(name, exception);
            }

            if (InputHandler.GetCurrentUsedInputScheme() != InputScheme.Gamepad && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        });
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        return button;
    }

    /// <summary>A numbered section heading with a dashed rule under it, as the paper's perforations are drawn.</summary>
    public void Header(Func<string> text, Func<bool>? visible = null)
    {
        int number = ++section;
        PaperRow row = NewRow("Header", HeaderHeight, visible, TextAnchor.LowerLeft);
        TMP_Text title = AddText(row.Root.transform, "Title", PaperText.Heading, TextAlignmentOptions.BottomLeft, flexible: true);
        title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        title.characterSpacing = 2f;
        AddDrawing(row.Root.transform, "Rule", RuleHeight, size => PaperDrawing.DashedRule(size.x, size.y));
        row.Refreshers.Add(() => PaperText.Set(title, $"{number}.  {text()}"));
        Add(row);
    }

    /// <summary>A paragraph in small print.</summary>
    public void Note(Func<string> text, Func<bool>? visible = null)
    {
        PaperRow row = NewRow("Note", 40f, visible);
        TMP_Text note = AddText(row.Root.transform, "Text", PaperText.Note, TextAlignmentOptions.Left, flexible: true);
        Wrap(note);
        note.fontStyle = FontStyles.Italic;
        note.color = Faded(PaperText.Ink, 0.75f);
        row.Refreshers.Add(() => PaperText.Set(note, text()));
        Add(row);
    }

    /// <summary>A printed label leading by dots to an answer written in pen.</summary>
    public void Field(Func<string> label, Func<string> value, Func<string>? about = null, Func<bool>? visible = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        TMP_Text answer = AddText(line, "Answer", PaperText.Label, TextAlignmentOptions.BottomRight, flexible: false);
        answer.color = Pen;
        row.Refreshers.Add(() => PaperText.Set(answer, value()));
        Add(row);
    }

    /// <summary>A label with a check box after it; pressing the row's box ticks or clears it.</summary>
    public void Check(Func<string> label, Func<bool> get, Action<bool> set, Func<string>? about = null, Func<bool>? visible = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        RectTransform box = Fixed(line, "Box", CheckSize, CheckSize);
        VectorView.Add(box, size => FormDrawing.CheckBox(size.x), PaperText.Paper);
        RectTransform tick = Fill(NewRect("Tick", box));
        PenMark mark = PenMark.Add(tick, (size, hand) => PenPaths.Tick(size.x, hand), CheckSize * 0.1f, 0.22f, get());

        // The whole line answers, label and all; only the box grows under the pointer.
        line.gameObject.AddComponent<Image>().color = Color.clear;
        PaperControl control = PaperControl.Add<PaperControl>(line.gameObject, () => set(!get()));
        control.Back = control.Pressed;
        control.Visual = box;
        row.First = control;
        row.Refreshers.Add(() => mark.Shown = get());
        Add(row);
    }

    /// <summary>
    /// A question whose answer is chosen from a few: every answer is printed
    /// and the chosen one is ringed in pen. Many answers show as one, with
    /// arrows to step through them. Either way a right click or left steps
    /// back, and the right arrow forward.
    /// </summary>
    public void Choice<T>(
        Func<string> label,
        Func<T> get,
        Action<T> set,
        Func<IReadOnlyList<T>> choices,
        Func<T, string> name,
        Func<string>? about = null,
        Func<bool>? visible = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        void StepBy(int by) => set(Steps.Next(choices(), get(), by));
        IReadOnlyList<T> first = choices();
        if (first.Count is > 0 and <= 4)
        {
            Ringed(row, line, first, get, set, name, StepBy);
        }
        else
        {
            Stepper(row, line, get, name, StepBy);
        }

        Add(row);
    }

    /// <summary>
    /// A rubber stamp to press for an action. One not available now shows
    /// faint but still answers, to say why. Pressed, it comes down with a thump.
    /// </summary>
    public void Stamp(
        Func<string> label,
        Func<string> word,
        Action press,
        Func<bool>? available = null,
        Func<string>? about = null,
        Func<bool>? visible = null,
        Func<string>? value = null,
        StampFace? face = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        if (value != null)
        {
            TMP_Text answer = AddText(line, "Answer", PaperText.Label, TextAlignmentOptions.BottomRight, flexible: false);
            answer.color = Pen;
            row.Refreshers.Add(() => PaperText.Set(answer, value()));
        }

        StampMark stamp = AddStamp(line, word, press, face);
        row.First = stamp.Control;
        row.Refreshers.Add(() => stamp.Refresh(available?.Invoke() ?? true));
        Add(row);
    }

    /// <summary>
    /// A line to sign at the foot of the form, set apart and to the right,
    /// with the signer's name written on it in pen, and a crayon doodle
    /// someone left over it.
    /// </summary>
    public void Signature(Func<string> label, Func<string> signer, Func<string>? motto = null, Func<bool>? visible = null)
    {
        PaperRow row = NewRow("Signature", 0f, visible);
        row.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 96, 10);
        RectTransform line = Line(row.Root.transform);
        RectTransform spare = NewRect("Spare", line);
        spare.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        TMP_Text? saying = null;
        if (motto != null)
        {
            RectTransform words = NewRect("Motto", spare);
            words.anchorMin = Vector2.zero;
            words.anchorMax = Vector2.one;
            words.offsetMin = words.offsetMax = Vector2.zero;
            saying = PaperText.Make(words.gameObject, template, PaperText.Value, TextAlignmentOptions.BottomLeft);
            saying.fontStyle = FontStyles.Bold | FontStyles.Italic | FontStyles.SmallCaps;
            saying.characterSpacing = 3f;
            saying.color = Faded(PaperText.Blue, 0.75f);
        }
        TMP_Text printed = AddText(line, "Label", PaperText.Label, TextAlignmentOptions.BottomRight, flexible: false);
        RectTransform blank = Fixed(line, "Blank", SignatureWidth, 48f);
        RectTransform rule = NewRect("Line", blank);
        rule.anchorMin = Vector2.zero;
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, 4f);
        VectorView.Add(rule, size => FormDrawing.Line(size.x, size.y), PaperText.Ink);

        // Drawn before the name, so the name is written over it.
        RectTransform doodle = NewRect("Doodle", blank);
        doodle.anchorMin = doodle.anchorMax = new Vector2(1f, 0f);
        doodle.pivot = new Vector2(1f, 0f);
        doodle.sizeDelta = new Vector2(150f, 128f);
        doodle.anchoredPosition = new Vector2(-6f, 14f);
        doodle.localRotation = Quaternion.Euler(0f, 0f, -8f);
        string drawn = string.Empty;
        VectorView sketch = VectorView.Add(doodle, size => FormDrawing.Doodle(size.x, size.y, drawn), Color.clear);

        RectTransform nameRect = NewRect("Name", blank);
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(16f, 2f);
        nameRect.offsetMax = Vector2.zero;
        TMP_Text name = PaperText.Make(nameRect.gameObject, template, PaperText.Title * 0.75f, TextAlignmentOptions.BottomLeft);
        name.fontStyle = FontStyles.Italic;
        name.color = Pen;
        nameRect.localRotation = Quaternion.Euler(0f, 0f, 2f);
        row.Refreshers.Add(() =>
        {
            PaperText.Set(printed, label());
            if (saying != null)
            {
                PaperText.Set(saying, motto!());
            }

            string who = signer();
            PaperText.Set(name, who);
            if (who != drawn)
            {
                drawn = who;
                sketch.Redraw();
            }
        });
        Add(row);
    }

    /// <summary>A whole piece of form the caller lays out itself, such as a board of cards.</summary>
    public RectTransform Custom(string name, Action refresh, Func<Selectable?>? first = null, Func<bool>? visible = null)
    {
        PaperRow row = NewRow(name, 0f, visible);
        row.Refreshers.Add(refresh);
        row.FirstOf = first;
        Add(row);
        return (RectTransform)row.Root.transform;
    }

    /// <summary>Something on the form that takes Back before the page does, as a held card goes back to its place.</summary>
    public void OnBack(Func<bool> back) => backs.Add(back);

    /// <summary>Gives Back to the form; false when nothing on it took it.</summary>
    public bool Back() => backs.Any(back => back());

    /// <summary>A rubber stamp anywhere on the form, such as beside a signature.</summary>
    public StampMark AddStamp(Transform parent, Func<string> word, Action press, StampFace? face = null) =>
        new(parent, template, word, press, face);

    public void Clear()
    {
        foreach (PaperRow row in rows)
        {
            // Gone from the layout now, though destroyed only at the frame's end.
            row.Root.SetActive(false);
            Object.Destroy(row.Root);
        }

        rows.Clear();
        backs.Clear();
        section = 0;
    }

    public void Refresh()
    {
        foreach (PaperRow row in rows)
        {
            row.Refresh();
        }
    }

    /// <summary>The first thing on the form a gamepad can select.</summary>
    public Selectable? FirstSelectable() =>
        rows.Where(r => r.Root.activeInHierarchy).Select(r => r.FirstOf?.Invoke() ?? r.First).FirstOrDefault(s => s != null && s.isActiveAndEnabled);

    internal static RectTransform NewRect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return (RectTransform)holder.transform;
    }

    internal static Color Faded(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

    internal TMP_Text AddText(Transform parent, string name, float size, TextAlignmentOptions alignment, bool flexible)
    {
        RectTransform rect = NewRect(name, parent);
        TMP_Text text = PaperText.Make(rect.gameObject, template, size, alignment);
        rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = flexible ? 1f : 0f;
        return text;
    }

    private static Color ToColor(Ink ink) => new(ink.R, ink.G, ink.B, ink.A);

    private static void Wrap(TMP_Text text)
    {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static RectTransform Line(Transform parent)
    {
        RectTransform line = NewRect("Line", parent);
        HorizontalLayoutGroup layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 12f;
        return line;
    }

    private static RectTransform Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform Fixed(Transform parent, string name, float width, float height)
    {
        RectTransform rect = NewRect(name, parent);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        size.minHeight = size.preferredHeight = height;
        size.flexibleWidth = 0f;
        return rect;
    }

    /// <summary>A drawing as wide as its row, laid out under what comes before it.</summary>
    private static VectorView AddDrawing(Transform parent, string name, float height, Func<Vector2, string> drawing)
    {
        RectTransform rect = NewRect(name, parent);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = height;
        size.flexibleWidth = 1f;
        return VectorView.Add(rect, drawing, PaperText.Dash);
    }

    private PaperRow NewRow(string name, float minHeight, Func<bool>? visible = null, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        RectTransform root = NewRect("BetterCoop." + name, content);
        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childAlignment = alignment;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.spacing = 4f;
        stack.padding = new RectOffset(12, 12, 6, 6);
        root.gameObject.AddComponent<LayoutElement>().minHeight = minHeight;
        return new PaperRow(root.gameObject, visible);
    }

    /// <summary>A field's row: the label and dots on one line, small print under it.</summary>
    private (PaperRow Row, RectTransform Line) FieldRow(Func<string> label, Func<string>? about, Func<bool>? visible, bool leader)
    {
        PaperRow row = NewRow("Field", FieldHeight, visible);
        RectTransform line = Line(row.Root.transform);
        TMP_Text printed = AddText(line, "Label", PaperText.Label, TextAlignmentOptions.BottomLeft, flexible: false);
        if (leader)
        {
            RectTransform dots = NewRect("Leader", line);
            LayoutElement size = dots.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 24f;
            size.flexibleWidth = 1f;
            size.minHeight = size.preferredHeight = LeaderHeight;
            LeaderDots.Add(dots);
        }

        row.Refreshers.Add(() => PaperText.Set(printed, label()));
        if (about != null)
        {
            TMP_Text small = AddText(row.Root.transform, "About", PaperText.Note, TextAlignmentOptions.TopLeft, flexible: true);
            small.color = Faded(PaperText.Ink, 0.7f);
            Wrap(small);
            row.Refreshers.Add(() => PaperText.Set(small, about()));
        }

        return (row, line);
    }

    private void Ringed<T>(PaperRow row, RectTransform line, IReadOnlyList<T> options, Func<T> get, Action<T> set, Func<T, string> name, Action<int> step)
    {
        var rings = new List<(T Value, PenMark Ring, TMP_Text Text)>();
        T chosenFirst = get();
        foreach (T option in options)
        {
            T value = option;
            RectTransform holder = NewRect("Answer", line);
            HorizontalLayoutGroup pad = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset((int)RingPadding, (int)RingPadding, 4, 4);
            pad.childControlWidth = pad.childControlHeight = true;
            pad.childForceExpandWidth = pad.childForceExpandHeight = false;
            TMP_Text text = AddText(holder, "Text", PaperText.Value, TextAlignmentOptions.Center, flexible: false);
            RectTransform ring = NewRect("Ring", holder);
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = new Vector2(-2f, -2f);
            ring.offsetMax = new Vector2(2f, 2f);
            PenMark ink = PenMark.Add(
                ring,
                (size, hand) => PenPaths.Ring(size.x, size.y, RingStroke, hand),
                RingStroke,
                0.32f,
                EqualityComparer<T>.Default.Equals(value, chosenFirst));
            holder.gameObject.AddComponent<Image>().color = Color.clear;
            PaperControl control = PaperControl.Add<PaperControl>(holder.gameObject, () => set(value));
            control.Back = () => step(-1);
            row.First ??= control;
            rings.Add((value, ink, text));
        }

        row.Refreshers.Add(() =>
        {
            T chosen = get();
            foreach ((T value, PenMark ring, TMP_Text text) in rings)
            {
                bool on = EqualityComparer<T>.Default.Equals(value, chosen);
                ring.Shown = on;

                PaperText.Set(text, name(value));
                Color color = on ? Pen : Faded(PaperText.Ink, 0.6f);
                if (text.color != color)
                {
                    text.color = Color.Lerp(text.color, color, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                }
            }
        });
    }

    private void Stepper<T>(PaperRow row, RectTransform line, Func<T> get, Func<T, string> name, Action<int> step)
    {
        RectTransform back = Arrow(line, "Back", true);
        RectTransform answer = NewRect("Answer", line);
        LayoutElement width = answer.gameObject.AddComponent<LayoutElement>();
        width.minWidth = width.preferredWidth = 140f;
        width.minHeight = 40f;
        SlidingText text = SlidingText.Add(answer, template, PaperText.Label, Pen);
        int stepped = 1;
        void Go(int by)
        {
            stepped = by;
            step(by);
        }

        RectTransform underline = NewRect("Underline", answer);
        underline.anchorMin = Vector2.zero;
        underline.anchorMax = new Vector2(1f, 0f);
        underline.pivot = new Vector2(0.5f, 1f);
        underline.sizeDelta = new Vector2(0f, 4f);
        VectorView.Add(underline, size => FormDrawing.Line(size.x, size.y), PaperText.Ink);
        RectTransform forward = Arrow(line, "Forward", false);

        // The whole answer is one control; the arrows also answer the mouse.
        answer.gameObject.AddComponent<Image>().color = Color.clear;
        PaperControl control = PaperControl.Add<PaperControl>(answer.gameObject, () => Go(1));
        control.Back = () => Go(-1);
        control.Step = Go;
        AddArrow(back, () => Go(-1));
        AddArrow(forward, () => Go(1));
        row.First = control;
        row.Refreshers.Add(() => text.Set(name(get()), stepped));
    }

    private static RectTransform Arrow(Transform parent, string name, bool back)
    {
        RectTransform arrow = Fixed(parent, name, 28f, 40f);
        VectorView.Add(arrow, size => FormDrawing.Arrow(size.x, size.y, back), Color.clear).GetComponent<RawImage>().raycastTarget = true;
        return arrow;
    }

    /// <summary>An arrow beside an answer: the mouse presses it, a gamepad steps with left and right instead.</summary>
    private static void AddArrow(RectTransform arrow, Action press)
    {
        PaperControl control = PaperControl.Add<PaperControl>(arrow.gameObject, press);
        control.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void Add(PaperRow row)
    {
        rows.Add(row);
        row.Refresh();
    }
}

/// <summary>One entry of a paper form, and what keeps it up to date.</summary>
internal sealed class PaperRow
{
    private readonly Func<bool>? visible;

    private CanvasGroup? fade;

    private float appear = 1f;

    public PaperRow(GameObject root, Func<bool>? visible)
    {
        Root = root;
        this.visible = visible;
    }

    public GameObject Root { get; }

    public List<Action> Refreshers { get; } = new();

    /// <summary>What a gamepad selects first in this entry.</summary>
    public Selectable? First { get; set; }

    public Func<Selectable?>? FirstOf { get; set; }

    public void Refresh()
    {
        bool shown = visible?.Invoke() ?? true;
        if (Root.activeSelf != shown)
        {
            Root.SetActive(shown);
            if (shown)
            {
                // Comes into view softly rather than all at once.
                fade ??= Root.AddComponent<CanvasGroup>();
                appear = 0f;
            }
        }

        if (!shown)
        {
            return;
        }

        if (fade != null && appear < 1f)
        {
            appear = Mathf.Min(1f, appear + Time.unscaledDeltaTime / 0.22f);
            fade.alpha = Easing.Out(appear);
        }

        foreach (Action refresh in Refreshers)
        {
            refresh();
        }
    }
}

/// <summary>
/// A stamp to press: its face, a rubber stamp unless given another, shows
/// its word; unavailable, it shows faint but still answers. Pressed, it
/// comes down with a thump.
/// </summary>
internal sealed class StampMark
{
    private readonly Func<string> word;

    private readonly CanvasGroup ink;

    private readonly StampFace face;

    public StampMark(Transform parent, TMP_Text template, Func<string> word, Action press, StampFace? face = null)
    {
        this.word = word;
        this.face = face ?? new RubberFace();
        RectTransform holder = PaperList.NewRect("Stamp", parent);
        ink = holder.gameObject.AddComponent<CanvasGroup>();
        this.face.Build(holder, template);
        holder.gameObject.AddComponent<Image>().color = Color.clear;
        Control = PaperControl.Add<StampControl>(holder.gameObject, press);
        Control.Visual = holder;
    }

    public PaperControl Control { get; }

    /// <summary>Inks the stamp in another colour, as a stamp that only keeps things as they are.</summary>
    public void Recolour(Color color) => face.Recolour(new Ink(color.r, color.g, color.b));

    /// <summary>Follows the word and whether the stamp is available, fading between faint and full.</summary>
    public void Refresh(bool available)
    {
        face.Show(word());
        float alpha = available ? 1f : 0.35f;
        if (!Mathf.Approximately(ink.alpha, alpha))
        {
            ink.alpha = Mathf.MoveTowards(ink.alpha, alpha, Time.unscaledDeltaTime / 0.2f);
        }
    }
}

/// <summary>A stamp comes down hard: it starts large and settles onto the paper.</summary>
internal sealed class StampControl : PaperControl
{
    protected override void Press()
    {
        base.Press();
        Thump(1.12f);
    }
}
