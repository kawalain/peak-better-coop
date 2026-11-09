using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BetterCoop.Core;

/// <summary>
/// Every sentence a player can see, in every language the mod speaks, as read
/// from the one strings file (<c>Ui/Locale/strings.json</c>): each key holds
/// one sentence per language code. A sentence missing in the language asked
/// for is given in English. Placeholders are named, as <c>{carrier}</c>.
/// </summary>
internal sealed class TextTable
{
    /// <summary>The language every key must have, and the one a missing sentence falls back to.</summary>
    public const string Fallback = "en";

    private readonly Dictionary<string, Dictionary<string, string>> entries;

    private TextTable(Dictionary<string, Dictionary<string, string>> entries)
    {
        this.entries = entries;
    }

    /// <summary>A table with no sentences, which answers every key with the key itself.</summary>
    public static TextTable Empty { get; } = new(new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal));

    /// <summary>Every key, in the file's order.</summary>
    public IEnumerable<string> Keys => entries.Keys;

    /// <summary>A key's sentences by language code; empty for an unknown key.</summary>
    public IReadOnlyDictionary<string, string> Sentences(string key) =>
        entries.TryGetValue(key, out Dictionary<string, string>? sentences)
            ? sentences
            : new Dictionary<string, string>();

    /// <summary>The key's sentence in that language, else in English, else the key itself.</summary>
    public string Get(string key, string language)
    {
        if (!entries.TryGetValue(key, out Dictionary<string, string>? sentences))
        {
            return key;
        }

        return sentences.TryGetValue(language, out string? sentence) || sentences.TryGetValue(Fallback, out sentence)
            ? sentence
            : key;
    }

    /// <summary>
    /// Puts each value in the placeholder of its name. A placeholder with no
    /// value is left as it is, so a slip shows rather than hides.
    /// </summary>
    public static string Fill(string template, params (string Name, object? Value)[] values)
    {
        if (values.Length == 0 || template.IndexOf('{') < 0)
        {
            return template;
        }

        var text = new StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            int close;
            if (template[i] == '{' && (close = template.IndexOf('}', i + 1)) > i)
            {
                string name = template.Substring(i + 1, close - i - 1);
                if (TryValue(values, name, out string? value))
                {
                    text.Append(value);
                    i = close + 1;
                    continue;
                }
            }

            text.Append(template[i]);
            i++;
        }

        return text.ToString();
    }

    /// <summary>The names of a sentence's placeholders, each once, in order.</summary>
    public static IReadOnlyList<string> Placeholders(string template)
    {
        var names = new List<string>();
        int i = 0;
        while ((i = template.IndexOf('{', i)) >= 0)
        {
            int close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                break;
            }

            string name = template.Substring(i + 1, close - i - 1);
            if (name.Length > 0 && !names.Contains(name))
            {
                names.Add(name);
            }

            i = close + 1;
        }

        return names;
    }

    private static bool TryValue((string Name, object? Value)[] values, string name, out string? value)
    {
        foreach ((string Name, object? Value) pair in values)
        {
            if (pair.Name == name)
            {
                value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Reads the strings file: one object whose members are keys, each an
    /// object of language code to sentence. A member whose name starts with
    /// an underscore is a note for translators and is skipped.
    /// </summary>
    /// <exception cref="FormatException">The file is not of that shape.</exception>
    public static TextTable Parse(string json)
    {
        var reader = new JsonReader(json);
        var entries = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        reader.Expect('{');
        if (!reader.TryTake('}'))
        {
            do
            {
                string key = reader.ReadString();
                reader.Expect(':');
                if (key.StartsWith("_", StringComparison.Ordinal))
                {
                    reader.SkipValue();
                    continue;
                }

                if (entries.ContainsKey(key))
                {
                    throw reader.Error($"key '{key}' is given twice");
                }

                entries[key] = ReadSentences(reader, key);
            }
            while (reader.TryTake(','));

            reader.Expect('}');
        }

        reader.ExpectEnd();
        return new TextTable(entries);
    }

    private static Dictionary<string, string> ReadSentences(JsonReader reader, string key)
    {
        var sentences = new Dictionary<string, string>(StringComparer.Ordinal);
        reader.Expect('{');
        if (reader.TryTake('}'))
        {
            return sentences;
        }

        do
        {
            string language = reader.ReadString();
            reader.Expect(':');
            if (sentences.ContainsKey(language))
            {
                throw reader.Error($"'{key}' gives '{language}' twice");
            }

            sentences[language] = reader.ReadString();
        }
        while (reader.TryTake(','));

        reader.Expect('}');
        return sentences;
    }

    /// <summary>Just enough JSON for the strings file: objects and strings.</summary>
    private sealed class JsonReader(string text)
    {
        private int at;

        public void Expect(char wanted)
        {
            SkipSpace();
            if (at >= text.Length || text[at] != wanted)
            {
                throw Error($"expected '{wanted}'");
            }

            at++;
        }

        public bool TryTake(char wanted)
        {
            SkipSpace();
            if (at < text.Length && text[at] == wanted)
            {
                at++;
                return true;
            }

            return false;
        }

        public void ExpectEnd()
        {
            SkipSpace();
            if (at < text.Length)
            {
                throw Error("expected the end of the file");
            }
        }

        public void SkipValue()
        {
            SkipSpace();
            if (at < text.Length && text[at] == '{')
            {
                at++;
                if (TryTake('}'))
                {
                    return;
                }

                do
                {
                    ReadString();
                    Expect(':');
                    SkipValue();
                }
                while (TryTake(','));

                Expect('}');
                return;
            }

            ReadString();
        }

        public string ReadString()
        {
            Expect('"');
            var value = new StringBuilder();
            while (true)
            {
                if (at >= text.Length)
                {
                    throw Error("a string is not closed");
                }

                char c = text[at++];
                if (c == '"')
                {
                    return value.ToString();
                }

                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }

                if (at >= text.Length)
                {
                    throw Error("a string is not closed");
                }

                char escaped = text[at++];
                switch (escaped)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'n': value.Append('\n'); break;
                    case 't': value.Append('\t'); break;
                    case 'r': value.Append('\r'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'u' when at + 4 <= text.Length
                        && int.TryParse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code):
                        value.Append((char)code);
                        at += 4;
                        break;
                    default:
                        throw Error($"unknown escape '\\{escaped}'");
                }
            }
        }

        public FormatException Error(string problem)
        {
            int line = 1;
            for (int i = 0; i < Math.Min(at, text.Length); i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return new FormatException($"strings file, line {line}: {problem}");
        }

        private void SkipSpace()
        {
            while (at < text.Length && (char.IsWhiteSpace(text[at]) || text[at] == '﻿'))
            {
                at++;
            }
        }
    }
}
