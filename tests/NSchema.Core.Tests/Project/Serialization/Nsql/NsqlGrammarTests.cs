using System.Text.Json;
using System.Text.RegularExpressions;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// The TextMate grammar in <c>grammar/</c> states the language a second time, in regex, for the editors that
/// cannot run the parser. These tests hold it to <see cref="NsqlKeywords"/> in both directions, so neither a
/// keyword added to the vocabulary nor a word left behind in the grammar can go unnoticed.
/// </summary>
public sealed class NsqlGrammarTests
{
    private const string ControlScope = "keyword.control.nsql";

    /// <summary>Matches the alternation a keyword pattern is built from: <c>(?i)\b(?:add|after|…)\b</c>.</summary>
    private static readonly Regex Alternation = new(@"\(\?:(?<words>[^)]*)\)", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public void EveryKeywordIsHighlighted()
    {
        // Arrange
        var highlighted = HighlightedWords().Values.SelectMany(words => words).ToHashSet(NsqlKeywords.Comparer);

        // Act
        var missing = NsqlKeywords.All.Where(keyword => !highlighted.Contains(keyword)).Order().ToList();

        // Assert
        missing.ShouldBeEmpty("every keyword needs a group in the grammar's keyword patterns");
    }

    [Fact]
    public void EveryHighlightedWordIsAKeyword()
    {
        // Arrange
        var highlighted = HighlightedWords().Values.SelectMany(words => words);

        // Act
        var unknown = highlighted.Where(word => !NsqlKeywords.All.Contains(word)).Order().ToList();

        // Assert
        unknown.ShouldBeEmpty("the grammar highlights a word the language no longer knows");
    }

    [Fact]
    public void EveryKeywordIsHighlightedOnce()
    {
        // Arrange
        var groups = HighlightedWords();

        // Act
        var repeated = groups.Values
            .SelectMany(words => words)
            .GroupBy(word => word, NsqlKeywords.Comparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToList();

        // Assert
        repeated.ShouldBeEmpty("a keyword in two groups would be scoped by whichever pattern is tried first");
    }

    [Fact]
    public void TheControlGroupIsExactlyTheStatementOpeners()
    {
        // The three groups are a stated rule, not a taste: control is what opens a statement. Pinning the rule
        // here is what stops the split drifting into a judgement call as the vocabulary grows.

        // Arrange
        var openers = NsqlKeywords.StatementOpeners.Concat(NsqlKeywords.SettingsStatementOpeners);

        // Act — the grammar spells its words in lower case, the vocabulary in upper; only the words are compared.
        var control = HighlightedWords()[ControlScope].Select(word => word.ToUpperInvariant());

        // Assert
        control.ShouldBe(openers, ignoreOrder: true);
    }

    [Fact]
    public void EveryPatternIsAValidExpression()
    {
        // A grammar with a broken pattern still parses as JSON and fails silently in the editor.

        // Arrange
        using var grammar = Grammar();

        // Act
        var invalid = Expressions(grammar.RootElement)
            // A body's end pattern back-references its opening tag, which only resolves against the begin match.
            .Where(expression => !expression.Contains('\\') || !Regex.IsMatch(expression, @"\\[1-9]"))
            .Where(expression => !Compiles(expression))
            .ToList();

        // Assert
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void TheBundleManifestMatchesTheGrammar()
    {
        // The manifest is what an editor reads to find the grammar, so a rename that misses it silently
        // leaves the language unhighlighted.

        // Arrange
        using var grammar = Grammar();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(GrammarRoot(), "package.json")));
        var contributed = manifest.RootElement.GetProperty("contributes").GetProperty("grammars")[0];

        // Act
        var scope = contributed.GetProperty("scopeName").GetString();
        var path = contributed.GetProperty("path").GetString()!;
        var extensions = manifest.RootElement.GetProperty("contributes").GetProperty("languages")[0]
            .GetProperty("extensions").EnumerateArray().Select(extension => extension.GetString()).ToList();

        // Assert
        scope.ShouldBe(grammar.RootElement.GetProperty("scopeName").GetString());
        File.Exists(Path.Combine(GrammarRoot(), path.TrimStart('.', '/'))).ShouldBeTrue();
        extensions.ShouldContain(".nsql");
    }

    /// <summary>The words each keyword pattern highlights, keyed by the scope it gives them.</summary>
    private static Dictionary<string, HashSet<string>> HighlightedWords()
    {
        using var grammar = Grammar();
        var patterns = grammar.RootElement
            .GetProperty("repository")
            .GetProperty("keywords")
            .GetProperty("patterns");

        return patterns.EnumerateArray().ToDictionary(
            pattern => pattern.GetProperty("name").GetString()!,
            pattern => Alternation.Match(pattern.GetProperty("match").GetString()!)
                .Groups["words"].Value
                .Split('|')
                .ToHashSet(NsqlKeywords.Comparer));
    }

    /// <summary>Every regular expression the grammar carries, wherever it sits in the tree.</summary>
    private static IEnumerable<string> Expressions(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        property.Name is "match" or "begin" or "end")
                    {
                        yield return property.Value.GetString()!;
                    }

                    foreach (var nested in Expressions(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var nested in element.EnumerateArray().SelectMany(Expressions))
                {
                    yield return nested;
                }

                break;
        }
    }

    private static bool Compiles(string expression)
    {
        try
        {
            _ = new Regex(expression, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static JsonDocument Grammar() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(GrammarRoot(), "nsql.tmLanguage.json")));

    private static string GrammarRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "grammar")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root."),
            "grammar");
    }
}
