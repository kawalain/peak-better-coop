using TMPro;

namespace BetterCoop.Ui.Paper;

/// <summary>Words on the paper to press, as a link: underlined while pointed at or selected.</summary>
internal sealed class LinkControl : PaperControl
{
    private TMP_Text? text;

    /// <summary>The words underlined while the link is pointed at.</summary>
    public TMP_Text? Text
    {
        get => text;
        set
        {
            text = value;
            Underline(false);
        }
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        Underline(state is SelectionState.Highlighted or SelectionState.Selected);
    }

    private void Underline(bool on)
    {
        if (text != null)
        {
            text.fontStyle = on ? text.fontStyle | FontStyles.Underline : text.fontStyle & ~FontStyles.Underline;
        }
    }
}
