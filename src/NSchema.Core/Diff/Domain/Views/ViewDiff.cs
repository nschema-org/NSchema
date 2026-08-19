using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Diff.Domain.Indexes;
using NSchema.Model;
using NSchema.Model.Views;

namespace NSchema.Diff.Domain.Views;

/// <summary>
/// Describes a change to a view.
/// </summary>
public sealed record ViewDiff : ISchemaObjectDiff
{
    [JsonConstructor]
    private ViewDiff() { }

    /// <summary>
    /// The name of the schema the view belongs to.
    /// </summary>
    public required SqlIdentifier Schema { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public ObjectAddress Address => ObjectAddress.View(Schema, Name);

    /// <summary>
    /// The view name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The change to the view.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added view; otherwise <see langword="null"/>.
    /// </summary>
    public View? Definition { get; init; }

    /// <summary>
    /// Whether the view is materialized (after the change, for a modified view).
    /// </summary>
    public bool IsMaterialized { get; init; }

    /// <summary>
    /// The change to the view's materialization when it converts between a plain and a materialized view.
    /// </summary>
    public ValueChange<bool>? Materialized { get; init; }

    /// <summary>
    /// Whether the view is schema-bound (after the change, for a modified view).
    /// </summary>
    public bool IsSchemaBound { get; init; }

    /// <summary>
    /// The change to the view's schema binding when it is bound or unbound.
    /// </summary>
    public ValueChange<bool>? SchemaBound { get; init; }

    /// <summary>
    /// Whether the change must be applied as a drop + recreate rather than an in-place replace.
    /// </summary>
    public bool RequiresRecreate { get; init; }

    /// <summary>
    /// In-place index changes on a view whose definition is unchanged.
    /// </summary>
    public IReadOnlyList<IndexDiff> Indexes { get; init; } = [];

    /// <summary>
    /// The change to the view's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a view being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A view being created, named by its own definition.
    /// </summary>
    public static ViewDiff Added(SqlIdentifier schema, View definition) => new()
    {
        Schema = schema,
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
        IsMaterialized = definition.IsMaterialized,
        IsSchemaBound = definition.IsSchemaBound,
    };

    /// <summary>
    /// A view being dropped.
    /// </summary>
    public static ViewDiff Removed(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A view altered in place; the individual changes are set on the result.
    /// </summary>
    public static ViewDiff Modified(SqlIdentifier schema, SqlIdentifier name) =>
        new() { Schema = schema, Name = name, Change = ChangeKind.Modify };
}
