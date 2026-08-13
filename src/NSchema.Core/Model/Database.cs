using System.Diagnostics;
using NSchema.Model.Domains;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;

namespace NSchema.Model;

/// <summary>
/// Represents the overall structure of a database.
/// </summary>
[DebuggerDisplay("{Schemas.Count} schemas")]
public sealed class Database : IEquatable<Database>
{
    /// <summary>
    /// A list of Schema objects, each representing a specific schema within the database.
    /// </summary>
    public List<Schema> Schemas { get; init; } = [];

    /// <summary>
    /// A list of database-global extensions. Extensions are not schema-scoped, so they live at the root of the
    /// database schema rather than inside a <see cref="Schema"/>.
    /// </summary>
    public List<Extension> Extensions { get; init; } = [];

    /// <summary>
    /// Every schema-level object of type <typeparamref name="T"/> across the database's schemas, each paired
    /// with the name of the schema that holds it.
    /// </summary>
    public IEnumerable<(SqlIdentifier Schema, T Object)> Objects<T>() where T : SchemaObject =>
        Schemas.SelectMany(s => s.Objects().OfType<T>().Select(o => (Schema: s.Name, Object: o)));

    /// <summary>
    /// The identity of everything the database contains: its schemas, their objects, and its extensions.
    /// </summary>
    public IdentitySet Identities() => new(
        [.. Schemas.Select(s => s.Address), .. Extensions.Select(e => e.Address)],
        [.. Schemas.SelectMany(s => s.Objects()).Select(o => o.Address)]);

    /// <summary>
    /// The spellings of every body-bearing object the database contains.
    /// </summary>
    public DefinitionSet Definitions() => new(
        [.. Objects<View>().Select(v => new ViewDefinition(v.Object.Address, v.Object.Body))],
        [.. Objects<Routine>().Select(r => new RoutineDefinition(r.Object.Address, r.Object.Arguments, r.Object.Definition))],
        [.. Objects<Table>().SelectMany(t => t.Object.Triggers.Select(trigger =>
            new TriggerDefinition(trigger.Address, trigger.When, trigger.FunctionArguments, trigger.Body)))])
    {
        // A domain's checks are spelled the same way a table's are, and carry their own address.
        Checks =
        [
            .. Objects<Table>().SelectMany(t => t.Object.CheckConstraints.Select(check =>
                new CheckConstraintDefinition(check.Address, check.Expression))),
            .. Objects<DomainType>().SelectMany(d => d.Object.Checks.Select(check =>
                new CheckConstraintDefinition(check.Address, check.Expression))),
        ],
        // Only the members carrying an expression: the rest have no spelling to disagree about.
        Columns =
        [
            .. Objects<Table>()
                .SelectMany(t => t.Object.Columns)
                .Where(column => column.DefaultExpression is not null || column.GeneratedExpression is not null)
                .Select(column => new ColumnExpressionDefinition(column.Address, column.DefaultExpression, column.GeneratedExpression)),
        ],
        Indexes =
        [
            .. Indexed().Where(index => index.Predicate is not null).Select(index => new IndexPredicateDefinition(index.Address, index.Predicate)),
        ],
        Exclusions =
        [
            .. Objects<Table>()
                .SelectMany(t => t.Object.ExclusionConstraints)
                .Where(exclusion => exclusion.Predicate is not null)
                .Select(exclusion => new ExclusionConstraintDefinition(exclusion.Address, exclusion.Predicate)),
        ],
        Domains =
        [
            .. Objects<DomainType>()
                .Where(d => d.Object.Default is not null)
                .Select(d => new DomainDefinition(d.Object.Address, d.Object.Default)),
        ],
    };

    // Indexes hang off tables and off views, and both spell a filter the same way.
    private IEnumerable<TableIndex> Indexed() =>
        Objects<Table>().SelectMany(t => t.Object.Indexes).Concat(Objects<View>().SelectMany(v => v.Object.Indexes));

