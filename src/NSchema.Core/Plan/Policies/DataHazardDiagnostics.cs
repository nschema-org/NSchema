using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="DataHazardPolicy"/>, each at the severity its configured enforcement selects.
/// </summary>
internal static class DataHazardDiagnostics
{
    private const string Source = "data-hazards";

    /// <summary>
    /// A required column added with nothing to fill existing rows from.
    /// </summary>
    public static Diagnostic RequiredColumnWithoutDefault(MemberAddress column, DiagnosticSeverity severity) =>
        new(Source, $"Column '{column}' is added NOT NULL without a default value. The migration will fail if the table holds rows.", severity);

    /// <summary>
    /// A column tightened to NOT NULL over rows that may hold NULLs.
    /// </summary>
    public static Diagnostic ColumnBecomesRequired(MemberAddress column, DiagnosticSeverity severity) =>
        new(Source, $"Column '{column}' becomes NOT NULL. The migration will fail if any existing rows are NULL.", severity);

    /// <summary>
    /// A column type change whose cast can fail on existing values.
    /// </summary>
    public static Diagnostic RiskyTypeChange(MemberAddress column, SqlType from, SqlType to, DiagnosticSeverity severity) =>
        new(Source, $"Column '{column}' changes type from {from} to {to}. The cast will fail if any existing values cannot be converted.", severity);

    /// <summary>
    /// A primary key added over columns the table already had.
    /// </summary>
    public static Diagnostic PrimaryKeyOverExistingData(MemberAddress primaryKey, IReadOnlyList<SqlIdentifier> columns, DiagnosticSeverity severity) =>
        new(Source, $"Primary key '{primaryKey.Member}' on '{primaryKey.Owner}' is added over existing {Columns(columns)}. The migration will fail if any rows hold duplicate or NULL values.", severity);

    /// <summary>
    /// A unique constraint added over columns the table already had.
    /// </summary>
    public static Diagnostic UniqueConstraintOverExistingData(MemberAddress constraint, IReadOnlyList<SqlIdentifier> columns, DiagnosticSeverity severity) =>
        new(Source, $"Unique constraint '{constraint.Member}' on '{constraint.Owner}' is added over existing {Columns(columns)}. The migration will fail if existing rows hold duplicate values.", severity);

    /// <summary>
    /// A unique index added over data the table already held.
    /// </summary>
    public static Diagnostic UniqueIndexOverExistingData(MemberAddress index, DiagnosticSeverity severity) =>
        new(Source, $"Unique index '{index.Member}' on '{index.Owner}' is added over existing data. The migration will fail if existing rows hold duplicate values.", severity);

    private static FormattedText Columns(IReadOnlyList<SqlIdentifier> names) =>
        $"{(names.Count == 1 ? "column" : "columns"):text} {string.Join(", ", names.Select(n => $"'{n}'"))}";
}
