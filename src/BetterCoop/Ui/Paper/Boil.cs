using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Hand-drawn lines boil, as in a hand-drawn cartoon: a doodle is drawn a
/// few times over, and each drawing shows in turn whenever the paper's
/// noise moves on, so the lines wobble in step with the grain.
/// </summary>
internal static class Boil
{
    /// <summary>How many drawings of each doodle take turns.</summary>
    public const int Frames = 4;

    /// <summary>Which drawing shows now. The game's window shader moves its noise at round(time × WiggleTime).</summary>
    public static int Frame => (int)((long)Mathf.Round(Time.timeSinceLevelLoad * PaperSheet.WiggleTime) % Frames);
}
