using Microsoft.CodeAnalysis;

namespace NSchema.Analyzers;

/// <summary>
/// The rules this project reports. A <see cref="DiagnosticDescriptor"/> is the fixed half of a diagnostic — its id,
/// its severity, and the sentence template; an analyzer supplies the arguments and the place to underline.
/// </summary>
/// <remarks>
/// Severity is <see cref="DiagnosticSeverity.Warning"/> rather than Error so a rule can be dialled down from
/// <c>.editorconfig</c> while a refactor is in flight. The engine builds with <c>TreatWarningsAsErrors</c>, so in
/// practice a violation still fails the build.
/// </remarks>
public static class Rules
{
    private const string Category = "Architecture";

    /// <summary>
    /// A slice referenced another slice its row in the layering table does not list.
    /// </summary>
    public static readonly DiagnosticDescriptor ForbiddenSliceDependency = new(
        id: "NS0001",
        title: "Slice depends on an undeclared slice",
        messageFormat: "'{0}' may not depend on '{1}'. {0} may depend only on {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Which slices of the engine may know about which is declared in Architecture.Layering. "
            + "Adding an edge there is a deliberate decision rather than an accident.");

    /// <summary>
    /// A slice's domain reached for one of the application services that orchestrate it.
    /// </summary>
    public static readonly DiagnosticDescriptor DomainDependsOnApplicationService = new(
        id: "NS0002",
        title: "Domain type depends on an application service",
        messageFormat: "'{0}' is a domain type and may not depend on the application service '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A slice's domain answers questions about the schema. The contract a slice is called through "
            + "composes its domain, never the other way about.");

    /// <summary>
    /// A provider seam reached for the operations that drive it
    /// .</summary>
    public static readonly DiagnosticDescriptor ProviderSeamDependsOnOperations = new(
        id: "NS0003",
        title: "Provider seam depends on Operations",
        messageFormat: "'{0}' is a provider seam and may not depend on '{1}', which belongs to Operations",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A provider implements a seam. How the engine sequences a run is none of its business.");

    /// <summary>
    /// A namespace introduced a top-level slice that the layering table has never heard of.
    /// </summary>
    public static readonly DiagnosticDescriptor UndeclaredSlice = new(
        id: "NS0004",
        title: "Namespace introduces an undeclared slice",
        messageFormat: "Namespace '{0}' introduces the slice '{1}', which is not declared in the layering table",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A top-level namespace nobody declares sits outside every layering rule. Give it a row in "
            + "Architecture.Layering, or move the type into an existing slice.");
}
