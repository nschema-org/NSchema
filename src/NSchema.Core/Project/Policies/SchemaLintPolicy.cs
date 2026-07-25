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
    private const string PolicyName = "schema-lint";

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
        var qualified = $"{schema.Name}.{table.Name}";

        if (table.PrimaryKey is not { } primaryKey)
        {
            diagnostics.Add(Warning($"Table '{qualified}' has no primary key."));
        }
        else
        {
            var nullableColumns = table.Columns.Where(c => c.IsNullable).Select(c => c.Name).ToHashSet();
            foreach (var column in primaryKey.ColumnNames.Where(nullableColumns.Contains))
            {
                diagnostics.Add(Warning($"Column '{column}' on '{qualified}' is part of the primary key but is declared nullable; it will be forced NOT NULL."));
            }

            ReportDuplicates(diagnostics, qualified, $"primary key '{primaryKey.Name}'", primaryKey.ColumnNames);
        }

        foreach (var index in table.Indexes)
        {
            // Duplicate-column linting applies to plain-column keys; expression keys are opaque.
            ReportDuplicates(diagnostics, qualified, $"index '{index.Name}'",
                index.Columns.Select(c => c.Column).OfType<SqlIdentifier>().ToList());
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            ReportDuplicates(diagnostics, qualified, $"foreign key '{foreignKey.Name}'", foreignKey.ColumnNames);
        }
    }

    private static void ReportDuplicates(
        List<Diagnostic> diagnostics, string qualified, string owner, IEnumerable<SqlIdentifier> columnNames)
    {
        var duplicates = columnNames
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            diagnostics.Add(Warning($"The {owner} on '{qualified}' lists column '{duplicate}' more than once."));
        }
    }

    private static Diagnostic Warning(string message) => Diagnostic.Warning(PolicyName, message);
}
