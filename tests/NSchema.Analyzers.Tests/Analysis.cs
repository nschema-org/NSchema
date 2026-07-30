using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NSchema.Analyzers.Tests;

/// <summary>
/// Compiles a snippet in memory and reports what an analyzer makes of it. Nothing is written to disk and no
/// analyzer testing package is involved — a compilation plus <c>WithAnalyzers</c> is the whole mechanism.
/// </summary>
internal static class Analysis
{
    /// <summary>
    /// Everything the test host itself is running against, so a snippet can use the BCL without listing references.
    /// </summary>
    private static readonly MetadataReference[] Runtime =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>
    /// What <paramref name="analyzer"/> reports about <paramref name="source"/>, optionally with
    /// <paramref name="otherAssembly"/> compiled separately and referenced — which is how a snippet can mention an
    /// <c>NSchema</c> type that does not belong to the assembly under analysis.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> Run(
        DiagnosticAnalyzer analyzer,
        string source,
        string? otherAssembly = null)
    {
        var references = otherAssembly is null
            ? Runtime
            : [.. Runtime, Compile("NSchema.Provider", otherAssembly).ToMetadataReference()];

        var compilation = Compile("NSchema.Core", source, references);

        // A snippet that does not compile resolves no symbols, so every rule would pass by saying nothing.
        var errors = compilation.GetDiagnostics().Where(entry => entry.Severity == DiagnosticSeverity.Error).ToList();
        errors.ShouldBeEmpty($"the snippet under test must compile:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

        return await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The ids reported, in source order — what most tests assert on.</summary>
    public static string[] Ids(this ImmutableArray<Diagnostic> diagnostics) =>
        [.. diagnostics.OrderBy(entry => entry.Location.SourceSpan.Start).Select(entry => entry.Id)];

    private static CSharpCompilation Compile(string name, string source, MetadataReference[]? references = null) =>
        CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references ?? Runtime,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
}
