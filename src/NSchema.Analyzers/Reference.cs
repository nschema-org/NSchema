using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NSchema.Analyzers;

/// <summary>
/// One place in the source where a type mentions another type in the same assembly. Every layering rule is a
/// question about one of these.
/// </summary>
/// <remarks>
/// Every reference to a type — a base type, a parameter, a generic argument, an attribute, a static call — reaches
/// the compiler as a name in the source, so resolving names is enough to see them all. Reading the source rather
/// than the compiled assembly also keeps compiler-generated members out of the picture, which matters in a codebase
/// this full of records.
/// </remarks>
internal sealed class Reference
{
    private Reference(INamedTypeSymbol source, INamedTypeSymbol target, Location location)
    {
        Source = source;
        Target = target;
        Location = location;
    }

    /// <summary>The type the name was written inside.</summary>
    public INamedTypeSymbol Source { get; }

    /// <summary>The type the name resolved to, or the type declaring the member it resolved to.</summary>
    public INamedTypeSymbol Target { get; }

    /// <summary>Where to underline when a rule objects.</summary>
    public Location Location { get; }

    public string SourceNamespace => Source.ContainingNamespace.ToDisplayString();

    public string TargetNamespace => Target.ContainingNamespace.ToDisplayString();

    /// <summary>
    /// The reference the name node under analysis makes, or <see langword="null"/> when it makes none worth judging.
    /// </summary>
    public static Reference? Resolve(SyntaxNodeAnalysisContext context)
    {
        // `Foo.Bar()` mentions Foo twice — once as the qualifier, once through the member. The qualifier reports it.
        if (context.Node.Parent is MemberAccessExpressionSyntax access
            && access.Name == context.Node
            && context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol
                is INamedTypeSymbol)
        {
            return null;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;

        // Namespaces (the `NSchema.Plan` of `NSchema.Plan.Foo`), locals, parameters and labels are not references
        // to a type. The type in that example is reached through its own name node.
        var target = (symbol as INamedTypeSymbol ?? symbol?.ContainingType)?.OriginalDefinition;

        if (target is null || context.ContainingSymbol is not { } container)
        {
            return null;
        }

        // Only what the engine says about itself is layering. A reference to the BCL or a NuGet package is not.
        if (!SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, context.Compilation.Assembly))
        {
            return null;
        }

        var source = (container as INamedTypeSymbol ?? container.ContainingType)?.OriginalDefinition;

        if (source is null || SymbolEqualityComparer.Default.Equals(source, target))
        {
            return null;
        }

        return new Reference(source, target, context.Node.GetLocation());
    }
}
