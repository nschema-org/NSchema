using System.Text.RegularExpressions;

namespace NSchema.Tests.Diagnostics;

/// <summary>
/// The set of findings NSchema can report, and the code each is addressed by.
/// </summary>
/// <remarks>
/// A code and a source both key findings in configuration and documentation, so both are a contract: this snapshot
/// makes adding, renaming or removing one a deliberate, reviewable change. It carries each finding's kind too, so
/// what a user is allowed to silence is reviewable in one place — an advisory finding can be lowered, a structural
/// one cannot. All are literals inside catalogue members, so they are read from the sources — nothing can enumerate
/// them at runtime without invoking every member.
/// </remarks>
public sealed class DiagnosticCatalogueTests
{
    private static readonly Regex Member = new("""public static (?:Diagnostic|NsqlDiagnostic) (?<member>\w+)""");

    private static readonly Regex Code = new("\"(?<code>[a-z0-9]+(?:-[a-z0-9]+)*)\"");

    private static readonly Regex RegisteredSource = new("DiagnosticSource (?<member>\\w+) = \"(?<source>[a-z0-9-]+)\"");

    private static readonly Regex SourceDeclaration = new(@"DiagnosticSource (?<field>\w+) = DiagnosticSources\.(?<member>\w+)");

    private static readonly Regex SourceUse = new("""(?:new|Diagnostic\.(?:Error|Warning|Info))\(\s*(?<field>\w*Source)\s*,""");

    private static readonly Regex Severity = new("""Diagnostic\.(?<severity>Error|Warning|Info)\(|DiagnosticSeverity\.(?<severity>Error|Warning|Info)""");

    private static List<(string Catalogue, string Member, string Source, string Code, string Severity, string Kind)> Catalogue()
    {
        var findings = new List<(string, string, string, string, string, string)>();
        var registry = Registry();
        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*Diagnostics.cs", SearchOption.AllDirectories))
        {
            var catalogue = Path.GetFileNameWithoutExtension(file);
            // Split the file into members so a finding's code and kind are read from its own body.
            var text = File.ReadAllText(file);
            // A catalogue names the registry member it reports through, which the registry gives the value of.
            var sources = SourceDeclaration.Matches(text)
                .Where(match => registry.ContainsKey(match.Groups["member"].Value))
                .ToDictionary(match => match.Groups["field"].Value, match => registry[match.Groups["member"].Value]);
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

                // A member names the source field it reports through, which the file declares the value of.
                var source = SourceUse.Match(body) is { Success: true } use
                    && sources.TryGetValue(use.Groups["field"].Value, out var declared) ? declared : "?";

                findings.Add((catalogue, declarations[i].Groups["member"].Value, source, code.Groups["code"].Value, severity, kind));
            }
        }

        return [.. findings.OrderBy(f => f.Item1, StringComparer.Ordinal).ThenBy(f => f.Item2, StringComparer.Ordinal)];
    }

    [Fact]
    public Task Catalogue_ListsEveryFindingAndItsCode() =>
        Verify(string.Join("\n", Catalogue().Select(f => $"{f.Severity,-8} {f.Kind,-11} {f.Source,-22} {f.Code,-42} {f.Catalogue}.{f.Member}")));

    /// <summary>
    /// The sources <see cref="DiagnosticSources"/> declares, by member name.
    /// </summary>
    private static Dictionary<string, string> Registry() =>
        RegisteredSource.Matches(File.ReadAllText(Path.Combine(SourceRoot(), "NSchema.Core", "Diagnostics", "DiagnosticSources.cs")))
            .ToDictionary(match => match.Groups["member"].Value, match => match.Groups["source"].Value);

    [Fact]
    public void EverySourceIsDeclaredRegisteredAndReported()
    {
        // What the registry declares, what it offers as All, and what the catalogues report under are one set —
        // so a source cannot be added without being reachable from configuration, nor linger once unused.
        var declared = Registry().Values.ToHashSet();
        var registered = DiagnosticSources.All.Select(source => source.Value).ToHashSet();
        var reported = Catalogue().Select(finding => finding.Source).ToHashSet();

        registered.ShouldBe(declared, ignoreOrder: true);
        reported.ShouldBe(declared, ignoreOrder: true);
    }

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
