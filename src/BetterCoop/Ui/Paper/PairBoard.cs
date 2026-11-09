using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// The Pair Assignment board, laid out as a yearbook's class page: the
/// scouts' passport photos in even rows across the paper, each with the name
/// printed under it. The two of a Pair stand side by side, the Carrier on
/// the left, under the Pair's number; the Unpaired Players follow apart.
/// The Troop Leader moves photos about a draft: held down and dragged, or
/// pressed to pick up, so the photo follows the pointer, and pressed again
/// where it goes. A photo dropped on another trades places with it. A right
/// click or Back puts a held photo back. On a gamepad, A picks up and drops,
/// the directions choose where, and B puts it back. Everyone else sees the
/// room's Pairs the same way.
/// </summary>
internal sealed class PairBoard : MonoBehaviour
{
    public const float PhotoWidth = ScoutPhoto.Width;

    public const float PhotoHeight = ScoutPhoto.PhotoHeight;

    private const float CaptionHeight = ScoutPhoto.CaptionHeight;

    private const float CellHeight = PhotoHeight + CaptionHeight;

    private const float PairLabelHeight = 32f;

    private const float PartnerGap = 8f;

    private const float PairWidth = 2f * PhotoWidth + PartnerGap;

    private const int PairsPerRow = 4;

    private const int UnpairedPerRow = 8;

    private readonly List<SlotView> slots = new();

    private PairDraft draft = null!;

    private IPortraits portraits = null!;

    private Func<int, string> nameOf = null!;

    private Action<int> moved = null!;

    private TMP_Text template = null!;

    private bool editable;

    private Slot? held;

    private RectTransform? ghost;

    private SlotView? marked;

    /// <summary>The photo just dropped, and where it was let go, so it settles from the hand rather than from its old place.</summary>
    private (int Actor, Vector3 At)? released;

    /// <summary>
    /// Lays the board out under <paramref name="root"/>. Each move changes
    /// <paramref name="draft"/> and calls <paramref name="moved"/> with the
    /// player moved, to draw the board again around them.
    /// </summary>
    public static PairBoard Build(
        RectTransform root,
        TMP_Text template,
        PairDraft draft,
        IPortraits portraits,
        Func<int, string> nameOf,
        bool editable,
        Action<int> moved)
    {
        PairBoard board = root.gameObject.AddComponent<PairBoard>();
        board.template = template;
        board.draft = draft;
        board.portraits = portraits;
        board.nameOf = nameOf;
        board.editable = editable;
        board.moved = moved;
        board.LayOut(root);
        return board;
    }

