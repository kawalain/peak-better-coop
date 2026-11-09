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
/// sections over printed rules, fields whose printed label leads by dots to
/// an answer in pen, check boxes, answers to circle, rubber stamps, and a
/// line to sign. Every text is a function, read again each frame, so the
/// form follows what it shows.
/// </summary>
internal sealed class PaperList
{
    /// <summary>The least a field takes; an explanation in small print adds to it.</summary>
    public const float FieldHeight = 56f;

    public const float HeaderHeight = 84f;

    private const float RuleHeight = 5f;

    /// <summary>When, as the form first opens, its typed values start being typed in, a little later for each row down.</summary>
    private const float TypeInDelay = 0.25f;

    private const float TypeInStep = 0.05f;

    /// <summary>The latest a row's typing starts, however far down the form it lies.</summary>
    private const float TypeInLongest = 0.9f;

    /// <summary>The gap between table columns, as a line lays its cells.</summary>
    private const float TableGap = 12f;

    /// <summary>How soon after the line above a table's next line is typed as the form first opens, and the longest all its lines take.</summary>
    private const float TableLineStep = 0.09f;

    private const float TableTyping = 1.2f;

    private const float LeaderHeight = 10f;

    private const float CheckSize = 46f;

    private const float SignatureWidth = 400f;

    /// <summary>The corner the credits are scrawled over.</summary>
    private static readonly Vector2 CreditsSize = new(236f, 96f);

    /// <summary>Where each maker's name fell among the doodles, and its slant, crossing them.</summary>
    private static readonly (Vector2 Spot, float Slant)[] CreditSpots =
    [
        (new Vector2(50f, 38f), 13f),
        (new Vector2(138f, 13f), -19f),
    ];

    /// <summary>How a scrawled name shifts (x, y) and turns (z, degrees) in each boil frame.</summary>
    private static readonly Vector3[] NameWobble =
    [
        new(0f, 0f, 0f),
        new(0.6f, -0.4f, 0.7f),
        new(-0.5f, 0.3f, -0.5f),
        new(0.3f, 0.5f, 0.4f),
    ];

    /// <summary>How far in from the blank's left the name starts.</summary>
    private const float SignatureInset = 16f;

    private const float StampHeight = 56f;

    private const float RingPadding = 10f;

    private const float RingStroke = 2.8f;

    /// <summary>A stepped answer's narrowest width, and its widest before the words shrink.</summary>
    private const float StepperWidth = 140f;

    private const float StepperWidest = 460f;

    /// <summary>Air either side of the longest stepped answer.</summary>
    private const float StepperPadding = 24f;

    /// <summary>The room each Merit Badge takes, patch and words.</summary>
    private static readonly Vector2 BadgeCell = new(250f, 290f);

    private const float BadgePatch = 150f;

    /// <summary>How each badge in a row was stuck on, in degrees.</summary>
    private static readonly float[] BadgeTilts = [-6f, 4f, -3f, 7f];

    /// <summary>How wide a table's columns after the first are.</summary>
    private const float TableColumnWidth = 170f;

    private readonly List<PaperRow> rows = new();

    private readonly List<Func<bool>> backs = new();

    private readonly RectTransform content;

    private readonly TMP_Text template;

    private int section;

    /// <summary>How many times the form has been filled; the first time, its typed values are typed in as it opens.</summary>
    private int fills;

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

    /// <summary>
    /// A numbered section heading in bold small capitals over a printed rule,
    /// as the Boy Scout Handbook sets its headings.
    /// </summary>
    /// <returns>The header's row, for the page to scroll to.</returns>
    public RectTransform Header(Func<string> text, Func<bool>? visible = null)
    {
        int number = ++section;
        PaperRow row = NewRow("Header", HeaderHeight, visible, TextAnchor.LowerLeft);
        TMP_Text title = AddText(row.Root.transform, "Title", PaperText.Heading, TextAlignmentOptions.BottomLeft, flexible: true);
        title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        title.characterSpacing = 2f;
        AddDrawing(row.Root.transform, "Rule", RuleHeight, size => FormDrawing.HeadingRule(size.x, size.y));
        row.Refreshers.Add(() => PaperText.Set(title, $"{number}.  {text()}"));
        Add(row);
        return (RectTransform)row.Root.transform;
    }

