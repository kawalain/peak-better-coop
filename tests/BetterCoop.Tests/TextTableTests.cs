using System.Text.RegularExpressions;
using BetterCoop.Core;
using Xunit;

namespace BetterCoop.Tests;

public class TextTableTests
{
    /// <summary>Languages every key outside the English-only groups must have.</summary>
    private static readonly string[] Complete = ["en", "ko"];

    /// <summary>Key groups kept in English only.</summary>
    private const string EnglishOnly = "config.";

    /// <summary>The files that name keys: the accessors, and the settings that read English.</summary>
    private static readonly string[] KeyUsers = ["src/BetterCoop/Ui/Strings.cs", "src/BetterCoop/CoopConfig.cs"];

    private static readonly string[] GameLanguages =
        ["en", "fr", "it", "de", "es-ES", "es-419", "pt-BR", "ru", "uk", "zh-Hans", "zh-Hant", "ja", "ko", "pl", "tr"];

    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BetterCoop.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static TextTable File_() =>
        TextTable.Parse(File.ReadAllText(Path.Combine(Repo(), "src/BetterCoop/Ui/Locale/strings.json")));

    [Fact]
    public void Every_key_has_English_and_every_shown_key_Korean()
    {
        TextTable table = File_();
        foreach (string key in table.Keys)
        {
            IReadOnlyDictionary<string, string> sentences = table.Sentences(key);
            string[] needed = key.StartsWith(EnglishOnly, StringComparison.Ordinal) ? ["en"] : Complete;
            foreach (string language in needed)
            {
                Assert.True(sentences.ContainsKey(language), $"'{key}' has no '{language}'");
            }
        }
    }

    [Fact]
    public void Every_sentence_is_in_a_language_the_game_has()
    {
        TextTable table = File_();
        foreach (string key in table.Keys)
        {
            foreach (string language in table.Sentences(key).Keys)
            {
                Assert.True(GameLanguages.Contains(language), $"'{key}' gives '{language}', which the game has not");
            }
        }
    }

    [Fact]
    public void Every_language_keeps_the_English_placeholders()
    {
        TextTable table = File_();
        foreach (string key in table.Keys)
        {
            IReadOnlyDictionary<string, string> sentences = table.Sentences(key);
            var english = TextTable.Placeholders(sentences["en"]).OrderBy(n => n, StringComparer.Ordinal).ToList();
            foreach ((string language, string sentence) in sentences)
            {
                var theirs = TextTable.Placeholders(sentence).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (key.EndsWith(".one", StringComparison.Ordinal))
                {
                    // A singular may spell its one out instead of using {count}.
                    Assert.True(theirs.All(english.Contains) || english.Count == 0, $"'{key}' in '{language}' adds a placeholder");
                    continue;
                }

                Assert.True(english.SequenceEqual(theirs), $"'{key}' in '{language}' has {{{string.Join("}, {", theirs)}}}, English {{{string.Join("}, {", english)}}}");
            }
        }
    }

    [Fact]
    public void The_code_names_only_keys_the_file_has_and_the_file_only_keys_in_use()
    {
        TextTable table = File_();
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in KeyUsers)
        {
            string code = File.ReadAllText(Path.Combine(Repo(), file));
            foreach (Match match in Regex.Matches(code, "\"([a-z][A-Za-z0-9]*(?:\\.[A-Za-z0-9-]+)+)\""))
            {
                named.Add(match.Groups[1].Value);
            }
        }

        var keys = table.Keys.ToHashSet(StringComparer.Ordinal);
        var missing = named.Where(k => !keys.Contains(k)).ToList();
        var unused = keys.Where(k => !named.Contains(k)).ToList();
        Assert.True(missing.Count == 0, $"Named but not in the file: {string.Join(", ", missing)}");
        Assert.True(unused.Count == 0, $"In the file but never named: {string.Join(", ", unused)}");
    }

    [Fact]
    public void A_missing_sentence_falls_back_to_English_and_a_missing_key_shows_itself()
    {
        TextTable table = TextTable.Parse("""{ "_note": "skipped", "a.b": { "en": "Hi", "ko": "안녕" }, "c.d": { "en": "Only" } }""");
        Assert.Equal("안녕", table.Get("a.b", "ko"));
        Assert.Equal("Only", table.Get("c.d", "ko"));
        Assert.Equal("x.y", table.Get("x.y", "en"));
        Assert.DoesNotContain("_note", table.Keys);
    }

    [Fact]
    public void Placeholders_are_filled_by_name_and_an_unknown_one_stays()
    {
        Assert.Equal("Ann carries Bo", TextTable.Fill("{carrier} carries {rider}", ("rider", "Bo"), ("carrier", "Ann")));
        Assert.Equal("{who} waits 3", TextTable.Fill("{who} waits {seconds}", ("seconds", 3)));
        Assert.Equal(["carrier", "rider"], TextTable.Placeholders("{carrier} → {rider}, {carrier}"));
    }

    [Fact]
    public void Escapes_are_read_and_a_broken_file_names_its_line()
    {
        TextTable table = TextTable.Parse("{ \"a.b\": { \"en\": \"line\\none \\\"q\\\" \\u00e9\" } }");
        Assert.Equal("line\none \"q\" é", table.Get("a.b", "en"));

        FormatException error = Assert.Throws<FormatException>(() => TextTable.Parse("{\n \"a.b\": { \"en\": 1 }\n}"));
        Assert.Contains("line 2", error.Message);
        Assert.Throws<FormatException>(() => TextTable.Parse("""{ "a.b": { "en": "x" }, "a.b": { "en": "y" } }"""));
    }
}
