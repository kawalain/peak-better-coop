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
/// The Pair Assignment board: a row per Pair with the Carrier's card and the
/// Rider's card side by side, and the Unpaired Players' cards apart. Each
/// card shows the scout's passport photo and name. The Troop Leader moves
/// cards about a draft: held down and dragged, or pressed to pick up, so the
/// card follows the pointer, and pressed again where it goes. A card dropped
/// on another trades places with it. A right click or Back puts a held card
/// back. On a gamepad, A picks up and drops, the directions choose where,
/// and B puts it back. Everyone else sees the room's Pairs the same way.
/// </summary>
internal sealed class PairBoard : MonoBehaviour
{
    public const float CardWidth = 300f;

    public const float CardHeight = 104f;

    private const float LabelWidth = 96f;

    private const float LinkWidth = 44f;

    private const float PhotoWidth = 68f;

    private const float PhotoHeight = 84f;

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

    /// <summary>The first card a gamepad selects.</summary>
    public Selectable? First => slots.Select(s => s.Control).FirstOrDefault(c => c != null);

    /// <summary>Puts a held card back; false when no card is held.</summary>
    public bool PutBack()
    {
        if (held == null)
        {
            return false;
        }

        Drop();
        return true;
    }

    /// <summary>Selects the card of this player, as after moving it with a gamepad.</summary>
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
        Drop();
        if (draft.Move(from, view.Slot))
        {
            Plugin.Verbose($"Moved {actor} on the Pair Assignment board to {view.Slot}.");
            moved(actor);
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
        view.CardAlpha = 0.3f;
        ghost = Card(transform, draft.At(view.Slot)!.Value, lifted: true);
        ghost.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = new Vector2(CardWidth, CardHeight);
        ghost.localRotation = Quaternion.Euler(0f, 0f, 3f);
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
            origin.CardAlpha = 1f;
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

    /// <summary>The held card hangs over the chosen place on a gamepad, else under the pointer.</summary>
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
            ghost.localPosition = (Vector2)root.InverseTransformPoint(centre) + new Vector2(16f, -16f);
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
        HorizontalLayoutGroup heading = Line(root);
        Fixed(heading.transform, "Gap", LabelWidth, 30f);
        Text(Fixed(heading.transform, "Carrier", CardWidth, 30f), Strings.RoleOf(true), PaperText.Value, TextAlignmentOptions.Bottom).fontStyle = FontStyles.Bold;
        Fixed(heading.transform, "Link", LinkWidth, 30f);
        Text(Fixed(heading.transform, "Rider", CardWidth, 30f), Strings.RoleOf(false), PaperText.Value, TextAlignmentOptions.Bottom).fontStyle = FontStyles.Bold;

        int number = 0;
        for (int i = 0; i < draft.Rows.Count; i++)
        {
            (int? carrier, int? rider) = draft.Rows[i];
            bool empty = carrier == null && rider == null;
            if (empty && !editable)
            {
                continue;
            }

            HorizontalLayoutGroup line = Line(root);
            string label = empty ? Strings.MenuNewPair : Strings.MenuPairNumber(++number);
            TMP_Text text = Text(Fixed(line.transform, "Number", LabelWidth, CardHeight), label, PaperText.Label, TextAlignmentOptions.Left);
            text.color = empty ? PaperList.Faded(PaperText.Ink, 0.55f) : PaperText.Ink;
            AddSlot(line.transform, new Slot(SlotKind.Carrier, i));
            Text(Fixed(line.transform, "Link", LinkWidth, CardHeight), "⇄", PaperText.Label, TextAlignmentOptions.Center).color = PaperText.Blue;
            AddSlot(line.transform, new Slot(SlotKind.Rider, i));
        }

        if (draft.Unpaired.Count == 0 && !editable)
        {
            return;
        }

        RectTransform title = Fixed(root, "UnpairedTitle", CardWidth, 44f);
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;
        Text(title, Strings.MenuUnpaired, PaperText.Label, TextAlignmentOptions.BottomLeft);

        RectTransform grid = PaperList.NewRect("Unpaired", root);
        GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.cellSize = new Vector2(CardWidth, CardHeight);
        cells.spacing = new Vector2(16f, 12f);
        cells.padding = new RectOffset((int)LabelWidth, 0, 0, 0);
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = 2;
        int count = draft.Unpaired.Count + (editable ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            AddSlot(grid, new Slot(SlotKind.Unpaired, i));
        }
    }

