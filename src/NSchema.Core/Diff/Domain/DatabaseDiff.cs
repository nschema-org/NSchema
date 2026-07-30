using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Extensions;
using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Services;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Scripts;
using NSchema.Model.Services;

namespace NSchema.Diff.Domain;

/// <summary>
/// The complete difference between the current and desired states.
/// </summary>
/// <param name="Schemas">The changed schemas.</param>
/// <param name="Extensions">The changed database-global extensions.</param>
public sealed record DatabaseDiff(IReadOnlyList<SchemaDiff>? Schemas = null, IReadOnlyList<ExtensionDiff>? Extensions = null)
{
    /// <summary>
    /// The changed schemas.
    /// </summary>
    public IReadOnlyList<SchemaDiff> Schemas { get; init; } = Schemas ?? [];

    /// <summary>
    /// The changed database-global extensions.
    /// </summary>
    public IReadOnlyList<ExtensionDiff> Extensions { get; init; } = Extensions ?? [];

    /// <summary>
    /// The deployment scripts to run, in declaration order.
    /// </summary>
    public IReadOnlyList<DeploymentScript> DeploymentScripts { get; init; } = [];

    /// <summary>
    /// The change-event scripts attached to the diff's nodes, in walk order.
    /// </summary>
    public IEnumerable<ChangeScript> ChangeScripts() => Schemas
        .SelectMany(schema => schema.Tables)
        .SelectMany(table => table.EnumerateMembers())
        .OfType<IMigratableDiff>()
        .Select(member => member.MigrationScript)
        .OfType<ChangeScript>();

    /// <summary>
    /// Gets a value indicating whether the diff contains no changes at all.
    /// </summary>
    public bool IsEmpty => Schemas.Count == 0 && Extensions.Count == 0 && DeploymentScripts.Count == 0;

