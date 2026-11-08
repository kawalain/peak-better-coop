using System;
using System.Linq;
using BetterCoop.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Zorro.UI;
using Object = UnityEngine.Object;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// A pause menu page drawn as a sheet of paper in the middle of the screen:
/// the title above a perforation, a scrolling list of rows, and the game's
/// Back button on the stub below. It rises like a ticket from the kiosk when
/// it opens and drops away when it closes. A page fills its list and says
/// what its rows depend on; the list is filled again when that changes, and
/// its texts refresh every frame while it is open.
/// </summary>
internal abstract class PaperPage : UIPage, IHaveParentPage, INavigationPage
{
    public const float PanelWidth = 1200f;

    private const float Padding = 44f;

    private const float TitleHeight = 96f;

    private const float FooterHeight = 104f;

    /// <summary>Room between a perforation and the list, so rows never touch it.</summary>
    private const float ListGap = 10f;

    private const float WheelStep = 70f;

    private readonly Vector3[] corners = new Vector3[4];

    private readonly ScrollMotion scroll = new();

    private UIPageHandler handler = null!;

    private UIPage parent = null!;

    private Func<string> title = null!;

    private TMP_Text titleText = null!;

    private TMP_Text formText = null!;

    private TMP_Text noteText = null!;

    private TMP_Text serialText = null!;

    private RectTransform panel = null!;

    private CanvasGroup group = null!;

    private Motion motion;

    private float motionSeconds;

    private RectTransform viewport = null!;

    private RectTransform content = null!;

    private Button back = null!;

    private string shape = string.Empty;

    private bool filled;

    private GameObject? lastSelected;

    /// <summary>The question laid over the page before leaving it, while it is asked.</summary>
    private PaperQuestion? asking;

    private enum Motion
    {
        Resting,
        Rising,
        Sinking,
    }

    protected PaperList List { get; private set; } = null!;

    protected UIPageHandler Handler => handler;

    /// <summary>
    /// Builds a page from a copy of the pause menu's Controls page while it
    /// cannot wake: under an inactive holder, so the copy's Controls page
    /// never runs or registers, and is removed before it could. Only its
    /// Back button is kept, which returns to <paramref name="parent"/>.
    /// </summary>
    public static T Create<T>(UIPageHandler handler, PauseMenuControlsPage controls, Button buttonTemplate, UIPage parent, Func<string> title)
        where T : PaperPage
    {
        var holder = new GameObject("BetterCoop.PageHolder");
        holder.SetActive(false);
        GameObject clone = Object.Instantiate(controls.gameObject, holder.transform);
        clone.name = "BetterCoop." + typeof(T).Name;

        PauseMenuControlsPage copy = clone.GetComponent<PauseMenuControlsPage>();
        Button back = copy.backButton;
        Object.DestroyImmediate(copy);
        foreach (PauseMenuRebindButton rebind in clone.GetComponentsInChildren<PauseMenuRebindButton>(true))
        {
            Object.DestroyImmediate(rebind.gameObject);
        }

        T page = clone.AddComponent<T>();
        page.handler = handler;
        page.parent = parent;
        page.title = title;
        page.back = back;
        page.LayOut((RectTransform)clone.transform, buttonTemplate);
        back.onClick = new Button.ButtonClickedEvent();
        back.onClick.AddListener(page.GoBack);

        clone.transform.SetParent(controls.transform.parent, false);
        clone.SetActive(false);
        clone.transform.SetSiblingIndex(controls.transform.GetSiblingIndex() + 1);
        Object.Destroy(holder);
        return page;
    }

    public (UIPage, PageTransistion) GetParentPage() => (parent, new SinkTransistion());

    public GameObject GetFirstSelectedGameObject() =>
        List.FirstSelectable()?.gameObject ?? back.gameObject;

    /// <summary>Gives Back to the form first, as to put a held card back; false to leave the page.</summary>
    internal bool TakeBack()
    {
        if (asking != null && asking.Open)
        {
            asking.Stay();
            return true;
        }

        if (List.Back())
        {
            return true;
        }

        if (HasDraft)
        {
            AskToLeave();
            return true;
        }

        return false;
    }

    /// <summary>Opens this page from the current one, which gives way at once while the paper rises.</summary>
    public void Open()
    {
        try
        {
            handler.TransistionToPage(this, new SetActivePageTransistion());
        }
        catch (Exception exception)
        {
            Guard.Report(name + ".Open", exception);
        }
    }

    public override void OnPageEnter()
    {
        base.OnPageEnter();
        scroll.Reset();
        lastSelected = null;
        if (asking != null)
        {
            Destroy(asking.gameObject);
            asking = null;
        }
        if (!filled)
        {
            Refill();
        }

        // The paper and its rules show drawn from the first frame.
        VectorView.DrawAll(panel);
        Rise();
    }

