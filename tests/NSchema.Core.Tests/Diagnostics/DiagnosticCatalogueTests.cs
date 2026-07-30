using System.Text.RegularExpressions;

namespace NSchema.Tests.Diagnostics;

/// <summary>
/// The set of findings NSchema can report, and the code each is addressed by.
/// </summary>
/// <remarks>
/// A code keys a finding in configuration and documentation, so it is a contract: this snapshot makes adding,
/// renaming or removing one a deliberate, reviewable change. It carries each finding's kind too, so what a user
/// is allowed to silence is reviewable in one place — an advisory finding can be lowered, a structural one cannot. Both are literals inside catalogue members, so they are read
/// from the sources — nothing can enumerate them at runtime without invoking every member.
/// </remarks>
public sealed class DiagnosticCatalogueTests
{
    private static readonly Regex Member = new("""public static (?:Diagnostic|NsqlDiagnostic) (?<member>\w+)""");

    private static readonly Regex Code = new("\"(?<code>[a-z0-9]+(?:-[a-z0-9]+)*)\"");

    private static readonly Regex Severity = new("""Diagnostic\.(?<severity>Error|Warning|Info)\(|DiagnosticSeverity\.(?<severity>Error|Warning|Info)""");

    private static List<(string Catalogue, string Member, string Code, string Severity, string Kind)> Catalogue()
    {
        var findings = new List<(string, string, string, string, string)>();
        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*Diagnostics.cs", SearchOption.AllDirectories))
        {
            var catalogue = Path.GetFileNameWithoutExtension(file);
            // Split the file into members so a finding's code and kind are read from its own body.
            var text = File.ReadAllText(file);
            var declarations = Member.Matches(text).ToList();
            for (var i = 0; i < declarations.Count; i++)
            {
                var start = declarations[i].Index;
                var end = i + 1 < declarations.Count ? declarations[i + 1].Index : text.Length;
                var body = text[start..end];
                if (Code.Match(body) is not { Success: true } code)
                {
                    continue;
                }

                // Reporting below error severity mints an advisory finding, so the kind follows the factory
                // unless the member overrides it.
                var severity = Severity.Match(body) is { Success: true } match
                    ? match.Groups["severity"].Value.ToLowerInvariant()
                    : "?";
                var kind = body.Contains("DiagnosticKind.Advisory", StringComparison.Ordinal) ? "advisory"
                    : body.Contains("DiagnosticKind.Structural", StringComparison.Ordinal) ? "structural"
                    : severity == "error" ? "structural"
                    : "advisory";

                findings.Add((catalogue, declarations[i].Groups["member"].Value, code.Groups["code"].Value, severity, kind));
            }
        }

        return [.. findings.OrderBy(f => f.Item1, StringComparer.Ordinal).ThenBy(f => f.Item2, StringComparer.Ordinal)];
    }

    [Fact]
    public Task Catalogue_ListsEveryFindingAndItsCode() =>
        Verify(string.Join("\n", Catalogue().Select(f => $"{f.Severity,-8} {f.Kind,-11} {f.Code,-42} {f.Catalogue}.{f.Member}")));

    [Fact]
    public void EveryCodeIsUnique()
    {
        // A code addresses one finding, so a repeat would make two impossible to configure apart.
        var duplicates = Catalogue()
            .GroupBy(f => f.Code)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(f => $"{f.Catalogue}.{f.Member}"))}")
            .ToList();

        duplicates.ShouldBeEmpty();
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root."),
            "src");
    }
}
