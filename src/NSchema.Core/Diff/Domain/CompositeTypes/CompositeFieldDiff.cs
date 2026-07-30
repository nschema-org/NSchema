using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;

namespace NSchema.Diff.Domain.CompositeTypes;

/// <summary>
/// Describes a change to a single field of a composite type.
/// </summary>
public sealed record CompositeFieldDiff
{
    [JsonConstructor]
    private CompositeFieldDiff() { }

    /// <summary>
    /// The change to the field.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The field name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The definition for an added field; otherwise <see langword="null"/>.
    /// </summary>
    public CompositeField? Definition { get; init; }

    /// <summary>
    /// The change to the field's type, set on an in-place retype (<c>ALTER ATTRIBUTE … TYPE</c>).
    /// </summary>
    public ValueChange<SqlType>? Type { get; init; }

    /// <summary>
    /// Whether this is a field being added, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// A field being created, named by its own definition.
    /// </summary>
    public static CompositeFieldDiff Added(CompositeField definition) =>
        new() { Change = ChangeKind.Add, Name = definition.Name, Definition = definition };

    /// <summary>
    /// A field being dropped.
    /// </summary>
    public static CompositeFieldDiff Removed(SqlIdentifier name) =>
        new() { Change = ChangeKind.Remove, Name = name };

    /// <summary>
    /// An in-place retype (<c>ALTER ATTRIBUTE … TYPE</c>).
    /// </summary>
    public static CompositeFieldDiff TypeChanged(SqlIdentifier name, ValueChange<SqlType> type) =>
        new() { Change = ChangeKind.Modify, Name = name, Type = type };
}
