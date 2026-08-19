using NSchema.Model;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Domain.Directives;

namespace NSchema.Project.Policies;

/// <summary>
/// Validates that every primary key, index, and foreign key references columns and tables that actually exist within the document.
/// </summary>
internal sealed class StructuralIntegrityPolicy : IProjectPolicy
{
    /// <inheritdoc />
    public IEnumerable<Diagnostic> Validate(ProjectDefinition project)
    {
        var database = project.Database;
        var declaredSchemas = database.Schemas.Select(s => s.Name).ToHashSet();
        var tablesByKey = database.Objects<Table>()
            .GroupBy(x => new ObjectAddress(x.Schema, x.Object.Name))
            .ToDictionary(g => g.Key, g => g.First().Object);

        var diagnostics = new List<Diagnostic>();
        foreach (var definition in database.Schemas)
        {
            foreach (var table in definition.Tables)
            {
                ValidateTable(definition, table, declaredSchemas, tablesByKey, diagnostics);
            }

            ValidateObjectNames(definition, diagnostics);
            ValidateRoutineNames(definition, diagnostics);
            ValidateIndexNames(definition, diagnostics);
            ValidateClustering(definition, diagnostics);
        }

        return diagnostics;
    }

    // A clustered index is the relation's row order rather than a structure beside it, so a relation has at
    // most one. Catching the second here beats letting the engine reject it at apply time.
    private static void ValidateClustering(Schema definition, List<Diagnostic> diagnostics)
    {
        var relations = definition.Tables
            .Select(t => (Relation: t.Name, Members: IndexBacked(t)))
            .Concat(definition.Views
                .Where(v => v.IsMaterialized)
                .Select(v => (Relation: v.Name, Members: v.Indexes.Cast<ObjectMember>())));

        foreach (var (relation, members) in relations)
        {
            if (members.Where(IsClustered).ToList() is { Count: > 1 } clustered)
            {
                var sites = string.Join(", ", clustered.Select(m => $"{m.Kind.Display()} '{m.Name}'"));
                diagnostics.Add(StructuralIntegrityDiagnostics.MultipleClusteredIndexes(
                    new ObjectAddress(definition.Name, relation), sites));
            }
        }

        static bool IsClustered(ObjectMember member) => member switch
        {
            TableIndex index => index.Clustered is true,
            PrimaryKey key => key.Clustered is true,
            UniqueConstraint unique => unique.Clustered is true,
            _ => false,
        };
    }

    // Index names are schema-scoped in the database (indexes live in pg_class alongside tables).
    private static void ValidateIndexNames(Schema definition, List<Diagnostic> diagnostics)
    {
        var named = definition.Tables
            .SelectMany(t => IndexBacked(t).Select(m => (m.Name, m.Kind, On: t.Name)))
            .Concat(definition.Views.Where(v => v.IsMaterialized)
                .SelectMany(v => v.Indexes.Select(i => (i.Name, i.Kind, On: v.Name))));

        foreach (var collision in named.GroupBy(x => x.Name).Where(g => g.Count() > 1))
        {
            var sites = string.Join(", ", collision.Select(x => $"{x.Kind.Display()} on '{new ObjectAddress(definition.Name, x.On)}'"));
            diagnostics.Add(StructuralIntegrityDiagnostics.DuplicateIndexName(DatabaseAddress.Schema(definition.Name), collision.Key, sites));
        }
    }

    // Tables, views, materialized views, sequences, and composite types all occupy a single name space per
    // schema in the database (Postgres's pg_class), and they additionally share pg_type with enums and domains
    // (every relation has a row type), so none of these kinds may reuse a name within a schema — a table and a
    // view called 'foo' cannot coexist. Routines live in a separate name space (pg_proc) and are checked apart.
    private static void ValidateObjectNames(Schema definition, List<Diagnostic> diagnostics)
    {
        var named = definition.Tables.Select(t => (t.Name, Kind: "table"))
            .Concat(definition.Views.Select(v => (v.Name, Kind: v.IsMaterialized ? "materialized view" : "view")))
            .Concat(definition.Sequences.Select(s => (s.Name, Kind: "sequence")))
            .Concat(definition.CompositeTypes.Select(c => (c.Name, Kind: "composite type")))
            .Concat(definition.Enums.Select(e => (e.Name, Kind: "enum")))
            .Concat(definition.Domains.Select(d => (d.Name, Kind: "domain")));

        foreach (var collision in named.GroupBy(x => x.Name).Where(g => g.Count() > 1))
        {
            var kinds = collision.Select(x => x.Kind).ToList();
            // A single kind appearing twice (e.g. two sequences) reads as a plain duplicate; a mix of kinds reads
            // as a name-space collision. Either way the database would reject it.
            var address = new ObjectAddress(definition.Name, collision.Key);
            diagnostics.Add(kinds.Distinct().Count() == 1
                ? StructuralIntegrityDiagnostics.DuplicateObjectName(address, kinds[0])
                : StructuralIntegrityDiagnostics.CollidingObjectName(address));
        }
    }

