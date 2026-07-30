using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that flags changes which are valid against the schema but can fail at apply time depending on the data already in the table.
/// </summary>
internal sealed class DataHazardPolicy : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan) => Hazards(plan.Diff);

    private static IEnumerable<Diagnostic> Hazards(DatabaseDiff diff)
    {
        // Only a modified table can hold data at apply time: an added table is empty and a removed one is gone,
        // so hazards exist nowhere else.
        foreach (var schema in diff.Schemas)
        {
            foreach (var table in schema.Tables.Where(t => t.Change == ChangeKind.Modify))
            {
                foreach (var hazard in TableHazards(table))
                {
                    yield return hazard;
                }
            }
        }
    }

    private static IEnumerable<Diagnostic> TableHazards(TableDiff table)
    {
        foreach (var column in table.Columns)
        {
            var path = table.Address.Member(column.Name);

            // Identity and generated columns compute their own values for existing rows, so only a plain required column with no default can fail the add.
            // A matched backfill migration handles the transition (the planner decomposes the add around it), so it silences this hazard.
            if (column is { Change: ChangeKind.Add, Definition: { IsNullable: false, DefaultExpression: null, IsIdentity: false, GeneratedExpression: null }, MigrationScript: null })
            {
                yield return DataHazardDiagnostics.RequiredColumnWithoutDefault(path);
            }

            if (column.Change != ChangeKind.Modify)
            {
                continue;
            }

            if (column.Nullability is { New: false })
            {
                yield return DataHazardDiagnostics.ColumnBecomesRequired(path);
            }

            if (column.Type is { Old: { } oldType, New: { } newType }
                && oldType.ConversionRiskTo(newType) == TypeConversionRisk.MayFail
                && column.MigrationScript is null)
            {
                yield return DataHazardDiagnostics.RiskyTypeChange(path, oldType, newType);
            }
        }

        // Uniqueness added over columns the table already had can collide with existing rows. Columns added in
        // this same diff start empty, so uniqueness confined to them cannot.
        var addedColumns = table.Columns
            .Where(c => c.Change == ChangeKind.Add)
            .Select(c => c.Name)
            .ToHashSet();

        // A matched migration means the user has declared how the data gets into shape (de-duplicated, backfilled) before the constraint lands, so it silences the hazard.
        foreach (var primaryKey in table.PrimaryKeys.Where(p => p is { Change: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(primaryKey.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return DataHazardDiagnostics.PrimaryKeyOverExistingData(table.Address.Member(primaryKey.Name), existing);
            }
        }

        foreach (var constraint in table.UniqueConstraints.Where(u => u is { Change: ChangeKind.Add, MigrationScript: null }))
        {
            var existing = ExistingColumns(constraint.Definition?.ColumnNames, addedColumns);
            if (existing.Count > 0)
            {
                yield return DataHazardDiagnostics.UniqueConstraintOverExistingData(table.Address.Member(constraint.Name), existing);
            }
        }

        foreach (var index in table.Indexes)
        {
            if (index is not { Change: ChangeKind.Add, Definition: { IsUnique: true } definition })
            {
                continue;
            }

            // An expression key is opaque, so it is assumed to read pre-existing data.
            if (definition.Columns.Any(k => k.Column is not { } column || !addedColumns.Contains(column)))
            {
                yield return DataHazardDiagnostics.UniqueIndexOverExistingData(table.Address.Member(index.Name));
            }
        }
    }

    private static List<SqlIdentifier> ExistingColumns(IReadOnlyList<SqlIdentifier>? columnNames, IReadOnlySet<SqlIdentifier> addedColumns) =>
        columnNames?.Where(c => !addedColumns.Contains(c)).ToList() ?? [];
}
