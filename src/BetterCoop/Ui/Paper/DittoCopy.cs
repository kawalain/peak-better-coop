using System.Collections.Generic;
using BetterCoop.Core;
using TMPro;
using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Words on a sheet that comes off a spirit duplicator (see <see cref="Ditto"/>):
/// printed from the sheet's top down as the drum turns, each line first in the
/// master's violet and then in its own ink. Each letter is coloured by where
/// it lies on the sheet, so lines in different texts come up in turn.
/// </summary>
internal sealed class DittoCopy : MonoBehaviour
{
    private readonly List<TMP_Text> texts = new();

    private RectTransform sheet = null!;

    private float startedAt;

    private bool done;

    /// <summary>Prints <paramref name="texts"/> on <paramref name="sheet"/> as a ditto copy, the drum starting <paramref name="delay"/> seconds from now.</summary>
    public static DittoCopy Add(RectTransform sheet, IEnumerable<TMP_Text> texts, float delay)
    {
        DittoCopy copy = sheet.gameObject.AddComponent<DittoCopy>();
        copy.sheet = sheet;
        copy.texts.AddRange(texts);
        copy.startedAt = Time.unscaledTime + delay;
        copy.Print();
        return copy;
    }

    private void LateUpdate()
    {
        if (!done)
        {
            Print();
        }
    }

    private void Print()
    {
        float elapsed = Time.unscaledTime - startedAt;
        done = Ditto.Done(elapsed);
        Color violet = PaperText.ToColor(Ditto.Violet);
        Rect area = sheet.rect;
        foreach (TMP_Text text in texts)
        {
            // Drawn afresh in its own colours, then laid over with the copy's.
            text.ForceMeshUpdate();
            if (done)
            {
                continue;
            }

            TMP_TextInfo info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo letter = info.characterInfo[i];
                if (!letter.isVisible)
                {
                    continue;
                }

                Vector3 middle = (letter.bottomLeft + letter.topRight) * 0.5f;
                Vector2 on = sheet.InverseTransformPoint(text.transform.TransformPoint(middle));
                float depth = Mathf.Clamp01((area.yMax - on.y) / Mathf.Max(1f, area.height));
                (float printed, float wet) = Ditto.At(depth, elapsed);
                Color32[] colours = info.meshInfo[letter.materialReferenceIndex].colors32;
                for (int corner = 0; corner < 4; corner++)
                {
                    Color own = colours[letter.vertexIndex + corner];
                    Color colour = Color.Lerp(own, new Color(violet.r, violet.g, violet.b, own.a), wet);
                    colour.a *= printed;
                    colours[letter.vertexIndex + corner] = colour;
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
