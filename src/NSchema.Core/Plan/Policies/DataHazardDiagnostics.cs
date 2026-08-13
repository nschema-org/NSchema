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

    /// <summary>
    /// An identity whose start moved, so its counter restarts over rows the table already holds.
    /// </summary>
    /// <remarks>
    /// An error where the rest of this class warns, and the difference is whether the danger announces itself.
    /// Every other hazard here <em>fails</em> the migration when the data does not fit, so a warning is enough.
    /// A restart succeeds quietly and hands the damage to whoever inserts next.
    /// </remarks>
    public static Diagnostic IdentityRestartsOverExistingRows(MemberAddress column, long? start) =>
        Diagnostic.Error(Source, "identity-restart-reissues-values",
            $"Column '{column}' has its identity start moved to {Start(start)}, which restarts the counter.")
            with
        { Kind = DiagnosticKind.Advisory };

    /// <summary>
    /// A sequence whose start moved, so its counter restarts over values it has already handed out.
    /// </summary>
    /// <remarks>
    /// An error rather than a warning for the same reason as <see cref="IdentityRestartsOverExistingRows"/>.
    /// Proceeding with this change succeeds but will issue duplicate values, causing data corruption.
    /// </remarks>
    public static Diagnostic SequenceRestartsOverIssuedValues(ObjectAddress sequence, long? start) =>
        Diagnostic.Error(Source, "sequence-restart-reissues-values",
            $"Sequence '{sequence}' has its start moved to {Start(start)}, which restarts the counter.")
            with
        { Kind = DiagnosticKind.Advisory };

    private static FormattedText Start(long? start) =>
        start is { } value ? $"{value}" : $"the engine's default";

    private static FormattedText Columns(IReadOnlyList<SqlIdentifier> names) =>
        $"{(names.Count == 1 ? "column" : "columns"):text} {string.Join(", ", names.Select(n => $"'{n}'"))}";
}