    /// <summary>
    /// Gets the aggregate counts of every changed element, grouped by <see cref="ChangeKind"/>.
    /// </summary>
    public DiffSummary GetSummary()
    {
        var added = 0;
        var modified = 0;
        var removed = 0;

        foreach (var extension in Extensions)
        {
            Tally(extension.Change);
        }

        foreach (var schema in Schemas)
        {
            if (schema.Change != ChangeKind.Touched)
            {
                Tally(schema.Change);
            }

            // Every changed object counts once, regardless of kind; a table's members then count individually.
            foreach (var obj in schema.EnumerateObjects())
            {
                Tally(obj.Change);
            }

            foreach (var member in schema.Tables.SelectMany(t => t.EnumerateMembers()))
            {
                Tally(member.Change);
            }
        }

        return new DiffSummary(added, modified, removed);

        void Tally(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Add: added++; break;
                case ChangeKind.Modify: modified++; break;
                case ChangeKind.Remove: removed++; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }

    /// <summary>
    /// Restricts the diff to <paramref name="scope"/>, adding back what its removals sever beyond it. A removal
    /// only a data-bearing column depends on cannot be widened over and fails the result instead.
    /// </summary>
    /// <param name="scope">The schemas in play.</param>
    /// <param name="current">The whole current database — the closure has to see past the scope to be right.</param>
    public Result<DatabaseDiff> ScopedTo(PlanningScope scope, Database current)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        var narrowed = this with { Schemas = [.. Schemas.Select(s => s.ScopedTo(scope)).OfType<SchemaDiff>()] };

        var graph = new DependencyGraph(current);
        List<Diagnostic> diagnostics = [];

        // An added foreign key cannot reach a table this run will neither create nor find. Leaving it out keeps
        // the plan applyable — a constraint is a definition, re-creatable once the target is in play.
        if (UnreachableForeignKeys(narrowed, scope, graph) is { Count: > 0 } unreachable)
        {
            narrowed = WithoutForeignKeys(narrowed, unreachable);
            diagnostics.Add(DiffDiagnostics.ForeignKeyTargetOutOfScope(unreachable));
        }

        // A type is part of the shape of what names it, so there is nothing to leave out: an addition reaching
        // a type this run will neither create nor find blocks the plan, which is still carried for review.
        if (UnreachableTypes(narrowed, scope, graph) is { Count: > 0 } unreachableTypes)
        {
            diagnostics.Add(DiffDiagnostics.TypeTargetOutOfScope(
                unreachableTypes.Select(t => t.Dependent).Distinct(),
                unreachableTypes.Select(t => (Address)t.Type).Distinct()));
        }

        var removals = Removals(narrowed).ToList();

        var severed = graph.AllDependentsOf(removals).Where(OutOfScope).ToList();
        if (severed.Count == 0)
        {
            return Result.From(narrowed, diagnostics);
        }

        // Everything reached without believing a guess is asserted; the remainder is hedged, because an edge
        // scanned out of a view body or bound by a bare type name can be wrong, and here a wrong edge removes
        // something that need not go.
        var stated = graph.StatedDependentsOf(removals).Where(OutOfScope).ToHashSet();

        // Closure severs definitions, never data: a constraint, view, domain, or composite type is re-creatable
        // from its declaration, but a column stands for its table's rows — so a column dependent blocks the
        // removal instead of widening the plan.
        var (blocked, severable) = (Columns(severed), Others(severed));

        if (Others(stated) is { Count: > 0 } statedSeverable)
        {
            diagnostics.Add(DiffDiagnostics.SeveredOutOfScope(statedSeverable.Select(n => n.Address)));
        }
        if (severable.Where(n => !stated.Contains(n)).ToList() is { Count: > 0 } inferredSeverable)
        {
            diagnostics.Add(DiffDiagnostics.InferredSeveredOutOfScope(inferredSeverable.Select(n => n.Address)));
        }
        if (Columns(stated) is { Count: > 0 } statedBlocked)
        {
            diagnostics.Add(DiffDiagnostics.ColumnBlocksRemoval(statedBlocked.Select(n => n.Address)));
        }
        if (blocked.Where(n => !stated.Contains(n)).ToList() is { Count: > 0 } inferredBlocked)
        {
            diagnostics.Add(DiffDiagnostics.InferredColumnMayBlockRemoval(inferredBlocked.Select(n => n.Address)));
        }

        return Result.From(Widen(narrowed, severable), diagnostics);

        bool OutOfScope(DependencyNode node) => node.Address switch
        {
            ObjectAddress address => !scope.Contains(address),
            MemberAddress member => !scope.Contains(member.Owner),
            _ => false,
        };

        static List<DependencyNode> Columns(IEnumerable<DependencyNode> nodes) =>
            [.. nodes.Where(n => n.Kind == DependencyKind.Column)];

        static List<DependencyNode> Others(IEnumerable<DependencyNode> nodes) =>
            [.. nodes.Where(n => n.Kind != DependencyKind.Column)];
    }

    /// <summary>
    /// The foreign keys this run adds whose referenced table it will neither create (out of scope) nor find
    /// (absent from the current database).
    /// </summary>
    private static List<MemberAddress> UnreachableForeignKeys(DatabaseDiff diff, PlanningScope scope, DependencyGraph graph) =>
    [
        .. from schema in diff.Schemas
           from table in schema.Tables
           from key in table.ForeignKeys
           where key.Change == ChangeKind.Add
               && key.Definition is { } definition
               && !scope.Contains(definition.References)
               && graph.At(definition.References).Count == 0
           select new MemberAddress(table.Schema, table.Name, key.Name)
    ];

    /// <summary>
    /// The additions naming a user type this run will neither create (out of scope) nor find (absent from the
    /// current database).
    /// </summary>
    private static List<(Address Dependent, ObjectAddress Type)> UnreachableTypes(DatabaseDiff diff, PlanningScope scope, DependencyGraph graph) =>
        [.. IntroducedTypes(diff).Where(t => !scope.Contains(t.Type) && graph.At(t.Type).Count == 0)];

