using System.Text.RegularExpressions;
using NSchema.Plan.Domain;

namespace NSchema.Plan.Backends;

/// <summary>
/// Diagnostics minted for <see cref="SqlDialect"/> renderings.
/// </summary>
internal static partial class SqlDialectDiagnostics
{
    private const string Source = "sql-dialect";

    public static Diagnostic Unsupported(MigrationAction action, string dialect) =>
        Diagnostic.Error(Source, $"{dialect} does not support the '{Describe(action)}' action.");

    public static Diagnostic Skipped(MigrationAction action, string dialect) =>
        Diagnostic.Warning(Source, $"{dialect} does not support the '{Describe(action)}' action; the change was skipped. Remove the declaration from the project to clear this warning.");

    public static Diagnostic Unknown(MigrationAction action, string dialect) =>
        Diagnostic.Error(Source, $"'{action.GetType().Name}' is not a migration action {dialect} recognizes.");

    /// <summary>
    /// The action type's name as prose: <c>AddExclusionConstraint</c> → <c>add exclusion constraint</c>.
    /// </summary>
    private static string Describe(MigrationAction action) =>
        WordStart().Replace(action.GetType().Name, " $1").Trim().ToLowerInvariant();

    [GeneratedRegex("([A-Z])")]
    private static partial Regex WordStart();
}
