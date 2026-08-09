using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project.Nsql;

namespace NSchema.Project;

/// <summary>
/// The diagnostics minted while reading and assembling the project.
/// </summary>
internal static class ProjectDiagnostics
{
    internal static readonly DiagnosticSource Source = "project";

    /// <summary>
    /// No DDL file matched any registered project source.
    /// </summary>
    public static Diagnostic NoFilesMatched() => Diagnostic.Error(Source, "no-files-matched",
        "No SQL files matched the registered schema sources.");

    /// <summary>
    /// A script declared more than once in the same scope (the address is the run-once and diagnostic identity).
    /// </summary>
    public static Diagnostic DuplicateScriptName(ScriptReference script) => Diagnostic.Error(Source, "duplicate-script-name",
        $"Duplicate script '{script}' declared.");

    /// <summary>
    /// Two change-event scripts declared for the same trigger and path.
    /// </summary>
    public static Diagnostic DuplicateChangeTarget(ChangeScript change) => Diagnostic.Error(Source, "duplicate-change-target",
        $"Duplicate migration for {ChangeScript.TriggerText(change.Target.Trigger)} '{change.Target.Path}' declared.");

    // ── Accumulation findings — project semantics, not grammar, but positioned like syntax errors. ──

    /// <summary>
    /// A schema declared more than once. Only the declaration is unique; objects land in a schema from any
    /// file without redeclaring it.
    /// </summary>
    public static NsqlDiagnostic SchemaAlreadyDeclared(SqlIdentifier name, SourcePosition position) =>
        Positioned($"Schema '{name}' is already declared.", position);

    /// <summary>
    /// The same named object declared more than once within a schema.
    /// </summary>
    public static NsqlDiagnostic ObjectAlreadyDeclared(SchemaObjectKind kind, SqlIdentifier schema, SqlIdentifier name, SourcePosition position) =>
        kind is SchemaObjectKind.Routine
            ? Positioned($"Routine '{schema}.{name}' is already declared.", position)
            : Positioned($"{Capitalized(kind.Display()):text} '{schema}.{name}' is already declared.", position);

    /// <summary>
    /// A database-global extension declared more than once.
    /// </summary>
    public static NsqlDiagnostic ExtensionAlreadyDeclared(SqlIdentifier name, SourcePosition position) =>
        Positioned($"Extension '{name}' is already declared.", position);

    /// <summary>
    /// The same named trigger declared more than once on a table.
    /// </summary>
    public static NsqlDiagnostic TriggerAlreadyDeclared(SqlIdentifier name, SqlIdentifier schema, SqlIdentifier table, SourcePosition position) =>
        Positioned($"Trigger '{name}' on '{schema}.{table}' is already declared.", position);

    /// <summary>
    /// The same named index declared more than once on a relation.
    /// </summary>
    public static NsqlDiagnostic IndexAlreadyDeclared(SqlIdentifier name, SqlIdentifier schema, SqlIdentifier relation, SourcePosition position) =>
        Positioned($"Index '{name}' on '{schema}.{relation}' is already declared.", position);

    /// <summary>
    /// A table grant whose table the project does not declare.
    /// </summary>
    public static NsqlDiagnostic UnknownGrantTable(SqlIdentifier schema, SqlIdentifier table, SourcePosition position) =>
        Positioned($"GRANT references unknown table '{schema}.{table}'.", position);

    /// <summary>
    /// A standalone trigger whose table the project does not declare.
    /// </summary>
    public static NsqlDiagnostic UnknownTriggerTable(SqlIdentifier schema, SqlIdentifier table, SourcePosition position) =>
        Positioned($"CREATE TRIGGER references unknown table '{schema}.{table}'.", position);

    /// <summary>
    /// A standalone index whose relation the project does not declare.
    /// </summary>
    public static NsqlDiagnostic UnknownIndexRelation(SqlIdentifier schema, SqlIdentifier relation, SourcePosition position) =>
        Positioned($"CREATE INDEX references unknown table or view '{schema}.{relation}'.", position);

    private static NsqlDiagnostic Positioned(FormattedText message, SourcePosition position) =>
        new(Source, "index-on-plain-view", $"{message} (at {position}).", DiagnosticSeverity.Error, position);

    private static string Capitalized(string prose) => char.ToUpperInvariant(prose[0]) + prose[1..];

    // ── Directive rules ──

    /// <summary>
    /// A rename whose target the project does not declare.
    /// </summary>
    public static Diagnostic RenameTargetNotDeclared(string kind, Address address, SqlIdentifier to) => Diagnostic.Error(Source, "rename-target-not-declared",
        $"Unable to rename {kind:text} '{address}' to {to}. The project does not declare '{to}'.");

    /// <summary>
    /// A rename whose previous name the project still declares.
    /// </summary>
    public static Diagnostic RenameSourceStillDeclared(string kind, Address address, SqlIdentifier to) => Diagnostic.Error(Source, "rename-source-still-declared",
        $"Unable to rename {kind:text} '{address}' to {to}. The previous name is still declared.");

    /// <summary>
    /// A directive addressing a schema the project does not declare.
    /// </summary>
    public static Diagnostic DirectiveSchemaNotDeclared(FormattedText directive, SqlIdentifier schema) => Diagnostic.Error(Source, "directive-schema-not-declared",
        $"{directive} addresses schema '{schema}', which the project does not declare.");

    /// <summary>
    /// A column rename addressing a table the project does not declare.
    /// </summary>
    public static Diagnostic DirectiveTableNotDeclared(MemberAddress reference) => Diagnostic.Error(Source, "directive-table-not-declared",
        $"RENAME COLUMN '{reference}' addresses a table the project does not declare.");

    /// <summary>
    /// A rename whose target is its own source.
    /// </summary>
    public static Diagnostic SelfRename(string kind, Address address) => Diagnostic.Error(Source, "self-rename",
        $"RENAME {kind:text} '{address}': the target is the same name.");

    /// <summary>
    /// Two renames sharing a source.
    /// </summary>
    public static Diagnostic DuplicateRenameSource(string kind, Address address) => Diagnostic.Error(Source, "duplicate-rename-source",
        $"Multiple renames of {kind:text} '{address}' declared.");

    /// <summary>
    /// Two renames sharing a target.
    /// </summary>
    public static Diagnostic DuplicateRenameTarget(string kind, Address address) => Diagnostic.Error(Source, "duplicate-rename-target",
        $"Multiple renames of {kind:text} to '{address}' declared.");

    /// <summary>
    /// One rename's target being another's source — unordered, therefore ambiguous.
    /// </summary>
    public static Diagnostic RenameChain(string kind, Address address) => Diagnostic.Error(Source, "rename-chain",
        $"Conflicting rename directives found for {kind:text} '{address}'.");
}
