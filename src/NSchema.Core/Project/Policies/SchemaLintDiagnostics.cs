using NSchema.Model;

namespace NSchema.Project.Policies;

/// <summary>
/// The diagnostics minted by <see cref="SchemaLintPolicy"/>.
/// </summary>
internal static class SchemaLintDiagnostics
{
    internal static readonly DiagnosticSource Source = "schema-lint";

    /// <summary>
    /// A table declared without a primary key.
    /// </summary>
    public static Diagnostic MissingPrimaryKey(ObjectAddress table) =>
        Diagnostic.Warning(Source, "missing-primary-key", $"Table '{table}' has no primary key.");

    /// <summary>
    /// A nullable column used in a primary key, which the database will force NOT NULL.
    /// </summary>
    public static Diagnostic NullablePrimaryKeyColumn(MemberAddress column) =>
        Diagnostic.Warning(Source, "nullable-primary-key-column", $"Column '{column.Member}' on '{column.Owner}' is part of the primary key but is declared nullable. It will be forced NOT NULL.");

    /// <summary>
    /// A key or index that lists the same column more than once.
    /// </summary>
    public static Diagnostic RepeatedColumn(string kind, MemberAddress key, SqlIdentifier column) =>
        Diagnostic.Warning(Source, "repeated-column", $"The {kind:text} '{key.Member}' on '{key.Owner}' lists column '{column}' more than once.");
}
