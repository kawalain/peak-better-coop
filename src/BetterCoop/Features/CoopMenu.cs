using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Ui;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Zorro.UI;
using Object = UnityEngine.Object;

namespace BetterCoop.Features;

/// <summary>
/// The Coop Menu: one "Coop" button in the pause menu opening a page that
/// keeps the game's own Back button and menu button style.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class CoopMenu
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    private static void AddMenu(PauseMenuMainPage __instance)
    {
        UIPageHandler handler = __instance.GetComponentInParent<UIPageHandler>(true);
        PauseMenuControlsPage? controls = handler != null ? handler.GetComponentInChildren<PauseMenuControlsPage>(true) : null;
        if (handler == null || controls == null)
        {
            Plugin.Log.LogWarning("The pause menu has no Controls page to build the Coop Menu from.");
            return;
        }

        CoopMenuPage page = CoopMenuPage.Build(handler, controls, __instance.m_settingsButton);
        AddButton(__instance, () => handler.TransistionToPage(page, new SetActivePageTransistion()));
    }

    /// <summary>A copy of a pause menu button with its own label and action, its text no longer localized by the game.</summary>
    internal static Button CloneButton(Button template, Transform parent, string name, Action onClick)
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

    /// <summary>Adds the Coop button to the pause menu, after Resume.</summary>
    private static void AddButton(PauseMenuMainPage menu, Action open)
    {
        Button template = menu.m_settingsButton;
        Transform parent = template.transform.parent;
        Button button = CloneButton(template, parent, "BetterCoop.CoopMenuButton", open);
        if (button.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
        {
            label.text = Strings.CoopMenu;
        }

        GameObject clone = button.gameObject;
        if (parent.GetComponent<LayoutGroup>() != null)
        {
            clone.transform.SetSiblingIndex(menu.m_resumeButton.transform.GetSiblingIndex() + 1);
            Plugin.Verbose("Added the Coop Menu to the pause menu's layout.");
            return;
        }

        // Without a layout group, sit one step below the lowest menu button.
        var buttons = new[]
            {
                menu.m_resumeButton, menu.m_settingsButton, menu.m_accoladesButton,
                menu.m_controllsButton, menu.m_inviteButton, menu.m_quitButton,
            }
            .Where(b => b != null && b.transform.parent == parent)
            .Select(b => (RectTransform)b.transform)
            .OrderByDescending(r => r.anchoredPosition.y)
            .ToList();
        float step = buttons.Count > 1 ? buttons[0].anchoredPosition.y - buttons[1].anchoredPosition.y : 60f;
        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = buttons.Count > 0
            ? buttons[^1].anchoredPosition - new Vector2(0, step)
            : rect.anchoredPosition - new Vector2(0, step);
        Plugin.Verbose($"Added the Coop Menu to the pause menu {step:F0} below its last button.");
    }
}

/// <summary>
/// The Coop Menu page: a sheet of paper in the middle of the screen with the
/// title, a scrolling list of rows, and Back. It rises like a ticket from the
/// kiosk when it opens and drops away when it closes. The rows are rebuilt
/// when the room's Pairs or the Troop Leader change, and their texts refresh
/// every frame while it is open.
/// </summary>
internal sealed class CoopMenuPage : UIPage, IHaveParentPage, INavigationPage
{
    private const float PanelWidth = 1000f;

    private const float Padding = 36f;

    private const float TitleHeight = 96f;

    private const float FooterHeight = 104f;

    private const float RowHeight = 60f;

    private const float DescribedRowHeight = 84f;

    private const float HeaderHeight = 72f;

    private const float RightColumn = 240f;

    /// <summary>Room between a perforation and the list, so rows never touch it.</summary>
    private const float ListGap = 10f;

    private const float WheelStep = 70f;

    private readonly List<MenuRow> rows = new();

    private readonly Vector3[] corners = new Vector3[4];

    private readonly ScrollMotion scroll = new();

    private UIPageHandler handler = null!;

    private RectTransform panel = null!;

    private CanvasGroup group = null!;

    private Motion motion;

    private float motionSeconds;

    private RectTransform viewport = null!;