    private void AddSlot(Transform parent, Slot slot)
    {
        RectTransform rect = PaperList.NewRect("Slot", parent);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = CardWidth;
        size.minHeight = size.preferredHeight = CardHeight;
        var view = new SlotView(slot, rect);
        view.Outline = VectorView.Add(rect, s => FormDrawing.Slot(s.x, s.y, view.Marked), Color.clear);
        int? actor = draft.At(slot);
        if (actor is int who)
        {
            RectTransform card = Card(rect, who, lifted: false);
            view.CardGroup = card.gameObject.AddComponent<CanvasGroup>();
        }
        else if (slot.Kind == SlotKind.Unpaired)
        {
            Text(rect, Strings.MenuDropHere, PaperText.Note, TextAlignmentOptions.Center).color = PaperList.Faded(PaperText.Ink, 0.5f);
        }

        if (editable)
        {
            // The whole slot takes the pointer, card or not.
            view.Outline.GetComponent<RawImage>().raycastTarget = true;
            SlotControl control = PaperControl.Add<SlotControl>(rect.gameObject);
            control.Board = this;
            control.View = view;
            control.Pressed = () => Press(view);
            control.Back = () => PutBack();
            view.Control = control;
        }

        slots.Add(view);
    }

    /// <summary>A scout's card: their passport photo in its frame, and their name.</summary>
    private RectTransform Card(Transform parent, int actor, bool lifted)
    {
        RectTransform card = PaperList.NewRect("Card", parent);
        card.anchorMin = Vector2.zero;
        card.anchorMax = Vector2.one;
        card.offsetMin = card.offsetMax = Vector2.zero;
        VectorView.Add(card, s => FormDrawing.Card(s.x, s.y, lifted), Color.clear);

        RectTransform photo = PaperList.NewRect("Photo", card);
        photo.anchorMin = photo.anchorMax = new Vector2(0f, 0.5f);
        photo.pivot = new Vector2(0f, 0.5f);
        photo.sizeDelta = new Vector2(PhotoWidth, PhotoHeight);
        photo.anchoredPosition = new Vector2(12f, 1f);
        RectTransform picture = PaperList.NewRect("Picture", photo);
        picture.anchorMin = Vector2.zero;
        picture.anchorMax = Vector2.one;
        picture.offsetMin = new Vector2(4f, 4f);
        picture.offsetMax = new Vector2(-4f, -4f);
        Texture? taken = portraits.Photo(actor);
        if (taken != null)
        {
            RawImage image = picture.gameObject.AddComponent<RawImage>();
            image.texture = taken;
            image.raycastTarget = false;
        }
        else
        {
            Color skin = portraits.Skin(actor);
            var ink = new Ink(skin.r, skin.g, skin.b);
            VectorView.Add(picture, s => FormDrawing.Silhouette(s.x, s.y, ink), skin);
        }

        RectTransform frame = PaperList.NewRect("Frame", photo);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        VectorView.Add(frame, s => FormDrawing.PhotoFrame(s.x, s.y), Color.clear);

        RectTransform name = PaperList.NewRect("Name", card);
        name.anchorMin = Vector2.zero;
        name.anchorMax = Vector2.one;
        name.offsetMin = new Vector2(12f + PhotoWidth + 14f, 8f);
        name.offsetMax = new Vector2(-16f, -8f);
        Text(name, nameOf(actor), PaperText.Label, TextAlignmentOptions.Left);
        return card;
    }

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

    private static HorizontalLayoutGroup Line(Transform parent)
    {
        RectTransform line = PaperList.NewRect("Line", parent);
        HorizontalLayoutGroup layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;
        return layout;
    }

    private static RectTransform Fixed(Transform parent, string name, float width, float height)
    {
        RectTransform rect = PaperList.NewRect(name, parent);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        size.minHeight = size.preferredHeight = height;
        return rect;
    }
}

/// <summary>One place on the board, and the card in it.</summary>
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

    public VectorView Outline { get; set; } = null!;

    public CanvasGroup? CardGroup { get; set; }

    public PaperControl? Control { get; set; }

    /// <summary>Whether a held card would land here.</summary>
    public bool Marked
    {
        get => marked;
        set
        {
            if (marked != value)
            {
                marked = value;
                Outline.Redraw();
            }
        }
    }

    public float CardAlpha
    {
        set
        {
            if (CardGroup != null)
            {
                CardGroup.alpha = value;
            }
        }
    }
}

/// <summary>A place on the board a player can press, and drag a card from.</summary>
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

    // Needed for the drag to begin; the held card follows the pointer by itself.
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
