using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Project.Policies;

/// <summary>
/// Reports likely schema mistakes that are still valid DDL:
/// - tables without a primary key,
/// - nullable columns used in a primary key,
/// - and columns repeated within a single key or index.
/// These are advisory, so they are reported as <see cref="DiagnosticSeverity.Warning"/> and do not fail validation.
/// </summary>
internal sealed class SchemaLintPolicy : IProjectPolicy
{
    /// <inheritdoc />
    public IEnumerable<Diagnostic> Validate(ProjectDefinition project)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var definition in project.Database.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                ValidateTable(definition, table, diagnostics);
            }
        }

        return diagnostics;
    }

    private static void ValidateTable(Schema schema, Table table, List<Diagnostic> diagnostics)
    {
        var address = new ObjectAddress(schema.Name, table.Name, table.Kind);

        if (table.PrimaryKey is not { } primaryKey)
        {
            diagnostics.Add(SchemaLintDiagnostics.MissingPrimaryKey(address));
        }
        else
        {
            var nullableColumns = table.Columns.Where(c => c.IsNullable).Select(c => c.Name).ToHashSet();
            foreach (var column in primaryKey.ColumnNames.Where(nullableColumns.Contains))
            {
                diagnostics.Add(SchemaLintDiagnostics.NullablePrimaryKeyColumn(address.Member(column)));
            }

            ReportDuplicates(diagnostics, "primary key", address.Member(primaryKey.Name), primaryKey.ColumnNames);
        }

        foreach (var index in table.Indexes)
        {
            // Duplicate-column linting applies to plain-column keys; expression keys are opaque.
            ReportDuplicates(diagnostics, "index", address.Member(index.Name),
                index.Columns.Select(c => c.Column).OfType<SqlIdentifier>().ToList());
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            ReportDuplicates(diagnostics, "foreign key", address.Member(foreignKey.Name), foreignKey.ColumnNames);
        }
    }

    private static void ReportDuplicates(
        List<Diagnostic> diagnostics, string kind, MemberAddress key, IEnumerable<SqlIdentifier> columnNames)
    {
        var duplicates = columnNames
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            diagnostics.Add(SchemaLintDiagnostics.RepeatedColumn(kind, key, duplicate));
        }
    }
}
