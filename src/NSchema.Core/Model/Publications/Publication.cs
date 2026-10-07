using System.Diagnostics;

namespace NSchema.Model.Publications;

/// <summary>
/// Represents a named set of tables whose row changes the database publishes to subscribers.
/// </summary>
[DebuggerDisplay("{Name,nq} (publication)")]
public sealed class Publication : DatabaseObject, IEquatable<Publication>
{
    /// <inheritdoc/>
    public override DatabaseObjectKind Kind => DatabaseObjectKind.Publication;

    /// <inheritdoc/>
    public override DatabaseAddress Address => DatabaseAddress.Publication(Name);

    /// <summary>
    /// Whether the publication publishes every table in the database, present and future.
    /// </summary>
    public bool AllTables { get; set; }

    /// <summary>
    /// The schemas whose tables, present and future, the publication publishes.
    /// </summary>
    public List<SqlIdentifier> Schemas { get; init; } = [];

    /// <summary>
    /// The individually published tables.
    /// </summary>
    public List<PublishedTable> Tables { get; init; } = [];

    /// <summary>
    /// The row changes the publication publishes.
    /// </summary>
    public PublishedOperations Operations { get; set; } = PublishedOperations.All;

    /// <inheritdoc/>
    public override Publication Clone() => new()
    {
        Name = Name,
        AllTables = AllTables,
        Schemas = [.. Schemas],
        Tables = [.. Tables],
        Operations = Operations,
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the comment is excluded, and membership is unordered.
    /// </summary>
    public bool Equals(Publication? other) =>
        other is not null
        && Name == other.Name
        && AllTables == other.AllTables
        && Operations == other.Operations
        && Schemas.ToHashSet().SetEquals(other.Schemas)
        && Tables.ToHashSet().SetEquals(other.Tables);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Publication other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, AllTables, Operations);
}