    /// <summary>
    /// Every user type this diff newly names, with what names it. Only a schema-qualified type names its target
    /// outright — a bare name is resolved against what already exists, so it can only reach something present.
    /// </summary>
    private static IEnumerable<(Address Dependent, ObjectAddress Type)> IntroducedTypes(DatabaseDiff diff)
    {
        foreach (var schema in diff.Schemas)
        {
            foreach (var table in schema.Tables)
            {
                // A created table carries its columns inline; a modified one carries them as column changes.
                if (table.Definition is { } created)
                {
                    foreach (var column in created.Columns)
                    {
                        if (Named(column.Type) is { } type)
                        {
                            yield return (new MemberAddress(table.Schema, table.Name, column.Name), type);
                        }
                    }
                }
                else
                {
                    foreach (var column in table.Columns)
                    {
                        if (Named(column.Change == ChangeKind.Add ? column.Definition?.Type : column.Type?.New) is { } type)
                        {
                            yield return (new MemberAddress(table.Schema, table.Name, column.Name), type);
                        }
                    }
                }
            }

            foreach (var domain in schema.Domains)
            {
                if (Named(domain.Change == ChangeKind.Add ? domain.Definition?.DataType : domain.DataType?.New) is { } type)
                {
                    yield return (new ObjectAddress(domain.Schema, domain.Name), type);
                }
            }

            foreach (var composite in schema.CompositeTypes)
            {
                var address = new ObjectAddress(composite.Schema, composite.Name);
                var fields = composite.Definition is { } createdType
                    ? createdType.Fields.Select(f => f.DataType)
                    : composite.Fields.Select(f => f.Definition?.DataType);
                foreach (var field in fields)
                {
                    if (Named(field) is { } type)
                    {
                        yield return (address, type);
                    }
                }
            }
        }

        static ObjectAddress? Named(SqlType? type) =>
            type?.Schema is { } schema ? new ObjectAddress(schema, type.Name) : null;
    }

    /// <summary>
    /// Drops the named foreign keys from the diff. A created table carries its constraints inline on its
    /// definition, so those lose them there too.
    /// </summary>
    private static DatabaseDiff WithoutForeignKeys(DatabaseDiff diff, IEnumerable<MemberAddress> dropped)
    {
        var drop = dropped.ToHashSet();
        return diff with
        {
            Schemas = [.. diff.Schemas.Select(schema => schema with { Tables = [.. schema.Tables.Select(Strip)] })],
        };

        TableDiff Strip(TableDiff table)
        {
            var kept = table.ForeignKeys.Where(k => !Dropped(table, k.Name)).ToList();
            if (kept.Count == table.ForeignKeys.Count)
            {
                return table;
            }

            var definition = table.Definition;
            if (definition is not null)
            {
                definition = definition.Clone();
                foreach (var key in definition.ForeignKeys.Where(k => Dropped(table, k.Name)).ToList())
                {
                    definition.ForeignKeys.Remove(key);
                }
            }

            return table with { ForeignKeys = kept, Definition = definition };
        }

        bool Dropped(TableDiff table, SqlIdentifier name) => drop.Contains(new MemberAddress(table.Schema, table.Name, name));
    }

    /// <summary>
    /// The nodes <paramref name="diff"/> removes — the seeds of what its removals cost.
    /// </summary>
    private static IEnumerable<DependencyNode> Removals(DatabaseDiff diff) =>
        diff.Schemas.SelectMany(schema =>
            Removed(schema.Tables, schema.Name, DependencyKind.Table)
                .Concat(Removed(schema.Views, schema.Name, DependencyKind.View))
                .Concat(Removed(schema.Enums, schema.Name, DependencyKind.Enum))
                .Concat(Removed(schema.Domains, schema.Name, DependencyKind.Domain))
                .Concat(Removed(schema.CompositeTypes, schema.Name, DependencyKind.CompositeType)));

