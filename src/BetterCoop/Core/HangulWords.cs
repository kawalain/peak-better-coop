using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// Keeps Korean words whole where text wraps. TextMeshPro breaks a Korean
/// line between any two syllables unless told otherwise for every text in
/// the game; marking each Korean word as one that must not break keeps the
/// mod's own lines readable and leaves the game's text as it is.
/// </summary>
internal static class HangulWords
{
    private const string Open = "<nobr>";

    private const string Close = "</nobr>";

    /// <summary>
    /// The text with every run of non-space characters that holds a Hangul
    /// syllable marked unbreakable, so lines break only at spaces. Text that
    /// already has markup, or no Hangul, comes back as it is.
    /// </summary>
    public static string KeepWhole(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0 || !HasHangul(text, 0, text.Length))
        {
            return text;
        }

        var kept = new StringBuilder(text.Length + 32);
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                kept.Append(text[i]);
                i++;
                continue;
            }

            int end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            bool korean = HasHangul(text, i, end);
            if (korean)
            {
                kept.Append(Open);
            }

            kept.Append(text, i, end - i);
            if (korean)
            {
                kept.Append(Close);
            }

            i = end;
        }

        return kept.ToString();
    }

    private static bool HasHangul(string text, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            char c = text[i];
            if ((c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏'))
            {
                return true;
            }
        }

        return false;
    }
}
