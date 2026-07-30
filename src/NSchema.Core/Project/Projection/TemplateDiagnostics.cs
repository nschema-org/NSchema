using NSchema.Model;

using NSchema.Project.Nsql;

namespace NSchema.Project.Projection;

/// <summary>
/// The diagnostics minted while applying templates and resolving includes.
/// </summary>
internal static class TemplateDiagnostics
{
    internal static readonly DiagnosticSource Source = "templates";

    /// <summary>
    /// The same template name declared more than once.
    /// </summary>
    /// <summary>
    /// A template body that qualifies an object with a schema, so it could not be applied to any other.
    /// </summary>
    public static NsqlDiagnostic QualifiedTemplateObject(SqlIdentifier templateName, SqlIdentifier schemaName, SourcePosition position) =>
        new(Source, "qualified-template-object",
            $"Template '{templateName}' declares objects in schema '{schemaName}'; objects inside a template must use "
            + "unqualified names so they are created in each schema the template is applied to.",
            DiagnosticSeverity.Error, position);

    public static Diagnostic DuplicateTemplate(SqlIdentifier name) => Diagnostic.Error(Source, "duplicate-template",
        $"Duplicate template '{name}' declared.");

    /// <summary>
    /// An APPLY TEMPLATE referencing a template that does not exist.
    /// </summary>
    public static Diagnostic UnknownTemplate(SqlIdentifier name) => Diagnostic.Error(Source, "unknown-template",
        $"APPLY TEMPLATE references unknown template '{name}'.");

    /// <summary>
    /// An APPLY TEMPLATE naming a table template (those are consumed via INCLUDE).
    /// </summary>
    public static Diagnostic AppliedTableTemplate(SqlIdentifier name) => Diagnostic.Error(Source, "applied-table-template",
        $"APPLY TEMPLATE targets schemas, but '{name}' is a table template; include it from a table body with INCLUDE.");

    /// <summary>
    /// An APPLY TEMPLATE naming a schema the project does not declare.
    /// </summary>
    public static Diagnostic UnknownTargetSchema(SqlIdentifier templateName, SqlIdentifier schemaName) => Diagnostic.Error(Source, "unknown-target-schema",
        $"APPLY TEMPLATE '{templateName}' targets unknown schema '{schemaName}'; declare it with CREATE SCHEMA.");

    /// <summary>
    /// An INCLUDE inside a template body targeting a table that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTable(SqlIdentifier templateName, ObjectAddress table) => Diagnostic.Error(Source, "include-unknown-table",
        $"INCLUDE '{templateName}' targets unknown table '{table}'.");

    /// <summary>
    /// A table INCLUDE naming a template that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTemplate(SqlIdentifier schemaName, SqlIdentifier tableName, SqlIdentifier templateName) => Diagnostic.Error(Source, "include-unknown-template",
        $"Table '{schemaName}.{tableName}' includes unknown template '{templateName}'.");

    /// <summary>
    /// A table INCLUDE naming a schema template (only FOR TABLE templates can be included).
    /// </summary>
    public static Diagnostic IncludedSchemaTemplate(SqlIdentifier schemaName, SqlIdentifier tableName, SqlIdentifier templateName) => Diagnostic.Error(Source, "included-schema-template",
        $"Table '{schemaName}.{tableName}' includes '{templateName}', which is a schema template; only a FOR TABLE template can be included.");

}
