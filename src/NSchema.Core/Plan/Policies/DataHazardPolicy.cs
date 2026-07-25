using Microsoft.Extensions.Options;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that flags changes which are valid against the schema but can fail at apply time depending on the data already in the table.
/// </summary>
/// <remarks>
/// Examples of diagnostics: required columns added without a default, columns tightened to NOT NULL, type changes
/// whose cast can fail, and uniqueness added over pre-existing columns. Each hazard is reported as its own
/// diagnostic, at the severity configured by <see cref="DataHazardOptions"/>.
/// </remarks>
internal sealed class DataHazardPolicy(IOptions<DataHazardOptions> options) : IPlanPolicy
{
    private const string PolicyName = "data-hazards";

    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        var diff = plan.Diff;
        if (options.Value.Policy == PolicyEnforcement.Ignore)
        {
            return [];
        }

        var severity = options.Value.Policy switch
        {
            PolicyEnforcement.Allow => DiagnosticSeverity.Info,
            PolicyEnforcement.Warn => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error,
        };

        return Hazards(diff).Select(message => new Diagnostic(PolicyName, message, severity));
    }

    private static IEnumerable<FormattedText> Hazards(DatabaseDiff diff)
    {
        // Only a modified table can hold data at apply time: an added table is empty and a removed one is gone,
        // so hazards exist nowhere else.
        foreach (var schema in diff.Schemas)
        {
            foreach (var table in schema.Tables.Where(t => t.Kind == ChangeKind.Modify))
            {
                foreach (var hazard in TableHazards(schema.Name, table))
                {
                    yield return hazard;
                }
            }
        }
    }

    private static IEnumerable<FormattedText> TableHazards(SqlIdentifier schemaName, TableDiff table)
    {
        var qualified = $"{schemaName}.{table.Name}";

        foreach (var column in table.Columns)
        {
            var path = $"{qualified}.{column.Name}";

            // Identity and generated columns compute their own values for existing rows, so only a plain required column with no default can fail the add.
            // A matched backfill migration handles the transition (the planner decomposes the add around it), so it silences this hazard.
            if (column is { Kind: ChangeKind.Add, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null }, MigrationScript: null })
            {
                yield return $"Column '{path}' is added NOT NULL without a default; the migration will fail if the table holds rows. Declaring a DEFAULT is usually the whole fix — PostgreSQL 11+ fills existing rows from it without rewriting the table.";
            }

            if (column.Kind != ChangeKind.Modify)
            {
                continue;
            }

            if (column.Nullability is { New: false })
            {
                yield return $"Column '{path}' becomes NOT NULL; the migration will fail if existing rows hold NULLs. Backfill them first.";
            }

            if (column.Type is { Old: { } oldType, New: { } newType }
                && oldType.ConversionRiskTo(newType) == TypeConversionRisk.MayFail
                && column.MigrationScript is null)
            {
                yield return $"Column '{path}' changes type from {oldType} to {newType}; the cast will fail for existing values that do not fit the new type.";
            }
        }

        // Uniqueness added over columns the table already had can collide with existing rows. Columns added in
        // this same diff start empty, so uniqueness confined to them cannot.
        var addedColumns = table.Columns
            .Where(c => c.Kind == ChangeKind.Add)
            .Select(c => c.Name)
            .ToHashSet();

        // A matched migration means the user has declared how the data gets into shape (de-duplicated, backfilled) before the constraint lands, so it silences the hazard.
        foreach (var primaryKey in table.PrimaryKeys.Where(p => p is { Kind: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(primaryKey.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return $"Primary key '{primaryKey.Name}' on '{qualified}' is added over existing {Columns(existing)}; the migration will fail if existing rows hold duplicates or NULLs.";
            }
        }

        foreach (var constraint in table.UniqueConstraints.Where(u => u is { Kind: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(constraint.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return $"Unique constraint '{constraint.Name}' on '{qualified}' is added over existing {Columns(existing)}; the migration will fail if existing rows hold duplicates.";
            }
        }

        foreach (var index in table.Indexes)
        {
            if (index is not { Kind: ChangeKind.Add, Definition: { IsUnique: true } definition })
            {
                continue;
            }

            // An expression key is opaque, so it is assumed to read pre-existing data.
            if (definition.Columns.Any(k => k.Column is not { } column || !addedColumns.Contains(column)))
            {
                yield return $"Unique index '{index.Name}' on '{qualified}' is added over existing data; the migration will fail if existing rows hold duplicates.";
            }
        }
    }

    private static List<SqlIdentifier> ExistingColumns(IReadOnlyList<SqlIdentifier>? columnNames, IReadOnlySet<SqlIdentifier> addedColumns) =>
        columnNames?.Where(c => !addedColumns.Contains(c)).ToList() ?? [];

    private static FormattedText Columns(List<SqlIdentifier> names) =>
        $"{(names.Count == 1 ? "column" : "columns"):text} {string.Join(", ", names.Select(n => $"'{n}'"))}";

}
