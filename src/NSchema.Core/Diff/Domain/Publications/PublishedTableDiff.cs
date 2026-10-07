using System.Text.Json.Serialization;
using NSchema.Model;
using NSchema.Model.Publications;

namespace NSchema.Diff.Domain.Publications;

/// <summary>
/// Describes a change to one of a publication's tables.
/// </summary>
public sealed record PublishedTableDiff
{
    [JsonConstructor]
    private PublishedTableDiff() { }

    /// <summary>
    /// The published table.
    /// </summary>
    public required ObjectAddress Table { get; init; }

    /// <summary>
    /// The change to the table's entry.
    /// </summary>
    public required ChangeKind Change { get; init; }

    /// <summary>
    /// The entry before the change, for a removed or changed entry.
    /// </summary>
    public PublishedTable? Previous { get; init; }

    /// <summary>
    /// The entry after the change, for an added or changed entry.
    /// </summary>
    public PublishedTable? Definition { get; init; }

    /// <summary>
    /// A table newly published.
    /// </summary>
    public static PublishedTableDiff Added(PublishedTable definition) =>
        new() { Table = definition.Table, Change = ChangeKind.Add, Definition = definition };

    /// <summary>
    /// A table no longer published.
    /// </summary>
    public static PublishedTableDiff Removed(PublishedTable previous) =>
        new() { Table = previous.Table, Change = ChangeKind.Remove, Previous = previous };

    /// <summary>
    /// A table still published, with different columns or a different filter.
    /// </summary>
    public static PublishedTableDiff Modified(PublishedTable previous, PublishedTable definition) =>
        new() { Table = definition.Table, Change = ChangeKind.Modify, Previous = previous, Definition = definition };
}