    /// <summary>Plays the rise again from below.</summary>
    public void Rise()
    {
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

    /// <summary>What the rows depend on; when it changes, the list is filled again.</summary>
    protected abstract string Shape();

    protected abstract void Fill(PaperList list);

    /// <summary>Runs before the list is filled again, to note where things were.</summary>
    protected virtual void BeforeRefill()
    {
    }

    /// <summary>Runs once the list is filled again and laid out, to carry things over from where they were.</summary>
    protected virtual void AfterRefill()
    {
    }

    /// <summary>Whether the page holds work not yet sent, which leaving would throw away.</summary>
    protected virtual bool HasDraft => false;

    /// <summary>Throws the page's unsent work away, as the player leaves without sending it.</summary>
    protected virtual void DropDraft()
    {
    }

    /// <summary>What form the paper is, printed small in its top left corner.</summary>
    protected virtual string FormNumber() => string.Empty;

    /// <summary>A note printed small in the paper's top right corner, such as how many it concerns.</summary>
    protected virtual string FormNote() => string.Empty;

    /// <summary>The paper's serial number, printed in red under the note, as on a membership card.</summary>
    protected virtual string FormSerial() => string.Empty;

    /// <summary>Runs every frame while the page is open, before its rows refresh.</summary>
    protected virtual void Tick()
    {
    }

    /// <summary>Fills the list anew now, as when what the rows show has been replaced.</summary>
    protected void Refill()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        shape = Shape();
        BeforeRefill();
        filled = true;
        List.Clear();
        Fill(List);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        // New rows' drawings show at once, not their fallback colour for a frame.
        VectorView.DrawAll(content);
        AfterRefill();
        Plugin.Verbose($"Filled {name} in {clock.Elapsed.TotalMilliseconds:F1} ms.");
    }

    private void GoBack()
    {
        if (asking != null && asking.Open)
        {
            return;
        }

        if (HasDraft)
        {
            AskToLeave();
            return;
        }

        Leave();
    }

    /// <summary>Asks on a slip of paper whether to throw the draft away before leaving.</summary>
    private void AskToLeave()
    {
        try
        {
            asking = PaperQuestion.Ask(
                panel,
                List.Template,
                Strings.DiscardDraft,
                Strings.DiscardDraftNote,
                Strings.StampDiscard,
                Strings.StampKeep,
                () =>
                {
                    DropDraft();
                    Leave();
                });
        }
        catch (Exception exception)
        {
            Guard.Report(name + ".AskToLeave", exception);
        }
    }

    private void Leave()
    {
        try
        {
            handler.TransistionToPage(parent, new SinkTransistion());
        }
        catch (Exception exception)
        {
            Guard.Report(name + ".Leave", exception);
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

            Tick();
            if (!filled || Shape() != shape)
            {
                Refill();
            }

            PaperText.Set(titleText, title());
            PaperText.Set(formText, FormNumber());
            PaperText.Set(noteText, FormNote());
            PaperText.Set(serialText, FormSerial());
            List.Refresh();
            KeepSelectionInView();
            scroll.Max = content.rect.height - viewport.rect.height;
            scroll.Step(Time.unscaledDeltaTime);
            content.anchoredPosition = new Vector2(0f, scroll.Position);
        }
        catch (Exception exception)
        {
            Guard.Report(name + ".LateUpdate", exception);
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
    private void LayOut(RectTransform root, Button buttonTemplate)
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
        VectorView paper = VectorView.Add(
            sheet,
            size => PaperDrawing.Svg(size.x - 2f * PaperDrawing.Margin, size.y - 2f * PaperDrawing.Margin, TitleHeight, FooterHeight),
            PaperText.Paper);
        // The paper takes the clicks that miss the rows, so nothing behind it answers.
        paper.GetComponent<RawImage>().raycastTarget = true;

        TMP_Text template = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        RectTransform titleRect = NewRect("Title", panel);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, TitleHeight);
        titleRect.anchoredPosition = Vector2.zero;
        titleText = PaperText.Make(titleRect.gameObject, template, PaperText.Title, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = PaperText.Blue;

        // The form's number and note stay printed in the head of the paper, above everything that scrolls.
        formText = Corner(titleRect, template, TextAlignmentOptions.TopLeft);
        noteText = Corner(titleRect, template, TextAlignmentOptions.TopRight);
        serialText = Corner(titleRect, template, TextAlignmentOptions.TopRight);
        serialText.rectTransform.offsetMax = new Vector2(-Padding, -40f);
        serialText.color = PaperList.StampRed;
        serialText.fontStyle = FontStyles.Bold;
        serialText.characterSpacing = 4f;

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
        List = new PaperList(content, buttonTemplate);

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

    private static TMP_Text Corner(RectTransform head, TMP_Text template, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect("Corner", head);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Padding, 0f);
        rect.offsetMax = new Vector2(-Padding, -14f);
        TMP_Text text = PaperText.Make(rect.gameObject, template, PaperText.Note, alignment);
        text.color = new Color(PaperText.Ink.r, PaperText.Ink.g, PaperText.Ink.b, 0.7f);
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return (RectTransform)holder.transform;
    }
}

/// <summary>Back on a paper page goes to its form first, so a held card goes back instead of the page closing.</summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class PaperBack
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PauseMenuHandler), "Update")]
    private static bool GiveBackToForm(PauseMenuHandler __instance)
    {
        if (__instance.currentPage is not PaperPage page || Character.localCharacter is not Character local)
        {
            return true;
        }

        bool back = local.input.pauseWasPressed || (__instance.backButton?.action?.WasPressedThisFrame() ?? false);
        if (!back || !page.TakeBack())
        {
            return true;
        }

        local.input.pauseWasPressed = false;
        return false;
    }
}

/// <summary>Hands the mouse wheel over a paper page's list to the page.</summary>
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
/// Leaves a paper page: the page it returns to comes back at once while the
/// paper drops away over it, so the menu's state never waits on the motion.
/// </summary>
internal sealed class SinkTransistion : PageTransistion
{
    public override void Transistion(PageBase oldSubPage, PageBase newSubPage)
    {
        oldSubPage.OnPageExit();
        if (oldSubPage is PaperPage paper)
        {
            paper.SinkAway();
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
