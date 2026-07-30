using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Indexes;
using NSchema.Diff.Domain.Triggers;
using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Tables;

/// <summary>
/// Describes the changes affecting a single table.
/// </summary>
public sealed record TableDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private TableDiff() { }

    /// <summary>
    /// The name of the schema the table belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, SchemaObjectKind.Table);

    /// <summary>
    /// The table name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the table.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous table name when the table is being renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The change to the table's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// The changed columns, ordered as encountered in the plan.
    /// </summary>
    public IReadOnlyList<ColumnDiff> Columns { get; init; } = [];

    /// <summary>
    /// Privileges granted or revoked on the table.
    /// </summary>
    public IReadOnlyList<GrantChange> Grants { get; init; } = [];

    /// <summary>
    /// Index changes on the table.
    /// </summary>
    public IReadOnlyList<IndexDiff> Indexes { get; init; } = [];

    /// <summary>
    /// Primary key changes on the table.
    /// </summary>
    public IReadOnlyList<PrimaryKeyDiff> PrimaryKeys { get; init; } = [];

    /// <summary>
    /// Foreign key changes on the table.
    /// </summary>
    public IReadOnlyList<ForeignKeyDiff> ForeignKeys { get; init; } = [];

    /// <summary>
    /// Unique constraint changes on the table.
    /// </summary>
    public IReadOnlyList<UniqueConstraintDiff> UniqueConstraints { get; init; } = [];

    /// <summary>
    /// Check constraint changes on the table.
    /// </summary>
    public IReadOnlyList<CheckConstraintDiff> Checks { get; init; } = [];

    /// <summary>
    /// Exclusion constraint changes on the table.
    /// </summary>
    public IReadOnlyList<ExclusionConstraintDiff> ExclusionConstraints { get; init; } = [];

    /// <summary>
    /// Trigger changes on the table.
    /// </summary>
    public IReadOnlyList<TriggerDiff> Triggers { get; init; } = [];

    /// <summary>
    /// The full table definition when the table is being created; otherwise <see langword="null"/>.
    /// </summary>
    public Table? Definition { get; init; }

    /// <summary>
    /// Whether this is a table being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A table being created, named by its own definition.
    /// </summary>
    public static TableDiff Added(SqlIdentifier schema, Table definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition
    };

    /// <summary>
    /// A table being dropped.
    /// </summary>
    public static TableDiff Removed(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A table altered in place; the member changes are set on the result.
    /// </summary>
    public static TableDiff Modified(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Modify };

    /// <summary>
    /// Enumerates every changed member of this table across all kinds (columns, indexes, constraints, triggers),
    /// for kind-agnostic consumers. A method rather than a property so serializers and snapshot tooling do not
    /// duplicate the per-kind collections. Grants are not members (they are keyed by role, not name).
    /// </summary>
    public IEnumerable<IObjectMemberDiff> EnumerateMembers() =>
        Columns.Cast<IObjectMemberDiff>()
            .Concat(Indexes)
            .Concat(PrimaryKeys)
            .Concat(ForeignKeys)
            .Concat(UniqueConstraints)
            .Concat(Checks)
            .Concat(ExclusionConstraints)
            .Concat(Triggers);
}
