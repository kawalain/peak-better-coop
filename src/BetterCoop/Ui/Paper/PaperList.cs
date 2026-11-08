using System;
using System.Collections.Generic;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The rows of a paper page, one under another: section headings with a
/// dashed rule, lines of information, and rows with a button on the right,
/// among them switches and choices. Every text is a function, read again
/// each frame, so rows follow what they show.
/// </summary>
internal sealed class PaperList
{
    public const float RowHeight = 76f;

    /// <summary>The least a row with an explanation takes; a long explanation wraps and the row grows.</summary>
    public const float DescribedRowHeight = 96f;

    public const float HeaderHeight = 84f;

    public const float RightColumn = 300f;

    /// <summary>As tall as the game's menu buttons leave room for in a row.</summary>
    public const float ButtonHeight = 60f;

    private const float RuleHeight = 4f;

    private readonly List<PaperRow> rows = new();

    private readonly RectTransform content;

    private readonly Button buttonTemplate;

    private readonly TMP_Text textTemplate;

    public PaperList(RectTransform content, Button buttonTemplate)
    {
        this.content = content;
        this.buttonTemplate = buttonTemplate;
        textTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
    }

    public IReadOnlyList<PaperRow> Rows => rows;

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

            // A mouse click would leave the button looking selected; a gamepad
            // keeps its selection to navigate.
            if (InputHandler.GetCurrentUsedInputScheme() != InputScheme.Gamepad && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        });
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        return button;
    }

    /// <summary>A section heading with a dashed rule under it, as the paper's perforations are drawn.</summary>
    public void Header(Func<string> text, Func<bool>? visible = null)
    {
        GameObject root = NewRow(HeaderHeight, TextAnchor.LowerLeft, described: false);
        RectTransform rule = NewRect("Rule", root.transform);
        rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rule.anchorMin = Vector2.zero;
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, RuleHeight);
        rule.anchoredPosition = Vector2.zero;
        VectorView.Add(rule, size => PaperDrawing.DashedRule(size.x, size.y), PaperText.Dash);
        Add(new PaperRow(root, text, null, null, null, visible, null, header: true));
    }

    /// <summary>A label, with a value on the right and a line of explanation under it when given.</summary>
    public void Info(Func<string> label, Func<string>? value = null, Func<bool>? visible = null, Func<string>? about = null)
    {
        GameObject root = NewRow(about == null ? RowHeight : DescribedRowHeight, TextAnchor.MiddleLeft, about != null);
        TMP_Text? valueText = value == null ? null : NewText(root.transform, "Value", TextAlignmentOptions.Right, RightColumn, PaperText.Value);
        Add(new PaperRow(root, label, about, value, null, visible, null, header: false, valueText));
    }

    /// <summary>
    /// A label and its explanation with a button on the right. A button not
    /// available dims but still answers, to say why.
    /// </summary>
    public void Action(
        Func<string> label,
        Func<string> button,
        Action onClick,
        Func<bool>? visible = null,
        Func<bool>? available = null,
        Func<string>? about = null)
    {
        GameObject root = NewRow(about == null ? RowHeight : DescribedRowHeight, TextAnchor.MiddleLeft, about != null);
        Button clone = CloneButton(buttonTemplate, root.transform, "Action", onClick);
        LayoutElement? existing = clone.GetComponent<LayoutElement>();
        LayoutElement size = existing != null ? existing : clone.gameObject.AddComponent<LayoutElement>();
        size.ignoreLayout = false;
        size.minWidth = RightColumn;
        size.preferredWidth = RightColumn;
        size.minHeight = ButtonHeight;
        size.preferredHeight = ButtonHeight;
        size.flexibleWidth = 0f;
        size.flexibleHeight = 0f;
        Add(new PaperRow(root, label, about, button, clone, visible, available, header: false));
    }

    /// <summary>A switch: the button reads On or Off and flips it.</summary>
    public void Toggle(Func<string> label, Func<bool> get, Action<bool> set, Func<string>? about = null, Func<bool>? visible = null) =>
        Action(label, () => get() ? Strings.On : Strings.Off, () => set(!get()), visible, about: about);

    /// <summary>A choice: the button reads the current one and steps to the next, round to the first.</summary>
    public void Cycle<T>(
        Func<string> label,
        Func<T> get,
        Action<T> set,
        Func<IReadOnlyList<T>> choices,
        Func<T, string> name,
        Func<string>? about = null,
        Func<bool>? visible = null)
    {
        Action(label, () => name(get()), () => set(Next(choices(), get())), visible, about: about);
    }

    public void Clear()
    {
        foreach (PaperRow row in rows)
        {
            // Gone from the layout now, though destroyed only at the frame's end.
            row.Root.SetActive(false);
            Object.Destroy(row.Root);
        }

        rows.Clear();
    }

    public void Refresh()
    {
        foreach (PaperRow row in rows)
        {
            row.Refresh();
        }
    }

    private static T Next<T>(IReadOnlyList<T> choices, T current)
    {
        if (choices.Count == 0)
        {
            return current;
        }

        for (int i = 0; i < choices.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(choices[i], current))
            {
                return choices[(i + 1) % choices.Count];
            }
        }

        return choices[0];
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return (RectTransform)holder.transform;
    }

    private void Add(PaperRow row)
    {
        rows.Add(row);
        row.Refresh();
    }

    /// <summary>A row whose left column holds the label, and its explanation under it when <paramref name="described"/>.</summary>
    private GameObject NewRow(float height, TextAnchor alignment, bool described)
    {
        RectTransform root = NewRect("BetterCoop.Row", content);
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 24f;
        layout.padding = new RectOffset(12, 12, 8, 8);
        // At least this tall; taller when an explanation wraps.
        root.gameObject.AddComponent<LayoutElement>().minHeight = height;

        RectTransform left = NewRect("Left", root);
        VerticalLayoutGroup stack = left.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.childAlignment = TextAnchor.MiddleLeft;
        stack.spacing = 4f;
        left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        NewText(left, "Label", TextAlignmentOptions.Left, -1f, PaperText.Label);
        if (described)
        {
            TMP_Text about = NewText(left, "About", TextAlignmentOptions.Left, -1f, PaperText.Note);
            about.color = new Color(about.color.r, about.color.g, about.color.b, about.color.a * 0.7f);
            about.textWrappingMode = TextWrappingModes.Normal;
            about.overflowMode = TextOverflowModes.Overflow;
        }

        return root.gameObject;
    }

    /// <summary>A line of text; a fixed width when <paramref name="width"/> is positive, else as wide as its column.</summary>
    private TMP_Text NewText(Transform parent, string name, TextAlignmentOptions alignment, float width, float fontSize)
    {
        RectTransform rect = NewRect(name, parent);
        TMP_Text text = PaperText.Make(rect.gameObject, textTemplate, fontSize, alignment);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            size.minWidth = width;
            size.preferredWidth = width;
            size.flexibleWidth = 0f;
        }
        else
        {
            size.flexibleWidth = 1f;
        }

        return text;
    }
}

