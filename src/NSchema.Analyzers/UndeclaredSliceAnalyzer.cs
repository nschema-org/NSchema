using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NSchema.Analyzers;

/// <summary>
/// Keeps the layering table honest about what the engine contains. A new top-level namespace nobody declares would
/// sit outside every other rule, so it is caught where it is introduced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UndeclaredSliceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        Rules.UndeclaredSlice
    ];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // `namespace NSchema.Plan;` and `namespace NSchema.Plan { }` are different syntax for the same thing.
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseNamespaceDeclarationSyntax)context.Node;
        var declared = declaration.Name.ToString();

        if (Namespaces.SliceOf(declared) is not { } slice || Architecture.Slices.Contains(slice))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rules.UndeclaredSlice, declaration.Name.GetLocation(), declared, slice));
    }
}