    /// <summary>A small heading inside a section, grouping the rows under it.</summary>
    public void Subheading(Func<string> text, Func<bool>? visible = null)
    {
        PaperRow row = NewRow("Subheading", 0f, visible, TextAnchor.LowerLeft);
        row.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 18, 0);
        TMP_Text title = AddText(row.Root.transform, "Title", PaperText.Note, TextAlignmentOptions.BottomLeft, flexible: true);
        title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        title.characterSpacing = 3f;
        title.color = Faded(PaperText.Blue, 0.85f);
        row.Refreshers.Add(() => PaperText.Set(title, text()));
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

    /// <summary>A printed label leading by dots to a value typed in by the office, as on a typed form.</summary>
    public void Field(Func<string> label, Func<string> value, Func<string>? about = null, Func<bool>? visible = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        TMP_Text answer = Typed(line, "Answer", PaperText.Label);
        row.Refreshers.Add(() => PaperText.Set(answer, value()));
        Add(row);
    }

    /// <summary>A label with a check box after it; pressing the row's box ticks or clears it.</summary>
    /// <remarks>While <paramref name="locked"/> gives a reason, the box shows faint and a press only says why.</remarks>
    public void Check(Func<string> label, Func<bool> get, Action<bool> set, Func<string>? about = null, Func<bool>? visible = null, Func<string?>? locked = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        void Toggle()
        {
            if (locked?.Invoke() is string reason)
            {
                CoopNotice.Info(reason);
                return;
            }

            set(!get());
        }

        if (locked != null)
        {
            CanvasGroup faint = line.gameObject.AddComponent<CanvasGroup>();
            row.Refreshers.Add(() => faint.alpha = locked() != null ? 0.45f : 1f);
        }

        RectTransform box = Fixed(line, "Box", CheckSize, CheckSize);
        VectorView.Add(box, size => FormDrawing.CheckBox(size.x), PaperText.Paper);
        RectTransform tick = Fill(NewRect("Tick", box));
        PenMark mark = PenMark.Add(tick, (size, hand) => PenPaths.Tick(size.x, hand), CheckSize * 0.1f, 0.22f, get());

        // The whole line answers, label and all; only the box grows under the pointer.
        line.gameObject.AddComponent<Image>().color = Color.clear;
        PaperControl control = PaperControl.Add<PaperControl>(line.gameObject, Toggle);
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
    /// back, and the right arrow forward. While <paramref name="locked"/>
    /// gives a reason, the answers show faint and a press only says why.
    /// </summary>
    public void Choice<T>(
        Func<string> label,
        Func<T> get,
        Action<T> change,
        Func<IReadOnlyList<T>> choices,
        Func<T, string> name,
        Func<string>? about = null,
        Func<bool>? visible = null,
        Func<string?>? locked = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        void set(T value)
        {
            if (locked?.Invoke() is string reason)
            {
                CoopNotice.Info(reason);
                return;
            }

            change(value);
        }

        if (locked != null)
        {
            CanvasGroup faint = line.gameObject.AddComponent<CanvasGroup>();
            row.Refreshers.Add(() => faint.alpha = locked() != null ? 0.45f : 1f);
        }

        void StepBy(int by) => set(Steps.Next(choices(), get(), by));
        IReadOnlyList<T> first = choices();
        if (first.Count is > 0 and <= 4)
        {
            Ringed(row, line, first, get, set, name, StepBy);
        }
        else
        {
            Stepper(row, line, choices, get, name, StepBy);
        }

        Add(row);
    }

    /// <summary>
    /// A rubber stamp to press for an action. One not available now shows
    /// faint but still answers, to say why. Pressed, it comes down with a thump
    /// and its face strikes solid, drying back to its outline; with
    /// <paramref name="stamped"/>, it stays struck while that holds, as a
    /// request made. With <paramref name="confirm"/>, the first press only turns
    /// its word to "Really?", and a second press soon after acts. A
    /// <paramref name="pose"/>, when set, holds the face that far struck, for a specimen.
    /// </summary>
    public void Stamp(
        Func<string> label,
        Func<string> word,
        Action press,
        Func<bool>? available = null,
        Func<string>? about = null,
        Func<bool>? visible = null,
        Func<string>? value = null,
        StampFace? face = null,
        bool confirm = false,
        Func<bool>? stamped = null,
        Func<float?>? pose = null)
    {
        (PaperRow row, RectTransform line) = FieldRow(label, about, visible, leader: true);
        if (value != null)
        {
            TMP_Text answer = Typed(line, "Answer", PaperText.Label);
            row.Refreshers.Add(() => PaperText.Set(answer, value()));
        }

        StampMark stamp = new(line, template, word, press, face, confirm, stamped, pose);
        row.First = stamp.Control;
        row.Refreshers.Add(() => stamp.Refresh(available?.Invoke() ?? true));
        Add(row);
    }

    /// <summary>
    /// A line to sign at the foot of the form, set apart and to the right,
    /// with the signer's name written on it in pen, and a crayon doodle
    /// someone left over it.
    /// </summary>
    public void Signature(Func<string> label, Func<string> signer, Func<bool>? visible = null)
    {
        PaperRow row = NewRow("Signature", 0f, visible);
        row.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 96, 10);
        RectTransform line = Line(row.Root.transform);
        RectTransform spare = NewRect("Spare", line);
        spare.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        TMP_Text printed = AddText(line, "Label", PaperText.Label, TextAlignmentOptions.BottomRight, flexible: false);
        RectTransform blank = Fixed(line, "Blank", SignatureWidth, 56f);
        RectTransform rule = NewRect("Line", blank);
        rule.anchorMin = Vector2.zero;
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, 4f);
        VectorView.Add(rule, size => FormDrawing.Line(size.x, size.y), PaperText.Ink);

