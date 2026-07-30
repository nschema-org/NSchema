namespace NSchema.Model.Tables;

/// <summary>
/// The diagnostics minted when a table adopts members.
/// </summary>
internal static class TableDiagnostics
{
    internal static readonly DiagnosticSource Source = "table";

    /// <summary>
    /// Adopting members that would give the table a second primary key.
    /// </summary>
    public static Diagnostic DuplicatePrimaryKey(SqlIdentifier table) =>
        Diagnostic.Error(Source, "duplicate-primary-key", $"Table '{table}' already declares a primary key.");

    /// <summary>
    /// Adopting a member under a name the table already uses for that kind.
    /// </summary>
    public static Diagnostic DuplicateMember(SqlIdentifier table, MemberKind kind, SqlIdentifier member) =>
        Diagnostic.Error(Source, "duplicate-member", $"Table '{table}' already declares {kind.Display():text} '{member}'.");
}
