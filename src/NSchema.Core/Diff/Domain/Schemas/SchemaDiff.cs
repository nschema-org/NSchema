using System.Text.Json.Serialization;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Model;

namespace NSchema.Diff.Domain.Schemas;

/// <summary>
/// Describes the changes in a given schema and its tables.
/// </summary>
public sealed record SchemaDiff
{
    [JsonConstructor]
    private SchemaDiff() { }

    /// <summary>
    /// The schema name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The schema's address.
    /// </summary>
    [JsonIgnore]
    public SchemaAddress Address => new(Name);

    /// <summary>
    /// The change to the schema itself, or <see langword="null"/> when only its contents changed.
    /// </summary>
    public ChangeKind? Kind { get; init; }

    /// <summary>
    /// The previous schema name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The change to the schema's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Usage grants and revocations on the schema.
    /// </summary>
    public IReadOnlyList<GrantChange> Grants { get; init; } = [];

    /// <summary>
    /// The changed tables within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<TableDiff> Tables { get; init; } = [];

    /// <summary>
    /// The changed views within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<ViewDiff> Views { get; init; } = [];

    /// <summary>
    /// The changed enum types within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<EnumDiff> Enums { get; init; } = [];

    /// <summary>
    /// The changed sequences within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<SequenceDiff> Sequences { get; init; } = [];

    /// <summary>
    /// The changed routines (functions and procedures) within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<RoutineDiff> Routines { get; init; } = [];

    /// <summary>
    /// The changed domains within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<DomainDiff> Domains { get; init; } = [];

    /// <summary>
    /// The changed composite types within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<CompositeTypeDiff> CompositeTypes { get; init; } = [];

    /// <summary>
    /// A schema being created.
    /// </summary>
    public static SchemaDiff Added(SqlIdentifier name) => new() { Name = name, Kind = ChangeKind.Add };

    /// <summary>
    /// A schema being dropped, along with everything it contains.
    /// </summary>
    public static SchemaDiff Removed(SqlIdentifier name) => new() { Name = name, Kind = ChangeKind.Remove };

    /// <summary>
    /// A schema whose own definition changed — renamed, or its comment or grants altered.
    /// </summary>
    public static SchemaDiff Modified(SqlIdentifier name) => new() { Name = name, Kind = ChangeKind.Modify };

    /// <summary>
    /// A schema untouched in itself, carried only because objects inside it changed.
    /// </summary>
    public static SchemaDiff Containing(SqlIdentifier name) => new() { Name = name };

    /// <summary>
    /// Narrows this schema's changes to what <paramref name="scope"/> covers, or <see langword="null"/> when nothing in it is covered.
    /// </summary>
    public SchemaDiff? ScopedTo(PlanningScope scope)
    {
        if (scope.Contains(Name))
        {
            return this;
        }

        var narrowed = this with
        {
            Kind = Kind == ChangeKind.Add ? ChangeKind.Add : null,
            RenamedFrom = null,
            Comment = null,
            Grants = [],
            Tables = [.. Tables.Where(Covered)],
            Views = [.. Views.Where(Covered)],
            Enums = [.. Enums.Where(Covered)],
            Sequences = [.. Sequences.Where(Covered)],
            Routines = [.. Routines.Where(Covered)],
            Domains = [.. Domains.Where(Covered)],
            CompositeTypes = [.. CompositeTypes.Where(Covered)],
        };

        // The container rides only as a dependency: with nothing covered inside it, the schema is not this
        // run's business at all.
        return narrowed.EnumerateObjects().Any() ? narrowed : null;

        bool Covered(ISchemaObjectDiff diff) => scope.Contains(new ObjectAddress(Name, diff.Name));
    }

    /// <summary>
    /// Enumerates every changed object in this schema across all kinds, for kind-agnostic consumers (change
    /// summaries, destructive-change detection). A method rather than a property so serializers and snapshot
    /// tooling do not duplicate the per-kind collections.
    /// </summary>
    public IEnumerable<ISchemaObjectDiff> EnumerateObjects() =>
        Tables.Cast<ISchemaObjectDiff>()
            .Concat(Views)
            .Concat(Enums)
            .Concat(Sequences)
            .Concat(Routines)
            .Concat(Domains)
            .Concat(CompositeTypes);
}
