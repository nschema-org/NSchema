using NSchema.Model;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// The diagnostics minted by the planner.
/// </summary>
internal static class PlanDiagnostics
{
    private const string Source = "plan";

    /// <summary>
    /// Planning without a registered SQL dialect (the plan's statements cannot be rendered).
    /// </summary>
    public static Diagnostic MissingDialect => Diagnostic.Error(Source, "Planning requires a database provider to render SQL, but none is registered.");

    /// <summary>
    /// A declared object matches an observed one only up to case.
    /// </summary>
    public static Diagnostic CaseOnlyMismatch(ObjectAddress declared, ObjectAddress observed) =>
        Diagnostic.Warning(Source, $"The project declares '{declared}' but the database has '{observed}', which differs only in case. Identifiers are case-sensitive, so these are different objects; if they should be the same one, match the declared spelling to the database's.");

    /// <summary>
    /// A declared schema matches an observed one only up to case.
    /// </summary>
    public static Diagnostic CaseOnlySchemaMismatch(SqlIdentifier declared, SqlIdentifier observed) =>
        Diagnostic.Warning(Source, $"The project declares schema '{declared}' but the database has '{observed}', which differs only in case. Identifiers are case-sensitive, so these are different schemas; if they should be the same one, match the declared spelling to the database's.");

    /// <summary>
    /// A declared extension matches an observed one only up to case.
    /// </summary>
    public static Diagnostic CaseOnlyExtensionMismatch(SqlIdentifier declared, SqlIdentifier observed) =>
        Diagnostic.Warning(Source, $"The project declares extension '{declared}' but the database has '{observed}', which differs only in case. Identifiers are case-sensitive, so these are different extensions; if they should be the same one, match the declared spelling to the database's.");
}
