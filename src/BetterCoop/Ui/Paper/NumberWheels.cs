using System;
using BetterCoop.Core;
using TMPro;
using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Figures set on a numbering machine's wheels (see <see cref="NumberingMachine"/>):
/// when they change, the wheels whose figure differs turn on to it, and the
/// machine clunks as the last comes to rest. <see cref="Roll"/> sets them
/// again from noughts, as when a form is numbered.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
internal sealed class NumberWheels : MonoBehaviour
{
    private static readonly char[] Figures = "0123456789".ToCharArray();

    private TMP_Text text = null!;

    private string from = string.Empty;

    private string to = string.Empty;

    private bool fullTurn;

    private float setAt = float.NegativeInfinity;

    private float restsIn;

    private bool resting = true;

    private bool rollNext;

    private float rollDelay;

    private string open = string.Empty;

    /// <summary>Sets the figures on <paramref name="text"/>'s wheels.</summary>
    /// <param name="advance">How far apart the wheels stand, in em, so a turning figure never pushes the others about.</param>
    public static NumberWheels Add(TMP_Text text, float advance)
    {
        NumberWheels wheels = text.gameObject.AddComponent<NumberWheels>();
        wheels.text = text;
        wheels.open = FormattableString.Invariant($"<mspace={advance}em>");
        return wheels;
    }

    /// <summary>Sets the wheels to <paramref name="figures"/>; the first figures stand at once, unless a <see cref="Roll"/> is due.</summary>
    public void Set(string figures)
    {
        if (figures == to)
        {
            return;
        }

        bool first = to.Length == 0 && !rollNext;
        Turn(rollNext ? NumberingMachine.Noughts(figures) : first ? figures : Now(), figures, rollNext, rollNext ? rollDelay : 0f);
        rollNext = false;
    }

    /// <summary>Turns the wheels from noughts to their figures again, <paramref name="delay"/> seconds from now or from when they are set.</summary>
    public void Roll(float delay = 0f)
    {
        if (to.Length == 0)
        {
            rollNext = true;
            rollDelay = delay;
            return;
        }

        Turn(NumberingMachine.Noughts(to), to, full: true, delay);
    }

    private void Turn(string start, string figures, bool full, float delay = 0f)
    {
        from = start;
        to = figures;
        fullTurn = full;
        setAt = Time.unscaledTime + delay;
        restsIn = NumberingMachine.Seconds(from, to, fullTurn);
        resting = restsIn <= 0f;
        Show();
    }

    private string Now() => NumberingMachine.Shown(from, to, Time.unscaledTime - setAt, fullTurn);

    private void LateUpdate()
    {
        if (resting)
        {
            return;
        }

        Show();
        if (Time.unscaledTime - setAt >= restsIn)
        {
            resting = true;
            if (text.isActiveAndEnabled)
            {
                PaperSounds.Play(PaperSounds.Kind.Clunk);
            }
        }
    }

    /// <summary>Shows the figures, the type before the first of them set as it is and the wheels evenly apart.</summary>
    private void Show()
    {
        string now = Now();
        int first = now.IndexOfAny(Figures);
        PaperText.Set(text, first < 0 ? now : now.Substring(0, first) + open + now.Substring(first));
    }
}
