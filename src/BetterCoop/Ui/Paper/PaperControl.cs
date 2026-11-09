using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Something on the paper a player can press: a check box, an answer to
/// circle, a stamp, or a card. A left click or a gamepad's submit presses
/// it; a right click goes back, as to the previous answer; with a step,
/// left and right on a gamepad or keyboard step through answers instead of
/// moving the selection. Selected or hovered, it grows a little, as a pen
/// hovering over the form; pressed, it sinks. It plays the game's own menu
/// sounds when it can find them.
/// </summary>
internal class PaperControl : Selectable, IPointerClickHandler, ISubmitHandler
{
    private const float Lift = 1.08f;

    private const float Sink = 0.94f;

    private static NicksButtonSFX? sounds;

    private float scale = 1f;

    private float target = 1f;

    /// <summary>Counts every press, step, or back on any paper control, so a stamp asking "Really?" knows another was used.</summary>
    public static int Uses { get; private set; }

    /// <summary>A left click or submit.</summary>
    public Action? Pressed { get; set; }

    /// <summary>A right click.</summary>
    public Action? Back { get; set; }

    /// <summary>Left (-1) or right (+1) while selected; when unset, left and right move the selection.</summary>
    public Action<int>? Step { get; set; }

    /// <summary>What grows and sinks; the control itself when unset.</summary>
    public Transform? Visual { get; set; }

    /// <summary>Takes the menu sounds from the game's menu button, for every control.</summary>
    public static void UseSoundsOf(Button template)
    {
        if (sounds == null)
        {
            sounds = template.GetComponentInChildren<NicksButtonSFX>(true);
        }
    }

    public static T Add<T>(GameObject holder, Action? pressed = null)
        where T : PaperControl
    {
        T control = holder.AddComponent<T>();
        control.transition = Transition.None;
        control.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        control.Pressed = pressed;
        return control;
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Press();
        }
        else if (eventData.button == PointerEventData.InputButton.Right && Back != null)
        {
            Uses++;
            Play(click: true);
            Run(Back);
        }

        // A mouse click would leave the control looking selected; a gamepad keeps its selection to navigate.
        if (eventData.button != PointerEventData.InputButton.Middle && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public virtual void OnSubmit(BaseEventData eventData)
    {
        if (IsInteractable())
        {
            Press();
        }
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (Step != null && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
        {
            Uses++;
            Play(click: true);
            Run(() => Step(eventData.moveDir == MoveDirection.Left ? -1 : 1));
            eventData.Use();
            return;
        }

        base.OnMove(eventData);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        Play(click: false);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (IsInteractable())
        {
            Play(click: false);
        }
    }

    /// <summary>Gives a pressed look for a moment, as a stamp's thump; the control settles back by itself.</summary>
    public void Thump(float depth = 0.85f) => scale = depth;

    protected virtual void Press()
    {
        Uses++;
        Play(click: true);
        Thump(Sink);
        if (Pressed != null)
        {
            Run(Pressed);
        }
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        target = state switch
        {
            SelectionState.Highlighted or SelectionState.Selected => Lift,
            SelectionState.Pressed => Sink,
            _ => 1f,
        };
        if (instant)
        {
            scale = target;
            Apply();
        }
    }

    protected virtual void LateUpdate()
    {
        if (Mathf.Abs(scale - target) < 0.001f)
        {
            return;
        }

        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        Apply();
    }

    private void Apply() => (Visual ?? transform).localScale = Vector3.one * scale;

    private void Play(bool click)
    {
        try
        {
            if (sounds != null && SFX_Player.instance != null)
            {
                SFX_Player.instance.PlaySFX(click ? sounds.sfxClick : sounds.sfxHover, transform.position);
            }
        }
        catch (Exception exception)
        {
            Guard.Report("PaperControl.Sound", exception);
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Guard.Report(name, exception);
        }
    }
}