        // Drawn before the name, so the name is written over it; placed by the
        // name's end once the name is known, its rays reaching over the last letters.
        RectTransform doodle = NewRect("Doodle", blank);
        doodle.anchorMin = doodle.anchorMax = Vector2.zero;
        doodle.pivot = new Vector2(0.5f, 0.5f);
        doodle.sizeDelta = new Vector2(150f, 128f);
        doodle.localRotation = Quaternion.Euler(0f, 0f, -8f);
        string drawn = string.Empty;
        VectorView sketch = VectorView.Add(doodle, size => FormDrawing.Doodle(size.x, size.y, drawn, Boil.Frame), Color.clear, boil: true);

        RectTransform nameRect = NewRect("Name", blank);
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(SignatureInset, 2f);
        nameRect.offsetMax = Vector2.zero;
        TMP_Text name = PaperText.Make(nameRect.gameObject, template, PaperText.Title * 1.125f, TextAlignmentOptions.BottomLeft);
        PaperText.Fit(name, PaperText.Title * 0.6f);
        name.fontStyle = FontStyles.Italic;
        name.color = Pen;
        nameRect.localRotation = Quaternion.Euler(0f, 0f, 2f);
        row.Refreshers.Add(() =>
        {
            PaperText.Set(printed, label());
            string who = signer();
            PaperText.Set(name, who);
            if (who != drawn)
            {
                drawn = who;
                float end = SignatureInset + Mathf.Min(name.GetPreferredValues(who).x, SignatureWidth - SignatureInset);
                // A long name puts the doodle over its last letters rather than off the paper.
                doodle.anchoredPosition = new Vector2(Mathf.Min(end + 34f, SignatureWidth - 40f), 30f);
                sketch.Redraw();
            }
        });
        Add(row);
    }

    /// <summary>
    /// The makers' names scrawled in the form's bottom-left corner among a
    /// silly sea anemone and a clumsy pony, each at its own slant and
    /// crossing the others, as doodles left in a margin. The names boil with
    /// the doodles. Pressed, a name opens its maker's page; pointed at, it is
    /// underlined.
    /// </summary>
    public void Credits(IReadOnlyList<(string Name, Action Open)> makers)
    {
        PaperRow row = NewRow("Credits", 0f);
        row.Root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(2, 12, 0, 0);
        RectTransform line = Line(row.Root.transform);

        // Laid out by hand rather than by the row: each piece where it fell.
        RectTransform scrawl = Fixed(line, "Scrawl", CreditsSize.x, CreditsSize.y);
        Scribble(scrawl, "Anemone", new Vector2(64f, 56f), new Vector2(104f, 46f), -12f, size => FormDrawing.Anemone(size.x, size.y, Boil.Frame));
        Scribble(scrawl, "Pony", new Vector2(80f, 62f), new Vector2(168f, 42f), 9f, size => FormDrawing.Pony(size.x, size.y, Boil.Frame));

        Color lead = ToColor(FormDrawing.Lead);
        for (int i = 0; i < makers.Count; i++)
        {
            (Vector2 spot, float slant) = CreditSpots[i % CreditSpots.Length];
            RectTransform holder = NewRect(makers[i].Name, scrawl);
            holder.anchorMin = holder.anchorMax = Vector2.zero;
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = spot + new Vector2(0f, CreditsSize.y * (i / CreditSpots.Length));
            TMP_Text name = PaperText.Make(holder.gameObject, template, PaperText.Note * 0.8f, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Italic;
            name.color = lead;
            name.raycastTarget = true;
            PaperText.Set(name, makers[i].Name);
            holder.sizeDelta = new Vector2(name.preferredWidth + 6f, name.preferredHeight + 2f);
            LinkControl link = PaperControl.Add<LinkControl>(holder.gameObject, makers[i].Open);
            link.Text = name;
            row.First ??= link;

            // The name boils with the doodles, a step behind its neighbour.
            int offset = i;
            Vector2 rest = holder.anchoredPosition;
            row.Refreshers.Add(() =>
            {
                int frame = (Boil.Frame + offset) % Boil.Frames;
                holder.localRotation = Quaternion.Euler(0f, 0f, slant + NameWobble[frame].z);
                holder.anchoredPosition = rest + new Vector2(NameWobble[frame].x, NameWobble[frame].y);
            });
        }

        RectTransform spare = NewRect("Spare", line);
        spare.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Add(row);
    }

    /// <summary>A doodle dropped into <paramref name="scrawl"/>, centred on <paramref name="spot"/> at a slant, boiling.</summary>
    private static void Scribble(RectTransform scrawl, string name, Vector2 size, Vector2 spot, float slant, Func<Vector2, string> drawing)
    {
        RectTransform rect = NewRect(name, scrawl);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = spot;
        rect.localRotation = Quaternion.Euler(0f, 0f, slant);
        VectorView.Add(rect, drawing, Color.clear, boil: true);
    }

    /// <summary>
    /// A table ruled under its column heads, one line per entry, as many as
    /// the entries are now: lines come and go as they change. The first
    /// column takes the room the others leave; the others are printed to the
    /// right. A faint entry is greyed, as for a scout who has left. With no
    /// entries, the table says <paramref name="empty"/> instead.
    /// </summary>
    public void Table(Func<IReadOnlyList<string>> heads, Func<IReadOnlyList<TableEntry>> entries, Func<string>? empty = null, Func<bool>? visible = null)
    {
        int columns = heads().Count;
        PaperRow row = NewRow("Table", 0f, visible);
        row.Root.GetComponent<VerticalLayoutGroup>().spacing = 2f;
        RectTransform headLine = Line(row.Root.transform);
        var headTexts = new List<TMP_Text>();
        for (int i = 0; i < columns; i++)
        {
            TMP_Text head = Cell(headLine, i, PaperText.Note);
            head.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            head.characterSpacing = 2f;
            head.color = Faded(PaperText.Blue, 0.85f);
            headTexts.Add(head);
        }

        AddDrawing(row.Root.transform, "Rule", RuleHeight, size => FormDrawing.HeadingRule(size.x, size.y));
        RectTransform body = NewRect("Body", row.Root.transform);
        // The ruling of a troop's advancement chart: a line down between the columns, under the heading and the entries alike.
        RectTransform ruling = NewRect("Ruling", row.Root.transform);
        ruling.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ruling.anchorMin = Vector2.zero;
        ruling.anchorMax = Vector2.one;
        ruling.offsetMin = new Vector2(12f, 6f);
        ruling.offsetMax = new Vector2(-12f, -6f);
        ruling.SetAsFirstSibling();
        VectorView.Add(ruling, size => FormDrawing.ColumnRules(size.x, size.y, ColumnLines(ruling, headTexts)), Color.clear);
        VerticalLayoutGroup stack = body.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childControlWidth = stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.spacing = 2f;
        TMP_Text? none = null;
        if (empty != null)
        {
            none = AddText(row.Root.transform, "Empty", PaperText.Note, TextAlignmentOptions.Left, flexible: true);
            none.fontStyle = FontStyles.Italic;
            none.color = Faded(PaperText.Ink, 0.75f);
        }

        var lines = new List<(GameObject Root, CanvasGroup Faint, TMP_Text[] Cells)>();
        bool opening = fills <= 1;
        bool firstLines = true;
        row.Refreshers.Add(() =>
        {
            IReadOnlyList<string> printed = heads();
            for (int i = 0; i < columns; i++)
            {
                PaperText.Set(headTexts[i], i < printed.Count ? printed[i] : string.Empty);
            }

            IReadOnlyList<TableEntry> now = entries();
            // Typed in from the top as the form first opens, each line soon after the last; a line added later is typed at once.
            float each = Mathf.Min(TableLineStep, TableTyping / Mathf.Max(1, now.Count));
            while (lines.Count < now.Count)
            {
                RectTransform line = Line(body);
                line.gameObject.AddComponent<LayoutElement>().minHeight = 36f;
                CanvasGroup faint = line.gameObject.AddComponent<CanvasGroup>();
                Image ruled = NewRect("Ruled", line).gameObject.AddComponent<Image>();
                ruled.raycastTarget = false;
                ruled.color = Faded(PaperText.Dash, 0.45f);
                ruled.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                RectTransform under = ruled.rectTransform;
                under.anchorMin = Vector2.zero;
                under.anchorMax = new Vector2(1f, 0f);
                under.pivot = new Vector2(0.5f, 1f);
                under.sizeDelta = new Vector2(0f, 1.2f);
                under.anchoredPosition = new Vector2(0f, -1f);
                var cells = new TMP_Text[columns];
                for (int i = 0; i < columns; i++)
                {
                    cells[i] = Cell(line, i, PaperText.Value);
                    cells[i].color = PaperText.Ink;
                    Typewritten.Add(cells[i], typeIn: !firstLines || opening, delay: firstLines ? TypeInDelay + lines.Count * each : 0f);
                }

                lines.Add((line.gameObject, faint, cells));
            }

            firstLines = false;

            for (int n = 0; n < lines.Count; n++)
            {
                bool shown = n < now.Count;
                if (lines[n].Root.activeSelf != shown)
                {
                    lines[n].Root.SetActive(shown);
                }

                if (!shown)
                {
                    continue;
                }

                lines[n].Faint.alpha = now[n].Faint ? 0.45f : 1f;
                for (int i = 0; i < columns; i++)
                {
                    PaperText.Set(lines[n].Cells[i], i < now[n].Cells.Count ? now[n].Cells[i] : string.Empty);
                }
            }

            if (none != null)
            {
                bool nothing = now.Count == 0;
                if (none.gameObject.activeSelf != nothing)
                {
                    none.gameObject.SetActive(nothing);
                }

                PaperText.Set(none, empty!());
            }
        });
        Add(row);
    }

    /// <summary>
    /// Merit Badges pinned in rows of four: each a pencilled scout patch,
    /// boiling, tilted as it was stuck on, with its name, who earned it, and
    /// what won it under it.
    /// </summary>
    public void Badges(Func<IReadOnlyList<BadgeCard>> read, Func<bool>? visible = null)
    {
        IReadOnlyList<BadgeCard> cards = read();
        PaperRow row = NewRow("Badges", 0f, visible);
        RectTransform holder = NewRect("Grid", row.Root.transform);
        GridLayoutGroup grid = holder.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = BadgeCell;
        grid.spacing = new Vector2(12f, 18f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;
        for (int i = 0; i < cards.Count; i++)
        {
            BadgeCard card = cards[i];
            RectTransform cell = NewRect(card.Kind.ToString(), holder);
            VerticalLayoutGroup stack = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            stack.spacing = 2f;

            RectTransform patch = Fixed(cell, "Patch", BadgePatch, BadgePatch);
            RectTransform face = Fill(NewRect("Face", patch));
            face.localRotation = Quaternion.Euler(0f, 0f, BadgeTilts[i % BadgeTilts.Length]);
            MeritBadgeKind kind = card.Kind;
            VectorView.Add(face, size => FormDrawing.Badge(size.x, size.y, kind, Boil.Frame), Color.clear, boil: true);

            TMP_Text title = AddText(cell, "Title", PaperText.Label, TextAlignmentOptions.Center, flexible: true);
            title.fontStyle = FontStyles.Bold;
            TMP_Text winners = AddText(cell, "Winners", PaperText.Value, TextAlignmentOptions.Center, flexible: true);
            Wrap(winners);
            winners.color = Pen;
            TMP_Text amount = AddText(cell, "Amount", PaperText.Note, TextAlignmentOptions.Center, flexible: true);
            amount.color = Faded(PaperText.Ink, 0.7f);
            int index = i;
            row.Refreshers.Add(() =>
            {
                IReadOnlyList<BadgeCard> now = read();
                BadgeCard shown = index < now.Count ? now[index] : card;
                PaperText.Set(title, shown.Title);
                PaperText.Set(winners, shown.Winners);
                PaperText.Set(amount, shown.Amount);
            });
        }

        Add(row);
    }

    /// <summary>A table's cell: the first takes the spare width, every other a set width printed to the right.</summary>
    private TMP_Text Cell(RectTransform line, int column, float size)
    {
        if (column == 0)
        {
            TMP_Text first = AddText(line, "Cell", size, TextAlignmentOptions.BottomLeft, flexible: true);
            first.overflowMode = TextOverflowModes.Ellipsis;
            return first;
        }

        RectTransform rect = Fixed(line, "Cell", TableColumnWidth, 0f);
        rect.GetComponent<LayoutElement>().minHeight = -1f;
        rect.GetComponent<LayoutElement>().preferredHeight = -1f;
        TMP_Text text = PaperText.Make(rect.gameObject, template, size, TextAlignmentOptions.BottomRight);
        PaperText.Fit(text, size * 0.7f);
        return text;
    }

    /// <summary>Where the lines between a table's columns fall across <paramref name="ruling"/>: midway in the gap left of each column after the first.</summary>
    private static IReadOnlyList<float> ColumnLines(RectTransform ruling, IReadOnlyList<TMP_Text> heads)
    {
        var at = new List<float>();
        Rect area = ruling.rect;
        for (int i = 1; i < heads.Count; i++)
        {
            RectTransform cell = heads[i].rectTransform;
            Vector3 left = ruling.InverseTransformPoint(cell.TransformPoint(new Vector3(cell.rect.xMin, 0f, 0f)));
            at.Add(left.x - area.xMin - TableGap / 2f);
        }

        return at;
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
        fills++;
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

    /// <summary>
    /// A value typed onto the form in ribbon black (see <see cref="Typewritten"/>):
    /// the first time the form is filled, typed in row after row as it opens.
    /// </summary>
    private TMP_Text Typed(Transform parent, string name, float size, TextAlignmentOptions alignment = TextAlignmentOptions.BottomRight, bool flexible = false)
    {
        TMP_Text text = AddText(parent, name, size, alignment, flexible);
        text.color = PaperText.Ink;
        bool opening = fills <= 1;
        Typewritten.Add(text, typeIn: opening, delay: TypeInDelay + Mathf.Min(rows.Count * TypeInStep, TypeInLongest));
        return text;
    }

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

    private void Stepper<T>(PaperRow row, RectTransform line, Func<IReadOnlyList<T>> choices, Func<T> get, Func<T, string> name, Action<int> step)
    {
        RectTransform back = Arrow(line, "Back", true);
        RectTransform answer = NewRect("Answer", line);
        LayoutElement width = answer.gameObject.AddComponent<LayoutElement>();
        width.minWidth = width.preferredWidth = StepperWidth;
        width.minHeight = 40f;
        SlidingText text = SlidingText.Add(answer, template, PaperText.Label, Pen);

        // As wide as the longest answer, so none spills past the arrows; one longer than the line can give shrinks.
        string widest = string.Empty;
        void Fit()
        {
            string all = string.Join("\n", choices().Select(name));
            if (all == widest)
            {
                return;
            }

            widest = all;
            float wanted = 0f;
            foreach (T value in choices())
            {
                wanted = Mathf.Max(wanted, text.Text.GetPreferredValues(name(value)).x);
            }

            width.minWidth = width.preferredWidth = Mathf.Clamp(wanted + StepperPadding, StepperWidth, StepperWidest);
        }

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
        row.Refreshers.Add(() =>
        {
            Fit();
            text.Set(name(get()), stepped);
        });
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

/// <summary>A Merit Badge as the form shows it: which, its name, who earned it, and what won it.</summary>
internal sealed class BadgeCard
{
    public BadgeCard(MeritBadgeKind kind, string title, string winners, string amount)
    {
        Kind = kind;
        Title = title;
        Winners = winners;
        Amount = amount;
    }

    public MeritBadgeKind Kind { get; }

    public string Title { get; }

    public string Winners { get; }

    public string Amount { get; }
}

/// <summary>One line of a table: its cells, and whether it shows greyed.</summary>
internal sealed class TableEntry
{
    public TableEntry(IReadOnlyList<string> cells, bool faint = false)
    {
        Cells = cells;
        Faint = faint;
    }

    public IReadOnlyList<string> Cells { get; }

    public bool Faint { get; }
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
    /// <summary>How long a stamp asking "Really?" waits for the second press.</summary>
    private const float ArmedSeconds = 3f;

    /// <summary>How long a struck face stays solid before it starts to dry back.</summary>
    private const float WetSeconds = 0.25f;

    /// <summary>How long a struck face takes to dry back to its outline.</summary>
    private const float DrySeconds = 1.4f;

    private readonly Func<bool>? stamped;

    private readonly Func<float?>? pose;

    /// <summary>When the face was last struck, or last held struck by its state.</summary>
    private float struckAt = float.NegativeInfinity;

    /// <summary>The pattern the ink of the last strike dries in.</summary>
    private int pattern = 1;

    private readonly Func<string> word;

    private readonly CanvasGroup ink;

    private readonly StampFace face;

    private readonly Action press;

    private readonly bool confirm;

    private float armedAt = float.NegativeInfinity;

    private int usesWhenArmed;

    /// <param name="confirm">Whether the first press only asks "Really?" and a second press acts.</param>
    /// <param name="stamped">Whether the face stays struck, as while a request it made stands; it dries back once that ends.</param>
    /// <param name="pose">How struck the face shows when set, instead of following its presses, as for a specimen.</param>
    public StampMark(Transform parent, TMP_Text template, Func<string> word, Action press, StampFace? face = null, bool confirm = false, Func<bool>? stamped = null, Func<float?>? pose = null)
    {
        this.word = word;
        this.press = press;
        this.confirm = confirm;
        this.stamped = stamped;
        this.pose = pose;
        this.face = face ?? new RubberFace();
        RectTransform holder = PaperList.NewRect("Stamp", parent);
        ink = holder.gameObject.AddComponent<CanvasGroup>();
        this.face.Build(holder, template);
        holder.gameObject.AddComponent<Image>().color = Color.clear;
        Control = PaperControl.Add<StampControl>(holder.gameObject, Pressed);
        Control.Visual = holder;
    }

    public PaperControl Control { get; }

    /// <summary>Whether the stamp is waiting for the second press.</summary>
    private bool Armed => Time.unscaledTime - armedAt < ArmedSeconds;

    private void Pressed()
    {
        if (!confirm)
        {
            Act();
            return;
        }

        if (Armed)
        {
            Disarm();
            Act();
            return;
        }

        armedAt = Time.unscaledTime;
        usesWhenArmed = PaperControl.Uses;
        face.Show(Strings.StampReally);
        Control.Thump(1.3f);
    }

    private void Disarm() => armedAt = float.NegativeInfinity;

    private void Act()
    {
        struckAt = Time.unscaledTime;
        pattern = UnityEngine.Random.Range(1, int.MaxValue);
        PaperSounds.Play(PaperSounds.Kind.Stamp);
        press();
    }

    /// <summary>How struck the face shows: solid while its state holds, else drying back after a press or once its state ends.</summary>
    private float Struck()
    {
        if (pose?.Invoke() is float posed)
        {
            return posed;
        }

        if (stamped?.Invoke() == true)
        {
            struckAt = Time.unscaledTime;
            return 1f;
        }

        float since = Time.unscaledTime - struckAt;
        return since < WetSeconds ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (since - WetSeconds) / DrySeconds);
    }

    /// <summary>
    /// Puts the stamp back to its word once its time runs out, another control
    /// is used, or the selection moves on to something else.
    /// </summary>
    private void KeepArmed()
    {
        if (!Armed)
        {
            return;
        }

        GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (PaperControl.Uses != usesWhenArmed || (selected != null && selected != Control.gameObject))
        {
            Disarm();
        }
    }

    /// <summary>Inks the stamp in another colour, as a stamp that only keeps things as they are.</summary>
    public void Recolour(Color color) => face.Recolour(new Ink(color.r, color.g, color.b));

    /// <summary>Follows the word and whether the stamp is available, fading between faint and full.</summary>
    public void Refresh(bool available)
    {
        KeepArmed();
        face.Show(Armed ? Strings.StampReally : word());
        face.Strike(Struck(), pattern);
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