    /// <summary>
    /// Returns a copy of the database with each body-bearing object's text replaced by its entry in <paramref name="definitions"/>.
    /// </summary>
    public Database WithDefinitions(DefinitionSet definitions)
    {
        if (definitions.IsEmpty)
        {
            return this;
        }

        var copy = Clone();
        foreach (var (_, view) in copy.Objects<View>())
        {
            if (definitions.FindView(view.Address) is { } declared)
            {
                view.Body = declared.Body;
            }
        }

        foreach (var (_, routine) in copy.Objects<Routine>())
        {
            if (definitions.FindRoutine(routine.Address) is { } declared)
            {
                routine.Arguments = declared.Arguments;
                routine.Definition = declared.Definition;
            }
        }

        foreach (var trigger in copy.Objects<Table>().SelectMany(t => t.Object.Triggers))
        {
            if (definitions.FindTrigger(trigger.Address) is { } declared)
            {
                trigger.When = declared.When;
                trigger.FunctionArguments = declared.FunctionArguments;
                trigger.Body = declared.Body;
            }
        }

        foreach (var check in copy.Objects<Table>().SelectMany(t => t.Object.CheckConstraints)
                     .Concat(copy.Objects<DomainType>().SelectMany(d => d.Object.Checks)))
        {
            if (definitions.FindCheck(check.Address) is { } declared)
            {
                check.Expression = declared.Expression;
            }
        }

        foreach (var column in copy.Objects<Table>().SelectMany(t => t.Object.Columns))
        {
            if (definitions.FindColumn(column.Address) is not { } declared)
            {
                continue;
            }

            // Only where the engine still reports an expression. A member that was dropped out from under us
            // is simply absent, but a column outlives its default — so overlaying one that is no longer there
            // would hide the drop rather than report it.
            if (column.DefaultExpression is not null && declared.Default is not null)
            {
                column.DefaultExpression = declared.Default;
            }

            if (column.GeneratedExpression is not null && declared.Generated is not null)
            {
                column.GeneratedExpression = declared.Generated;
            }
        }

        foreach (var index in copy.Indexed())
        {
            if (index.Predicate is not null && definitions.FindIndex(index.Address) is { Predicate: not null } declared)
            {
                index.Predicate = declared.Predicate;
            }
        }

        foreach (var exclusion in copy.Objects<Table>().SelectMany(t => t.Object.ExclusionConstraints))
        {
            if (exclusion.Predicate is not null && definitions.FindExclusion(exclusion.Address) is { Predicate: not null } declared)
            {
                exclusion.Predicate = declared.Predicate;
            }
        }

        foreach (var (_, domain) in copy.Objects<DomainType>())
        {
            if (domain.Default is not null && definitions.FindDomain(domain.Address) is { Default: not null } declared)
            {
                domain.Default = declared.Default;
            }
        }

        return copy;
    }

    /// <summary>
    /// Returns a deep copy of the database.
    /// </summary>
    public Database Clone() => new()
    {
        Schemas = [.. Schemas.Select(s => s.Clone())],
        Extensions = [.. Extensions.Select(e => e.Clone())],
    };

    /// <summary>
    /// Returns a copy of the database restricted to the schemas, objects, and extensions whose identity is in the set.
    /// </summary>
    public Database FilteredTo(IdentitySet identities) => new()
    {
        Schemas = [.. Schemas.Select(schema => Filter(schema, identities)).OfType<Schema>()],
        Extensions = [.. Extensions.Where(e => identities.ContainsExtension(e.Name)).Select(e => e.Clone())],
    };

    private static Schema? Filter(Schema schema, IdentitySet identities)
    {
        var filtered = schema.FilteredTo(identities);
        if (identities.ContainsSchema(schema.Name))
        {
            return filtered;
        }

        if (!filtered.Objects().Any())
        {
            return null;
        }

        // Held on to for its contents alone, which is what an implicit schema is: somewhere to put them.
        return filtered.AsImplicit();
    }

    /// <summary>
    /// Returns a new database model restricted to the current scope.
    /// </summary>
    public Database ScopedTo(PlanningScope scope)
    {
        if (scope.IsUnscoped)
        {
            return this;
        }

        // A targeted object still needs its container in the tree, even though the scope does not cover the
        // schema itself.
        var covered = Identities().ScopedTo(scope);
        return FilteredTo(covered with
        {
            DatabaseObjects = [.. covered.DatabaseObjects.Union(covered.SchemaObjects.Select(o => DatabaseAddress.Schema(o.Schema)))],
        });
    }

    /// <summary>
    /// Structural equality over the declared contents.
    /// </summary>
    public bool Equals(Database? other) =>
        other is not null
        && Schemas.SequenceEqual(other.Schemas)
        && Extensions.SequenceEqual(other.Extensions);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Database other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Schemas.Count, Extensions.Count);
}