    private static IEnumerable<DependencyNode> Removed(IEnumerable<ISchemaObjectDiff> objects, SqlIdentifier schema, DependencyKind kind) =>
        objects.Where(o => o.Change == ChangeKind.Remove)
            .Select(o => new DependencyNode(new ObjectAddress(schema, o.Name), kind));

    // Every dependency node names an object or one of its members, so a severed one always sits in a schema.
    private static SqlIdentifier SchemaOf(Address address) => address switch
    {
        ObjectAddress o => o.Schema,
        MemberAddress m => m.Schema,
        _ => throw new ArgumentOutOfRangeException(nameof(address), address, "A dependency node is not schema-scoped."),
    };

    /// <summary>
    /// Folds the severed nodes back in as changes to the schemas they live in.
    /// </summary>
    /// <remarks>
    /// A severed schema is only ever touched, never dropped, so its <see cref="SchemaDiff.Change"/> stays
    /// <see cref="ChangeKind.Touched"/>:
    /// the run is not about this schema, it just cannot avoid it. A partially-covered schema may already be
    /// in the diff, in which case the severed changes join its entry.
    /// </remarks>
    private static DatabaseDiff Widen(DatabaseDiff diff, IReadOnlyList<DependencyNode> severed)
    {
        var schemas = diff.Schemas.ToList();

        var schemaScoped = severed.Select(node => (Schema: SchemaOf(node.Address), Node: node));

        foreach (var bySchema in schemaScoped.GroupBy(x => x.Schema, x => x.Node))
        {
            var addition = SchemaDiff.Containing(bySchema.Key) with
            {
                Tables = [.. SeveredTables(bySchema)],
                Views = [.. bySchema
                    .Where(n => n.Kind == DependencyKind.View)
                    .Select(n => ViewDiff.Removed(bySchema.Key, ((ObjectAddress)n.Address).Name))
                    .OrderBy(v => v.Name)],
                Domains = [.. bySchema
                    .Where(n => n.Kind == DependencyKind.Domain)
                    .Select(n => DomainDiff.Removed(bySchema.Key, ((ObjectAddress)n.Address).Name))
                    .OrderBy(d => d.Name)],
                CompositeTypes = [.. bySchema
                    .Where(n => n.Kind == DependencyKind.CompositeType)
                    .Select(n => CompositeTypeDiff.Removed(bySchema.Key, ((ObjectAddress)n.Address).Name))
                    .OrderBy(c => c.Name)],
            };

            var index = schemas.FindIndex(s => s.Name == bySchema.Key);
            if (index < 0)
            {
                schemas.Add(addition);
            }
            else
            {
                schemas[index] = schemas[index] with
                {
                    Tables = [.. schemas[index].Tables, .. addition.Tables],
                    Views = [.. schemas[index].Views, .. addition.Views],
                    Domains = [.. schemas[index].Domains, .. addition.Domains],
                    CompositeTypes = [.. schemas[index].CompositeTypes, .. addition.CompositeTypes],
                };
            }
        }

        return diff with { Schemas = schemas };
    }

    /// <summary>
    /// Rebuilds each disturbed table as a modification carrying only the constraints that must go.
    /// </summary>
    private static IEnumerable<TableDiff> SeveredTables(IEnumerable<DependencyNode> severed) => severed
        .Where(n => n.Kind == DependencyKind.ForeignKey)
        .Select(n => (MemberAddress)n.Address)
        .GroupBy(a => (a.Schema, a.Object))
        .Select(byTable => TableDiff.Modified(byTable.Key.Schema, byTable.Key.Object) with
        {
            ForeignKeys = [.. byTable.Select(a => ForeignKeyDiff.Removed(a.Member)).OrderBy(f => f.Name)],
        })
        .OrderBy(t => t.Name);
}
