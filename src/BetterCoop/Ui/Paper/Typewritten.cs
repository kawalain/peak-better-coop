using BetterCoop.Core;
using TMPro;
using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Text typed onto the form: each letter struck a little off the line, with
/// its own weight of ribbon ink (see <see cref="Typewriter"/>), and whenever
/// the words change, typed in again a letter at a time to the clack of the
/// keys. The letters are moved as the text is drawn, so they keep their
/// places however often it is drawn again.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
internal sealed class Typewritten : MonoBehaviour
{
    /// <summary>The least time between two key clacks from all the text on the paper.</summary>
    private const float ClackSeconds = 0.06f;

    private static float lastClack = float.NegativeInfinity;

    private TMP_Text text = null!;

    private string typed = string.Empty;

    private float startedAt = float.NegativeInfinity;

    private int shown = int.MaxValue;

    private bool first = true;

    /// <summary>The longest its words take to type, however many there are.</summary>
    public float Longest { get; set; } = Typewriter.LineSeconds;

    /// <summary>How long after its words first show they start being typed; negative shows them at once.</summary>
    public float FirstDelay { get; set; } = -1f;

    /// <summary>Types <paramref name="text"/> as a typewriter would.</summary>
    /// <param name="typeIn">Whether its first words are typed in, after <paramref name="delay"/>, rather than already on the paper.</param>
    public static Typewritten Add(TMP_Text text, bool typeIn = false, float delay = 0f)
    {
        Typewritten typewritten = text.gameObject.AddComponent<Typewritten>();
        typewritten.FirstDelay = typeIn ? delay : -1f;
        return typewritten;
    }

    /// <summary>Types its words in again from the first letter, as on a line put back on the form.</summary>
    public void TypeAgain()
    {
        typed = string.Empty;
        shown = 0;
    }

    private void Awake() => text = GetComponent<TMP_Text>();

    private void OnEnable() => text.OnPreRenderText += Strike;

    private void OnDisable() => text.OnPreRenderText -= Strike;

    private void LateUpdate()
    {
        Follow();
        int letters = text.textInfo?.characterCount ?? typed.Length;
        int typing = startedAt == float.NegativeInfinity ? int.MaxValue : Typewriter.Typed(Time.unscaledTime - startedAt, letters, Longest);
        if (typing != shown)
        {
            if (typing > shown && typing <= letters && Time.unscaledTime - lastClack >= ClackSeconds)
            {
                lastClack = Time.unscaledTime;
                PaperSounds.Play(PaperSounds.Kind.Key);
            }

            shown = typing;
            text.ForceMeshUpdate();
        }
    }

    /// <summary>Starts typing the words anew when they have changed.</summary>
    private void Follow()
    {
        string now = text.text ?? string.Empty;
        if (now == typed)
        {
            return;
        }

        // A clock or count ticking on shows its new figures at once, without the keys.
        bool ticking = !first && Typewriter.Ticks(typed, now);
        typed = now;
        if (ticking)
        {
            startedAt = float.NegativeInfinity;
            shown = int.MaxValue;
            return;
        }

        bool atOnce = first && FirstDelay < 0f;
        startedAt = atOnce ? float.NegativeInfinity : Time.unscaledTime + (first ? FirstDelay : 0f);
        first = false;
        // Until the keys strike, nothing new shows, even if the text is drawn before this frame's typing.
        shown = atOnce ? int.MaxValue : 0;
    }

    /// <summary>Strikes each letter off the line with its own ink, and hides those not typed yet.</summary>
    private void Strike(TMP_TextInfo info)
    {
        Follow();
        float size = text.fontSize;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo letter = info.characterInfo[i];
            if (!letter.isVisible)
            {
                continue;
            }

            (float rise, float shift, float lean, float ink) = Typewriter.Strike(i, letter.character);
            int at = letter.vertexIndex;
            Vector3[] corners = info.meshInfo[letter.materialReferenceIndex].vertices;
            Color32[] colours = info.meshInfo[letter.materialReferenceIndex].colors32;
            Vector3 middle = (corners[at] + corners[at + 2]) * 0.5f;
            Quaternion turn = Quaternion.Euler(0f, 0f, lean);
            var move = new Vector3(shift * size, rise * size, 0f);
            for (int corner = 0; corner < 4; corner++)
            {
                corners[at + corner] = middle + turn * (corners[at + corner] - middle) + move;
                Color32 colour = colours[at + corner];
                colour.a = i < shown ? (byte)(colour.a * ink) : (byte)0;
                colours[at + corner] = colour;
            }
        }
    }
}
