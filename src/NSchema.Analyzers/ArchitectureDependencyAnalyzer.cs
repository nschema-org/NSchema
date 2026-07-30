using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NSchema.Analyzers;

/// <summary>
/// Reports the three rules about one type referring to another: the layering table, and the two shape rules that
/// say something more specific than it does.
/// </summary>
/// <remarks>
/// One analyzer covers all three because they answer the same question about the same event — a name in the source
/// resolving to a type elsewhere in the engine — and resolving that name once is cheaper than three times.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchitectureDependencyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Every rule this analyzer can report. Reporting one that is missing here is an error
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Rules.ForbiddenSliceDependency,
        Rules.DomainDependsOnApplicationService,
        Rules.ProviderSeamDependsOnOperations
    );

    /// <summary>
    /// Subscribes to the syntax the rules care about. Called once per compilation, before any analysis.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Every mention of a type is one of these two: `TableDiff` is an identifier, `Result<TableDiff>` is a
        // generic name (and its argument is an identifier in turn, so both halves get seen).
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (Reference.Resolve(context) is not { } reference)
        {
            return;
        }

        var from = Namespaces.SliceOf(reference.SourceNamespace);
        var to = Namespaces.SliceOf(reference.TargetNamespace);

        // A slice the table has never heard of is NS0004's business, not ours —
        // judging it here would bury that one real problem under a diagnostic per reference.
        if (from is null || to is null || !Architecture.Slices.Contains(from) || !Architecture.Slices.Contains(to))
        {
            return;
        }

        var target = reference.Target.ToDisplayString();

        // Seams first: several slices have one, so "a provider may not know how a run is sequenced" is a sentence
        // the table cannot say, and leaving it until last would mean never saying it.
        if (Namespaces.IsProviderSeam(reference.SourceNamespace) && to == "Operations")
        {
            Report(context, Rules.ProviderSeamDependsOnOperations, reference, reference.Source.Name, target);
            return;
        }

        // Then the table. Where it already forbids the edge, "Diff may not depend on Apply" is the more useful
        // sentence, even when the reference happens to be a domain type reaching for a service.
        if (!Architecture.Allows(from, to))
        {
            Report(context, Rules.ForbiddenSliceDependency, reference, from, target, Describe(Architecture.AllowedFor(from)));
            return;
        }

        // Within what the table permits, the direction inside a slice still holds.
        if (Namespaces.IsDomain(reference.SourceNamespace) && Namespaces.IsApplication(reference.TargetNamespace))
        {
            Report(context, Rules.DomainDependsOnApplicationService, reference, reference.Source.Name, target);
        }
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule,
        Reference reference,
        params object[] arguments
    ) => context.ReportDiagnostic(Diagnostic.Create(rule, reference.Location, arguments));

    private static string Describe(string[] slices) => slices.Length == 0 ? "nothing" : string.Join(", ", slices);
}