    // Functions and procedures share one name space, as they do in the database, so they live in a single
    // routine list; a single duplicate-name check covers both same-kind duplicates and function/procedure
    // collisions. The DDL parser and document aggregation enforce this for parsed schemas; this is the catch-all
    // for code-built ones.
    private static void ValidateRoutineNames(Schema definition, List<Diagnostic> diagnostics)
    {
        foreach (var duplicate in Duplicates(definition.Routines.Select(r => r.Name)))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.DuplicateRoutineName(new ObjectAddress(definition.Name, duplicate)));
        }
    }

    private static void ValidateTable(
        Schema definition,
        Table table,
        HashSet<SqlIdentifier> declaredSchemas,
        IReadOnlyDictionary<ObjectAddress, Table> tablesByKey,
        List<Diagnostic> diagnostics)
    {
        var address = new ObjectAddress(definition.Name, table.Name, table.Kind);
        var columns = table.Columns.Select(c => c.Name).ToHashSet();

        if (table.Columns.Count == 0)
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.EmptyTable(address));
        }

        foreach (var duplicate in Duplicates(table.Columns.Select(c => c.Name)))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.DuplicateColumn(address.Member(duplicate)));
        }

        // A generated column is computed from an expression, so it cannot also carry a default — the database
        // rejects a column that declares both.
        foreach (var column in table.Columns.Where(c => c.DefaultExpression is not null && c.GeneratedExpression is not null))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.DefaultOnGeneratedColumn(address.Member(column.Name)));
        }

        if (table.PrimaryKey is { } primaryKey)
        {
            foreach (var missing in primaryKey.ColumnNames.Where(c => !columns.Contains(c)))
            {
                diagnostics.Add(StructuralIntegrityDiagnostics.UnknownPrimaryKeyColumn(address.Member(primaryKey.Name), missing));
            }
        }

        foreach (var index in table.Indexes)
        {
            // Only plain-column keys (and covering INCLUDE columns) reference table columns directly; an
            // expression key (e.g. (lower(email))) names columns inside opaque text we don't parse.
            var referenced = index.Columns.Select(c => c.Column).OfType<SqlIdentifier>().Concat(index.Include);
            foreach (var missing in referenced.Where(c => !columns.Contains(c)))
            {
                diagnostics.Add(StructuralIntegrityDiagnostics.UnknownIndexColumn(address.Member(index.Name), missing));
            }
        }

        foreach (var foreignKey in table.ForeignKeys)
        {
            ValidateForeignKey(address, foreignKey, columns, declaredSchemas, tablesByKey, diagnostics);
        }
    }

    private static void ValidateForeignKey(
        ObjectAddress table,
        ForeignKey foreignKey,
        HashSet<SqlIdentifier> localColumns,
        HashSet<SqlIdentifier> declaredSchemas,
        IReadOnlyDictionary<ObjectAddress, Table> tablesByKey,
        List<Diagnostic> diagnostics)
    {
        var address = table.Member(foreignKey.Name);

        foreach (var missing in foreignKey.ColumnNames.Where(c => !localColumns.Contains(c)))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.UnknownLocalColumn(address, missing));
        }

        if (foreignKey.ColumnNames.Count != foreignKey.ReferencedColumnNames.Count)
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.ForeignKeyArityMismatch(
                address, foreignKey.ColumnNames.Count, foreignKey.ReferencedColumnNames.Count));
            return;
        }

        // Only resolve targets in schemas this project declares.
        if (!declaredSchemas.Contains(foreignKey.References.Schema))
        {
            return;
        }

        // An undeclared table is not necessarily missing, it may just be unmanaged.
        var target = foreignKey.References;
        if (!tablesByKey.TryGetValue(foreignKey.References, out var referencedTable))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.UndeclaredForeignKeyTarget(address, target));
            return;
        }

        var referencedColumns = referencedTable.Columns.Select(c => c.Name).ToHashSet();
        var missingReferenced = foreignKey.ReferencedColumnNames.Where(c => !referencedColumns.Contains(c)).ToList();
        foreach (var missing in missingReferenced)
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.UnknownReferencedColumn(address, target.Member(missing)));
        }

        // A foreign key must reference a uniquely-constrained set of columns; check only once the target columns resolve.
        if (missingReferenced.Count == 0 && !IsUniquelyConstrained(referencedTable, foreignKey.ReferencedColumnNames))
        {
            diagnostics.Add(StructuralIntegrityDiagnostics.ForeignKeyTargetNotUnique(
                address, target, foreignKey.ReferencedColumnNames));
        }
    }

    private static bool IsUniquelyConstrained(Table table, IReadOnlyList<SqlIdentifier> columnNames)
    {
        var referenced = new HashSet<SqlIdentifier>(columnNames);

        if (table.PrimaryKey is { } primaryKey && referenced.SetEquals(primaryKey.ColumnNames))
        {
            return true;
        }

        // A partial (predicated) unique index cannot back a foreign key, and an expression index cannot either
        // (its keys aren't plain columns), so neither counts. INCLUDE columns aren't part of the uniqueness key,
        // so they don't affect the match.
        return table.Indexes.Any(i => i is { IsUnique: true, Predicate: null }
            && i.Columns.All(c => c.Column is not null)
            && referenced.SetEquals(i.Columns.Select(c => c.Column).OfType<SqlIdentifier>()));
    }

    // The members that occupy the schema's index name space: an index, and the constraints the database backs
    // with one. A check constraint has no index, so it is not among them.
    private static IEnumerable<ObjectMember> IndexBacked(Table table) =>
        table.Indexes.Cast<ObjectMember>()
            .Concat(table.PrimaryKey is { } primaryKey ? [primaryKey] : [])
            .Concat(table.UniqueConstraints)
            .Concat(table.ExclusionConstraints);

    private static IEnumerable<SqlIdentifier> Duplicates(IEnumerable<SqlIdentifier> names) => names
        .GroupBy(n => n)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);
}
