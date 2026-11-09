using BetterCoop.Core;
using TMPro;
using UnityEngine;

namespace BetterCoop.Ui.Paper;

/// <summary>
/// Text on paper: the sizes the game's own settings page gives its title,
/// section headings, setting names, values, and notes, in ink colours.
/// </summary>
internal static class PaperText
{
    public const float Title = 48f;

    public const float Heading = 32f;

    public const float Label = 28f;

    public const float Value = 24f;

    public const float Note = 20f;

    public static Color Paper => ToColor(PaperDrawing.Paper);

    public static Color Ink => ToColor(PaperDrawing.InkBlack);

    public static Color Blue => ToColor(PaperDrawing.PassportBlue);

    public static Color Dash => ToColor(PaperDrawing.DashInk);

    /// <summary>One line of ink in the game's menu font, without the menu's glow or outline.</summary>
    public static TMP_Text Make(GameObject holder, TMP_Text template, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
        text.font = template.font;
        text.fontSharedMaterial = template.font != null ? template.font.material : template.fontSharedMaterial;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // Ellipsis would also cut a line whose box is a little short of its height, hiding all of it.
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Shrinks the text as far as <paramref name="smallest"/> to fit its box, as a long name in a narrow caption.</summary>
    public static void Fit(TMP_Text text, float smallest)
    {
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = smallest;
        text.enableAutoSizing = true;
    }

    /// <summary>Sets the words, keeping Korean words whole where the text wraps.</summary>
    public static void Set(TMP_Text text, string value)
    {
        if (text.textWrappingMode != TextWrappingModes.NoWrap)
        {
            value = HangulWords.KeepWhole(value);
        }

        if (text.text != value)
        {
            text.text = value;
        }
    }

    internal static Color ToColor(Core.Ink ink) => new(ink.R, ink.G, ink.B, ink.A);
}
