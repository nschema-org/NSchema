using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Publications;

namespace NSchema.Diff.Domain.Publications;

/// <summary>
/// Describes a change to a publication.
/// </summary>
public sealed record PublicationDiff : IDatabaseObjectDiff
{
    [JsonConstructor]
    private PublicationDiff() { }

    /// <summary>
    /// The publication name.
    /// </summary>
    public required SqlIdentifier Name { get; init; }

    /// <summary>
    /// The publication's address.
    /// </summary>
    [JsonIgnore]
    public DatabaseAddress Address => DatabaseAddress.Publication(Name);

    /// <summary>
    /// The change to the publication.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The previous name when renamed; otherwise <see langword="null"/>.
    /// </summary>
    public SqlIdentifier? RenamedFrom { get; init; }

    /// <summary>
    /// The definition for an added or recreated publication; otherwise <see langword="null"/>.
    /// </summary>
    public Publication? Definition { get; init; }

    /// <summary>
    /// The change to whether every table is published, set when it changed (which forces a recreate).
    /// </summary>
    public ValueChange<bool>? AllTables { get; init; }

    /// <summary>
    /// The tables published or no longer published, and those whose columns or filter changed.
    /// </summary>
    public IReadOnlyList<PublishedTableDiff> Tables { get; init; } = [];

    /// <summary>
    /// The schemas published or no longer published.
    /// </summary>
    public IReadOnlyList<PublishedSchemaChange> Schemas { get; init; } = [];

    /// <summary>
    /// The change to the published operations, if any.
    /// </summary>
    public ValueChange<PublishedOperations>? Operations { get; init; }

    /// <summary>
    /// The change to the publication's comment, if any.
    /// </summary>
    public ValueChange<string>? Comment { get; init; }

    /// <summary>
    /// Whether this is a publication being created, and so carries the <see cref="Definition"/> to create it from.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool IsAdd() => Change == ChangeKind.Add && Definition is not null;

    /// <summary>
    /// Whether the publication must be dropped and created from <see cref="Definition"/>, since what it publishes
    /// changed from every table to a list, or back.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Definition))]
    public bool RequiresRecreate => AllTables is not null && Definition is not null;

    /// <summary>
    /// A publication being created, named by its own definition.
    /// </summary>
    public static PublicationDiff Added(Publication definition) => new()
    {
        Name = definition.Name,
        Change = ChangeKind.Add,
        Definition = definition,
        Comment = ValueChange.Between(null, definition.Comment),
    };

    /// <summary>
    /// A publication being dropped.
    /// </summary>
    public static PublicationDiff Removed(SqlIdentifier name) =>
        new() { Name = name, Change = ChangeKind.Remove };

    /// <summary>
    /// A publication altered in place; the individual changes are set on the result.
    /// </summary>
    public static PublicationDiff Modified(SqlIdentifier name) =>
        new() { Name = name, Change = ChangeKind.Modify };

    /// <summary>
    /// A publication switching between every table and a list, recreated from <paramref name="definition"/>.
    /// </summary>
    public static PublicationDiff Recreated(Publication definition, bool wasAllTables) => new()
    {
        Name = definition.Name,
        Change = ChangeKind.Modify,
        Definition = definition,
        AllTables = new ValueChange<bool>(wasAllTables, definition.AllTables),
        Comment = ValueChange.Between(null, definition.Comment),
    };
}
