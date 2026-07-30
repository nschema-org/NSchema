using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Diff.Domain.Constraints;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Domains;

namespace NSchema.Diff.Domain.Domains;

/// <summary>
/// Describes a change to a domain.
/// </summary>
public sealed record DomainDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private DomainDiff() { }

    /// <summary>
    /// The name of the schema the domain belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => new(Schema, Name, SchemaObjectKind.Domain);

    /// <summary>
    /// The domain name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the domain.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added domain; otherwise <see langword="null"/>.
    /// </summary>
    public DomainType? Definition { get; init; }

    /// <summary>
    /// The change to the base type, set when it changed (which forces a recreate).
    /// </summary>
    public ValueChange<SqlType>? DataType { get; init; }

    /// <summary>
    /// The change to the default expression, if any (applied in place with <c>ALTER DOMAIN</c>).
    /// </summary>
    public ValueChange<SqlDefaultExpression>? Default { get; init; }

    /// <summary>
    /// The change to the not-null requirement, if any (applied in place with <c>ALTER DOMAIN</c>).
    /// </summary>
    public ValueChange<bool>? NotNull { get; init; }

    /// <summary>
    /// In-place check-constraint changes (added/dropped via <c>ALTER DOMAIN</c>).
    /// </summary>
    public IReadOnlyList<CheckConstraintDiff> Checks { get; init; } = [];

    /// <summary>
    /// The change to the domain's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a domain being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A domain being created, named by its own definition.
    /// </summary>
    public static DomainDiff Added(SqlIdentifier schema, DomainType definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A domain being dropped.
    /// </summary>
    public static DomainDiff Removed(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A domain altered in place; the individual changes are set on the result.
    /// </summary>
    public static DomainDiff Modified(SqlIdentifier schema, SqlIdentifier name) => new()
    {
        Schema = schema,
        Name = name,
        Change = ChangeKind.Modify
    };

    /// <summary>
    /// The base type changed, so the domain must be dropped and recreated — Postgres has no
    /// <c>ALTER DOMAIN … TYPE</c>. The default, not-null and checks ride along on <see cref="Definition"/>.
    /// </summary>
    public bool RequiresRecreate => DataType is not null;
}
