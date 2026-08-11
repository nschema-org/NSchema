using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="DataHazardPolicy"/>.
/// </summary>
internal static class DataHazardDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.DataHazards;

    /// <summary>
    /// A required column added with nothing to fill existing rows from.
    /// </summary>
    public static Diagnostic RequiredColumnWithoutDefault(MemberAddress column) =>
        Diagnostic.Warning(Source, "required-column-without-default", $"Column '{column}' is added NOT NULL without a default value. The migration will fail if the table holds rows.");

    /// <summary>
    /// A column tightened to NOT NULL over rows that may hold NULLs.
    /// </summary>
    public static Diagnostic ColumnBecomesRequired(MemberAddress column) =>
        Diagnostic.Warning(Source, "column-becomes-required", $"Column '{column}' becomes NOT NULL. The migration will fail if any existing rows are NULL.");

    /// <summary>
    /// A column type change whose cast can fail on existing values.
    /// </summary>
    public static Diagnostic RiskyTypeChange(MemberAddress column, SqlType from, SqlType to) =>
        Diagnostic.Warning(Source, "risky-type-change", $"Column '{column}' changes type from {from} to {to}. The cast will fail if any existing values cannot be converted.");

    /// <summary>
    /// A primary key added over columns the table already had.
    /// </summary>
    public static Diagnostic PrimaryKeyOverExistingData(MemberAddress primaryKey, IReadOnlyList<SqlIdentifier> columns) =>
        Diagnostic.Warning(Source, "primary-key-over-existing-data", $"Primary key '{primaryKey.Member}' on '{primaryKey.Owner}' is added over existing {Columns(columns)}. The migration will fail if any rows hold duplicate or NULL values.");

    /// <summary>
    /// A unique constraint added over columns the table already had.
    /// </summary>
    public static Diagnostic UniqueConstraintOverExistingData(MemberAddress constraint, IReadOnlyList<SqlIdentifier> columns) =>
        Diagnostic.Warning(Source, "unique-constraint-over-existing-data", $"Unique constraint '{constraint.Member}' on '{constraint.Owner}' is added over existing {Columns(columns)}. The migration will fail if existing rows hold duplicate values.");

    /// <summary>
    /// A unique index added over data the table already held.
    /// </summary>
    public static Diagnostic UniqueIndexOverExistingData(MemberAddress index) =>
        Diagnostic.Warning(Source, "unique-index-over-existing-data", $"Unique index '{index.Member}' on '{index.Owner}' is added over existing data. The migration will fail if existing rows hold duplicate values.");

    private static FormattedText Columns(IReadOnlyList<SqlIdentifier> names) =>
        $"{(names.Count == 1 ? "column" : "columns"):text} {string.Join(", ", names.Select(n => $"'{n}'"))}";
}