    /// <summary>
    /// Lays the board out again after its draft changed, each photo gliding
    /// from where it was, without touching the rest of the form, whose marks
    /// would otherwise all be drawn afresh.
    /// </summary>
    public void Redraw()
    {
        IReadOnlyDictionary<int, Vector3> before = Places();
        released = null;
        Drop();
        slots.Clear();
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        var root = (RectTransform)transform;
        LayOut(root);

        // The board may have grown or shrunk: the form around it is laid out again at once, so the photos glide from true places.
        RectTransform outer = root;
        while (outer.parent is RectTransform parent && parent.GetComponent<LayoutGroup>() != null)
        {
            outer = parent;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(outer);
        VectorView.DrawAll(root);
        GlideFrom(before);
    }

    /// <summary>The first photo a gamepad selects.</summary>
    public Selectable? First => slots.Select(s => s.Control).FirstOrDefault(c => c != null);

    /// <summary>Puts a held photo back; false when none is held.</summary>
    public bool PutBack()
    {
        if (held == null)
        {
            return false;
        }

        Drop();
        return true;
    }

    /// <summary>Selects the photo of this player, as after moving it with a gamepad.</summary>
    public void Select(int actor)
    {
        if (draft.Find(actor) is Slot slot
            && slots.FirstOrDefault(s => s.Slot == slot)?.Control is PaperControl control
            && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
    }

    internal void Press(SlotView view)
    {
        if (held is not Slot from)
        {
            if (draft.At(view.Slot) != null)
            {
                PickUp(view);
            }

            return;
        }

        if (from == view.Slot)
        {
            Drop();
            return;
        }

        int actor = draft.At(from)!.Value;
        if (ghost != null)
        {
            released = (actor, ghost.position);
        }

        Drop();
        if (draft.Move(from, view.Slot))
        {
            Plugin.Verbose($"Moved {actor} on the Pair Assignment board to {view.Slot}.");
            moved(actor);
        }
    }

    /// <summary>Where each scout's photo is now, by actor, in world space.</summary>
    public IReadOnlyDictionary<int, Vector3> Places()
    {
        var places = new Dictionary<int, Vector3>();
        foreach (SlotView view in slots)
        {
            if (draft.At(view.Slot) is int actor && view.Content != null)
            {
                places[actor] = view.Content.transform.position;
            }
        }

        if (released is (int dropped, Vector3 at))
        {
            places[dropped] = at;
        }

        return places;
    }

    /// <summary>Moves each photo from where it was on the board before; a scout new to the board fades in.</summary>
    public void GlideFrom(IReadOnlyDictionary<int, Vector3> before)
    {
        foreach (SlotView view in slots)
        {
            if (draft.At(view.Slot) is int actor)
            {
                Glide.Start((RectTransform)view.Content.transform, before.TryGetValue(actor, out Vector3 at) ? at : null);
            }
        }
    }

    internal void BeginDrag(SlotView view)
    {
        if (held == null && draft.At(view.Slot) != null)
        {
            PickUp(view);
        }
    }

    internal void EndDrag(PointerEventData eventData)
    {
        if (held == null)
        {
            return;
        }

        GameObject? under = eventData.pointerCurrentRaycast.gameObject;
        SlotView? target = under != null ? slots.FirstOrDefault(s => under.transform.IsChildOf(s.Rect)) : null;
        if (target == null)
        {
            Drop();
            return;
        }

        Press(target);
    }

    private void PickUp(SlotView view)
    {
        held = view.Slot;
        view.Faded = true;

        // The photo lifts off the page as a print cut from it, with its own shadow.
        ghost = PaperList.NewRect("Held", transform);
        ghost.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = new Vector2(PhotoWidth + 16f, CellHeight + 12f);
        ghost.localRotation = Quaternion.Euler(0f, 0f, 3f);
        VectorView.Add(ghost, s => FormDrawing.Card(s.x, s.y, lifted: true), Color.clear);
        RectTransform inner = PaperList.NewRect("Print", ghost);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(6f, 8f);
        inner.offsetMax = new Vector2(-10f, -4f);
        Portrait(inner, draft.At(view.Slot)!.Value, view.Slot.Kind);
        CanvasGroup group = ghost.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        ghost.SetAsLastSibling();
        VectorView.DrawAll(ghost);
        Follow();
    }

    private void Drop()
    {
        if (held is Slot slot && slots.FirstOrDefault(s => s.Slot == slot) is SlotView origin)
        {
            origin.Faded = false;
        }

        held = null;
        Mark(null);
        if (ghost != null)
        {
            Destroy(ghost.gameObject);
            ghost = null;
        }
    }

    private void LateUpdate()
    {
        try
        {
            if (held != null)
            {
                Follow();
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PairBoard.LateUpdate", exception);
        }
    }

    private void OnDisable()
    {
        if (held != null)
        {
            Drop();
        }
    }

    /// <summary>The held photo hangs over the chosen place on a gamepad, else under the pointer.</summary>
    private void Follow()
    {
        if (ghost == null)
        {
            return;
        }

        var root = (RectTransform)transform;
        SlotView? chosen = null;
        GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (InputHandler.GetCurrentUsedInputScheme() == InputScheme.Gamepad && selected != null)
        {
            chosen = slots.FirstOrDefault(s => s.Control != null && s.Control.gameObject == selected);
        }

        if (chosen != null)
        {
            Vector3 centre = chosen.Rect.TransformPoint(chosen.Rect.rect.center);
            ghost.localPosition = (Vector2)root.InverseTransformPoint(centre) + new Vector2(18f, -18f);
        }
        else if (UnityEngine.InputSystem.Mouse.current is { } mouse)
        {
            Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
            Camera? eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = mouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, eye, out Vector2 local))
            {
                ghost.localPosition = local;
            }

            chosen = slots.FirstOrDefault(s => RectTransformUtility.RectangleContainsScreenPoint(s.Rect, screen, eye));
        }

        Mark(chosen);
    }

    private void Mark(SlotView? view)
    {
        if (marked == view)
        {
            return;
        }

        if (marked != null)
        {
            marked.Marked = false;
        }

        marked = view;
        if (marked != null)
        {
            marked.Marked = true;
        }
    }

    private void LayOut(RectTransform root)
    {
        GridLayoutGroup pairs = Grid(root, "Pairs", new Vector2(PairWidth, PairLabelHeight + CellHeight), PairsPerRow, new Vector2(22f, 12f));
        int number = 0;
        for (int i = 0; i < draft.Rows.Count; i++)
        {
            (int? carrier, int? rider) = draft.Rows[i];
            bool empty = carrier == null && rider == null;
            if (empty && !editable)
            {
                continue;
            }

            RectTransform pair = PaperList.NewRect("Pair", pairs.transform);
            RectTransform label = PaperList.NewRect("Number", pair);
            label.anchorMin = new Vector2(0f, 1f);
            label.anchorMax = Vector2.one;
            label.pivot = new Vector2(0.5f, 1f);
            label.sizeDelta = new Vector2(0f, PairLabelHeight);
            TMP_Text text = Text(label, empty ? Strings.MenuNewPair : Strings.MenuPairNumber(++number), PaperText.Value, TextAlignmentOptions.Top);
            text.fontStyle = FontStyles.Bold;
            text.color = empty ? PaperList.Faded(PaperText.Blue, 0.5f) : PaperText.Blue;
            AddSlot(Cell(pair, 0f), new Slot(SlotKind.Carrier, i));
            AddSlot(Cell(pair, PhotoWidth + PartnerGap), new Slot(SlotKind.Rider, i));

            // Drawn over the photos, so one that grows as it is chosen never hides it.
            label.SetAsLastSibling();
        }

        if (draft.Unpaired.Count == 0 && !editable)
        {
            return;
        }

        RectTransform title = PaperList.NewRect("UnpairedTitle", root);
        LayoutElement titleSize = title.gameObject.AddComponent<LayoutElement>();
        titleSize.minHeight = titleSize.preferredHeight = 44f;
        Text(title, Strings.MenuUnpaired, PaperText.Value, TextAlignmentOptions.Bottom).fontStyle = FontStyles.Bold;

        GridLayoutGroup unpaired = Grid(root, "Unpaired", new Vector2(PhotoWidth, CellHeight), UnpairedPerRow, new Vector2(10f, 12f));
        int count = draft.Unpaired.Count + (editable ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            AddSlot(PaperList.NewRect("Cell", unpaired.transform), new Slot(SlotKind.Unpaired, i));
        }
    }

    /// <summary>One photo's place in a Pair: under the number, left or right.</summary>
    private static RectTransform Cell(RectTransform pair, float x)
    {
        RectTransform cell = PaperList.NewRect("Cell", pair);
        cell.anchorMin = cell.anchorMax = new Vector2(0f, 1f);
        cell.pivot = new Vector2(0f, 1f);
        cell.sizeDelta = new Vector2(PhotoWidth, CellHeight);
        cell.anchoredPosition = new Vector2(x, -PairLabelHeight);
        return cell;
    }

    private static GridLayoutGroup Grid(Transform root, string name, Vector2 cell, int columns, Vector2 spacing)
    {
        RectTransform rect = PaperList.NewRect(name, root);
        GridLayoutGroup grid = rect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cell;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        return grid;
    }

    private void AddSlot(RectTransform rect, Slot slot)
    {
        var view = new SlotView(slot, rect);
        int? actor = draft.At(slot);
        RectTransform content = PaperList.NewRect("Content", rect);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;
        view.Content = content.gameObject.AddComponent<CanvasGroup>();
        if (actor is int who)
        {
            Portrait(content, who, slot.Kind);
        }
        else
        {
            RectTransform place = ScoutPhoto.PhotoArea(content);
            VectorView.Add(place, s => FormDrawing.Slot(s.x, s.y, marked: false), Color.clear);
            TMP_Text empty = Text(ScoutPhoto.Caption(content), slot.Kind == SlotKind.Unpaired ? Strings.MenuDropHere : Strings.MenuEmptyPlace, PaperText.Note, TextAlignmentOptions.Top);
            empty.color = PaperList.Faded(PaperText.Ink, 0.45f);
        }

        // Over the photo, where a held one would land.
        RectTransform mark = ScoutPhoto.PhotoArea(rect);
        view.Mark = VectorView.Add(mark, s => FormDrawing.Slot(s.x, s.y, marked: true), Color.clear).GetComponent<RawImage>();
        view.Mark.enabled = false;

        if (editable)
        {
            // The whole place takes the pointer, photo and name alike.
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            SlotControl control = PaperControl.Add<SlotControl>(rect.gameObject);
            control.Board = this;
            control.View = view;
            control.Visual = content;
            control.Pressed = () => Press(view);
            control.Back = () => PutBack();
            view.Control = control;
        }

        slots.Add(view);
    }

    /// <summary>A scout as the yearbook prints them: their photo, and their name and role under it.</summary>
    private void Portrait(RectTransform parent, int actor, SlotKind kind) =>
        ScoutPhoto.Add(parent, template, portraits, actor, nameOf(actor), kind == SlotKind.Unpaired ? null : Strings.RoleOf(kind == SlotKind.Carrier));

    private TMP_Text Text(RectTransform rect, string value, float size, TextAlignmentOptions alignment)
    {
        RectTransform holder = PaperList.NewRect("Text", rect);
        holder.anchorMin = Vector2.zero;
        holder.anchorMax = Vector2.one;
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        TMP_Text text = PaperText.Make(holder.gameObject, template, size, alignment);
        text.text = value;
        return text;
    }
}

/// <summary>One place on the board, and the photo in it.</summary>
internal sealed class SlotView
{
    private bool marked;

    public SlotView(Slot slot, RectTransform rect)
    {
        Slot = slot;
        Rect = rect;
    }

    public Slot Slot { get; }

    public RectTransform Rect { get; }

    public CanvasGroup Content { get; set; } = null!;

    public RawImage Mark { get; set; } = null!;

    public PaperControl? Control { get; set; }

    /// <summary>Whether a held photo would land here.</summary>
    public bool Marked
    {
        get => marked;
        set
        {
            marked = value;
            Mark.enabled = value;
        }
    }

    /// <summary>Whether the photo has been picked up from here.</summary>
    public bool Faded
    {
        set => Content.alpha = value ? 0.3f : 1f;
    }
}

/// <summary>A place on the board a player can press, and drag a photo from.</summary>
internal sealed class SlotControl : PaperControl, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PairBoard Board { get; set; } = null!;

    public SlotView View { get; set; } = null!;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
        {
            Board.BeginDrag(View);
        }
    }

    // Needed for the drag to begin; the held photo follows the pointer by itself.
    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Board.EndDrag(eventData);
        }
    }
}
