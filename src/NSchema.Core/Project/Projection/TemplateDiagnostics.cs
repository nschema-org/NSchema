using NSchema.Model;

namespace NSchema.Project.Projection;

/// <summary>
/// The diagnostics minted while applying templates and resolving includes.
/// </summary>
internal static class TemplateDiagnostics
{
    private const string Source = "templates";

    /// <summary>
    /// The same template name declared more than once.
    /// </summary>
    public static Diagnostic DuplicateTemplate(SqlIdentifier name) => Diagnostic.Error(Source,
        $"Duplicate template '{name}' declared.");

    /// <summary>
    /// An APPLY TEMPLATE referencing a template that does not exist.
    /// </summary>
    public static Diagnostic UnknownTemplate(SqlIdentifier name) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE references unknown template '{name}'.");

    /// <summary>
    /// An APPLY TEMPLATE naming a table template (those are consumed via INCLUDE).
    /// </summary>
    public static Diagnostic AppliedTableTemplate(SqlIdentifier name) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE targets schemas, but '{name}' is a table template; include it from a table body with INCLUDE.");

    /// <summary>
    /// An APPLY TEMPLATE naming a schema the project does not declare.
    /// </summary>
    public static Diagnostic UnknownTargetSchema(SqlIdentifier templateName, SqlIdentifier schemaName) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE '{templateName}' targets unknown schema '{schemaName}'; declare it with CREATE SCHEMA.");

    /// <summary>
    /// An INCLUDE inside a template body targeting a table that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTable(SqlIdentifier templateName, ObjectAddress table) => Diagnostic.Error(Source,
        $"INCLUDE '{templateName}' targets unknown table '{table}'.");

    /// <summary>
    /// A table INCLUDE naming a template that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTemplate(SqlIdentifier schemaName, SqlIdentifier tableName, SqlIdentifier templateName) => Diagnostic.Error(Source,
        $"Table '{schemaName}.{tableName}' includes unknown template '{templateName}'.");

    /// <summary>
    /// A table INCLUDE naming a schema template (only FOR TABLE templates can be included).
    /// </summary>
    public static Diagnostic IncludedSchemaTemplate(SqlIdentifier schemaName, SqlIdentifier tableName, SqlIdentifier templateName) => Diagnostic.Error(Source,
        $"Table '{schemaName}.{tableName}' includes '{templateName}', which is a schema template; only a FOR TABLE template can be included.");

}
