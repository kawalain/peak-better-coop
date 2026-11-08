using System;
using System.Linq;
using BetterCoop.Core;
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

    /// <summary>Runs every frame while the page is open, before its rows refresh.</summary>
    protected virtual void Tick()
    {
    }

    /// <summary>Fills the list anew now, as when what the rows show has been replaced.</summary>
    protected void Refill()
    {
        shape = Shape();
        filled = true;
        List.Clear();
        Fill(List);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        // New rows' drawings show at once, not their fallback colour for a frame.
        VectorView.DrawAll(content);
    }

    private void GoBack()
    {
        try
        {
            handler.TransistionToPage(parent, new SinkTransistion());
        }
        catch (Exception exception)
        {
            Guard.Report(name + ".GoBack", exception);
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

    private static RectTransform NewRect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return (RectTransform)holder.transform;
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