/// <summary>One row of a paper page: a label and its explanation, and a value or a button.</summary>
internal sealed class PaperRow
{
    private const float UnavailableAlpha = 0.5f;

    private readonly Func<string> label;

    private readonly Func<string>? about;

    private readonly Func<string>? right;

    private readonly Func<bool>? visible;

    private readonly Func<bool>? available;

    private readonly TMP_Text labelText;

    private readonly TMP_Text? aboutText;

    private readonly TMP_Text? rightText;

    private readonly Color rightColor;

    public PaperRow(
        GameObject root,
        Func<string> label,
        Func<string>? about,
        Func<string>? right,
        Button? button,
        Func<bool>? visible,
        Func<bool>? available,
        bool header,
        TMP_Text? valueText = null)
    {
        Root = root;
        Button = button;
        this.label = label;
        this.about = about;
        this.right = right;
        this.visible = visible;
        this.available = available;
        Transform left = root.transform.Find("Left");
        labelText = left.Find("Label").GetComponent<TMP_Text>();
        aboutText = left.Find("About")?.GetComponent<TMP_Text>();
        if (header)
        {
            labelText.fontStyle = FontStyles.Bold;
            labelText.fontSize = PaperText.Heading;
        }

        rightText = button != null ? button.GetComponentInChildren<TMP_Text>(true) : valueText;
        rightColor = rightText != null ? rightText.color : Color.white;
    }

    public GameObject Root { get; }

    public Button? Button { get; }

    /// <summary>Runs after the button's own animation each frame, so the label's colour is the row's.</summary>
    public void Refresh()
    {
        bool shown = visible?.Invoke() ?? true;
        if (Root.activeSelf != shown)
        {
            Root.SetActive(shown);
        }

        if (!shown)
        {
            return;
        }

        PaperText.Set(labelText, label());
        if (aboutText != null && about != null)
        {
            PaperText.Set(aboutText, about());
        }

        if (rightText != null && right != null)
        {
            PaperText.Set(rightText, right());
            Color color = rightColor;
            if (available != null && !available())
            {
                color.a *= UnavailableAlpha;
            }

            if (rightText.color != color)
            {
                rightText.color = color;
            }
        }
    }
}