    private RectTransform content = null!;

    private Button back = null!;

    private Button buttonTemplate = null!;

    private TMP_Text textTemplate = null!;

    private string shape = string.Empty;

    private GameObject? lastSelected;

    /// <summary>
    /// Copies the Controls page while it cannot wake: under an inactive
    /// holder, so the copy's Controls page never runs or registers, and is
    /// removed before it could. Only its Back button is kept; the page lays
    /// out its own panel, so the Controls page's layout cannot stretch or
    /// push the rows off the screen.
    /// </summary>
    internal static CoopMenuPage Build(UIPageHandler handler, PauseMenuControlsPage original, Button buttonTemplate)
    {
        var holder = new GameObject("BetterCoop.MenuHolder");
        holder.SetActive(false);
        GameObject clone = Object.Instantiate(original.gameObject, holder.transform);
        clone.name = "BetterCoop.CoopMenu";

        PauseMenuControlsPage copy = clone.GetComponent<PauseMenuControlsPage>();
        Button back = copy.backButton;
        Object.DestroyImmediate(copy);
        foreach (PauseMenuRebindButton rebind in clone.GetComponentsInChildren<PauseMenuRebindButton>(true))
        {
            Object.DestroyImmediate(rebind.gameObject);
        }

        CoopMenuPage page = clone.AddComponent<CoopMenuPage>();
        page.handler = handler;
        page.buttonTemplate = buttonTemplate;
        page.textTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        page.back = back;
        page.LayOut((RectTransform)clone.transform);
        back.onClick = new Button.ButtonClickedEvent();
        back.onClick.AddListener(page.GoBack);

        clone.transform.SetParent(original.transform.parent, false);
        clone.SetActive(false);
        clone.transform.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);
        Object.Destroy(holder);
        return page;
    }

    private enum Motion
    {
        Resting,
        Rising,
        Sinking,
    }

    public (UIPage, PageTransistion) GetParentPage() =>
        (handler.GetPage<PauseMenuMainPage>(), new SinkTransistion());

    public override void OnPageEnter()
    {
        base.OnPageEnter();
        scroll.Reset();
        lastSelected = null;
        motion = Motion.Rising;
        motionSeconds = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        Pose();
    }

    /// <summary>Drops the paper away; the page turns itself off once it is gone.</summary>
    internal void SinkAway()
    {
        motion = Motion.Sinking;
        motionSeconds = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        Pose();
    }

    public GameObject GetFirstSelectedGameObject() =>
        rows.Select(r => r.Button).FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy)?.gameObject
        ?? back.gameObject;

    /// <summary>A heading over the rows below it, with a rule under it.</summary>
    internal void AddHeader(Func<string> text, Func<bool>? visible = null)
    {
        GameObject root = NewRow(HeaderHeight, TextAnchor.LowerLeft, described: false);
        RectTransform rule = NewRect("Rule", root.transform);
        rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rule.anchorMin = Vector2.zero;
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, 2f);
        rule.anchoredPosition = Vector2.zero;
        Image line = rule.gameObject.AddComponent<Image>();
        line.color = new Color(PaperView.Blue.r, PaperView.Blue.g, PaperView.Blue.b, 0.45f);
        line.raycastTarget = false;
        Add(new MenuRow(root, text, null, null, null, visible, null, header: true, textTemplate));
    }

    /// <summary>A label, with a value on the right and a line of explanation under it when given.</summary>
    internal void AddInfo(Func<string> label, Func<string>? value = null, Func<bool>? visible = null, Func<string>? about = null)
    {
        GameObject root = NewRow(about == null ? RowHeight : DescribedRowHeight, TextAnchor.MiddleLeft, about != null);
        TMP_Text? valueText = value == null ? null : NewText(root.transform, "Value", TextAlignmentOptions.Right, RightColumn, 0.7f);
        Add(new MenuRow(root, label, about, value, null, visible, null, header: false, textTemplate, valueText));
    }

    /// <summary>
    /// A label and its explanation with a button on the right. A button not
    /// available dims but still answers, to say why.
    /// </summary>
    internal void AddAction(
        Func<string> label,
        Func<string> button,
        Action onClick,
        Func<bool>? visible = null,
        Func<bool>? available = null,
        Func<string>? about = null)
    {
        GameObject root = NewRow(about == null ? RowHeight : DescribedRowHeight, TextAnchor.MiddleLeft, about != null);
        Button clone = CoopMenu.CloneButton(buttonTemplate, root.transform, "Action", onClick);
        LayoutElement? existing = clone.GetComponent<LayoutElement>();
        LayoutElement size = existing != null ? existing : clone.gameObject.AddComponent<LayoutElement>();
        size.ignoreLayout = false;
        size.minWidth = RightColumn;
        size.preferredWidth = RightColumn;
        size.minHeight = RowHeight - 8f;
        size.preferredHeight = RowHeight - 8f;
        size.flexibleWidth = 0f;
        size.flexibleHeight = 0f;
        Add(new MenuRow(root, label, about, button, clone, visible, available, header: false, textTemplate));
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return (RectTransform)holder.transform;
    }

    private void GoBack()
    {
        try
        {
            handler.TransistionToPage(handler.GetPage<PauseMenuMainPage>(), new SinkTransistion());
        }
        catch (Exception exception)
        {
            Guard.Report("CoopMenuPage.GoBack", exception);
        }
    }

    private void OnDisable()
    {
        // The pause menu closed while the paper was still dropping: it is gone.
        if (motion == Motion.Sinking)
        {
            motion = Motion.Resting;
            group.alpha = 0f;
        }
    }

    private void LateUpdate()
    {
        try
        {
            // Left behind by a drop the pause menu cut short.
            if (handler.currentPage != this && motion != Motion.Sinking)
            {
                gameObject.SetActive(false);
                return;
            }

            if (!Animate())
            {
                return;
            }

            string now = MenuShape.Of();
            if (now != shape)
            {
                shape = now;
                Rebuild();
            }

            foreach (MenuRow row in rows)
            {
                row.Refresh();
            }

            KeepSelectionInView();
            scroll.Max = content.rect.height - viewport.rect.height;
            scroll.Step(Time.unscaledDeltaTime);
            content.anchoredPosition = new Vector2(0f, scroll.Position);
        }
        catch (Exception exception)
        {
            Guard.Report("CoopMenuPage.LateUpdate", exception);
        }
    }

    /// <summary>
    /// Moves the paper along its rise or drop, on unscaled time as a lone
    /// player's pause stops the clock. False once the drop has ended and the
    /// page has turned off.
    /// </summary>
    private bool Animate()
    {
        if (motion == Motion.Resting)
        {
            return true;
        }

        motionSeconds += Time.unscaledDeltaTime;
        if (motion == Motion.Rising && motionSeconds >= TicketMotion.RiseSeconds)
        {
            motion = Motion.Resting;
        }
        else if (motion == Motion.Sinking && motionSeconds >= TicketMotion.SinkSeconds)
        {
            motion = Motion.Resting;
            gameObject.SetActive(false);
            return false;
        }

        Pose();
        return true;
    }

    /// <summary>Places the paper where its motion has it now.</summary>
    private void Pose()
    {
        float offset = 0f;
        float tilt = 0f;
        float alpha = 1f;
        if (motion == Motion.Rising)
        {
            offset = TicketMotion.RiseOffset(motionSeconds);
            tilt = TicketMotion.RiseTilt(motionSeconds);
            alpha = TicketMotion.RiseAlpha(motionSeconds);
        }
        else if (motion == Motion.Sinking)
        {
            offset = TicketMotion.SinkOffset(motionSeconds);
            tilt = TicketMotion.SinkTilt(motionSeconds);
            alpha = TicketMotion.SinkAlpha(motionSeconds);
        }

        // From the paper's resting place to wholly below the screen.
        float travel = ((RectTransform)panel.parent).rect.height * 0.94f + PaperDrawing.Margin + PaperDrawing.ShadowDrop;
        panel.anchoredPosition = new Vector2(0f, offset * travel);
        panel.localRotation = Quaternion.Euler(0f, 0f, tilt);
        group.alpha = alpha;
    }

    private void OnWheel(PointerEventData eventData)
    {
        float wheel = eventData.scrollDelta.y;
        // Some input modules count a notch as 120, others as 1.
        float notches = Mathf.Min(Mathf.Abs(wheel) >= 10f ? Mathf.Abs(wheel) / 120f : Mathf.Abs(wheel), 3f);
        scroll.Push(-Mathf.Sign(wheel) * notches * WheelStep);
    }

    /// <summary>A sheet of paper in the middle of the screen: the title, a scrolling list of rows, and Back.</summary>
    private void LayOut(RectTransform root)
    {
        var backRect = (RectTransform)back.transform;
        Vector2 backSize = backRect.rect.size;

        panel = NewRect("BetterCoop.Panel", root);
        // The Controls page may lay out its children; the panel keeps its own anchors.
        panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        panel.anchorMin = new Vector2(0.5f, 0.06f);
        panel.anchorMax = new Vector2(0.5f, 0.94f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, 0f);
        panel.anchoredPosition = Vector2.zero;
        group = panel.gameObject.AddComponent<CanvasGroup>();

        // The paper reaches past the panel by its shadow's margin, so its edge is the panel's.
        RectTransform sheet = NewRect("Paper", panel);
        sheet.anchorMin = Vector2.zero;
        sheet.anchorMax = Vector2.one;
        sheet.offsetMin = new Vector2(-PaperDrawing.Margin, -PaperDrawing.Margin);
        sheet.offsetMax = new Vector2(PaperDrawing.Margin, PaperDrawing.Margin);
        sheet.gameObject.AddComponent<RawImage>().color = PaperView.Paper;
        PaperView paper = sheet.gameObject.AddComponent<PaperView>();
        paper.Panel = panel;
        paper.TopTear = TitleHeight;
        paper.BottomTear = FooterHeight;

        RectTransform titleRect = NewRect("Title", panel);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, TitleHeight);
        titleRect.anchoredPosition = Vector2.zero;
        TMP_Text title = Text(titleRect.gameObject, textTemplate.fontSize * 1.1f, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.color = PaperView.Blue;
        title.text = Strings.CoopMenu;

        viewport = NewRect("Viewport", panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(Padding, FooterHeight + ListGap);
        viewport.offsetMax = new Vector2(-Padding, -(TitleHeight + ListGap));
        // Catches the mouse wheel over the gaps between rows.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        // Rows fade out at the list's edges rather than being cut.
        viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 18);
        viewport.gameObject.AddComponent<WheelCatcher>().Wheel = OnWheel;

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 8, 16);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        backRect.SetParent(panel, false);
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.sizeDelta = backSize;
        backRect.anchoredPosition = new Vector2(0f, (FooterHeight - backSize.y) / 2f);

        // Everything else of the Controls page stays hidden.
        foreach (Transform child in root.Cast<Transform>().ToList())
        {
            if (child != panel)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Glides a row a gamepad moves to into view.</summary>
    private void KeepSelectionInView()
    {
        GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelected)
        {
            return;
        }

        lastSelected = selected;
        if (selected == null || !selected.transform.IsChildOf(content))
        {
            return;
        }

        // Measured down from the list's top, as the scroll position is.
        ((RectTransform)selected.transform).GetWorldCorners(corners);
        float top = -content.InverseTransformPoint(corners[1]).y;
        float bottom = -content.InverseTransformPoint(corners[0]).y;
        float view = viewport.rect.height;
        if (top < scroll.Position)
        {
            scroll.GlideTo(top - ListGap);
        }
        else if (bottom > scroll.Position + view)
        {
            scroll.GlideTo(bottom - view + ListGap);
        }
    }

    private void Rebuild()
    {
        foreach (MenuRow row in rows)
        {
            Destroy(row.Root);
        }

        rows.Clear();
        PlayerRows.Add(this);
        if (PhotonNetwork.IsMasterClient)
        {
            TroopLeaderRows.Add(this);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void Add(MenuRow row)
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
        layout.padding = new RectOffset(12, 12, 4, 4);
        LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
        size.minHeight = height;
        size.preferredHeight = height;

        RectTransform left = NewRect("Left", root);
        VerticalLayoutGroup stack = left.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        stack.childAlignment = TextAnchor.MiddleLeft;
        stack.spacing = 2f;
        left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        NewText(left, "Label", TextAlignmentOptions.Left, -1f, 0.7f);
        if (described)
        {
            TMP_Text about = NewText(left, "About", TextAlignmentOptions.Left, -1f, 0.5f);
            about.color = new Color(about.color.r, about.color.g, about.color.b, about.color.a * 0.65f);
        }

        return root.gameObject;
    }

    /// <summary>A line of text; a fixed width when <paramref name="width"/> is positive, else as wide as its column.</summary>
    private TMP_Text NewText(Transform parent, string name, TextAlignmentOptions alignment, float width, float scale)
    {
        RectTransform rect = NewRect(name, parent);
        TMP_Text text = Text(rect.gameObject, textTemplate.fontSize * scale, alignment);
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

    private TMP_Text Text(GameObject holder, float fontSize, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
        // Ink on paper: the font's plain material, without the menu's glow or outline.
        text.font = textTemplate.font;
        text.fontSharedMaterial = textTemplate.font != null ? textTemplate.font.material : textTemplate.fontSharedMaterial;
        text.fontSize = fontSize;
        text.color = PaperView.Ink;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}

/// <summary>One row of the Coop Menu: a label and its explanation, and a value or a button.</summary>
internal sealed class MenuRow
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

    public MenuRow(
        GameObject root,
        Func<string> label,
        Func<string>? about,
        Func<string>? right,
        Button? button,
        Func<bool>? visible,
        Func<bool>? available,
        bool header,
        TMP_Text template,
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
            labelText.fontSize = template.fontSize * 0.8f;
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

        Set(labelText, label());
        if (aboutText != null && about != null)
        {
            Set(aboutText, about());
        }

        if (rightText != null && right != null)
        {
            Set(rightText, right());
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

    private static void Set(TMP_Text text, string value)
    {
        if (text.text != value)
        {
            text.text = value;
        }
    }
}

/// <summary>What decides the Coop Menu's rows: the room, the Pairs, and who leads.</summary>
internal static class MenuShape
{
    public static string Of()
    {
        if (!PhotonNetwork.InRoom)
        {
            return "out";
        }

        string pairs = string.Join(",", PairRoster.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"));
        string players = string.Join(",", PhotonNetwork.PlayerList.Select(p => p.ActorNumber));
        return $"{PhotonNetwork.IsMasterClient}|{PairRoster.Formed}|{pairs}|{players}";
    }
}

/// <summary>Every player's rows: their own Pair and Role Preference, and the Pairs of the room.</summary>
internal static class PlayerRows
{
    public static void Add(CoopMenuPage page)
    {
        page.AddAction(
            () => Strings.MenuRolePreference,
            () => Strings.RoleName(CoopConfig.RolePreference.Value),
            CycleRolePreference,
            about: () => Strings.AboutRolePreference);
        page.AddAction(
            PartnerLine,
            () => RoleSwap.Pending ? Strings.CancelSwap : Strings.SwapPlaces,
            RoleSwap.Toggle,
            visible: () => PairRoster.TryGetPair(Character.localCharacter, out _),
            available: () => RoleSwap.Pending || RoleSwap.CanRequest(out _),
            about: () => Strings.AboutSwapPlaces);
        page.AddInfo(
            () => Strings.MenuNoPartner,
            visible: () => PairRoster.Formed && !PairRoster.TryGetPair(Character.localCharacter, out _),
            about: () => Strings.AboutNoPartner);
        page.AddInfo(
            () => Strings.MenuNextShuffle,
            NextShuffle,
            visible: () => PairRoster.Formed,
            about: () => Strings.AboutNextShuffle);
        page.AddInfo(() => Strings.MenuPairsForm, visible: () => !PairRoster.Formed);

        if (!PairRoster.Formed)
        {
            return;
        }

        page.AddHeader(() => Strings.MenuPairs);
        foreach (Pair pair in PairRoster.Pairs)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // The Troop Leader turns any Pair round without waiting for both to ask.
                page.AddAction(
                    () => Strings.MenuPair(NameOf(pair.Carrier), NameOf(pair.Rider)),
                    () => Strings.SwapPlaces,
                    () => TroopLeaderRows.Order(PairShuffle.Request(OperationKind.RoleSwap, pair.Carrier, pair.Rider)),
                    available: () => !PairShuffle.Busy);
            }
            else
            {
                page.AddInfo(() => Strings.MenuPair(NameOf(pair.Carrier), NameOf(pair.Rider)));
            }
        }

        page.AddInfo(
            () => Strings.MenuUnpaired,
            () => string.Join(", ", UnpairedActors().Select(NameOf)),
            visible: () => UnpairedActors().Any());
    }

    internal static IEnumerable<int> UnpairedActors() =>
        PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => !PairRoster.Pairs.Any(p => p.Contains(a)));

    internal static string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? $"#{actor}";

    private static string PartnerLine()
    {
        Character local = Character.localCharacter;
        if (!PairRoster.TryGetPair(local, out Pair pair))
        {
            return string.Empty;
        }

        int me = PairRoster.ActorOf(local);
        return Strings.MenuPartner(NameOf(pair.PartnerOf(me)), PairRoster.RoleOf(local) == PairRole.Carrier);
    }

    private static string NextShuffle()
    {
        if (PairShuffle.Busy)
        {
            return Strings.MenuShuffleNow;
        }

        return PairShuffle.SecondsToTimedShuffle() is double seconds
            ? Strings.Clock(seconds)
            : Strings.MenuShuffleOff;
    }

    private static void CycleRolePreference()
    {
        CoopConfig.RolePreference.Value = CoopConfig.RolePreference.Value switch
        {
            RolePreference.Random => RolePreference.Carrier,
            RolePreference.Carrier => RolePreference.Rider,
            _ => RolePreference.Random,
        };
    }
}

/// <summary>The Troop Leader's rows: Pair changes and the Run Rules.</summary>
internal static class TroopLeaderRows
{
    /// <summary>Timed Shuffle intervals the menu steps through, in minutes.</summary>
    private static readonly int[] Intervals = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60];

    /// <summary>The players picked for a Pair Assignment, by actor; -1 for none yet.</summary>
    private static int assignCarrier = -1;

    private static int assignRider = -1;

    public static void Add(CoopMenuPage page)
    {
        page.AddHeader(() => Strings.MenuTroopLeader);
        page.AddAction(
            () => Strings.MenuShuffleNowLabel,
            () => Strings.MenuGo,
            () => Order(PairShuffle.Request(OperationKind.OrderedShuffle)),
            visible: () => PairRoster.Formed,
            available: () => !PairShuffle.Busy,
            about: () => Strings.AboutShuffleNow);
        page.AddAction(
            () => Strings.MenuRecover,
            () => Strings.MenuGo,
            CarryKeeper.RequestRecovery,
            visible: () => PairRoster.Formed,
            about: () => Strings.AboutRecover);
        page.AddAction(
            () => Strings.MenuAssignCarrier,
            () => PickName(assignCarrier),
            () => assignCarrier = NextPick(assignCarrier),
            visible: () => PairRoster.Formed,
            about: () => Strings.AboutAssignCarrier);
        page.AddAction(
            () => Strings.MenuAssignRider,
            () => PickName(assignRider),
            () => assignRider = NextPick(assignRider),
            visible: () => PairRoster.Formed,
            about: () => Strings.AboutAssignRider);
        page.AddAction(
            () => Strings.MenuAssign,
            () => Strings.MenuGo,
            Assign,
            visible: () => PairRoster.Formed,
            available: () => !PairShuffle.Busy && PairShuffle.CanAssign(assignCarrier, assignRider),
            about: () => Strings.AboutAssign);

        page.AddHeader(() => Strings.MenuRunRules);
        Toggle(page, () => Strings.RuleCampfireShuffle, () => Strings.AboutCampfireShuffle, CoopConfig.CampfireShuffle);
        Toggle(page, () => Strings.RuleTimedShuffle, () => Strings.AboutTimedShuffle, CoopConfig.TimedShuffle);
        page.AddAction(
            () => Strings.RuleTimedShuffleInterval,
            () => Strings.Minutes(ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value)),
            NextInterval,
            about: () => Strings.AboutTimedShuffleInterval);
        Toggle(page, () => Strings.RuleHandOverPlace, () => Strings.AboutHandOverPlace, CoopConfig.HandOverPlace);
        Toggle(page, () => Strings.RuleHandOverItems, () => Strings.AboutHandOverItems, CoopConfig.HandOverItems);
        Toggle(page, () => Strings.RuleCarryWeight, () => Strings.AboutCarryWeight, CoopConfig.CarryWeight);
        Toggle(page, () => Strings.RuleRiderPicksFromBackpack, () => Strings.AboutRiderPicksFromBackpack, CoopConfig.RiderPicksFromBackpack);
        Toggle(page, () => Strings.RuleAllowOddPlayerCount, () => Strings.AboutAllowOddPlayerCount, CoopConfig.AllowOddPlayerCount);
    }

    /// <summary>Says why a Pair change the Troop Leader ordered did not start.</summary>
    internal static void Order(bool started)
    {
        if (!started)
        {
            CoopNotice.Info(Strings.MenuBusy);
        }
    }

    /// <summary>
    /// A Run Rule as a switch. It writes the Troop Leader's own setting, which
    /// is saved and published to the room as any change in the config editor is.
    /// </summary>
    private static void Toggle(CoopMenuPage page, Func<string> label, Func<string> about, BepInEx.Configuration.ConfigEntry<bool> rule) =>
        page.AddAction(label, () => rule.Value ? Strings.On : Strings.Off, () => rule.Value = !rule.Value, about: about);

    private static void Assign()
    {
        if (!PairShuffle.CanAssign(assignCarrier, assignRider))
        {
            CoopNotice.Info(Strings.MenuAssignUnavailable);
            return;
        }

        Order(PairShuffle.Request(OperationKind.PairAssignment, assignCarrier, assignRider));
    }

    /// <summary>Players a Pair Assignment can name: in the room with this version.</summary>
    private static List<int> Pickable() =>
        PhotonNetwork.PlayerList
            .Where(p => !p.IsInactive && Net.ModPresence.VersionOf(p) == MyPluginInfo.PLUGIN_VERSION)
            .Select(p => p.ActorNumber)
            .OrderBy(a => a)
            .ToList();

    private static int NextPick(int current)
    {
        List<int> actors = Pickable();
        if (actors.Count == 0)
        {
            return -1;
        }

        int next = actors.FirstOrDefault(a => a > current);
        return next == 0 ? actors[0] : next;
    }

    private static string PickName(int actor) =>
        actor >= 0 && Pickable().Contains(actor) ? PlayerRows.NameOf(actor) : "-";

    private static void NextInterval()
    {
        int now = ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value);
        int next = Intervals.FirstOrDefault(m => m > now);
        CoopConfig.TimedShuffleInterval.Value = next == 0 ? Intervals[0] : next;
    }
}

/// <summary>Hands the mouse wheel over the Coop Menu's list to the page.</summary>
internal sealed class WheelCatcher : MonoBehaviour, IScrollHandler
{
    public Action<PointerEventData>? Wheel { get; set; }

    public void OnScroll(PointerEventData eventData)
    {
        try
        {
            Wheel?.Invoke(eventData);
        }
        catch (Exception exception)
        {
            Guard.Report("WheelCatcher.OnScroll", exception);
        }
    }
}

/// <summary>
/// Leaves the Coop Menu: the pause menu comes back at once while the paper
/// drops away over it, so the menu's state never waits on the motion.
/// </summary>
internal sealed class SinkTransistion : PageTransistion
{
    public override void Transistion(PageBase oldSubPage, PageBase newSubPage)
    {
        oldSubPage.OnPageExit();
        if (oldSubPage is CoopMenuPage menu)
        {
            menu.SinkAway();
        }
        else
        {
            oldSubPage.gameObject.SetActive(false);
        }

        newSubPage.gameObject.SetActive(true);
        newSubPage.OnPageEnter();
        newSubPage.OnPageEntered();
    }
}
